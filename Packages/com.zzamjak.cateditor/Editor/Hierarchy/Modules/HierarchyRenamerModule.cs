using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace CAT.HierarchyUtility
{
    // 하이어라키 창 하단에 UI를 주입하여 선택된 오브젝트의 이름을 변경하는 모듈.
    // UIOrder = 0 (가장 먼저 초기화)
    public class HierarchyRenamerModule : IHierarchyToolModule
    {
        public string ModuleName => "HierarchyRenamer";
        public int UIOrder => 0;

        private const string PREF_KEY_FOLDED = "HierarchyRenamer_IsFolded";
        private const float EXPANDED_HEIGHT = 50f;
        private const float FOLDED_HEIGHT = 18f;

        private string _inputText = "";
        private string _replaceText = "";
        private int _numberPadding = 2;
        private bool _isFolded;
        private VisualElement _parentContainer;

        public void Initialize(HierarchyWindowAccessor accessor)
        {
            // EditorPrefs에서 이전 접기 상태 복원
            _isFolded = EditorPrefs.GetBool(PREF_KEY_FOLDED, false);
        }

        public void InitUI(VisualElement container)
        {
            // 중복 주입 방지
            if (container.Q<VisualElement>("HierarchyRenamerContainer") != null) return;

            _parentContainer = new VisualElement
            {
                name = "HierarchyRenamerContainer",
                style =
                {
                    position = Position.Absolute,
                    bottom = 5f,
                    right = 5f,
                    height = _isFolded ? FOLDED_HEIGHT : EXPANDED_HEIGHT,
                    flexDirection = FlexDirection.Column
                }
            };

            if (_isFolded)
            {
                // 접힌 상태: 버튼 너비만큼만 표시, 배경 없음
                _parentContainer.style.width = 26f;
            }
            else
            {
                // 펼친 상태: 전체 너비 + 배경
                _parentContainer.style.left = 33f;
                _parentContainer.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
                _parentContainer.style.borderTopWidth = 1;
                _parentContainer.style.borderTopColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            }

            var imguiContainer = new IMGUIContainer(OnInjectedGUI);
            imguiContainer.style.flexGrow = 1;

            _parentContainer.Add(imguiContainer);
            container.Add(_parentContainer);
        }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect) { }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }
        public void OnHierarchyChanged() { }
        public void Dispose() { }

        private void OnInjectedGUI()
        {
            if (_isFolded)
            {
                // 접힌 상태: 펼치기 화살표 버튼만 표시 (배경 없음)
                if (GUILayout.Button("▲", GUILayout.Width(22), GUILayout.Height(16)))
                    SetFolded(false);
                return;
            }

            // 펼쳐진 상태: 기존 UI + 접기 버튼
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            _inputText = EditorGUILayout.TextField(_inputText, GUILayout.ExpandWidth(true));
            _replaceText = EditorGUILayout.TextField(_replaceText, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Arr", GUILayout.Height(20))) { RenameObjects(RenameAction.Sort); }
            if (GUILayout.Button("Rn", GUILayout.Height(20))) { RenameObjects(RenameAction.Rename); }
            if (GUILayout.Button("Rp", GUILayout.Height(20))) { RenameObjects(RenameAction.Replace); }
            if (GUILayout.Button("T_", GUILayout.Height(20))) { RenameObjects(RenameAction.Prefix); }
            if (GUILayout.Button("_T", GUILayout.Height(20))) { RenameObjects(RenameAction.Suffix); }
            if (GUILayout.Button("Num", GUILayout.Height(20))) { RenameObjects(RenameAction.Number); }
            _numberPadding = EditorGUILayout.IntField(_numberPadding, GUILayout.Height(20), GUILayout.Width(30));
            if (GUILayout.Button("▼", GUILayout.Width(22), GUILayout.Height(20)))
            {
                SetFolded(true);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void SetFolded(bool folded)
        {
            _isFolded = folded;
            EditorPrefs.SetBool(PREF_KEY_FOLDED, _isFolded);

            if (_parentContainer == null) return;

            if (_isFolded)
            {
                // 접힌 상태: 버튼 크기만큼만, 배경 제거
                _parentContainer.style.height = FOLDED_HEIGHT;
                _parentContainer.style.width = 26f;
                _parentContainer.style.left = StyleKeyword.Auto;
                _parentContainer.style.backgroundColor = new StyleColor(Color.clear);
                _parentContainer.style.borderTopWidth = 0;
            }
            else
            {
                // 펼친 상태: 전체 너비 + 배경 복원
                _parentContainer.style.height = EXPANDED_HEIGHT;
                _parentContainer.style.width = StyleKeyword.Auto;
                _parentContainer.style.left = 33f;
                _parentContainer.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
                _parentContainer.style.borderTopWidth = 1;
            }
        }

        private enum RenameAction { Sort, Rename, Replace, Prefix, Suffix, Number }

        private void RenameObjects(RenameAction action)
        {
            GUI.FocusControl(null);
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("[Renamer] 변경할 오브젝트가 선택되지 않았습니다.");
                return;
            }

            if (action != RenameAction.Number && action != RenameAction.Sort && string.IsNullOrEmpty(_inputText))
            {
                Debug.LogWarning("[Renamer] 입력 필드가 비어있습니다.");
                return;
            }

            Undo.RecordObjects(selectedObjects, "Rename Object(s)");

            int counter = 0;
            foreach (var obj in selectedObjects)
            {
                switch (action)
                {
                    case RenameAction.Sort:
                        // 정렬은 별도로 처리하므로 여기서는 아무것도 하지 않음
                        break;
                    case RenameAction.Rename:
                        obj.name = _inputText;
                        break;
                    case RenameAction.Replace:
                        obj.name = obj.name.Replace(_inputText, _replaceText);
                        break;
                    case RenameAction.Prefix:
                        obj.name = _inputText + obj.name;
                        break;
                    case RenameAction.Suffix:
                        obj.name = obj.name + _inputText;
                        break;
                    case RenameAction.Number:
                        if (_numberPadding == 0)
                        {
                            obj.name = counter.ToString("D1");
                        }
                        else
                        {
                            string numberStr = counter.ToString("D" + _numberPadding);
                            string baseName = string.IsNullOrEmpty(_inputText) ? obj.name : _inputText;
                            obj.name = $"{baseName}_{numberStr}";
                        }
                        break;
                }
                counter++;
            }

            // 정렬 액션의 경우 별도로 처리
            if (action == RenameAction.Sort)
            {
                SortSelectedObjects(selectedObjects);
            }
        }

        // 선택된 오브젝트들을 "_" 기준으로 분리하여 다단계 정렬.
        // 부모가 선택된 경우 해당 부모의 직계 자식들을 정렬.
        private void SortSelectedObjects(GameObject[] objects)
        {
            var objectsToSort = new List<GameObject>();

            foreach (var obj in objects)
            {
                // 선택된 오브젝트가 부모인지 확인 (자식이 있는지 체크)
                if (obj.transform.childCount > 0)
                {
                    // 부모의 직계 자식들을 추가
                    for (int i = 0; i < obj.transform.childCount; i++)
                    {
                        objectsToSort.Add(obj.transform.GetChild(i).gameObject);
                    }
                }
                else
                {
                    // 자식이 없는 경우 해당 오브젝트 자체를 정렬 대상에 추가
                    objectsToSort.Add(obj);
                }
            }

            if (objectsToSort.Count == 0)
            {
                Debug.LogWarning("[Renamer] 정렬할 오브젝트가 없습니다.");
                return;
            }

            // 부모별로 그룹화하여 정렬
            var parentGroups = objectsToSort.GroupBy(obj => obj.transform.parent).ToArray();

            foreach (var parentGroup in parentGroups)
            {
                var children = parentGroup.ToArray();

                // 같은 부모를 가진 오브젝트들만 정렬
                var sortedChildren = children.OrderBy(obj => obj.name, new HierarchicalNameComparer()).ToArray();

                // 정렬된 순서대로 하이어라키에서의 위치를 변경
                // 역순으로 설정하여 인덱스 충돌을 방지
                for (int i = sortedChildren.Length - 1; i >= 0; i--)
                {
                    sortedChildren[i].transform.SetSiblingIndex(i);
                }
            }

            Debug.Log($"[Renamer] {objectsToSort.Count}개의 오브젝트를 정렬했습니다.");
        }

        // "_" 기준으로 분리하여 다단계 정렬을 위한 비교자
        private class HierarchicalNameComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                var partsX = x.Split('_');
                var partsY = y.Split('_');

                int maxLength = Math.Max(partsX.Length, partsY.Length);

                for (int i = 0; i < maxLength; i++)
                {
                    string partX = i < partsX.Length ? partsX[i] : "";
                    string partY = i < partsY.Length ? partsY[i] : "";

                    // 숫자와 문자열을 구분하여 비교
                    int comparison = CompareParts(partX, partY);
                    if (comparison != 0)
                        return comparison;
                }

                return 0;
            }

            private int CompareParts(string partX, string partY)
            {
                // 둘 다 숫자인지 확인
                if (int.TryParse(partX, out int numX) && int.TryParse(partY, out int numY))
                {
                    return numX.CompareTo(numY);
                }

                // 둘 다 숫자가 아니면 문자열로 비교
                return string.Compare(partX, partY, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
