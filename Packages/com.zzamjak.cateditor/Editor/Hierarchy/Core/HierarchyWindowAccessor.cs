using System;
using UnityEditor;
using UnityEngine;

namespace CAT.HierarchyUtility
{
    // Hierarchy Window EditorWindow 참조를 캐싱하는 래퍼 클래스.
    // null 체크 후 재탐색하여 Window 재생성 시에도 안정적으로 참조를 유지.
    public class HierarchyWindowAccessor
    {
        private static readonly Type _hierarchyWindowType =
            typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");

        // Window를 못 찾았을 때 매 틱 재탐색하면 Resources.FindObjectsOfTypeAll이
        // 전체 오브젝트를 스캔하므로 에디터가 눈에 띄게 느려진다. 재탐색 간격을 둔다.
        private const double SearchRetryInterval = 1.0;

        private EditorWindow _window;
        private double _nextSearchTime;

        public EditorWindow Window
        {
            get
            {
                if (_window != null) return _window;

                double now = EditorApplication.timeSinceStartup;
                if (now < _nextSearchTime) return null;
                _nextSearchTime = now + SearchRetryInterval;

                var windows = Resources.FindObjectsOfTypeAll(_hierarchyWindowType);
                _window = windows.Length > 0 ? windows[0] as EditorWindow : null;
                return _window;
            }
        }

        // Window 캐시 무효화 (플레이 모드 전환, Window 재생성 등의 상황에서 호출).
        public void InvalidateCache()
        {
            _window = null;
            _nextSearchTime = 0d;   // 즉시 재탐색 허용
        }
    }
}
