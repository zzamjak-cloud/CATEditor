// 필수 네임스페이스들
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace CAT.Utility
{
    /// <summary>
    /// Unity 툴바에 씬 관리 드롭다운과 버튼을 추가하는 에디터 스크립트입니다.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneToolbar
    {
        private static int selectedSceneIndex = 0;
        private static bool useSelectedSceneOnPlay = true;
        private static string[] sceneNames;
        private static string[] scenePaths;
        private const string PluginSplitLabel = "──────── Plugins ────────";
        private const string SelectedSceneIndexKey = "SceneToolbar.SelectedSceneIndex";
        private const string UseSelectedSceneOnPlayKey = "SceneToolbar.UseSelectedSceneOnPlay";

        static SceneToolbar()
        {
            // 에디터가 로드될 때 EditorPrefs에서 저장된 인덱스를 불러옵니다.
            selectedSceneIndex = EditorPrefs.GetInt(SelectedSceneIndexKey, 0);
            useSelectedSceneOnPlay = EditorPrefs.GetBool(UseSelectedSceneOnPlayKey, true);

            UpdateSceneList();
            ToolbarExtender.LeftToolbarGUI.Add(OnToolbarGUI);
            EditorBuildSettings.sceneListChanged += UpdateSceneList;
            EditorApplication.projectChanged += UpdateSceneList;
        }

        private static void OnToolbarGUI()
        {
            GUILayout.BeginHorizontal();

            // 선택된 인덱스가 범위를 벗어날 경우 0으로 초기화
            if (selectedSceneIndex >= sceneNames.Length)
            {
                selectedSceneIndex = 0;
            }

            // 드롭다운의 값이 변경되는지 감지하고 값이 변경되면 EditorPrefs에 저장합니다.
            EditorGUI.BeginChangeCheck();
            selectedSceneIndex = EditorGUILayout.Popup(selectedSceneIndex, sceneNames, GUILayout.Width(120));
            if (EditorGUI.EndChangeCheck())
            {
                if (!IsSelectableSceneIndex(selectedSceneIndex))
                {
                    selectedSceneIndex = GetNearestSelectableSceneIndex(selectedSceneIndex);
                }
                EditorPrefs.SetInt(SelectedSceneIndexKey, selectedSceneIndex);
            }

            EditorGUI.BeginChangeCheck();
            useSelectedSceneOnPlay = GUILayout.Toggle(useSelectedSceneOnPlay, GUIContent.none, GUILayout.Width(18));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(UseSelectedSceneOnPlayKey, useSelectedSceneOnPlay);
            }

            if (GUILayout.Button("Open", GUILayout.Width(90)))
            {
                var selectedScenePath = GetSelectedScenePath();
                if (!string.IsNullOrEmpty(selectedScenePath))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(selectedScenePath);
                    }
                }
            }

            var playStartScenePath = GetSelectedScenePath();
            if (useSelectedSceneOnPlay && !string.IsNullOrEmpty(playStartScenePath))
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(playStartScenePath);
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
            else
            {
                // null이면 현재 열려있는 씬을 시작 씬으로 사용합니다.
                EditorSceneManager.playModeStartScene = null;
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 프로젝트 전체 씬 목록을 가져옵니다.
        /// 일반 씬과 Plugins 씬 사이에 구분 라인을 추가합니다.
        /// </summary>
        private static void UpdateSceneList()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var allScenePaths = guids.Select(AssetDatabase.GUIDToAssetPath)
                                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

            var normalScenePaths = allScenePaths.Where(path => !path.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase)).ToList();
            var pluginScenePaths = allScenePaths.Where(path => path.StartsWith("Assets/Plugins/", StringComparison.OrdinalIgnoreCase)).ToList();

            var displayNames = new List<string>();
            var displayPaths = new List<string>();

            foreach (var path in normalScenePaths)
            {
                displayNames.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                displayPaths.Add(path);
            }

            if (normalScenePaths.Count > 0 && pluginScenePaths.Count > 0)
            {
                displayNames.Add(PluginSplitLabel);
                displayPaths.Add(string.Empty);
            }

            foreach (var path in pluginScenePaths)
            {
                displayNames.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                displayPaths.Add(path);
            }

            sceneNames = displayNames.ToArray();
            scenePaths = displayPaths.ToArray();

            if (sceneNames.Length == 0)
            {
                sceneNames = new string[] { "No Scenes in Project" };
                scenePaths = null;
                selectedSceneIndex = 0;
            }
            else
            {
                selectedSceneIndex = Mathf.Clamp(selectedSceneIndex, 0, sceneNames.Length - 1);
                if (!IsSelectableSceneIndex(selectedSceneIndex))
                {
                    selectedSceneIndex = GetNearestSelectableSceneIndex(selectedSceneIndex);
                }
                EditorPrefs.SetInt(SelectedSceneIndexKey, selectedSceneIndex);
            }
        }

        private static bool IsSelectableSceneIndex(int index)
        {
            if (scenePaths == null || index < 0 || index >= scenePaths.Length)
            {
                return false;
            }

            return !string.IsNullOrEmpty(scenePaths[index]);
        }

        private static int GetNearestSelectableSceneIndex(int preferredIndex)
        {
            if (scenePaths == null || scenePaths.Length == 0)
            {
                return 0;
            }

            for (int i = Mathf.Clamp(preferredIndex, 0, scenePaths.Length - 1); i < scenePaths.Length; i++)
            {
                if (IsSelectableSceneIndex(i))
                {
                    return i;
                }
            }

            for (int i = Mathf.Clamp(preferredIndex, 0, scenePaths.Length - 1); i >= 0; i--)
            {
                if (IsSelectableSceneIndex(i))
                {
                    return i;
                }
            }

            return 0;
        }

        private static string GetSelectedScenePath()
        {
            if (scenePaths == null || selectedSceneIndex < 0 || selectedSceneIndex >= scenePaths.Length)
            {
                return null;
            }

            return scenePaths[selectedSceneIndex];
        }
    }


    /// <summary>
    /// Unity 툴바 확장을 위한 헬퍼 클래스 (수정 불필요)
    /// </summary>
    [InitializeOnLoad]
    public static class ToolbarExtender
    {
        private static readonly Type m_toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        private static ScriptableObject m_currentToolbar;

        public static readonly List<Action> LeftToolbarGUI = new List<Action>();

        static ToolbarExtender()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (m_currentToolbar == null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(m_toolbarType);
                m_currentToolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;
            }

            if (m_currentToolbar != null)
            {
                var root = m_currentToolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m_currentToolbar) as VisualElement;
                var toolbarZone = root.Q("ToolbarZonePlayMode");
                //var toolbarZone = root.Q("ToolbarZoneLeftAlign");

                var container = toolbarZone?.Q<IMGUIContainer>("SceneToolbarContainer");

                if (container == null)
                {
                    container = new IMGUIContainer();
                    container.name = "SceneToolbarContainer";
                    container.onGUIHandler = () =>
                    {
                        foreach (var handler in LeftToolbarGUI)
                        {
                            handler();
                        }
                    };
                    toolbarZone.Add(container);
                }

                EditorApplication.update -= OnUpdate;
            }
        }
    }
}