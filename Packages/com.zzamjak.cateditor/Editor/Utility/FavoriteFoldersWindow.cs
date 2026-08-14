using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CAT.Utility
{
    // JSON 저장용 폴더 엔트리 (GUID + 컬러)
    [System.Serializable]
    public class FavoriteFolderEntry
    {
        public string guid;
        public float r, g, b, a;
        public bool includeChildren;

        public Color GetColor()
        {
            return new Color(r, g, b, a);
        }

        public void SetColor(Color color)
        {
            r = color.r;
            g = color.g;
            b = color.b;
            a = color.a;
        }

        public bool HasColor()
        {
            return a > 0f;
        }
    }

    // JSON 저장용 데이터 클래스
    [System.Serializable]
    public class FavoriteFoldersJsonData
    {
        // 하위 호환: 기존 folderGUIDs도 로드 가능
        public List<string> folderGUIDs;
        public List<FavoriteFolderEntry> entries = new List<FavoriteFolderEntry>();
    }

    // 즐겨찾기 폴더를 관리하는 에디터 창
    public class FavoriteFoldersWindow : EditorWindow
    {
        private const string PREFS_KEY = "CAT_FavoriteFoldersData";
        private const float HIGHLIGHT_ALPHA = 0.28f;
        private const float CHILD_HIGHLIGHT_ALPHA = 0.16f;
        private static readonly Color DEFAULT_COLOR = new Color(0.3f, 0.7f, 1f); // 파랑

        private Color _handleColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        private List<DefaultAsset> _favoriteFolders = new List<DefaultAsset>();
        private List<Color> _folderColors = new List<Color>();
        private List<bool> _includeChildren = new List<bool>();
        private Vector2 _scrollPosition;
        private bool _isDragging;
        private int _dragSourceIndex = -1;
        private Rect _dragRect;
        private bool _showUIElements;

        private GUIStyle _handleStyle;
        private GUIStyle _editModeToggleStyle;
        private bool _stylesInitialized;

        // Project View 하이라이트용 캐시
        private static Dictionary<string, Color> _guidColorCache = new Dictionary<string, Color>();
        private static bool _callbackRegistered;

        [MenuItem("CAT/Utility/Favorite")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteFoldersWindow>("Favorite");
        }

        private void OnEnable()
        {
            LoadFromEditorPrefs();
            RegisterProjectWindowCallback();
        }

        private void OnDisable()
        {
            SaveToEditorPrefs();
        }

        // Project View 콜백 등록
        private void RegisterProjectWindowCallback()
        {
            if (!_callbackRegistered)
            {
                EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
                EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
                _callbackRegistered = true;
            }
        }

        // Project View 각 아이템이 그려질 때 호출 (캐시에 알파값 포함)
        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (_guidColorCache.TryGetValue(guid, out Color color))
            {
                EditorGUI.DrawRect(selectionRect, color);
            }
        }

        // GUID→Color 캐시 재구축
        private void RebuildGuidColorCache()
        {
            _guidColorCache.Clear();
            for (int i = 0; i < _favoriteFolders.Count; i++)
            {
                if (_favoriteFolders[i] == null) continue;
                if (i >= _folderColors.Count) continue;

                Color color = _folderColors[i];
                if (color.a <= 0f) continue;

                string folderPath = AssetDatabase.GetAssetPath(_favoriteFolders[i]);
                string guid = AssetDatabase.AssetPathToGUID(folderPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    // 부모 폴더: 기본 알파
                    _guidColorCache[guid] = new Color(color.r, color.g, color.b, HIGHLIGHT_ALPHA);
                }

                // 자식 폴더 포함 옵션이 켜져 있으면 하위 폴더도 캐시에 추가 (낮은 알파)
                bool includeChildren = i < _includeChildren.Count && _includeChildren[i];
                if (includeChildren && !string.IsNullOrEmpty(folderPath))
                {
                    Color childColor = new Color(color.r, color.g, color.b, CHILD_HIGHLIGHT_ALPHA);
                    string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
                    AddChildFoldersRecursive(subFolders, childColor);
                }
            }

            EditorApplication.RepaintProjectWindow();
        }

        // 자식 폴더를 재귀적으로 캐시에 추가
        private void AddChildFoldersRecursive(string[] folderPaths, Color color)
        {
            foreach (string path in folderPaths)
            {
                string childGuid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(childGuid) && !_guidColorCache.ContainsKey(childGuid))
                {
                    _guidColorCache[childGuid] = color;
                }

                string[] subFolders = AssetDatabase.GetSubFolders(path);
                if (subFolders.Length > 0)
                {
                    AddChildFoldersRecursive(subFolders, color);
                }
            }
        }

        private void LoadFromEditorPrefs()
        {
            _favoriteFolders.Clear();
            _folderColors.Clear();
            _includeChildren.Clear();

            if (!EditorPrefs.HasKey(PREFS_KEY)) return;

            try
            {
                string json = EditorPrefs.GetString(PREFS_KEY);
                var jsonData = JsonUtility.FromJson<FavoriteFoldersJsonData>(json);
                if (jsonData == null) return;

                // 새 포맷 (entries) 우선
                if (jsonData.entries != null && jsonData.entries.Count > 0)
                {
                    foreach (var entry in jsonData.entries)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                        if (string.IsNullOrEmpty(path)) continue;

                        DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                        if (folder != null && AssetDatabase.IsValidFolder(path))
                        {
                            _favoriteFolders.Add(folder);
                            _folderColors.Add(entry.GetColor());
                            _includeChildren.Add(entry.includeChildren);
                        }
                    }
                }
                // 하위 호환: 기존 folderGUIDs 포맷
                else if (jsonData.folderGUIDs != null)
                {
                    foreach (string guid in jsonData.folderGUIDs)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;

                        DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                        if (folder != null && AssetDatabase.IsValidFolder(path))
                        {
                            _favoriteFolders.Add(folder);
                            _folderColors.Add(DEFAULT_COLOR);
                            _includeChildren.Add(false);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Favorite 폴더 데이터 로드 실패: {e.Message}");
                _favoriteFolders.Clear();
                _folderColors.Clear();
                _includeChildren.Clear();
            }

            RebuildGuidColorCache();
        }

        private void SaveToEditorPrefs()
        {
            try
            {
                var jsonData = new FavoriteFoldersJsonData();
                jsonData.entries = new List<FavoriteFolderEntry>();

                for (int i = 0; i < _favoriteFolders.Count; i++)
                {
                    if (_favoriteFolders[i] == null) continue;

                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_favoriteFolders[i]));
                    if (string.IsNullOrEmpty(guid)) continue;

                    var entry = new FavoriteFolderEntry { guid = guid };
                    if (i < _folderColors.Count)
                        entry.SetColor(_folderColors[i]);
                    if (i < _includeChildren.Count)
                        entry.includeChildren = _includeChildren[i];

                    jsonData.entries.Add(entry);
                }

                string json = JsonUtility.ToJson(jsonData, true);
                EditorPrefs.SetString(PREFS_KEY, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Favorite 폴더 데이터 저장 실패 (EditorPrefs): {e.Message}");
            }
        }

        // 외부에서 컬러 변경 시 호출 (팝업 → 메인 윈도우)
        public void SetFolderColor(int index, Color color)
        {
            if (index < 0 || index >= _folderColors.Count) return;
            _folderColors[index] = color;
            RebuildGuidColorCache();
            SaveToEditorPrefs();
            Repaint();
        }

        public Color GetFolderColor(int index)
        {
            if (index < 0 || index >= _folderColors.Count) return Color.clear;
            return _folderColors[index];
        }

        private void OnGUI()
        {
            InitializeStyles();
            EditorGUILayout.BeginVertical();

            DrawHeader();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawFolders();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            HandleDragVisuals();
            HandleWindowDragDrop();
        }

        private void HandleDragVisuals()
        {
            if (_isDragging)
            {
                Event current = Event.current;
                Vector2 mousePos = current.mousePosition;

                float deltaY = mousePos.y - _dragRect.y;
                Color indicatorColor = deltaY > 0 ? Color.green : Color.red;

                Rect indicator = new Rect(mousePos.x - 5, mousePos.y - 1, 10, 2);
                EditorGUI.DrawRect(indicator, indicatorColor);

                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _handleStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = _handleColor },
                alignment = TextAnchor.MiddleCenter
            };

            _editModeToggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 10 };

            _stylesInitialized = true;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            float minEditWidth = 40f;
            string toggleLabel = position.width - 40f >= minEditWidth ? "Edit" : "E";
            float toggleWidth = position.width - 40f >= minEditWidth ? minEditWidth : 20f;

            bool newShowUIElements = GUILayout.Toggle(_showUIElements, toggleLabel, _editModeToggleStyle, GUILayout.Width(toggleWidth));
            if (newShowUIElements != _showUIElements)
            {
                _showUIElements = newShowUIElements;
                if (!_showUIElements)
                {
                    SaveToEditorPrefs();
                }
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        private void DrawFolders()
        {
            if (_favoriteFolders == null) return;
            int folderToDelete = -1;

            for (int i = 0; i < _favoriteFolders.Count; i++)
            {
                // 리스트 동기화
                while (_folderColors.Count <= i)
                    _folderColors.Add(DEFAULT_COLOR);
                while (_includeChildren.Count <= i)
                    _includeChildren.Add(false);

                EditorGUILayout.BeginHorizontal();

                if (_showUIElements)
                {
                    GUILayout.Label("☰", _handleStyle, GUILayout.Width(20));
                    Rect folderDragHandleRect = GUILayoutUtility.GetLastRect();
                    HandleReordering(folderDragHandleRect, i);
                }

                // 컬러가 지정된 폴더에 컬러 인디케이터 표시
                Color folderColor = _folderColors[i];
                if (folderColor.a > 0f)
                {
                    Rect colorIndicator = GUILayoutUtility.GetRect(4, 16, GUILayout.Width(4));
                    EditorGUI.DrawRect(colorIndicator, folderColor);
                    GUILayout.Space(2);
                }

                GUIContent folderIcon = EditorGUIUtility.IconContent("Folder Icon");
                GUILayout.Label(folderIcon, GUILayout.Width(16), GUILayout.Height(16));

                DefaultAsset folder = _favoriteFolders[i];
                if (folder != null)
                {
                    if (GUILayout.Button(folder.name, EditorStyles.label))
                    {
                        AssetDatabase.OpenAsset(folder);
                    }
                }
                else
                {
                    GUI.color = Color.gray;
                    GUILayout.Label("[Missing Folder]", EditorStyles.label);
                    GUI.color = Color.white;
                }

                GUILayout.FlexibleSpace();

                if (_showUIElements)
                {
                    // 자식 폴더 포함 체크박스
                    bool prevInclude = _includeChildren[i];
                    bool newInclude = GUILayout.Toggle(prevInclude, new GUIContent("▼", "자식 폴더에도 컬러 적용"), GUILayout.Width(16));
                    if (newInclude != prevInclude)
                    {
                        _includeChildren[i] = newInclude;
                        RebuildGuidColorCache();
                        SaveToEditorPrefs();
                    }
                    GUILayout.Space(2);

                    DrawColorButton(i);
                    GUILayout.Space(2);

                    GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                    if (GUILayout.Button("×", GUILayout.Width(18), GUILayout.Height(18)))
                    {
                        folderToDelete = i;
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (folderToDelete != -1)
            {
                _favoriteFolders.RemoveAt(folderToDelete);
                if (folderToDelete < _folderColors.Count)
                    _folderColors.RemoveAt(folderToDelete);
                if (folderToDelete < _includeChildren.Count)
                    _includeChildren.RemoveAt(folderToDelete);
                RebuildGuidColorCache();
            }
        }

        // 컬러 선택 버튼 (Edit 모드에서 표시)
        private void DrawColorButton(int index)
        {
            Color currentColor = _folderColors[index];
            bool hasColor = currentColor.a > 0f;

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = hasColor ? currentColor : new Color(0.3f, 0.3f, 0.3f);

            string buttonLabel = hasColor ? " " : "—";
            if (GUILayout.Button(buttonLabel, GUILayout.Width(20), GUILayout.Height(18)))
            {
                Rect buttonRect = GUILayoutUtility.GetLastRect();
                // 스크린 좌표로 변환
                buttonRect.position = GUIUtility.GUIToScreenPoint(buttonRect.position);
                FavoriteColorPickerPopup.Show(buttonRect, currentColor, this, index);
            }
            GUI.backgroundColor = prevBg;
        }

        private void HandleReordering(Rect handleRect, int folderIndex)
        {
            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (current.type)
            {
                case EventType.MouseDown when handleRect.Contains(current.mousePosition) && current.button == 0:
                    _isDragging = true;
                    _dragSourceIndex = folderIndex;
                    _dragRect = handleRect;
                    GUIUtility.hotControl = controlID;
                    current.Use();
                    break;

                case EventType.MouseDrag when _isDragging && GUIUtility.hotControl == controlID:
                    Repaint();
                    current.Use();
                    break;

                case EventType.MouseUp when _isDragging && GUIUtility.hotControl == controlID:
                    PerformSimpleReordering(current.mousePosition);
                    _isDragging = false;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    Repaint();
                    break;
            }
        }

        private void PerformSimpleReordering(Vector2 mousePos)
        {
            float deltaY = mousePos.y - _dragRect.y;

            if (Mathf.Abs(deltaY) > 20)
            {
                int direction = deltaY > 0 ? 1 : -1;
                int targetIndex = _dragSourceIndex + direction;
                if (targetIndex >= 0 && targetIndex < _favoriteFolders.Count)
                {
                    var tempFolder = _favoriteFolders[_dragSourceIndex];
                    _favoriteFolders[_dragSourceIndex] = _favoriteFolders[targetIndex];
                    _favoriteFolders[targetIndex] = tempFolder;

                    if (_dragSourceIndex < _folderColors.Count && targetIndex < _folderColors.Count)
                    {
                        var tempColor = _folderColors[_dragSourceIndex];
                        _folderColors[_dragSourceIndex] = _folderColors[targetIndex];
                        _folderColors[targetIndex] = tempColor;
                    }

                    if (_dragSourceIndex < _includeChildren.Count && targetIndex < _includeChildren.Count)
                    {
                        var tempInclude = _includeChildren[_dragSourceIndex];
                        _includeChildren[_dragSourceIndex] = _includeChildren[targetIndex];
                        _includeChildren[targetIndex] = tempInclude;
                    }
                }
            }
        }

        private void HandleWindowDragDrop()
        {
            Event current = Event.current;

            if (!_showUIElements) return;

            switch (current.type)
            {
                case EventType.DragUpdated:
                    bool isFolder = DragAndDrop.objectReferences.Any(obj => obj is DefaultAsset && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)));
                    DragAndDrop.visualMode = isFolder ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    current.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    bool addedAny = false;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is DefaultAsset folder && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)))
                        {
                            if (!_favoriteFolders.Contains(folder))
                            {
                                _favoriteFolders.Add(folder);
                                _folderColors.Add(DEFAULT_COLOR);
                                _includeChildren.Add(false);
                                addedAny = true;
                            }
                        }
                    }
                    if (addedAny)
                    {
                        Repaint();
                    }
                    current.Use();
                    break;
            }
        }
    }

    // 컬러 선택 드롭다운 팝업
    public class FavoriteColorPickerPopup : EditorWindow
    {
        private static readonly Color[] PresetColors = new Color[]
        {
            new Color(1f, 0.4f, 0.4f),       // 빨강
            new Color(1f, 0.7f, 0.3f),        // 주황
            new Color(1f, 0.9f, 0.3f),        // 노랑
            new Color(0.4f, 0.86f, 0.4f),     // 초록
            new Color(0.3f, 0.7f, 1f),        // 파랑
            new Color(0.63f, 0.47f, 1f),      // 보라
            new Color(1f, 0.47f, 0.78f),      // 분홍
            new Color(0.63f, 0.63f, 0.63f),   // 회색
        };

        private FavoriteFoldersWindow _parentWindow;
        private int _folderIndex;
        private Color _currentColor;
        private Color _customColor = Color.white;
        private bool _showCustomPicker;

        public static void Show(Rect buttonRect, Color currentColor, FavoriteFoldersWindow parent, int folderIndex)
        {
            var popup = CreateInstance<FavoriteColorPickerPopup>();
            popup._currentColor = currentColor;
            popup._customColor = currentColor.a > 0f ? currentColor : Color.white;
            popup._parentWindow = parent;
            popup._folderIndex = folderIndex;
            popup.ShowAsDropDown(buttonRect, new Vector2(206, 64));
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            // 프리셋 컬러 그리드
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);

            for (int i = 0; i < PresetColors.Length; i++)
            {
                Color preset = PresetColors[i];
                bool isSelected = _currentColor.a > 0f &&
                    Mathf.Abs(_currentColor.r - preset.r) < 0.01f &&
                    Mathf.Abs(_currentColor.g - preset.g) < 0.01f &&
                    Mathf.Abs(_currentColor.b - preset.b) < 0.01f;

                Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
                EditorGUI.DrawRect(colorRect, preset);

                // 선택된 컬러에 흰색 테두리
                if (isSelected)
                {
                    DrawBorder(colorRect, Color.white, 2);
                }

                if (Event.current.type == EventType.MouseDown && colorRect.Contains(Event.current.mousePosition))
                {
                    ApplyColor(preset);
                    Close();
                    Event.current.Use();
                    return;
                }

                GUILayout.Space(2);
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // 하단: 커스텀 + 제거
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);

            if (GUILayout.Button("+ 커스텀", EditorStyles.miniButton, GUILayout.Height(18)))
            {
                _showCustomPicker = true;
                // 팝업 크기 확장
                minSize = new Vector2(206, 114);
                maxSize = new Vector2(206, 114);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("∅ 제거", EditorStyles.miniButton, GUILayout.Height(18)))
            {
                ApplyColor(Color.clear);
                Close();
                return;
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();

            // 커스텀 컬러 피커
            if (_showCustomPicker)
            {
                GUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                _customColor = EditorGUILayout.ColorField(GUIContent.none, _customColor, true, false, false, GUILayout.Height(20));
                if (EditorGUI.EndChangeCheck())
                {
                    // 실시간 미리보기
                    ApplyColor(_customColor);
                }

                GUILayout.Space(4);

                if (GUILayout.Button("확인", EditorStyles.miniButton, GUILayout.Width(40), GUILayout.Height(20)))
                {
                    ApplyColor(_customColor);
                    Close();
                    return;
                }

                GUILayout.Space(4);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ApplyColor(Color color)
        {
            _currentColor = color;
            if (_parentWindow != null)
            {
                _parentWindow.SetFolderColor(_folderIndex, color);
            }
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
