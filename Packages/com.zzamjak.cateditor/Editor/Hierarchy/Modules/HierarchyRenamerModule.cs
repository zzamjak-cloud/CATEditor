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
        private const float FOLDED_HEIGHT = 18f;
        private const float ACTION_HEIGHT = 20f;
        private const float BUTTON_GAP = 1f;
        private const float FIELD_GAP = 2f;
        private const float DIGIT_WIDTH = 16f;
        private const float FOLD_WIDTH = 20f;
        private const int DIGIT_MIN = 0;
        private const int DIGIT_MAX = 9;
        private const int BUTTON_COUNT = 6;
        private const int ROW_BUTTON_COUNT = 3;

        private static readonly string[][] ButtonLabels =
        {
            new[] { "sort", "srt", "so" },
            new[] { "rename", "ren", "rn" },
            new[] { "replace", "rep", "rp" },
            new[] { "prefix", "pre", "pf" },
            new[] { "suffix", "suf", "sf" },
            new[] { "digit", "dig", "dg" }
        };

        private static GUIStyle _actionButtonStyle;
        private static readonly GUIContent _measureContent = new GUIContent();

        private string _inputText = "";
        private string _replaceText = "";
        private int _numberPadding = 2;
        private bool _isFolded;
        private bool _twoRows;
        private float _appliedHeight;
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
                    flexDirection = FlexDirection.Column,
                    borderTopColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f))
                }
            };

            if (_isFolded) ApplyFoldedLayout();
            else ApplyExpandedLayout();

            var imguiContainer = new IMGUIContainer(OnInjectedGUI);
            imguiContainer.style.flexGrow = 1;

            _parentContainer.Add(imguiContainer);
            container.Add(_parentContainer);
        }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect) { }
        public void OnUpdate() { }

        public void OnSelectionChanged()
        {
            if (_isFolded || _parentContainer == null) return;
            ApplyExpandedLayout();
            _parentContainer.MarkDirtyRepaint();
        }

        public void OnHierarchyChanged() { }
        public void Dispose() { }

        private static bool HasGameObjectSelection()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        private void ApplyFoldedLayout()
        {
            if (_parentContainer == null) return;
            _appliedHeight = FOLDED_HEIGHT;
            _parentContainer.style.height = FOLDED_HEIGHT;
            _parentContainer.style.width = 26f;
            _parentContainer.style.left = StyleKeyword.Auto;
            _parentContainer.style.backgroundColor = new StyleColor(Color.clear);
            _parentContainer.style.borderTopWidth = 0;
        }

        private void ApplyExpandedLayout()
        {
            if (_parentContainer == null) return;

            _parentContainer.style.width = StyleKeyword.Auto;
            _parentContainer.style.left = 33f;
            _parentContainer.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f));
            _parentContainer.style.borderTopWidth = 1;
            SetExpandedHeight(HasGameObjectSelection(), _twoRows);
        }

        private static float ComputeExpandedHeight(bool hasSelection, bool twoRows)
        {
            float height = 3f;
            if (hasSelection)
                height += EditorGUIUtility.singleLineHeight + 1f;
            height += ACTION_HEIGHT;
            if (twoRows)
                height += BUTTON_GAP + ACTION_HEIGHT;
            return height;
        }

        private void SetExpandedHeight(bool hasSelection, bool twoRows)
        {
            if (_parentContainer == null) return;

            float height = ComputeExpandedHeight(hasSelection, twoRows);
            if (Mathf.Approximately(_appliedHeight, height)) return;

            _appliedHeight = height;
            _parentContainer.style.height = height;
        }

        private void OnInjectedGUI()
        {
            if (_isFolded)
            {
                // 접힌 상태: 펼치기 화살표 버튼만 표시 (배경 없음)
                if (GUILayout.Button("▲", GUILayout.Width(22), GUILayout.Height(16)))
                    SetFolded(false);
                return;
            }

            bool hasSelection = HasGameObjectSelection();
            var style = ActionButtonStyle;
            float width = _parentContainer != null && _parentContainer.layout.width > 1f
                ? _parentContainer.layout.width
                : EditorGUIUtility.currentViewWidth;
            _twoRows = NeedsTwoRows(style, width);
            SetExpandedHeight(hasSelection, _twoRows);

            if (hasSelection)
            {
                EditorGUILayout.Space(1);
                DrawTextFields();
            }

            EditorGUILayout.Space(1);
            if (_twoRows) DrawTwoActionRows(style);
            else DrawSingleActionRow(style);
        }

        private static GUIStyle ActionButtonStyle
        {
            get
            {
                if (_actionButtonStyle == null)
                {
                    _actionButtonStyle = new GUIStyle(GUI.skin.button)
                    {
                        padding = new RectOffset(4, 4, 1, 1),
                        margin = new RectOffset(0, 0, 0, 0),
                        overflow = new RectOffset(0, 0, 0, 0),
                        alignment = TextAnchor.MiddleCenter,
                        clipping = TextClipping.Clip,
                        stretchWidth = true,
                        fixedHeight = ACTION_HEIGHT
                    };
                }

                return _actionButtonStyle;
            }
        }

        private void DrawTextFields()
        {
            var row = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(0f));

            float fieldWidth = Mathf.Max(0f, (row.width - FIELD_GAP) * 0.5f);
            var inputRect = new Rect(row.x, row.y, fieldWidth, row.height);
            var replaceRect = new Rect(row.x + fieldWidth + FIELD_GAP, row.y, fieldWidth, row.height);

            _inputText = DrawPlaceholderTextField(inputRect, _inputText, "입력 텍스트");
            _replaceText = DrawPlaceholderTextField(replaceRect, _replaceText, "대체 텍스트");
        }

        private void DrawSingleActionRow(GUIStyle style)
        {
            var row = EditorGUILayout.GetControlRect(
                false,
                ACTION_HEIGHT,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(0f));

            float remaining = row.width - DIGIT_WIDTH - FOLD_WIDTH - BUTTON_GAP * 7;
            float buttonWidth = Mathf.Max(0f, remaining / BUTTON_COUNT);
            int tier = SelectLabelTier(style, buttonWidth, 0, BUTTON_COUNT);

            float x = DrawButtons(row, row.x, style, 0, BUTTON_COUNT, buttonWidth, tier);
            DrawDigitAndFold(row, x);
        }

        private void DrawTwoActionRows(GUIStyle style)
        {
            var row1 = EditorGUILayout.GetControlRect(
                false,
                ACTION_HEIGHT,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(0f));
            float row1Width = Mathf.Max(0f, (row1.width - BUTTON_GAP * (ROW_BUTTON_COUNT - 1)) / ROW_BUTTON_COUNT);
            int tier1 = SelectLabelTier(style, row1Width, 0, ROW_BUTTON_COUNT);
            DrawButtons(row1, row1.x, style, 0, ROW_BUTTON_COUNT, row1Width, tier1);

            var row2 = EditorGUILayout.GetControlRect(
                false,
                ACTION_HEIGHT,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(0f));
            float remaining = row2.width - DIGIT_WIDTH - FOLD_WIDTH - BUTTON_GAP * 4;
            float row2Width = Mathf.Max(0f, remaining / ROW_BUTTON_COUNT);
            int tier2 = SelectLabelTier(style, row2Width, ROW_BUTTON_COUNT, ROW_BUTTON_COUNT);
            float x = DrawButtons(row2, row2.x, style, ROW_BUTTON_COUNT, ROW_BUTTON_COUNT, row2Width, tier2);
            DrawDigitAndFold(row2, x);
        }

        private float DrawButtons(
            Rect row,
            float x,
            GUIStyle style,
            int start,
            int count,
            float buttonWidth,
            int tier)
        {
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                var rect = new Rect(x, row.y, buttonWidth, ACTION_HEIGHT);
                if (GUI.Button(rect, ButtonLabels[index][tier], style))
                    RenameObjects((RenameAction)index);
                x += buttonWidth + BUTTON_GAP;
            }

            return x;
        }

        private void DrawDigitAndFold(Rect row, float x)
        {
            _numberPadding = DrawDigitField(new Rect(x, row.y, DIGIT_WIDTH, ACTION_HEIGHT), _numberPadding);
            x += DIGIT_WIDTH + BUTTON_GAP;
            if (GUI.Button(new Rect(x, row.y, FOLD_WIDTH, ACTION_HEIGHT), "▼", ActionButtonStyle))
                SetFolded(true);
        }

        private static bool NeedsTwoRows(GUIStyle style, float width)
        {
            float maxLabelWidth = 0f;
            for (int i = 0; i < BUTTON_COUNT; i++)
            {
                _measureContent.text = ButtonLabels[i][0];
                maxLabelWidth = Mathf.Max(maxLabelWidth, style.CalcSize(_measureContent).x);
            }

            float needed = maxLabelWidth * BUTTON_COUNT + DIGIT_WIDTH + FOLD_WIDTH + BUTTON_GAP * 7;
            return width < needed;
        }

        // 지정 구간 버튼이 잘리지 않는 가장 긴 라벨 단계를 선택.
        private static int SelectLabelTier(GUIStyle style, float buttonWidth, int start, int count)
        {
            float usable = Mathf.Max(0f, buttonWidth);
            for (int tier = 0; tier < ButtonLabels[0].Length; tier++)
            {
                bool allFit = true;
                for (int i = 0; i < count; i++)
                {
                    _measureContent.text = ButtonLabels[start + i][tier];
                    if (style.CalcSize(_measureContent).x > usable)
                    {
                        allFit = false;
                        break;
                    }
                }

                if (allFit) return tier;
            }

            return ButtonLabels[0].Length - 1;
        }

        // 비어 있을 때 비활성 스타일 플레이스홀더를 표시하는 텍스트 필드.
        private static string DrawPlaceholderTextField(Rect rect, string value, string placeholder)
        {
            string newValue = EditorGUI.TextField(rect, value ?? "");

            if (string.IsNullOrEmpty(value) && Event.current.type == EventType.Repaint)
            {
                var labelRect = new Rect(rect.x + 3f, rect.y, rect.width - 3f, rect.height);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(labelRect, placeholder);
            }

            return newValue;
        }

        // 0~9 한 자리만 허용하는 자릿수 필드.
        private static int DrawDigitField(Rect rect, int value)
        {
            string raw = EditorGUI.TextField(rect, Mathf.Clamp(value, DIGIT_MIN, DIGIT_MAX).ToString());

            if (string.IsNullOrEmpty(raw)) return DIGIT_MIN;

            for (int i = raw.Length - 1; i >= 0; i--)
            {
                char c = raw[i];
                if (c >= '0' && c <= '9') return c - '0';
            }

            return Mathf.Clamp(value, DIGIT_MIN, DIGIT_MAX);
        }

        private void SetFolded(bool folded)
        {
            _isFolded = folded;
            EditorPrefs.SetBool(PREF_KEY_FOLDED, _isFolded);

            if (_parentContainer == null) return;

            if (_isFolded) ApplyFoldedLayout();
            else ApplyExpandedLayout();
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
