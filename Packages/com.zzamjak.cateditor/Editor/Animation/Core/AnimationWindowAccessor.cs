using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CAT.AnimationUtility
{
    // Animation Window의 내부 상태에 Reflection으로 접근하는 래퍼.
    // AnimSyncTool, AnimationOffset, AnimationParticleSimulator 3곳의 중복 Reflection 코드 통합.
    public class AnimationWindowAccessor
    {
        private static readonly Type _animWindowType;

        // 캐싱 필드
        private EditorWindow _window;
        private object _animEditor;

        static AnimationWindowAccessor()
        {
            _animWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow");
        }

        // Animation Window 참조 (null이면 재탐색)
        public EditorWindow Window
        {
            get
            {
                if (_window == null) FindWindow();
                return _window;
            }
        }

        // 현재 활성 Animation Clip
        public AnimationClip ActiveClip
        {
            get
            {
                var state = GetState();
                if (state == null) return null;
                var prop = state.GetType().GetProperty("activeAnimationClip",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return prop?.GetValue(state) as AnimationClip;
            }
        }

        // 현재 루트 GameObject
        public GameObject ActiveRoot
        {
            get
            {
                var state = GetState();
                if (state == null) return null;
                var prop = state.GetType().GetProperty("activeRootGameObject",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return prop?.GetValue(state) as GameObject;
            }
        }

        // 현재 재생 시간 (초)
        public float CurrentTime
        {
            get
            {
                var state = GetState();
                if (state == null) return 0f;
                var prop = state.GetType().GetProperty("currentTime",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop == null) return 0f;
                return (float)prop.GetValue(state);
            }
        }

        // 현재 프레임
        public int CurrentFrame
        {
            get
            {
                var state = GetState();
                if (state == null) return 0;
                var prop = state.GetType().GetProperty("currentFrame",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop == null) return 0;
                return (int)prop.GetValue(state);
            }
        }

        // 재생 중 여부
        public bool IsPlaying
        {
            get
            {
                var state = GetState();
                if (state == null) return false;
                var prop = state.GetType().GetProperty("playing",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop == null) return false;
                return (bool)prop.GetValue(state);
            }
        }

        // AnimationWindowState 객체 반환.
        // 방법 1: window.state property (공식적인 방법)
        // 방법 2: window.m_AnimEditor.m_State 필드 (AnimSyncTool 방식, fallback)
        public object GetState()
        {
            var win = Window;
            if (win == null) return null;

            // 방법 1: state property
            var stateProp = win.GetType().GetProperty("state",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateProp != null)
            {
                var s = stateProp.GetValue(win);
                if (s != null) return s;
            }

            // 방법 2: m_AnimEditor.m_State 필드
            var editor = GetAnimEditor();
            if (editor == null) return null;
            return GetFieldDeep(editor, "m_State");
        }

        // m_AnimEditor 객체 반환 (캐싱)
        public object GetAnimEditor()
        {
            if (_animEditor != null) return _animEditor;
            var win = Window;
            if (win == null) return null;
            _animEditor = GetFieldDeep(win, "m_AnimEditor");
            return _animEditor;
        }

        // Window 캐시 무효화 (Window 닫힐 때, 플레이 모드 전환 시 호출)
        public void InvalidateCache()
        {
            _window = null;
            _animEditor = null;
        }

        // 베이스 클래스까지 탐색하는 깊은 Reflection 필드 접근 헬퍼
        public object GetFieldDeep(object obj, string name)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            while (type != null)
            {
                var f = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null) return f.GetValue(obj);
                type = type.BaseType;
            }
            return null;
        }

        // Unity 6 / Unity 2022.x 호환 TreeView 스크롤.
        // Unity 6: Frame(int, bool, bool, bool), Unity 2022: FrameItem(int)
        public void SmartScrollToID(int id)
        {
            try
            {
                var animEditor = GetAnimEditor();
                if (animEditor == null) return;

                var hierarchy = GetFieldDeep(animEditor, "m_Hierarchy");
                if (hierarchy == null) return;

                object treeView = GetFieldDeep(hierarchy, "m_TreeView");
                if (treeView == null) treeView = GetFieldDeep(hierarchy, "treeView");
                if (treeView == null) return;

                var treeType = treeView.GetType();

                // 1번째 시도: Unity 6 방식 (Frame 메서드)
                var frameMethod = treeType.GetMethod("Frame",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new Type[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) }, null);
                if (frameMethod != null)
                {
                    frameMethod.Invoke(treeView, new object[] { id, true, false, true });
                    return;
                }

                // 2번째 시도: Unity 2022.x 방식 (FrameItem 메서드)
                var frameItemMethod = treeType.GetMethod("FrameItem",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (frameItemMethod != null)
                {
                    frameItemMethod.Invoke(treeView, new object[] { id });
                }
            }
            catch { /* 스크롤 실패는 무시 */ }
        }

        // currentFrame을 +1/-1 왕복하여 Animation Window 강제 리프레시
        public void ForceRefresh()
        {
            try
            {
                var state = GetState();
                if (state == null) return;
                var frameProp = state.GetType().GetProperty("currentFrame",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (frameProp == null) return;
                int currentFrame = (int)frameProp.GetValue(state);
                frameProp.SetValue(state, currentFrame + 1);
                EditorApplication.delayCall += () =>
                {
                    if (state != null) frameProp.SetValue(state, currentFrame);
                };
            }
            catch { /* 새로고침 실패는 무시 */ }
        }

        // Animation Window 탐색 (Resources.FindObjectsOfTypeAll 방식)
        private void FindWindow()
        {
            if (_animWindowType == null) return;
            var wins = Resources.FindObjectsOfTypeAll(_animWindowType);
            if (wins == null || wins.Length == 0) return;
            _window = wins[0] as EditorWindow;
            _animEditor = null; // 새 Window이면 animEditor 캐시도 초기화
        }
    }
}
