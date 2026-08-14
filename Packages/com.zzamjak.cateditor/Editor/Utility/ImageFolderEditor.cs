using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace CAT.Utility
{
    // ===========================================
    // Component Editors
    // ===========================================

    // Image용 커스텀 에디터
    [CustomEditor(typeof(Image), true)]
    [CanEditMultipleObjects]
    public class FilteredImageEditor : UnityEditor.UI.ImageEditor
    {
        private FilteredSpriteFinderDrawer drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    // RawImage용 커스텀 에디터
    [CustomEditor(typeof(RawImage), true)]
    [CanEditMultipleObjects]
    public class FilteredRawImageEditor : UnityEditor.UI.RawImageEditor
    {
        private FilteredSpriteFinderDrawer drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Texture").objectReferenceValue = sprite != null ? sprite.texture : null;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    // SpriteRenderer용 커스텀 에디터
    [CustomEditor(typeof(SpriteRenderer), true)]
    [CanEditMultipleObjects]
    public class FilteredSpriteRendererEditor : Editor
    {
        private FilteredSpriteFinderDrawer drawer;
        private Editor defaultEditor;

        private void OnEnable()
        {
            drawer = new FilteredSpriteFinderDrawer();
            drawer.Initialize();

            var targets = serializedObject.targetObjects;
            var editorType = Type.GetType("UnityEditor.SpriteRendererEditor, UnityEditor");
            defaultEditor = CreateEditor(targets, editorType);
        }

        private void OnDisable()
        {
            if (defaultEditor != null)
            {
                DestroyImmediate(defaultEditor);
            }
        }

        public override void OnInspectorGUI()
        {
            defaultEditor.OnInspectorGUI();

            Action<Sprite> onSpriteSelected = (sprite) =>
            {
                serializedObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                serializedObject.ApplyModifiedProperties();
            };
            drawer.DrawInspectorGUI(onSpriteSelected);
        }
    }

    // ===========================================
    // Filtered Sprite Finder Drawer
    // ===========================================

    public class FilteredSpriteFinderDrawer
    {
        private const string FOLDERS_PREFS_KEY = "FilteredSpriteFinder_Folders";
        private const string FOLDOUT_STATE_KEY = "FilteredSpriteFinder_Foldout";

        private static List<DefaultAsset> searchFolders = new List<DefaultAsset>();
        private static bool isInitialized = false;
        private static bool isFoldedOut = true;

        // 초기화 메서드
        public void Initialize()
        {
            if (!isInitialized)
            {
                LoadFoldersFromPrefs();
                isFoldedOut = EditorPrefs.GetBool(FOLDOUT_STATE_KEY, true);
                isInitialized = true;
            }
        }

        // 인스펙터 GUI 그리기
        public void DrawInspectorGUI(Action<Sprite> onSpriteSelectedAction)
        {
            EditorGUILayout.Space();

            // 헤더 영역을 더 넓게 설정 (드래그 영역 확보)
            Rect headerRect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            
            // 헤더 배경 그리기 (드래그 영역 시각화)
            EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            
            // 폴드아웃 버튼 영역
            Rect foldoutRect = new Rect(headerRect.x + 5, headerRect.y + 5, headerRect.width - 10, 20);
            bool newFoldoutState = EditorGUI.Foldout(foldoutRect, isFoldedOut, "Sprite Folder Filter", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != isFoldedOut)
            {
                isFoldedOut = newFoldoutState;
                EditorPrefs.SetBool(FOLDOUT_STATE_KEY, isFoldedOut);
            }

            // 전체 헤더 영역에서 드래그 앤 드롭 처리
            HandleDragAndDrop(headerRect);

            if (isFoldedOut)
            {
                EditorGUI.indentLevel++;

                // 등록된 폴더 목록 정리
                if (searchFolders.RemoveAll(f => f == null) > 0)
                {
                    SaveFoldersToPrefs();
                }

                // 등록된 폴더들 표시
                for (int i = 0; i < searchFolders.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(searchFolders[i], typeof(DefaultAsset), false);

                    if (GUILayout.Button("Find", GUILayout.Width(50)))
                    {
                        string path = AssetDatabase.GetAssetPath(searchFolders[i]);
                        FilteredSpriteSelector.ShowWindow(path, onSpriteSelectedAction);
                    }

                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        searchFolders.RemoveAt(i);
                        SaveFoldersToPrefs();
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        // 드래그 앤 드롭 처리 메서드
        private void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;

            // 드래그 앤 드롭이 활성화되어 있는지 확인
            if (DragAndDrop.objectReferences == null || DragAndDrop.objectReferences.Length == 0)
                return;

            switch (evt.type)
            {
                case EventType.DragUpdated:
                    if (dropRect.Contains(evt.mousePosition))
                    {
                        bool canAcceptDrag = false;
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is DefaultAsset)
                            {
                                string path = AssetDatabase.GetAssetPath(draggedObject);
                                if (AssetDatabase.IsValidFolder(path))
                                {
                                    canAcceptDrag = true;
                                    break;
                                }
                            }
                        }

                        if (canAcceptDrag)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            // 드래그 중임을 시각적으로 표시
                            EditorGUI.DrawRect(dropRect, new Color(0.0f, 0.5f, 1.0f, 0.2f));
                        }
                        else
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                        }
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (dropRect.Contains(evt.mousePosition))
                    {
                        bool addedAny = false;
                        int addedCount = 0;
                        
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is DefaultAsset folder)
                            {
                                string path = AssetDatabase.GetAssetPath(folder);
                                if (AssetDatabase.IsValidFolder(path) && !searchFolders.Contains(folder))
                                {
                                    searchFolders.Add(folder);
                                    addedAny = true;
                                    addedCount++;
                                }
                            }
                        }

                        if (addedAny)
                        {
                            SaveFoldersToPrefs();
                            // 폴드아웃을 자동으로 열어서 추가된 폴더를 보여줌
                            if (!isFoldedOut)
                            {
                                isFoldedOut = true;
                                EditorPrefs.SetBool(FOLDOUT_STATE_KEY, isFoldedOut);
                            }
                            
                            // 성공 메시지 표시
                            Debug.Log($"폴더 {addedCount}개가 추가되었습니다.");
                        }

                        DragAndDrop.AcceptDrag();
                        evt.Use();
                    }
                    break;

                case EventType.DragExited:
                    // 드래그가 끝났을 때 시각적 피드백 제거
                    evt.Use();
                    break;
            }
        }

        // 폴더 목록을 PlayerPrefs로 저장
        private void SaveFoldersToPrefs()
        {
            var folderGUIDs = searchFolders
                .Where(f => f != null)
                .Select(f => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(f)))
                .ToList();

            // 폴더 개수 저장
            EditorPrefs.SetInt(FOLDERS_PREFS_KEY + "_Count", folderGUIDs.Count);
            
            // 각 폴더 GUID 저장
            for (int i = 0; i < folderGUIDs.Count; i++)
            {
                EditorPrefs.SetString(FOLDERS_PREFS_KEY + "_" + i, folderGUIDs[i]);
            }
        }

        // PlayerPrefs에서 폴더 목록 로드
        private void LoadFoldersFromPrefs()
        {
            searchFolders.Clear();
            
            int folderCount = EditorPrefs.GetInt(FOLDERS_PREFS_KEY + "_Count", 0);
            
            for (int i = 0; i < folderCount; i++)
            {
                string guid = EditorPrefs.GetString(FOLDERS_PREFS_KEY + "_" + i, "");
                if (!string.IsNullOrEmpty(guid))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                        if (folder != null)
                        {
                            searchFolders.Add(folder);
                        }
                    }
                }
            }
        }
    }

    // ===========================================
    // Filtered Sprite Selector Window
    // ===========================================

    public class FilteredSpriteSelector : EditorWindow
    {
        private class FolderNode
        {
            public string name;
            public string path;
            public List<FolderNode> children = new List<FolderNode>();
            public bool isFoldedOut = true;
        }

        // 설정 저장을 위한 키들
        private const string SEARCH_STRING_KEY = "FilteredSpriteSelector_SearchString";
        private const string GRID_SIZE_KEY = "FilteredSpriteSelector_GridSize";
        private const string SHOW_AS_LIST_KEY = "FilteredSpriteSelector_ShowAsList";
        private const string SELECTED_FOLDER_KEY = "FilteredSpriteSelector_SelectedFolder";

        private static Action<Sprite> onSpriteSelectedCallback;
        private Vector2 leftPaneScroll;
        private Vector2 rightPaneScroll;
        private string selectedFolderPath;
        private string rootFolderPath; // 루트 폴더 경로 저장
        private string searchString = "";
        private List<Sprite> spritesInSelectedFolder = new List<Sprite>();
        private List<FolderNode> folderNodes = new List<FolderNode>();
        private float gridSize = 1.0f; // 그리드 크기 조절 (0.0 ~ 1.0)
        private bool showAsList = false; // 리스트 뷰 여부

        // 윈도우 표시 메서드
        public static void ShowWindow(string initialPath, Action<Sprite> onSpriteSelected)
        {
            onSpriteSelectedCallback = onSpriteSelected;
            var window = GetWindow<FilteredSpriteSelector>("스프라이트 선택기");
            window.rootFolderPath = initialPath; // 루트 폴더 경로 설정
            window.LoadSettings(); // 저장된 설정 로드
            window.BuildFolderTree();
            window.LoadSpritesForFolder(window.selectedFolderPath);
            window.Show();
        }

        // 설정 저장 메서드
        private void SaveSettings()
        {
            EditorPrefs.SetString(SEARCH_STRING_KEY, searchString);
            EditorPrefs.SetFloat(GRID_SIZE_KEY, gridSize);
            EditorPrefs.SetBool(SHOW_AS_LIST_KEY, showAsList);
            EditorPrefs.SetString(SELECTED_FOLDER_KEY, selectedFolderPath);
        }

        // 설정 로드 메서드
        private void LoadSettings()
        {
            searchString = EditorPrefs.GetString(SEARCH_STRING_KEY, "");
            gridSize = EditorPrefs.GetFloat(GRID_SIZE_KEY, 1.0f);
            showAsList = EditorPrefs.GetBool(SHOW_AS_LIST_KEY, false);
            
            // 저장된 폴더 경로가 유효한지 확인
            string savedFolderPath = EditorPrefs.GetString(SELECTED_FOLDER_KEY, rootFolderPath);
            if (string.IsNullOrEmpty(savedFolderPath) || !AssetDatabase.IsValidFolder(savedFolderPath))
            {
                selectedFolderPath = rootFolderPath;
            }
            else
            {
                // 저장된 폴더가 현재 루트 폴더의 하위인지 확인
                if (savedFolderPath.StartsWith(rootFolderPath))
                {
                    selectedFolderPath = savedFolderPath;
                }
                else
                {
                    selectedFolderPath = rootFolderPath;
                }
            }
        }

        // GUI 메서드
        private void OnGUI()
        {
            DrawPanes();
        }

        // 윈도우가 닫힐 때 설정 저장
        private void OnDestroy()
        {
            SaveSettings();
        }

        private void DrawPanes()
        {
            EditorGUILayout.BeginHorizontal();

            // 왼쪽 폴더 패널
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(200), GUILayout.ExpandHeight(true));
            //EditorGUILayout.LabelField("폴더", EditorStyles.boldLabel);
            leftPaneScroll = EditorGUILayout.BeginScrollView(leftPaneScroll);
            DrawFolderTree();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // 오른쪽 스프라이트 패널
            DrawSpritePane();

            EditorGUILayout.EndHorizontal();
        }

        // 폴더 트리 빌드 메서드
        private void BuildFolderTree()
        {
            folderNodes.Clear();
            if (string.IsNullOrEmpty(rootFolderPath)) return;

            // 루트 폴더 기준으로 고정된 트리 구조 생성
            string[] folders = AssetDatabase.GetSubFolders(rootFolderPath);
            foreach (string folder in folders)
            {
                var node = new FolderNode
                {
                    name = Path.GetFileName(folder),
                    path = folder
                };
                BuildSubFolders(node);
                folderNodes.Add(node);
            }
        }

        // 재귀적으로 하위 폴더 빌드
        private void BuildSubFolders(FolderNode node)
        {
            string[] subFolders = AssetDatabase.GetSubFolders(node.path);
            foreach (string subFolder in subFolders)
            {
                var childNode = new FolderNode
                {
                    name = Path.GetFileName(subFolder),
                    path = subFolder
                };
                BuildSubFolders(childNode);
                node.children.Add(childNode);
            }
        }

        // 폴더 트리 그리기
        private void DrawFolderTree()
        {
            var rootStyle = new GUIStyle(EditorStyles.label);
            rootStyle.fontStyle = FontStyle.Bold;

            // 루트 폴더가 선택되었는지 확인
            var rootSelectedStyle = (selectedFolderPath == rootFolderPath)
                ? new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } }
                : rootStyle;

            if (GUILayout.Button(Path.GetFileName(rootFolderPath), rootSelectedStyle))
            {
                SelectFolder(rootFolderPath);
            }

            foreach (var node in folderNodes)
            {
                DrawFolderNode(node, 1);
            }
        }

        // 재귀적으로 폴더 노드 그리기
        private void DrawFolderNode(FolderNode node, int indent)
        {
            var foldoutButtonStyle = new GUIStyle(EditorStyles.miniButton);
            foldoutButtonStyle.fixedWidth = 20;
            foldoutButtonStyle.fixedHeight = 16;
            foldoutButtonStyle.fontSize = 10;

            var folderLabelStyle = new GUIStyle(EditorStyles.label);
            var selectedFolderStyle = new GUIStyle(EditorStyles.boldLabel);

            // 선택된 폴더는 색상을 다르게 표시
            if (selectedFolderPath == node.path)
            {
                selectedFolderStyle.normal.textColor = Color.cyan;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 15);

            var style = (selectedFolderPath == node.path)
                ? selectedFolderStyle : folderLabelStyle;

            if (node.children.Any())
            {
                string foldoutChar = node.isFoldedOut ? "−" : "+";
                if (GUILayout.Button(foldoutChar, foldoutButtonStyle, GUILayout.Width(20)))
                {
                    node.isFoldedOut = !node.isFoldedOut;
                }
            }
            else
            {
                GUILayout.Space(24f);
            }

            if (GUILayout.Button(node.name, style))
            {
                SelectFolder(node.path);
            }

            EditorGUILayout.EndHorizontal();

            if (node.isFoldedOut && node.children.Any())
            {
                foreach (var child in node.children)
                {
                    DrawFolderNode(child, indent + 1);
                }
            }
        }

        // 스프라이트 패널 그리기
        private void DrawSpritePane()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // 툴바 영역
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            string newSearchString = GUILayout.TextField(searchString, EditorStyles.toolbarSearchField);
            if (newSearchString != searchString)
            {
                searchString = newSearchString;
                SaveSettings(); // 검색 문자열 변경 시 저장
            }

            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(22)))
            {
                searchString = "";
                SaveSettings(); // 검색 문자열 초기화 시 저장
                GUI.FocusControl(null);
            }
            
            // 그리드 크기 조절 슬라이더
            GUILayout.Space(10);
            GUILayout.Label("크기:", EditorStyles.miniLabel, GUILayout.Width(30));
            float newGridSize = GUILayout.HorizontalSlider(gridSize, 0.0f, 1.0f, GUILayout.Width(100));
            if (newGridSize != gridSize)
            {
                gridSize = newGridSize;
                showAsList = gridSize < 0.1f; // 슬라이더가 매우 낮으면 리스트 뷰로 전환
                SaveSettings(); // 그리드 크기 변경 시 저장
            }
            GUILayout.EndHorizontal();

            rightPaneScroll = EditorGUILayout.BeginScrollView(rightPaneScroll);

            var filteredSprites = string.IsNullOrEmpty(searchString)
                ? spritesInSelectedFolder
                : spritesInSelectedFolder.Where(s => s.name.ToLower().Contains(searchString.ToLower())).ToList();

            if (filteredSprites.Count == 0)
            {
                EditorGUILayout.HelpBox("이 폴더에 스프라이트가 없거나, 검색 결과가 없습니다.", MessageType.Info);
            }
            else
            {
                if (showAsList)
                {
                    // 리스트 뷰
                    DrawListView(filteredSprites);
                }
                else
                {
                    // 그리드 뷰
                    DrawGridView(filteredSprites);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // 리스트 뷰 그리기
        private void DrawListView(List<Sprite> sprites)
        {
            // Unity의 기본 스타일을 사용하여 최적화
            var listStyle = new GUIStyle(EditorStyles.miniButton);
            listStyle.alignment = TextAnchor.MiddleLeft;
            listStyle.fixedHeight = 20;
            listStyle.imagePosition = ImagePosition.ImageLeft;
            listStyle.padding = new RectOffset(5, 5, 2, 2);
            listStyle.margin = new RectOffset(0, 0, 1, 1);
            
            // 모든 상태에서 배경을 명시적으로 설정
            listStyle.normal.background = null; // 기본 상태는 완전 투명
            listStyle.hover.background = EditorStyles.miniButton.hover.background; // 호버 시 배경
            listStyle.active.background = EditorStyles.miniButton.active.background; // 클릭 시 배경
            listStyle.focused.background = null; // 포커스 상태도 투명
            listStyle.onNormal.background = null; // onNormal 상태도 투명
            listStyle.onHover.background = EditorStyles.miniButton.hover.background; // onHover 상태
            listStyle.onActive.background = EditorStyles.miniButton.active.background; // onActive 상태
            listStyle.onFocused.background = null; // onFocused 상태도 투명

            foreach (var sprite in sprites)
            {
                var content = new GUIContent(sprite.name, AssetPreview.GetAssetPreview(sprite));
                
                if (GUILayout.Button(content, listStyle))
                {
                    onSpriteSelectedCallback?.Invoke(sprite);
                    Close();
                }
            }
        }

        // 그리드 뷰 그리기
        private void DrawGridView(List<Sprite> sprites)
        {
            // 그리드 크기에 따른 버튼 크기 계산
            float minSize = 60f;
            float maxSize = 120f;
            float buttonSize = Mathf.Lerp(minSize, maxSize, gridSize);

            // Unity의 기본 스타일을 사용하여 최적화
            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fixedWidth = buttonSize;
            buttonStyle.fixedHeight = buttonSize;
            buttonStyle.imagePosition = ImagePosition.ImageAbove;
            buttonStyle.alignment = TextAnchor.LowerCenter;
            buttonStyle.padding = new RectOffset(2, 2, 2, 2);
            buttonStyle.clipping = TextClipping.Clip;
            
            // 모든 상태에서 배경을 명시적으로 설정
            buttonStyle.normal.background = null; // 기본 상태는 완전 투명
            buttonStyle.hover.background = GUI.skin.button.hover.background; // 호버 시 배경
            buttonStyle.active.background = GUI.skin.button.active.background; // 클릭 시 배경
            buttonStyle.focused.background = null; // 포커스 상태도 투명
            buttonStyle.onNormal.background = null; // onNormal 상태도 투명
            buttonStyle.onHover.background = GUI.skin.button.hover.background; // onHover 상태
            buttonStyle.onActive.background = GUI.skin.button.active.background; // onActive 상태
            buttonStyle.onFocused.background = null; // onFocused 상태도 투명

            // 그리드 열 개수 계산
            int columns = Mathf.FloorToInt((position.width - 200) / (buttonSize + 10));
            columns = Mathf.Max(1, columns);

            for (int i = 0; i < sprites.Count; i++)
            {
                if (i % columns == 0) GUILayout.BeginHorizontal();

                var sprite = sprites[i];
                
                // 텍스트 줄임표 처리
                string displayName = sprite.name;
                if (displayName.Length > 12) // 버튼 크기에 따라 조절 가능
                {
                    displayName = displayName.Substring(0, 12) + "...";
                }
                
                var content = new GUIContent(displayName, AssetPreview.GetAssetPreview(sprite));

                if (GUILayout.Button(content, buttonStyle))
                {
                    onSpriteSelectedCallback?.Invoke(sprite);
                    Close();
                }

                if (i % columns == columns - 1 || i == sprites.Count - 1)
                {
                    GUILayout.EndHorizontal();
                }
            }
        }

        // 폴더 선택 메서드
        private void SelectFolder(string path)
        {
            if (selectedFolderPath == path) return;
            selectedFolderPath = path;
            LoadSpritesForFolder(path);
            SaveSettings(); // 폴더 선택 시 저장
            // 폴더 트리는 다시 빌드하지 않음 - 고정된 구조 유지
            Repaint();
        }

        // 선택된 폴더에서 스프라이트 로드 메서드
        private void LoadSpritesForFolder(string folderPath)
        {
            spritesInSelectedFolder.Clear();

            // 검색할 이미지 파일 확장자 목록
            var validExtensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd" };

            // System.IO를 사용해 '현재 폴더에서만' 파일 목록을 가져옵니다. (하위 폴더는 검색 안 함)
            string[] filePaths = Directory.GetFiles(folderPath);

            foreach (string filePath in filePaths)
            {
                // 가져온 파일이 이미지 확장자를 가졌는지 확인
                if (validExtensions.Contains(Path.GetExtension(filePath).ToLower()))
                {
                    // 해당 경로의 파일을 스프라이트로 로드 시도
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
                    if (sprite != null)
                    {
                        spritesInSelectedFolder.Add(sprite);
                    }
                }
            }

            // 이름순으로 정렬
            spritesInSelectedFolder = spritesInSelectedFolder.OrderBy(s => s.name).ToList();
        }
    }
}

