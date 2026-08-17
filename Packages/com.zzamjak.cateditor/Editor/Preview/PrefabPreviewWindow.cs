using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Utility.PrefabPreview
{
    /// <summary>
    /// 프로젝트 뷰에서 프리팹을 선택하면 미리보기를 표시하는 창.
    /// UI 프리팹은 레퍼런스 해상도 캔버스 기준으로, 이펙트 프리팹은 재생 상태로 렌더링한다.
    /// </summary>
    public class PrefabPreviewWindow : EditorWindow
    {
        private const string PrefKeyBackground = "CAT_PrefabPreview_Background";
        private const string PrefKeyShowRect = "CAT_PrefabPreview_ShowRect";
        private const string PrefKeyRefWidth = "CAT_PrefabPreview_RefWidth";
        private const string PrefKeyRefHeight = "CAT_PrefabPreview_RefHeight";

        private enum BackgroundMode { Dark, Checker, Light }

        private static readonly string[] BackgroundNames = { "Dark", "Checker", "Light" };

        private PrefabPreviewRenderer _renderer;
        private GameObject _target;

        private BackgroundMode _background = BackgroundMode.Dark;
        private bool _showRect;
        private bool _locked;
        private Vector2 _referenceResolution = new Vector2(1080f, 1920f);

        private const double PlaybackInterval = 1.0 / 30.0;
        private const double VisibilityTimeout = 0.5;

        private double _lastUpdateTime;
        private double _lastGuiTime;
        private string _statusMessage;

        // 매 리페인트마다 새로 만들면 GC 부담이 되므로 캐싱한다.
        private static GUIStyle _hintStyle;
        private static GUIStyle _centerLabelStyle;
        private static GUIStyle _statusStyle;

        [MenuItem("CAT/Utility/Prefab Preview")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabPreviewWindow>("Prefab Preview");
            window.minSize = new Vector2(260f, 280f);
        }

        private void OnEnable()
        {
            LoadSettings();

            _renderer = new PrefabPreviewRenderer { ReferenceResolution = _referenceResolution };
            _lastUpdateTime = EditorApplication.timeSinceStartup;

            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;

            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;

            SaveSettings();

            _renderer?.Dispose();
            _renderer = null;
        }

        private void LoadSettings()
        {
            _background = (BackgroundMode)EditorPrefs.GetInt(PrefKeyBackground, (int)BackgroundMode.Dark);
            _showRect = EditorPrefs.GetBool(PrefKeyShowRect, false);
            _referenceResolution = new Vector2(
                EditorPrefs.GetFloat(PrefKeyRefWidth, 1080f),
                EditorPrefs.GetFloat(PrefKeyRefHeight, 1920f));
        }

        private void SaveSettings()
        {
            EditorPrefs.SetInt(PrefKeyBackground, (int)_background);
            EditorPrefs.SetBool(PrefKeyShowRect, _showRect);
            EditorPrefs.SetFloat(PrefKeyRefWidth, _referenceResolution.x);
            EditorPrefs.SetFloat(PrefKeyRefHeight, _referenceResolution.y);
        }

        // ── 선택 감지 ───────────────────────────────────────────────────────────
        private void OnSelectionChanged()
        {
            if (_locked || _renderer == null) return;

            GameObject prefab = GetSelectedPrefabAsset();

            // 프리팹이 아닌 항목(폴더, 텍스처, 스크립트...)을 고르면 대상을 비워
            // 시뮬레이션·렌더링·프리뷰 씬을 모두 즉시 정리한다.
            // 대상 에셋이 삭제된 경우에도 정리되도록 파괴 여부를 따로 확인한다.
            bool targetDestroyed = !ReferenceEquals(_target, null) && _target == null;
            if (!targetDestroyed && ReferenceEquals(prefab, _target)) return;

            _target = prefab;
            ApplyTarget();
            Repaint();
        }

        // 프로젝트 뷰의 프리팹 에셋만 대상으로 한다 (하이어라키 인스턴스는 무시).
        private static GameObject GetSelectedPrefabAsset()
        {
            Object[] selection = Selection.objects;
            for (int i = 0; i < selection.Length; i++)
            {
                var go = selection[i] as GameObject;
                if (go == null) continue;
                if (!PrefabUtility.IsPartOfPrefabAsset(go)) continue;
                return go;
            }
            return null;
        }

        private void ApplyTarget()
        {
            _renderer.ReferenceResolution = _referenceResolution;
            _renderer.SetTarget(_target);
            _statusMessage = BuildStatusMessage();
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;

            // 재생할 것이 없으면 아무 일도 하지 않는다 (프리팹 외 선택 시 여기서 끝난다).
            if (_renderer == null || !_renderer.NeedsContinuousRepaint)
            {
                _lastUpdateTime = now;
                return;
            }

            // 다른 탭에 가려져 OnGUI가 오지 않거나, 에디터가 백그라운드이거나,
            // 컴파일·에셋 임포트 중이면 재생을 멈춰 에디터 작업을 방해하지 않는다.
            if (now - _lastGuiTime > VisibilityTimeout ||
                !InternalEditorUtility.isApplicationActive ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                _lastUpdateTime = now;
                return;
            }

            double elapsed = now - _lastUpdateTime;
            if (elapsed < PlaybackInterval) return;   // 30fps로 제한

            _lastUpdateTime = now;
            _renderer.Tick(Mathf.Min((float)elapsed, 0.1f));
            Repaint();
        }

        // ── GUI ─────────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (_renderer == null) return;

            if (Event.current.type == EventType.Repaint)
            {
                _lastGuiTime = EditorApplication.timeSinceStartup;
            }

            EnsureStyles();

            bool isUI = _renderer.Kind == PrefabPreviewRenderer.PreviewKind.UI;
            bool isEffect = _renderer.Kind == PrefabPreviewRenderer.PreviewKind.Effect;

            float toolbarHeight = 20f + ((isUI || isEffect) ? 20f : 0f);
            string status = _statusMessage;
            float statusHeight = string.IsNullOrEmpty(status) ? 0f : 20f;

            var toolbarRect = new Rect(0f, 0f, position.width, toolbarHeight);
            GUILayout.BeginArea(toolbarRect);
            DrawMainToolbar();
            if (isUI) DrawUIToolbar();
            else if (isEffect) DrawEffectToolbar();
            GUILayout.EndArea();

            var previewRect = new Rect(0f, toolbarHeight, position.width,
                Mathf.Max(0f, position.height - toolbarHeight - statusHeight));

            DrawPreview(previewRect);

            if (statusHeight > 0f)
            {
                var statusRect = new Rect(0f, position.height - statusHeight, position.width, statusHeight);
                DrawStatusBar(statusRect, status);
            }
        }

        private void DrawMainToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool locked = GUILayout.Toggle(_locked, _locked ? "고정됨" : "고정", EditorStyles.toolbarButton, GUILayout.Width(52f));
            if (locked != _locked)
            {
                _locked = locked;
                if (!_locked) OnSelectionChanged();
            }

            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                ApplyTarget();
            }

            GUILayout.FlexibleSpace();

            var background = (BackgroundMode)EditorGUILayout.Popup((int)_background, BackgroundNames, EditorStyles.toolbarPopup, GUILayout.Width(70f));
            if (background != _background)
            {
                _background = background;
                Repaint();
            }

            if (GUILayout.Button("뷰 리셋", EditorStyles.toolbarButton, GUILayout.Width(56f)))
            {
                _renderer.ResetView();
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawUIToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("레퍼런스", EditorStyles.miniLabel, GUILayout.Width(52f));

            EditorGUI.BeginChangeCheck();
            float width = EditorGUILayout.DelayedFloatField(_referenceResolution.x, EditorStyles.toolbarTextField, GUILayout.Width(52f));
            GUILayout.Label("×", EditorStyles.miniLabel, GUILayout.Width(12f));
            float height = EditorGUILayout.DelayedFloatField(_referenceResolution.y, EditorStyles.toolbarTextField, GUILayout.Width(52f));
            if (EditorGUI.EndChangeCheck())
            {
                SetReferenceResolution(new Vector2(width, height));
            }

            if (GUILayout.Button("자동", EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                SetReferenceResolution(DetectReferenceResolution(_target));
            }

            GUILayout.FlexibleSpace();

            bool showRect = GUILayout.Toggle(_showRect, "Rect 표시", EditorStyles.toolbarButton, GUILayout.Width(66f));
            if (showRect != _showRect)
            {
                _showRect = showRect;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEffectToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool playing = GUILayout.Toggle(_renderer.IsPlaying, _renderer.IsPlaying ? "❚❚ 일시정지" : "▶ 재생",
                EditorStyles.toolbarButton, GUILayout.Width(76f));
            if (playing != _renderer.IsPlaying)
            {
                _renderer.IsPlaying = playing;
                Repaint();
            }

            if (GUILayout.Button("↺ 처음부터", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                _renderer.ResetPlayback();
                Repaint();
            }

            GUILayout.Label("속도", EditorStyles.miniLabel, GUILayout.Width(28f));
            _renderer.Speed = GUILayout.HorizontalSlider(_renderer.Speed, 0.1f, 3f, GUILayout.Width(60f));

            GUILayout.FlexibleSpace();

            string loopTag = _renderer.IsLooping ? "loop" : "1회성 · 반복 재생";
            GUILayout.Label($"{_renderer.PlayTime:0.00}s / {_renderer.Duration:0.00}s  ({loopTag})", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreview(Rect rect)
        {
            if (_target == null)
            {
                EditorGUI.DrawRect(rect, GetBackgroundColor());
                DrawCenteredHint(rect, "프로젝트 뷰에서 프리팹을 선택하세요");
                return;
            }

            var status = _renderer.Status;
            if (status == PrefabPreviewRenderer.PreviewStatus.ZeroSize ||
                status == PrefabPreviewRenderer.PreviewStatus.Empty)
            {
                // 억지로 렌더하면 검은 사각형만 나오므로 아이콘과 안내로 대체한다.
                EditorGUI.DrawRect(rect, GetBackgroundColor());
                DrawFallbackIcon(rect);
                return;
            }

            if (_renderer.HandleInput(rect)) Repaint();

            // 그려지는 Graphic이 없는 컨테이너는 외곽선이라도 보여야 판독이 된다.
            bool showRect = _showRect || status == PrefabPreviewRenderer.PreviewStatus.NoGraphic;

            _renderer.Draw(rect, GetBackgroundColor(), _background == BackgroundMode.Checker, showRect);
        }

        private static void EnsureStyles()
        {
            if (_hintStyle != null) return;

            _hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
            _centerLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            _statusStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
        }

        private void DrawFallbackIcon(Rect rect)
        {
            Texture icon = AssetPreview.GetMiniThumbnail(_target);
            if (icon != null)
            {
                var iconRect = new Rect(rect.center.x - 24f, rect.center.y - 34f, 48f, 48f);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            var labelRect = new Rect(rect.x, rect.center.y + 18f, rect.width, 20f);
            GUI.Label(labelRect, _target.name, _centerLabelStyle);
        }

        private static void DrawCenteredHint(Rect rect, string message)
        {
            GUI.Label(rect, message, _hintStyle);
        }

        private void DrawStatusBar(Rect rect, string message)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));

            _statusStyle.normal.textColor = GetStatusColor();
            GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height), message, _statusStyle);
        }

        private string BuildStatusMessage()
        {
            if (_target == null) return null;

            switch (_renderer.Status)
            {
                case PrefabPreviewRenderer.PreviewStatus.Stretch:
                    return $"Stretch 앵커 — {_referenceResolution.x:0}×{_referenceResolution.y:0} 기준으로 표시 중";
                case PrefabPreviewRenderer.PreviewStatus.NoGraphic:
                    return "그려지는 Graphic이 없습니다 — Rect 외곽선만 표시";
                case PrefabPreviewRenderer.PreviewStatus.ZeroSize:
                    return "앵커 전용 — 부모 Rect 없이는 크기가 정의되지 않아 미리보기 불가";
                case PrefabPreviewRenderer.PreviewStatus.Empty:
                    return "렌더 가능한 요소가 없습니다";
                default:
                    return null;
            }
        }

        private Color GetStatusColor()
        {
            switch (_renderer.Status)
            {
                case PrefabPreviewRenderer.PreviewStatus.ZeroSize:
                case PrefabPreviewRenderer.PreviewStatus.Empty:
                    return new Color(1f, 0.6f, 0.4f);
                default:
                    return new Color(0.75f, 0.75f, 0.75f);
            }
        }

        private Color GetBackgroundColor()
        {
            switch (_background)
            {
                case BackgroundMode.Light: return new Color(0.78f, 0.78f, 0.78f, 1f);
                case BackgroundMode.Checker: return new Color(0.25f, 0.25f, 0.25f, 1f);
                default: return new Color(0.12f, 0.12f, 0.12f, 1f);
            }
        }

        private void SetReferenceResolution(Vector2 resolution)
        {
            resolution = new Vector2(Mathf.Max(1f, resolution.x), Mathf.Max(1f, resolution.y));
            if (resolution == _referenceResolution) return;

            _referenceResolution = resolution;
            _renderer.ReferenceResolution = resolution;
            _renderer.RefreshUILayout();
            _statusMessage = BuildStatusMessage();
            SaveSettings();
            Repaint();
        }

        // 프리팹 자체의 CanvasScaler → 열린 씬의 CanvasScaler 순으로 탐색한다.
        private Vector2 DetectReferenceResolution(GameObject prefab)
        {
            if (prefab != null)
            {
                var scaler = prefab.GetComponentInChildren<CanvasScaler>(true);
                if (scaler != null && scaler.referenceResolution.sqrMagnitude > 1f)
                {
                    return scaler.referenceResolution;
                }
            }

            var sceneScaler = Object.FindFirstObjectByType<CanvasScaler>(FindObjectsInactive.Include);
            if (sceneScaler != null && sceneScaler.referenceResolution.sqrMagnitude > 1f)
            {
                return sceneScaler.referenceResolution;
            }

            return new Vector2(1080f, 1920f);
        }
    }
}
