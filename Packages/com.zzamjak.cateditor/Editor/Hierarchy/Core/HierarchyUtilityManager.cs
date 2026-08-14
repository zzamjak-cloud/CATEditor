using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace CAT.HierarchyUtility
{
    // 하이어라키 유틸리티 모듈 중앙 관리자.
    // IHierarchyToolModule 구현체를 TypeCache로 자동 발견하고, 단일 이벤트 등록으로 통합 관리.
    [InitializeOnLoad]
    public static class HierarchyUtilityManager
    {
        private static readonly List<IHierarchyToolModule> _modules = new List<IHierarchyToolModule>();
        private static readonly HierarchyWindowAccessor _accessor = new HierarchyWindowAccessor();
        private static EditorWindow _injectedWindow; // UI가 주입된 Window 추적

        static HierarchyUtilityManager()
        {
            // TypeCache로 IHierarchyToolModule 구현 클래스 자동 발견
            foreach (var type in TypeCache.GetTypesDerivedFrom<IHierarchyToolModule>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                try
                {
                    var module = Activator.CreateInstance(type) as IHierarchyToolModule;
                    if (module != null) _modules.Add(module);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HierarchyUtility] 모듈 생성 실패 ({type.Name}): {e.Message}");
                }
            }

            // UIOrder 기준 오름차순 정렬
            _modules.Sort((a, b) => a.UIOrder.CompareTo(b.UIOrder));

            // 각 모듈 초기화
            foreach (var module in _modules)
                module.Initialize(_accessor);

            // 이벤트 단일 등록 (중복 제거)
            EditorApplication.update += Update;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
        }

        private static void Update()
        {
            var win = _accessor.Window;
            if (win == null)
            {
                // Window가 닫혔으면 캐시 및 주입 상태 초기화
                if (_injectedWindow != null)
                {
                    _accessor.InvalidateCache();
                    _injectedWindow = null;
                }
            }
            else if (_injectedWindow != win)
            {
                // 새로운 Window이면 UI 주입 (1회)
                InjectUI(win);
                _injectedWindow = win;
            }

            // 각 모듈 업데이트
            foreach (var module in _modules)
                module.OnUpdate();
        }

        // Hierarchy Window rootVisualElement에 모듈 UI를 단 1회 주입
        private static void InjectUI(EditorWindow hierarchyWindow)
        {
            var root = hierarchyWindow.rootVisualElement;
            if (root == null) return;

            // 중복 주입 방지 마커 (크기 0, 숨김)
            if (root.Q<VisualElement>("HierarchyUtilityManagerRoot") != null) return;
            var marker = new VisualElement
            {
                name = "HierarchyUtilityManagerRoot",
                style = { display = DisplayStyle.None }
            };
            root.Add(marker);

            // 각 모듈의 UI는 rootVisualElement에 직접 추가
            foreach (var module in _modules)
            {
                try
                {
                    module.InitUI(root);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[HierarchyUtility] {module.ModuleName} UI 초기화 실패: {e.Message}");
                }
            }
        }

        private static void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            foreach (var module in _modules)
                module.OnHierarchyItemGUI(instanceID, selectionRect);
        }

        private static void OnHierarchyChanged()
        {
            foreach (var module in _modules)
                module.OnHierarchyChanged();
        }

        private static void OnSelectionChanged()
        {
            foreach (var module in _modules)
                module.OnSelectionChanged();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 플레이 모드 전환 시 Window 캐시 초기화 (Window가 재생성될 수 있음)
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                _accessor.InvalidateCache();
                _injectedWindow = null;
            }
        }

        private static void OnPrefabStageOpened(PrefabStage stage) => OnHierarchyChanged();
        private static void OnPrefabStageClosing(PrefabStage stage) => OnHierarchyChanged();
    }
}
