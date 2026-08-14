using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using AnimUtil = UnityEditor.AnimationUtility;

namespace CAT.AnimationUtility
{
    // Hierarchy에서 GameObject를 선택하면 Animation Window에서 해당 항목으로 자동 스크롤.
    // 기존 AnimSyncTool.cs를 IAnimationToolModule 기반으로 리팩토링.
    public class AnimationSyncModule : IAnimationToolModule
    {
        private AnimationWindowAccessor _accessor;

        public string ModuleName => "AnimSync";
        public int UIOrder => 0;

        public void Initialize(AnimationWindowAccessor accessor)
        {
            _accessor = accessor;
        }

        // UI 없음
        public void InitUI(VisualElement container) { }

        // 매 프레임 업데이트 불필요
        public void OnUpdate() { }

        // Hierarchy 선택 변경 시 Animation Window 자동 스크롤
        public void OnSelectionChanged()
        {
            // Hierarchy 창이 활성화된 경우에만 동작 (Animation Window 포커스 중에는 무시)
            if (EditorWindow.focusedWindow == null ||
                EditorWindow.focusedWindow.GetType().Name != "SceneHierarchyWindow")
                return;

            var selectedGo = Selection.activeGameObject;
            if (selectedGo == null) return;

            var win = _accessor.Window;
            if (win == null) return;

            try
            {
                var editor = _accessor.GetAnimEditor();
                if (editor == null) return;

                var state = _accessor.GetState();
                if (state == null) return;

                // activeRootGameObject로 경로 계산
                var rootProp = state.GetType().GetProperty("activeRootGameObject",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var rootGo = rootProp?.GetValue(state) as GameObject;
                if (rootGo == null) return;

                var targetPath = AnimUtil.CalculateTransformPath(selectedGo.transform, rootGo.transform);

                // hierarchyData에서 행 목록 가져오기
                var hd = _accessor.GetFieldDeep(state, "hierarchyData");
                if (hd == null) return;

                var getRows = hd.GetType().GetMethod("GetRows",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var rows = getRows?.Invoke(hd, null) as System.Collections.IEnumerable;
                if (rows == null) return;

                var matchingIDs = new List<int>();

                // 경로가 일치하는 행 ID 수집 및 그룹 확장
                foreach (object item in rows)
                {
                    if (item == null) continue;
                    var pathField = item.GetType().GetField("path",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var nodePath = pathField?.GetValue(item) as string;

                    if (nodePath != null && (nodePath == targetPath || nodePath.EndsWith("/" + selectedGo.name)))
                    {
                        var idProp = item.GetType().GetProperty("id",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        int id = (int)idProp.GetValue(item);
                        matchingIDs.Add(id);

                        // 그룹 확장
                        state.GetType().GetMethod("SetExpanded",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.Invoke(state, new object[] { id, true });
                    }
                }

                if (matchingIDs.Count == 0) return;

                // hierarchyState의 selectedIDs 업데이트
                var hierarchyStateField = state.GetType().GetField("hierarchyState",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var hierarchyState = hierarchyStateField?.GetValue(state);
                if (hierarchyState == null) return;

                var selectedIDsProp = hierarchyState.GetType().GetProperty("selectedIDs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var selectedIDs = selectedIDsProp?.GetValue(hierarchyState) as List<int>;
                if (selectedIDs == null) return;

                selectedIDs.Clear();
                selectedIDs.AddRange(matchingIDs);

                // 선택 변경 내부 알림
                try
                {
                    var onSelChanged = state.GetType().GetMethod("OnHierarchySelectionChanged",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (onSelChanged != null)
                    {
                        var p = onSelChanged.GetParameters();
                        if (p.Length == 0) onSelChanged.Invoke(state, null);
                        else if (p.Length == 1) onSelChanged.Invoke(state, new object[] { selectedIDs });
                    }
                }
                catch { }

                win.Repaint();

                // delayCall로 스크롤 (UI 업데이트 완료 후)
                int firstID = matchingIDs[0];
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (win == null) return;
                        _accessor.SmartScrollToID(firstID);
                        win.Repaint();
                    };
                };
            }
            catch (Exception) { }
        }

        public void Dispose() { }
    }
}
