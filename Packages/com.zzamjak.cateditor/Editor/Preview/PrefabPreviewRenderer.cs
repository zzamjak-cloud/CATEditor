using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Utility.PrefabPreview
{
    /// <summary>
    /// 프리팹 종류별 미리보기 렌더링 코어.
    /// UI / 이펙트 / 일반 프리팹을 각각 다른 씬 구성과 프레이밍 전략으로 렌더링한다.
    /// </summary>
    public class PrefabPreviewRenderer : System.IDisposable
    {
        public enum PreviewKind { None, UI, Effect, Generic }

        // 렌더링 결과의 신뢰도. UI 프리팹은 앵커 구성에 따라 정상 렌더가 불가능한 경우가 있다.
        public enum PreviewStatus
        {
            Ok,         // 정상
            Stretch,    // 레퍼런스 캔버스 대부분을 채움 (Stretch 앵커 — 렌더 자체는 정상)
            NoGraphic,  // 그려지는 Graphic이 없음 (레이아웃 컨테이너) — Rect 와이어프레임으로 대체
            ZeroSize,   // 크기가 0 — 앵커 전용이라 부모 없이는 크기가 정의되지 않음
            Empty       // 렌더 대상 자체가 없음
        }

        private const float ZeroSizeThreshold = 1f;
        private const float StretchRatio = 0.81f;   // 가로세로 각 90% → 면적 81%
        private const float FramePadding = 1.04f;
        private const float EffectBoundsSafety = 0.06f;
        private const float AnalysisTimeCap = 8f;
        private static readonly float Sqrt2 = Mathf.Sqrt(2f);

        private PreviewRenderUtility _preview;
        private GameObject _canvasGO;
        private GameObject _instance;

        private PreviewKind _kind = PreviewKind.None;
        private PreviewStatus _status = PreviewStatus.Empty;

        private Vector2 _referenceResolution = new Vector2(1080f, 1920f);
        private Bounds _frameBounds;
        private RectTransform[] _rects = new RectTransform[0];

        // 이펙트 재생 상태
        private ParticleSystem[] _rootParticles = new ParticleSystem[0];
        private ParticleSystem[] _allParticles = new ParticleSystem[0];
        private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[256];
        private Renderer[] _renderers = new Renderer[0];
        private Renderer[] _nonParticleRenderers = new Renderer[0];
        private ParticleSystemRenderer[] _particleRenderers = new ParticleSystemRenderer[0];
        private Animator[] _animators = new Animator[0];
        private bool _isLooping;
        private float _rawDuration = 1f;
        private float _duration = 1f;
        private float _playTime;
        private bool _isPlaying = true;
        private float _speed = 1f;

        // 카메라 조작 상태
        private Vector2 _rotation;
        private Vector2 _pan;
        private float _zoom = 1f;

        private readonly Vector3[] _corners = new Vector3[4];
        private static Texture2D _checkerTexture;

        public PreviewKind Kind => _kind;
        public PreviewStatus Status => _status;
        public float Duration => _duration;
        public float PlayTime => _playTime;
        /// <summary>원본 파티클이 루프 설정인지. false여도 미리보기는 반복 재생한다.</summary>
        public bool IsLooping => _isLooping;
        public bool IsPlaying { get => _isPlaying; set => _isPlaying = value; }
        public float Speed { get => _speed; set => _speed = Mathf.Clamp(value, 0.1f, 3f); }

        // 이펙트가 재생 중일 때만 연속 리페인트가 필요하다.
        public bool NeedsContinuousRepaint => _kind == PreviewKind.Effect && _isPlaying && _rootParticles.Length > 0;

        public Vector2 ReferenceResolution
        {
            get => _referenceResolution;
            set => _referenceResolution = new Vector2(Mathf.Max(1f, value.x), Mathf.Max(1f, value.y));
        }

        // 프리뷰 씬·카메라·렌더텍스처는 대상이 있을 때만 유지한다.
        // 프리팹이 아닌 항목을 선택하면 즉시 해제되어 유휴 상태에서 자원을 점유하지 않는다.
        private void EnsurePreview()
        {
            if (_preview != null) return;

            _preview = new PreviewRenderUtility();
            SetupLights();
        }

        private void ReleasePreview()
        {
            if (_preview == null) return;

            _preview.Cleanup();
            _preview = null;
        }

        private void SetupLights()
        {
            _preview.lights[0].intensity = 1.2f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
            _preview.lights[1].intensity = 1.0f;
            _preview.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
            _preview.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        /// <summary>
        /// 미리보기 대상 프리팹을 설정한다. null이면 정리만 수행.
        /// </summary>
        public void SetTarget(GameObject prefab)
        {
            ClearInstance();

            if (prefab == null)
            {
                _kind = PreviewKind.None;
                _status = PreviewStatus.Empty;
                ReleasePreview();
                return;
            }

            EnsurePreview();
            _kind = DetectKind(prefab);

            switch (_kind)
            {
                case PreviewKind.UI:
                    BuildUI(prefab);
                    break;
                default:
                    BuildWorld(prefab);
                    break;
            }

            ResetView();
        }

        private static PreviewKind DetectKind(GameObject prefab)
        {
            bool isRect = prefab.transform is RectTransform;
            bool hasGraphic = prefab.GetComponentInChildren<Graphic>(true) != null;
            bool hasParticle = prefab.GetComponentInChildren<ParticleSystem>(true) != null;

            // Graphic이 있으면 UI. 파티클을 포함한 UI도 캔버스 기준으로 봐야 한다.
            if (isRect && hasGraphic) return PreviewKind.UI;

            // RectTransform 루트라도 Graphic 없이 파티클만 있으면 UI 공간에서 만든 이펙트다.
            if (hasParticle) return PreviewKind.Effect;

            return isRect ? PreviewKind.UI : PreviewKind.Generic;
        }

        // ── UI 프리팹 ────────────────────────────────────────────────────────────
        // 앵커는 부모 Rect가 없으면 크기가 정의되지 않는다. 그래서 레퍼런스 해상도를 가진
        // 전용 World Space 캔버스를 만들어 부모로 제공한 뒤 레이아웃을 강제로 해석시킨다.
        private void BuildUI(GameObject prefab)
        {
            // RectTransform을 명시적으로 먼저 붙인다. Canvas만 지정하면 Transform으로 생성되어 캐스팅이 실패한다.
            _canvasGO = new GameObject("__CATPreviewCanvas", typeof(RectTransform), typeof(Canvas));
            _preview.AddSingleGO(_canvasGO);
            _canvasGO.hideFlags = HideFlags.HideAndDontSave;

            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;   // Overlay는 프리뷰 카메라가 렌더할 수 없다
            canvas.worldCamera = _preview.camera;

            var canvasRT = (RectTransform)_canvasGO.transform;
            canvasRT.sizeDelta = _referenceResolution;
            canvasRT.position = Vector3.zero;
            canvasRT.rotation = Quaternion.identity;
            canvasRT.localScale = Vector3.one;

            _instance = Object.Instantiate(prefab, canvasRT, false);
            _instance.name = prefab.name;
            _instance.SetActive(true);   // 비활성 상태로 저장된 팝업류도 미리보기는 되어야 한다
            ApplyHideFlags(_instance);

            RebuildUILayout();

            _rects = _instance.GetComponentsInChildren<RectTransform>(false);
            ComputeUIFraming();
        }

        private void ComputeUIFraming()
        {
            var graphics = _instance.GetComponentsInChildren<Graphic>(false);
            var visible = new List<Graphic>(graphics.Length);
            foreach (var g in graphics)
            {
                if (g == null || !g.enabled) continue;
                if (g.color.a <= 0.01f) continue;
                visible.Add(g);
            }

            Bounds bounds;

            if (visible.Count == 0)
            {
                // 그려지는 요소가 없는 레이아웃 컨테이너. Rect 와이어프레임으로 대체 표현한다.
                _status = PreviewStatus.NoGraphic;
                if (!TryBoundsFromRects(out bounds))
                {
                    _status = PreviewStatus.Empty;
                    bounds = ReferenceBounds();
                }
            }
            else
            {
                bounds = BoundsFromRects(visible);

                if (bounds.size.x < ZeroSizeThreshold || bounds.size.y < ZeroSizeThreshold)
                {
                    // 앵커만으로 구성되어 부모 없이는 크기가 0인 케이스. 억지 렌더 대신 안내로 대체.
                    _status = PreviewStatus.ZeroSize;
                    bounds = ReferenceBounds();
                }
                else
                {
                    float area = bounds.size.x * bounds.size.y;
                    float canvasArea = _referenceResolution.x * _referenceResolution.y;
                    _status = area >= canvasArea * StretchRatio ? PreviewStatus.Stretch : PreviewStatus.Ok;
                }
            }

            _frameBounds = bounds;
        }

        private Bounds ReferenceBounds()
        {
            return new Bounds(Vector3.zero, new Vector3(_referenceResolution.x, _referenceResolution.y, 0f));
        }

        // 루트 Rect가 아니라 실제 렌더되는 Graphic들의 월드 코너 합집합으로 바운드를 잡는다.
        private Bounds BoundsFromRects(List<Graphic> graphics)
        {
            bool init = false;
            Bounds bounds = new Bounds();

            foreach (var g in graphics)
            {
                var rt = g.rectTransform;
                if (rt == null) continue;
                rt.GetWorldCorners(_corners);

                for (int i = 0; i < 4; i++)
                {
                    if (!init) { bounds = new Bounds(_corners[i], Vector3.zero); init = true; }
                    else bounds.Encapsulate(_corners[i]);
                }
            }

            return init ? bounds : new Bounds();
        }

        private bool TryBoundsFromRects(out Bounds bounds)
        {
            bool init = false;
            bounds = new Bounds();

            foreach (var rt in _rects)
            {
                if (rt == null) continue;
                rt.GetWorldCorners(_corners);
                for (int i = 0; i < 4; i++)
                {
                    if (!init) { bounds = new Bounds(_corners[i], Vector3.zero); init = true; }
                    else bounds.Encapsulate(_corners[i]);
                }
            }

            return init && bounds.size.x >= ZeroSizeThreshold && bounds.size.y >= ZeroSizeThreshold;
        }

        // ── 이펙트 / 일반 프리팹 ────────────────────────────────────────────────
        private void BuildWorld(GameObject prefab)
        {
            _instance = Object.Instantiate(prefab);
            _instance.name = prefab.name;
            _instance.SetActive(true);
            _instance.transform.position = Vector3.zero;
            _instance.transform.rotation = Quaternion.identity;
            _preview.AddSingleGO(_instance);
            ApplyHideFlags(_instance);

            _renderers = _instance.GetComponentsInChildren<Renderer>(true);

            var nonParticle = new List<Renderer>(_renderers.Length);
            foreach (var r in _renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                nonParticle.Add(r);
            }
            _nonParticleRenderers = nonParticle.ToArray();

            if (_kind == PreviewKind.Effect)
            {
                SetupParticles();
                AnalyzeEffect();
                ResetPlayback();
            }
            else
            {
                if (!TryComputeRendererBounds(out _frameBounds))
                {
                    _frameBounds = new Bounds(Vector3.zero, Vector3.one);
                }
            }

            _status = _frameBounds.size.sqrMagnitude > 0.0001f ? PreviewStatus.Ok : PreviewStatus.Empty;
        }

        private void SetupParticles()
        {
            _allParticles = _instance.GetComponentsInChildren<ParticleSystem>(true);
            var roots = new List<ParticleSystem>();
            float duration = 0f;
            bool looping = _allParticles.Length > 0;

            foreach (var ps in _allParticles)
            {
                ps.useAutoRandomSeed = false;    // 프레임마다 결과가 튀지 않도록 고정

                var main = ps.main;
                float life = Mathf.Max(main.startLifetime.constant, main.startLifetime.constantMax);
                float delay = Mathf.Max(main.startDelay.constant, main.startDelay.constantMax);
                duration = Mathf.Max(duration, main.duration + life + delay);

                if (!main.loop) looping = false;
                if (IsRootParticle(ps)) roots.Add(ps);
            }

            // 스트레치 빌보드는 속도 방향으로 길어지므로 파티클별로 직접 계산해야 한다.
            // (트레일은 과거 궤적을 따라가므로 시간 합집합 바운드에 이미 포함된다)
            _particleRenderers = new ParticleSystemRenderer[_allParticles.Length];
            for (int i = 0; i < _allParticles.Length; i++)
            {
                _particleRenderers[i] = _allParticles[i].GetComponent<ParticleSystemRenderer>();
            }

            _rootParticles = roots.ToArray();
            _isLooping = looping;
            _rawDuration = Mathf.Clamp(duration, 0.5f, 30f);
            _duration = _rawDuration;
            _animators = _instance.GetComponentsInChildren<Animator>(true);
        }

        // 이펙트는 시간에 따라 퍼지므로 한 시점의 바운드로는 전체 형태를 담을 수 없다.
        // 재생 구간을 훑으며 바운드 합집합(최대 확장)과 실제로 파티클이 살아 있는 구간을 함께 구한다.
        private void AnalyzeEffect()
        {
            const int SampleCount = 20;

            SimulateTo(0f);

            // 분석 비용은 시뮬레이션한 총 시간에 비례한다. 긴 이펙트는 앞부분만 훑어 선택 시 멈칫거림을 막는다.
            float analyzeDuration = Mathf.Min(_rawDuration, AnalysisTimeCap);
            float step = analyzeDuration / SampleCount;
            bool init = false;
            Bounds bounds = new Bounds();
            float lastAliveTime = 0f;

            for (int i = 1; i <= SampleCount; i++)
            {
                foreach (var ps in _rootParticles)
                {
                    if (ps != null) ps.Simulate(step, true, false, true);
                }

                if (TryComputeEffectBounds(out Bounds sample))
                {
                    if (!init) { bounds = sample; init = true; }
                    else bounds.Encapsulate(sample);
                }

                if (CountAliveParticles() > 0) lastAliveTime = step * i;
            }

            // 트레일·속도 스트레치 빌보드는 파티클 중심 위치보다 넓게 그려지므로 여유를 둔다.
            if (init) bounds.Expand(bounds.size * EffectBoundsSafety);

            _frameBounds = init ? bounds : new Bounds(Vector3.zero, Vector3.one);

            // 비루프 이펙트는 파티클이 모두 사라진 뒤의 빈 구간을 잘라내야 반복 재생이 답답하지 않다.
            _duration = _isLooping || lastAliveTime <= 0f
                ? _rawDuration
                : Mathf.Clamp(lastAliveTime + step, 0.5f, _rawDuration);
        }

        // ParticleSystemRenderer.bounds는 컬링용 보수적 바운드라 실제 그려지는 범위보다 훨씬 크다.
        // 살아 있는 파티클의 실제 위치·크기로 잡아야 프리뷰가 화면을 제대로 채운다.
        private bool TryComputeEffectBounds(out Bounds bounds)
        {
            bool init = false;
            bounds = new Bounds();

            for (int psIndex = 0; psIndex < _allParticles.Length; psIndex++)
            {
                ParticleSystem ps = _allParticles[psIndex];
                if (ps == null) continue;

                int count = ps.particleCount;
                if (count == 0) continue;

                ParticleSystemRenderer psRenderer = _particleRenderers[psIndex];
                bool stretched = psRenderer != null && psRenderer.renderMode == ParticleSystemRenderMode.Stretch;
                float lengthScale = stretched ? Mathf.Max(1f, psRenderer.lengthScale) : 1f;
                float velocityScale = stretched ? psRenderer.velocityScale : 0f;

                if (_particleBuffer.Length < count)
                {
                    _particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
                }

                int read = ps.GetParticles(_particleBuffer, count);
                bool localSpace = ps.main.simulationSpace != ParticleSystemSimulationSpace.World;
                Matrix4x4 toWorld = ps.transform.localToWorldMatrix;
                Vector3 lossyScale = ps.transform.lossyScale;
                float sizeScale = localSpace
                    ? Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z))
                    : 1f;

                for (int i = 0; i < read; i++)
                {
                    // 거의 투명한 파티클은 보이지 않으므로 프레이밍에서 제외한다.
                    if (_particleBuffer[i].GetCurrentColor(ps).a < 8) continue;

                    Vector3 position = localSpace
                        ? toWorld.MultiplyPoint3x4(_particleBuffer[i].position)
                        : _particleBuffer[i].position;

                    // 빌보드는 회전하면 한 변의 √2배까지 벌어지므로 대각선 기준으로 잡는다.
                    float particleSize = _particleBuffer[i].GetCurrentSize(ps);
                    float radius = particleSize * 0.5f * Sqrt2;

                    if (stretched)
                    {
                        float speed = _particleBuffer[i].totalVelocity.magnitude;
                        radius = Mathf.Max(radius, particleSize * 0.5f * lengthScale + velocityScale * speed);
                    }

                    radius *= sizeScale;
                    var particleBounds = new Bounds(position, Vector3.one * (radius * 2f));

                    if (!init) { bounds = particleBounds; init = true; }
                    else bounds.Encapsulate(particleBounds);
                }
            }

            // 파티클이 아닌 메시 렌더러는 렌더러 바운드가 정확하다.
            if (TryComputeRendererBounds(_nonParticleRenderers, out Bounds meshBounds))
            {
                if (!init) { bounds = meshBounds; init = true; }
                else bounds.Encapsulate(meshBounds);
            }

            return init;
        }

        private int CountAliveParticles()
        {
            int count = 0;
            foreach (var ps in _allParticles)
            {
                if (ps != null) count += ps.particleCount;
            }
            return count;
        }

        private static bool IsRootParticle(ParticleSystem ps)
        {
            var parent = ps.transform.parent;
            while (parent != null)
            {
                if (parent.GetComponent<ParticleSystem>() != null) return false;
                parent = parent.parent;
            }
            return true;
        }

        private bool TryComputeRendererBounds(out Bounds bounds) => TryComputeRendererBounds(_renderers, out bounds);

        private static bool TryComputeRendererBounds(Renderer[] renderers, out Bounds bounds)
        {
            bool init = false;
            bounds = new Bounds();

            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.bounds.size.sqrMagnitude <= 0.0000001f) continue;

                if (!init) { bounds = r.bounds; init = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!init) return false;

            // 한 축이 완전히 납작하면 프레이밍 반지름이 과소평가되므로 최소 두께를 준다.
            Vector3 size = bounds.size;
            float min = Mathf.Max(size.x, size.y, size.z) * 0.02f;
            bounds.size = new Vector3(Mathf.Max(size.x, min), Mathf.Max(size.y, min), Mathf.Max(size.z, min));
            return true;
        }

        // ── 재생 제어 ───────────────────────────────────────────────────────────
        public void Tick(float deltaTime)
        {
            if (_kind != PreviewKind.Effect || !_isPlaying) return;
            if (_rootParticles.Length == 0) return;

            float step = deltaTime * _speed;
            _playTime += step;

            if (_playTime >= _duration)
            {
                _playTime -= _duration;

                // 루프 이펙트는 파티클 시스템이 알아서 순환하므로 재시작하면 오히려 끊긴다.
                // 1회성 이펙트만 처음으로 되돌려, 미리보기에서는 항상 반복 재생되도록 한다.
                if (!_isLooping)
                {
                    SimulateTo(_playTime);
                    RebindAnimators();
                    return;
                }
            }

            foreach (var ps in _rootParticles)
            {
                if (ps == null) continue;
                ps.Simulate(step, true, false, true);
            }

            foreach (var animator in _animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                animator.Update(step);
            }
        }

        private void RebindAnimators()
        {
            foreach (var animator in _animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        private void SimulateTo(float time)
        {
            foreach (var ps in _rootParticles)
            {
                if (ps == null) continue;
                ps.Simulate(Mathf.Max(0f, time), true, true, true);
            }
        }

        public void ResetPlayback()
        {
            _playTime = 0f;
            SimulateTo(0f);
            RebindAnimators();
        }

        public void ResetView()
        {
            _rotation = Vector2.zero;
            _pan = Vector2.zero;
            _zoom = 1f;
        }

        /// <summary>
        /// 레퍼런스 해상도가 바뀌면 UI 프리팹은 레이아웃을 다시 해석해야 한다.
        /// </summary>
        public void RefreshUILayout()
        {
            if (_kind != PreviewKind.UI || _canvasGO == null || _instance == null) return;

            ((RectTransform)_canvasGO.transform).sizeDelta = _referenceResolution;

            RebuildUILayout();
            ComputeUIFraming();
        }

        private void RebuildUILayout()
        {
            Canvas.ForceUpdateCanvases();

            var instanceRT = _instance.transform as RectTransform;
            if (instanceRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(instanceRT);

            // TMP는 프리뷰 씬에서 메시가 자동 갱신되지 않아 명시적으로 갱신해야 한다.
            foreach (var text in _instance.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (text != null) text.ForceMeshUpdate();
            }

            Canvas.ForceUpdateCanvases();
        }

        // ── 입력 ────────────────────────────────────────────────────────────────
        /// <summary>카메라 조작 입력을 처리하고, 뷰가 변경되었으면 true를 반환한다.</summary>
        public bool HandleInput(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return false;

            if (e.type == EventType.ScrollWheel)
            {
                _zoom = Mathf.Clamp(_zoom * (1f + e.delta.y * 0.05f), 0.05f, 20f);
                e.Use();
                return true;
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                // UI는 회전시키면 오히려 판독이 어려워지므로 팬만 허용한다.
                if (_kind == PreviewKind.UI) _pan += new Vector2(-e.delta.x, e.delta.y);
                else _rotation += new Vector2(e.delta.x, e.delta.y);
                e.Use();
                return true;
            }

            if (e.type == EventType.MouseDrag && e.button == 2)
            {
                _pan += new Vector2(-e.delta.x, e.delta.y);
                e.Use();
                return true;
            }

            return false;
        }

        // ── 렌더링 ──────────────────────────────────────────────────────────────
        public void Draw(Rect rect, Color backgroundColor, bool useChecker, bool showRectGizmo)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (rect.width < 4f || rect.height < 4f) return;

            DrawBackground(rect, backgroundColor, useChecker);

            if (_instance == null || _preview == null) return;
            if (_status == PreviewStatus.ZeroSize || _status == PreviewStatus.Empty) return;

            // 체커 배경일 때만 투명 클리어(알파 합성). 단색 배경은 카메라가 직접 클리어해
            // 프리뷰 RT의 알파 처리 방식에 의존하지 않도록 한다.
            Color clearColor = useChecker ? new Color(0f, 0f, 0f, 0f) : backgroundColor;

            _preview.BeginPreview(rect, GUIStyle.none);
            ConfigureCamera(rect, clearColor);
            _preview.camera.Render();
            Texture texture = _preview.EndPreview();
            // 단색 배경은 카메라가 이미 불투명하게 클리어했으므로 알파 블렌드를 끈다.
            // (일부 셰이더가 알파 0을 기록해 화면 전체가 사라지는 것을 방지)
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, useChecker);

            if (showRectGizmo && _kind == PreviewKind.UI) DrawRectGizmo(rect);
        }

        /// <summary>
        /// GUI 컨텍스트 없이 지정 크기로 렌더링한다. 썸네일 캐싱과 자동 검증에 사용.
        /// </summary>
        public Texture2D RenderToTexture(int width, int height, Color background)
        {
            if (_instance == null || _preview == null) return null;
            if (width < 4 || height < 4) return null;

            Camera cam = _preview.camera;
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            ConfigureCamera(new Rect(0f, 0f, width, height), background);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply();

            RenderTexture.active = previousActive;
            cam.targetTexture = previousTarget;
            RenderTexture.ReleaseTemporary(rt);

            return texture;
        }

        /// <summary>디버그/검증용 프레이밍 바운드.</summary>
        public Bounds FrameBounds => _frameBounds;

        private void DrawBackground(Rect rect, Color backgroundColor, bool useChecker)
        {
            if (useChecker)
            {
                Texture2D checker = GetCheckerTexture();
                Vector2 tiling = new Vector2(rect.width / checker.width, rect.height / checker.height);
                GUI.DrawTextureWithTexCoords(rect, checker, new Rect(0f, 0f, tiling.x, tiling.y));
            }
            else
            {
                EditorGUI.DrawRect(rect, backgroundColor);
            }
        }

        private void ConfigureCamera(Rect rect, Color clearColor)
        {
            Camera cam = _preview.camera;

            // uGUI 캔버스는 CameraType.Preview 카메라에서 렌더되지 않는다(파티클/메시는 정상).
            // UI일 때만 Game으로 전환해 캔버스가 그려지도록 한다.
            cam.cameraType = _kind == PreviewKind.UI ? CameraType.Game : CameraType.Preview;

            cam.cullingMask = ~0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clearColor;

            if (_kind == PreviewKind.UI) ConfigureOrthoCamera(cam, rect);
            else ConfigurePerspectiveCamera(cam, rect);
        }

        private void ConfigureOrthoCamera(Camera cam, Rect rect)
        {
            float aspect = rect.width / rect.height;
            float halfHeight = GetOrthoHalfHeight(rect);

            // 팬은 픽셀 단위로 누적되므로 월드 단위로 환산해야 드래그와 이동량이 일치한다.
            float worldPerPixel = (halfHeight * 2f) / rect.height;
            Vector3 center = _frameBounds.center + new Vector3(_pan.x * worldPerPixel, _pan.y * worldPerPixel, 0f);

            cam.orthographic = true;
            cam.orthographicSize = halfHeight;
            cam.aspect = aspect;
            cam.transform.position = new Vector3(center.x, center.y, -1000f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100000f;
        }

        private float GetOrthoHalfHeight(Rect rect)
        {
            float aspect = Mathf.Max(0.01f, rect.width / rect.height);

            float halfByHeight = _frameBounds.extents.y * FramePadding;
            float halfByWidth = (_frameBounds.extents.x * FramePadding) / aspect;
            return Mathf.Max(0.01f, Mathf.Max(halfByHeight, halfByWidth)) * _zoom;
        }

        private void ConfigurePerspectiveCamera(Camera cam, Rect rect)
        {
            const float fieldOfView = 35f;

            float aspect = Mathf.Max(0.01f, rect.width / rect.height);
            float tanVertical = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * aspect;

            Quaternion rotation = Quaternion.Euler(_rotation.y * 0.4f, _rotation.x * 0.4f, 0f);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;

            // 바운딩 박스 8개 꼭짓점을 카메라 공간으로 투영해, 가로·세로 모두 딱 들어차는 최소 거리를 구한다.
            // 바운딩 구 기준으로 맞추면 대각선 반지름만큼 항상 여유가 생겨 실제보다 작게 보인다.
            Vector3 extents = _frameBounds.extents * FramePadding;
            float distance = 0.0001f;
            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);

                float x = Vector3.Dot(corner, right);
                float y = Vector3.Dot(corner, up);
                float z = Vector3.Dot(corner, forward);

                distance = Mathf.Max(distance, Mathf.Abs(x) / tanHorizontal - z);
                distance = Mathf.Max(distance, Mathf.Abs(y) / tanVertical - z);
                minDepth = Mathf.Min(minDepth, z);
                maxDepth = Mathf.Max(maxDepth, z);
            }

            distance *= _zoom;

            float worldPerPixel = (2f * distance * tanVertical) / rect.height;
            Vector3 center = _frameBounds.center + rotation * new Vector3(_pan.x * worldPerPixel, _pan.y * worldPerPixel, 0f);

            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            cam.aspect = aspect;
            cam.transform.rotation = rotation;
            cam.transform.position = center - forward * distance;
            cam.nearClipPlane = Mathf.Max(0.01f, (distance + minDepth) * 0.5f);
            cam.farClipPlane = distance + maxDepth + Mathf.Max(1f, maxDepth - minDepth);
        }

        // Rect 와이어프레임 오버레이. UI 카메라는 회전이 없어 월드 → GUI 매핑이 선형이다.
        private void DrawRectGizmo(Rect rect)
        {
            float halfHeight = GetOrthoHalfHeight(rect);
            float halfWidth = halfHeight * (rect.width / rect.height);
            Vector3 center = _preview.camera.transform.position;

            float xMin = center.x - halfWidth;
            float yMax = center.y + halfHeight;
            float scaleX = rect.width / (halfWidth * 2f);
            float scaleY = rect.height / (halfHeight * 2f);

            Color lineColor = new Color(0.35f, 0.85f, 1f, 0.7f);

            foreach (var rt in _rects)
            {
                if (rt == null) continue;
                rt.GetWorldCorners(_corners);

                Vector2 min = new Vector2(
                    rect.x + (_corners[0].x - xMin) * scaleX,
                    rect.y + (yMax - _corners[1].y) * scaleY);
                Vector2 max = new Vector2(
                    rect.x + (_corners[2].x - xMin) * scaleX,
                    rect.y + (yMax - _corners[0].y) * scaleY);

                Rect r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (r.width < 1f || r.height < 1f) continue;

                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), lineColor);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), lineColor);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), lineColor);
                EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), lineColor);
            }
        }

        private static Texture2D GetCheckerTexture()
        {
            if (_checkerTexture != null) return _checkerTexture;

            const int size = 32;
            const int cell = 16;
            _checkerTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };

            Color a = new Color(0.22f, 0.22f, 0.22f, 1f);
            Color b = new Color(0.28f, 0.28f, 0.28f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool even = ((x / cell) + (y / cell)) % 2 == 0;
                    _checkerTexture.SetPixel(x, y, even ? a : b);
                }
            }

            _checkerTexture.Apply();
            return _checkerTexture;
        }

        // ── 정리 ────────────────────────────────────────────────────────────────
        private static void ApplyHideFlags(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void ClearInstance()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
            if (_canvasGO != null) Object.DestroyImmediate(_canvasGO);

            _instance = null;
            _canvasGO = null;
            _rects = new RectTransform[0];
            _rootParticles = new ParticleSystem[0];
            _allParticles = new ParticleSystem[0];
            _renderers = new Renderer[0];
            _nonParticleRenderers = new Renderer[0];
            _particleRenderers = new ParticleSystemRenderer[0];
            _animators = new Animator[0];
            _isLooping = false;
            _playTime = 0f;
            _duration = 1f;
            _rawDuration = 1f;
        }

        public void Dispose()
        {
            ClearInstance();
            ReleasePreview();
        }
    }
}
