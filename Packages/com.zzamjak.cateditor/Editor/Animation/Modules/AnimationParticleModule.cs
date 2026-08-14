using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using AnimUtil = UnityEditor.AnimationUtility;

namespace CAT.AnimationUtility
{
    // Animation Window에서 파티클 시뮬레이션을 제어하는 모듈.
    // 게임오브젝트가 켜지는 키프레임 시점에 파티클을 자동 시뮬레이션.
    // 기존 AnimationParticleSimulator.cs를 IAnimationToolModule로 리팩토링.
    public class AnimationParticleModule : IAnimationToolModule
    {
        private struct AnimFrameInfo
        {
            public float time;
            public float value;
        }

        // 인스턴스 필드 (기존 static → 인스턴스로 전환)
        private AnimationClip _animationClip;
        private int _editorFrame;
        private Dictionary<string, AnimFrameInfo> _propertyDirties = new Dictionary<string, AnimFrameInfo>();
        private bool _isOnSimulation;
        private GUIContent _iconContent;
        private bool _isForceClearDirty;

        private AnimationWindowAccessor _accessor;

        public string ModuleName => "AnimationParticle";
        public int UIOrder => 20;

        public void Initialize(AnimationWindowAccessor accessor)
        {
            _accessor = accessor;
        }

        // Animation Window 상단에 파티클 시뮬레이션 토글 버튼 주입
        public void InitUI(VisualElement container)
        {
            var parent = new VisualElement
            {
                style =
                {
                    position = Position.Relative,
                    left = 212f,
                    width = 30f,
                    height = 20f
                }
            };

            var imguiContainer = new IMGUIContainer(OnGUIParticleSimulationToggle);
            imguiContainer.style.flexGrow = 1;
            parent.Add(imguiContainer);
            container.Add(parent);
        }

        // 매 프레임: 파티클 시뮬레이션 처리
        public void OnUpdate()
        {
            if (_isOnSimulation == false && _isForceClearDirty == false)
                return;

            var state = _accessor.GetState();
            if (state == null) return;

            var stateType = state.GetType();
            var bindingFlags = System.Reflection.BindingFlags.Public |
                               System.Reflection.BindingFlags.NonPublic |
                               System.Reflection.BindingFlags.Instance;

            var playingProp = stateType.GetProperty("playing", bindingFlags);
            var frameProp = stateType.GetProperty("currentFrame", bindingFlags);
            var timeProp = stateType.GetProperty("currentTime", bindingFlags);
            var activeRootGameObjectProp = stateType.GetProperty("activeRootGameObject", bindingFlags);

            if (playingProp == null || frameProp == null || timeProp == null || activeRootGameObjectProp == null)
                return;

            var currentFrame = (int)frameProp.GetValue(state);
            var currentTime = (float)timeProp.GetValue(state);

            if (currentFrame != _editorFrame)
            {
                var activeAnimationClipProp = stateType.GetProperty("activeAnimationClip", bindingFlags);
                if (activeAnimationClipProp == null) return;

                var animationClip = activeAnimationClipProp.GetValue(state) as AnimationClip;
                if (_animationClip != animationClip)
                {
                    _propertyDirties.Clear();
                    _animationClip = animationClip;
                }

                if (_animationClip == null) return;

                var activeRootGameObject = activeRootGameObjectProp.GetValue(state) as GameObject;
                if (activeRootGameObject == null) return;

                var curveBindings = AnimUtil.GetCurveBindings(_animationClip);

                foreach (var binding in curveBindings)
                {
                    var curve = AnimUtil.GetEditorCurve(_animationClip, binding);
                    var value = curve.Evaluate(currentTime);
                    var findTarget = activeRootGameObject.transform.Find(binding.path);

                    if (findTarget == null) continue;

                    // 게임오브젝트가 켜지는 키프레임에서만 파티클 처리
                    if (binding.propertyName != "m_IsActive" || !(value >= 0f)) continue;

                    var particleSystems = findTarget.GetComponentsInChildren<ParticleSystem>(true);

                    var key = $"{binding.path}_{binding.propertyName}";
                    if (!_propertyDirties.ContainsKey(key))
                        _propertyDirties[key] = new AnimFrameInfo();
                    var prevValue = _propertyDirties[key].value;

                    if (Math.Abs(prevValue - value) > float.Epsilon)
                    {
                        _propertyDirties[key] = new AnimFrameInfo { time = currentTime, value = value };
                    }

                    // 선택 상태 유지를 위한 selection 업데이트
                    var currentSelection = Selection.objects;
                    var newSelection = new Object[currentSelection.Length + 1];
                    newSelection[0] = findTarget;
                    currentSelection.CopyTo(newSelection, 1);

                    foreach (var particleSystem in particleSystems)
                    {
                        particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        if (!particleSystem.useAutoRandomSeed) continue;
                        particleSystem.useAutoRandomSeed = false;
                        particleSystem.randomSeed = (uint)particleSystem.GetHashCode();
                    }

                    if (_isForceClearDirty == false)
                    {
                        float t = currentTime - _propertyDirties[key].time;
                        foreach (var particleSystem in particleSystems)
                        {
                            particleSystem.Play(false);
                            particleSystem.time = t;
                            particleSystem.Simulate(t, false, true, true);
                        }
                    }

                    _isForceClearDirty = false;
                }
            }

            _editorFrame = currentFrame;
        }

        public void OnSelectionChanged() { }

        // 파티클 시뮬레이션 토글 버튼 GUI
        private void OnGUIParticleSimulationToggle()
        {
            if (_iconContent == null)
            {
                var icon = EditorGUIUtility.IconContent("Particle Effect");
                _iconContent = new GUIContent(icon.image, "Particle Simulation");
            }

            using (new GUILayout.AreaScope(new Rect(0f, 0f, 30f, 30f)))
            {
                var isOnSimulation = GUILayout.Toggle(_isOnSimulation, _iconContent, EditorStyles.toolbarButton);
                if (_isOnSimulation == isOnSimulation) return;
                _isOnSimulation = isOnSimulation;
                _isForceClearDirty = true;
            }
        }

        public void Dispose()
        {
            _propertyDirties.Clear();
        }
    }
}
