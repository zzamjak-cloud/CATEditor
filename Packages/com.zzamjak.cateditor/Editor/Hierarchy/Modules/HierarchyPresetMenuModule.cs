using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace CAT.HierarchyUtility
{
    // 하이어라키 창 상단에 프리팹 생성 드롭다운 메뉴를 추가하는 모듈.
    // AdvancedDropdown을 사용하여 마우스 스크롤 및 검색 기능을 지원.
    // 대상 폴더 이름은 EditorPrefs로 저장되어 코드 수정 없이 변경 가능.
    // UIOrder = 20
    public class HierarchyPresetMenuModule : IHierarchyToolModule
    {
        private const string PrefKeyTargetFolder = "HierarchyPresetMenu_TargetFolder";
        private const string DefaultTargetFolder = "Presets";

        public string ModuleName => "HierarchyPresetMenu";
        public int UIOrder => 20;

        private string _targetFolderName;
        private GUIContent _buttonContent;

        // 폴더 이름 편집용 상태
        private bool _isEditingFolderName;
        private string _editingFolderName;

        public void Initialize(HierarchyWindowAccessor accessor)
        {
            _targetFolderName = EditorPrefs.GetString(PrefKeyTargetFolder, DefaultTargetFolder);
            UpdateButtonContent();
        }

        private void UpdateButtonContent()
        {
            _buttonContent = new GUIContent(_targetFolderName);
        }

        public void InitUI(VisualElement container) { }
        public void OnUpdate() { }
        public void OnSelectionChanged() { }
        public void OnHierarchyChanged() { }
        public void Dispose() { }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            // 프리팹 편집 모드
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                DrawButton(new Rect(0, 0, 0, 0));
            }
            else if (selectionRect.y < 20 && selectionRect.x < 50)
            {
                // 일반 씬: 첫 번째 아이템 렌더링 시에만 버튼 표시
                DrawButton(selectionRect);
            }
        }

        private void DrawButton(Rect selectionRect)
        {
            const float buttonWidth = 60f;
            float buttonX = EditorGUIUtility.currentViewWidth - buttonWidth - 4f;
            Rect buttonRect = new Rect(buttonX, 0, buttonWidth, 20f);

            // 우클릭: 폴더 이름 변경 컨텍스트 메뉴
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && buttonRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                ShowContextMenu(buttonRect);
                return;
            }

            if (EditorGUI.DropdownButton(buttonRect, _buttonContent, FocusType.Passive))
            {
                var dropdown = new PrefabDropdown(new AdvancedDropdownState(), _targetFolderName, OnPrefabSelected);
                dropdown.Show(buttonRect);
            }
        }

        // 우클릭 컨텍스트 메뉴: 폴더 이름 변경
        private void ShowContextMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("폴더 이름 변경..."), false, () =>
            {
                var inputWindow = FolderNameInputWindow.Show(_targetFolderName, newName =>
                {
                    if (!string.IsNullOrEmpty(newName) && newName != _targetFolderName)
                    {
                        _targetFolderName = newName;
                        EditorPrefs.SetString(PrefKeyTargetFolder, _targetFolderName);
                        UpdateButtonContent();
                        Debug.Log($"대상 폴더 이름이 '{_targetFolderName}'으로 변경되었습니다.");
                    }
                });
            });
            menu.AddItem(new GUIContent($"초기화 ({DefaultTargetFolder})"), false, () =>
            {
                _targetFolderName = DefaultTargetFolder;
                EditorPrefs.SetString(PrefKeyTargetFolder, _targetFolderName);
                UpdateButtonContent();
                Debug.Log($"대상 폴더 이름이 기본값 '{DefaultTargetFolder}'으로 초기화되었습니다.");
            });
            menu.ShowAsContext();
        }

        // 찾을 폴더 이름을 설정. 프로젝트 전체에서 해당 이름의 모든 폴더를 찾아 프리팹을 수집.
        public void SetTargetFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                Debug.LogWarning($"폴더 이름은 비어있을 수 없습니다. 기본값 '{DefaultTargetFolder}'을 사용합니다.");
                return;
            }

            _targetFolderName = folderName;
            EditorPrefs.SetString(PrefKeyTargetFolder, _targetFolderName);
            UpdateButtonContent();
            Debug.Log($"대상 폴더 이름이 '{folderName}'으로 설정되었습니다.");
        }

        // 현재 설정된 대상 폴더 이름 반환
        public string GetTargetFolderName() => _targetFolderName;

        // 메뉴에서 프리팹이 선택되었을 때 호출되는 콜백
        private static void OnPrefabSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"프리팹을 로드할 수 없습니다: {path}");
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = prefab.name;

            // 선택된 오브젝트를 우선적으로 부모로 설정
            GameObject parentObject = Selection.activeGameObject;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                // 프리팹 편집 모드에서는 선택된 오브젝트가 있으면 그것을 부모로, 없으면 프리팹 루트를 부모로 설정
                if (parentObject != null && parentObject.scene == prefabStage.scene)
                {
                    instance.transform.SetParent(parentObject.transform, false);
                }
                else
                {
                    instance.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
                }
            }
            else
            {
                // 일반 씬에서는 선택된 오브젝트를 부모로 설정
                if (parentObject != null)
                {
                    instance.transform.SetParent(parentObject.transform, false);
                }
                // 선택된 오브젝트가 없으면 씬 루트에 생성 (부모 설정 안 함)
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
            Selection.activeObject = instance;
        }

        // 폴더 이름 입력 팝업 윈도우
        private class FolderNameInputWindow : EditorWindow
        {
            private string _folderName;
            private Action<string> _onConfirm;
            private bool _focused;

            public static FolderNameInputWindow Show(string currentName, Action<string> onConfirm)
            {
                var window = CreateInstance<FolderNameInputWindow>();
                window.titleContent = new GUIContent("폴더 이름 변경");
                window._folderName = currentName;
                window._onConfirm = onConfirm;
                window._focused = false;

                // 화면 중앙에 작은 팝업으로 표시
                Vector2 size = new Vector2(300, 60);
                window.minSize = size;
                window.maxSize = size;
                window.ShowUtility();
                return window;
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("대상 폴더 이름:");

                GUI.SetNextControlName("FolderNameField");
                _folderName = EditorGUILayout.TextField(_folderName);

                if (!_focused)
                {
                    EditorGUI.FocusTextInControl("FolderNameField");
                    _focused = true;
                }

                // Enter 키로 확인
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                {
                    Confirm();
                    return;
                }

                // Escape 키로 취소
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    Close();
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("확인"))
                {
                    Confirm();
                }
                if (GUILayout.Button("취소"))
                {
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }

            private void Confirm()
            {
                _onConfirm?.Invoke(_folderName);
                Close();
            }
        }

        // 프리팹 폴더 구조를 기반으로 계층적인 드롭다운 메뉴를 생성하는 클래스
        private class PrefabDropdown : AdvancedDropdown
        {
            private readonly Action<string> _onItemSelected;
            private readonly string _targetFolderName;
            private readonly Dictionary<int, string> _itemPaths = new Dictionary<int, string>();

            public PrefabDropdown(AdvancedDropdownState state, string targetFolderName, Action<string> onItemSelected) : base(state)
            {
                _onItemSelected = onItemSelected;
                _targetFolderName = targetFolderName;
                // 드롭다운 최소 크기 설정
                minimumSize = new Vector2(210, 1000);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Prefabs");
                _itemPaths.Clear();

                // 프로젝트 전체에서 대상 이름의 폴더 탐색
                string[] allFolders = AssetDatabase.GetAllAssetPaths()
                    .Where(path => path.StartsWith("Assets/") && Directory.Exists(path))
                    .ToArray();

                var targetFolders = allFolders
                    .Where(folderPath => Path.GetFileName(folderPath) == _targetFolderName)
                    .ToArray();

                if (targetFolders.Length == 0)
                {
                    var noFolderItem = new AdvancedDropdownItem($"폴더 없음: {_targetFolderName}");
                    noFolderItem.AddChild(new AdvancedDropdownItem($"프로젝트에서 '{_targetFolderName}' 이름의 폴더를 찾을 수 없습니다") { enabled = false });
                    noFolderItem.AddChild(new AdvancedDropdownItem($"폴더를 생성하거나 이름을 변경하세요") { enabled = false });
                    root.AddChild(noFolderItem);
                    return root;
                }

                // 모든 대상 폴더에서 프리팹 수집
                var allPrefabPaths = new List<string>();
                foreach (string targetFolder in targetFolders)
                {
                    string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder });
                    foreach (string guid in prefabGuids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        allPrefabPaths.Add(path);
                    }
                }

                if (allPrefabPaths.Count == 0)
                {
                    var noPrefabItem = new AdvancedDropdownItem($"프리팹 없음: {_targetFolderName}");
                    noPrefabItem.AddChild(new AdvancedDropdownItem($"'{_targetFolderName}' 폴더들에서 프리팹을 찾을 수 없습니다") { enabled = false });
                    noPrefabItem.AddChild(new AdvancedDropdownItem($"프리팹을 추가하세요") { enabled = false });
                    root.AddChild(noPrefabItem);
                    return root;
                }

                // 폴더별 그룹화하여 메뉴 구성
                var folderGroups = new Dictionary<string, List<string>>();
                foreach (string prefabPath in allPrefabPaths)
                {
                    string parentFolder = targetFolders.FirstOrDefault(folder => prefabPath.StartsWith(folder));
                    if (parentFolder != null)
                    {
                        if (!folderGroups.ContainsKey(parentFolder))
                        {
                            folderGroups[parentFolder] = new List<string>();
                        }
                        folderGroups[parentFolder].Add(prefabPath);
                    }
                }

                int idCounter = 0;

                foreach (var folderGroup in folderGroups)
                {
                    string folderPath = folderGroup.Key;
                    List<string> prefabPaths = folderGroup.Value;

                    // 각 프리팹을 직접 루트에 추가 (최상위 폴더는 표시하지 않음)
                    foreach (string prefabPath in prefabPaths)
                    {
                        string relativePath = prefabPath.Substring(folderPath.Length + 1);
                        string[] pathParts = relativePath.Split('/');

                        AdvancedDropdownItem currentParent = root;

                        // 하위 폴더 구조 생성
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            var existingChild = currentParent.children?.FirstOrDefault(child => child.name == pathParts[i]);

                            if (existingChild == null)
                            {
                                var subFolderItem = new AdvancedDropdownItem(pathParts[i]);
                                currentParent.AddChild(subFolderItem);
                                currentParent = subFolderItem;
                            }
                            else
                            {
                                currentParent = existingChild;
                            }
                        }

                        // 최종 프리팹 아이템 추가
                        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
                        var prefabItem = new AdvancedDropdownItem(prefabName)
                        {
                            id = idCounter++
                        };

                        _itemPaths[prefabItem.id] = prefabPath;
                        currentParent.AddChild(prefabItem);
                    }
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                base.ItemSelected(item);
                if (_itemPaths.TryGetValue(item.id, out string path))
                {
                    _onItemSelected?.Invoke(path);
                }
            }
        }
    }
}
