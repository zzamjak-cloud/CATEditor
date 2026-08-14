using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace CAT.AnimationUtility
{
    // 애니메이션 에셋 자동 저장 모듈.
    // AnimationClip, AnimatorController 등이 변경되면 일정 간격으로 자동 저장.
    // Tools/Animation Auto Save/Enabled 메뉴로 ON/OFF 전환.
    public class AnimationAutoSaveModule : IAnimationToolModule
    {
        private const string EnabledPrefKey = "AnimationAutoSave.Enabled";
        private const string MenuPath = "Tools/Animation Auto Save/Enabled";
        private const double SaveIntervalSeconds = 2.0;

        private bool _pendingSave;
        private double _nextSaveTime;
        private bool _enabled;
        private bool _registeredCallbacks;

        public string ModuleName => "AnimationAutoSave";
        public int UIOrder => 100; // 우선순위가 낮은 유틸리티 모듈

        public void Initialize(AnimationWindowAccessor accessor)
        {
            _enabled = EditorPrefs.GetBool(EnabledPrefKey, true);
            RegisterCallbacks();
        }

        // UI 없음
        public void InitUI(VisualElement container) { }

        public void OnUpdate()
        {
            // 메뉴 토글 반영 (EditorPrefs 읽기는 Dictionary 조회 수준으로 가벼움)
            _enabled = EditorPrefs.GetBool(EnabledPrefKey, true);

            if (!_enabled) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!_pendingSave) return;

            var now = EditorApplication.timeSinceStartup;
            if (now < _nextSaveTime) return;

            var savedCount = SaveDirtyAnimationAssets();
            _nextSaveTime = now + SaveIntervalSeconds;

            if (savedCount > 0)
                _pendingSave = false;
        }

        // 선택 변경 처리 불필요
        public void OnSelectionChanged() { }

        public void Dispose()
        {
            UnregisterCallbacks();
        }

        // ──────────────────────────────────────────
        // Undo 콜백 등록 (모듈 인터페이스에 없는 이벤트)
        // ──────────────────────────────────────────

        private void RegisterCallbacks()
        {
            if (_registeredCallbacks) return;
            _registeredCallbacks = true;

            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += MarkPendingSave;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void UnregisterCallbacks()
        {
            if (!_registeredCallbacks) return;
            _registeredCallbacks = false;

            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= MarkPendingSave;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        // ──────────────────────────────────────────
        // 변경 감지
        // ──────────────────────────────────────────

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
        {
            if (!_enabled) return modifications;

            for (int i = 0; i < modifications.Length; i++)
            {
                var target = modifications[i].currentValue?.target;
                if (IsAnimationRelated(target))
                {
                    _pendingSave = true;
                    break;
                }
            }

            return modifications;
        }

        private void MarkPendingSave()
        {
            if (!_enabled) return;
            _pendingSave = true;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!_enabled) return;
            if (state == PlayModeStateChange.EnteredEditMode)
                _pendingSave = true;
        }

        // ──────────────────────────────────────────
        // 저장 로직
        // ──────────────────────────────────────────

        private static int SaveDirtyAnimationAssets()
        {
            int dirtyCount = 0;

            foreach (var clip in Resources.FindObjectsOfTypeAll<AnimationClip>())
            {
                if (!IsDirtyProjectAsset(clip)) continue;
                AssetDatabase.SaveAssetIfDirty(clip);
                dirtyCount++;
            }

            foreach (var controller in Resources.FindObjectsOfTypeAll<AnimatorController>())
            {
                if (!IsDirtyProjectAsset(controller)) continue;
                AssetDatabase.SaveAssetIfDirty(controller);
                dirtyCount++;
            }

            foreach (var overrideController in Resources.FindObjectsOfTypeAll<AnimatorOverrideController>())
            {
                if (!IsDirtyProjectAsset(overrideController)) continue;
                AssetDatabase.SaveAssetIfDirty(overrideController);
                dirtyCount++;
            }

            foreach (var animState in Resources.FindObjectsOfTypeAll<AnimatorState>())
            {
                if (!IsDirtyProjectAsset(animState)) continue;
                if (SaveOwnerAssetIfDirty(animState)) dirtyCount++;
            }

            foreach (var stateMachine in Resources.FindObjectsOfTypeAll<AnimatorStateMachine>())
            {
                if (!IsDirtyProjectAsset(stateMachine)) continue;
                if (SaveOwnerAssetIfDirty(stateMachine)) dirtyCount++;
            }

            foreach (var transition in Resources.FindObjectsOfTypeAll<AnimatorTransitionBase>())
            {
                if (!IsDirtyProjectAsset(transition)) continue;
                if (SaveOwnerAssetIfDirty(transition)) dirtyCount++;
            }

            foreach (var blendTree in Resources.FindObjectsOfTypeAll<BlendTree>())
            {
                if (!IsDirtyProjectAsset(blendTree)) continue;
                if (SaveOwnerAssetIfDirty(blendTree)) dirtyCount++;
            }

            return dirtyCount;
        }

        private static bool IsDirtyProjectAsset(Object obj)
        {
            if (obj == null) return false;
            if (!AssetDatabase.Contains(obj)) return false;
            return EditorUtility.IsDirty(obj);
        }

        private static bool SaveOwnerAssetIfDirty(Object subAsset)
        {
            var assetPath = AssetDatabase.GetAssetPath(subAsset);
            if (string.IsNullOrEmpty(assetPath)) return false;

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (mainAsset == null) return false;

            if (!EditorUtility.IsDirty(mainAsset))
                EditorUtility.SetDirty(mainAsset);
            AssetDatabase.SaveAssetIfDirty(mainAsset);
            return true;
        }

        private static bool IsAnimationRelated(Object obj)
        {
            return obj is AnimationClip
                   || obj is AnimatorController
                   || obj is AnimatorOverrideController
                   || obj is AnimatorState
                   || obj is AnimatorStateMachine
                   || obj is AnimatorTransitionBase
                   || obj is BlendTree
                   || obj is Motion;
        }

        // ──────────────────────────────────────────
        // 메뉴 토글 (static — 모듈 인스턴스와 독립)
        // ──────────────────────────────────────────

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            var enabled = !EditorPrefs.GetBool(EnabledPrefKey, true);
            EditorPrefs.SetBool(EnabledPrefKey, enabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(EnabledPrefKey, true));
            return true;
        }
    }
}
