using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CAT.HierarchyUtility
{
    // 하이어라키 창에서 오브젝트 참조 관계를 아이콘으로 표시하는 모듈.
    // UIOrder = 10
    public class HierarchyMarkerModule : IHierarchyToolModule
    {
        public string ModuleName => "HierarchyMarker";
        public int UIOrder => 10;

        private HierarchyWindowAccessor _accessor;

        private class ParentInfo
        {
            public int parentId;
            public bool isPrefabRoot;
            public MonoBehaviour script;
            public string fieldName;
        }

        private readonly Dictionary<int, List<ParentInfo>> _childToParentMap = new Dictionary<int, List<ParentInfo>>();

        private Texture2D _defaultIcon;
        private Texture2D _prefabRootIcon;
        private GUIStyle _countLabelStyle;

        // Hierarchy 포커스용 애니메이션 상태
        private bool _isFocusAnimating;
        private int _focusTargetInstanceID;
        private double _focusStartTime;
        private int _focusRequestVersion;
        private const double FocusDurationSec = 0.55;

        // RoundedRect 포인트 캐시(세그먼트 고정으로 비할당 렌더링)
        private const int FocusSegPerCorner = 6;
        // (seg + 1) * 4 = 28
        private readonly Vector3[] _focusFillPoints = new Vector3[(FocusSegPerCorner + 1) * 4];
        // +1 for closing point
        private readonly Vector3[] _focusOutlinePoints = new Vector3[(FocusSegPerCorner + 1) * 4 + 1];

        public void Initialize(HierarchyWindowAccessor accessor)
        {
            _accessor = accessor;
            UpdateMarkedObjectsCache();
        }

        public void InitUI(VisualElement container) { }
        public void OnUpdate()
        {
            if (!_isFocusAnimating) return;

            var elapsed = EditorApplication.timeSinceStartup - _focusStartTime;
            if (elapsed >= FocusDurationSec)
            {
                _isFocusAnimating = false;
                EditorApplication.RepaintHierarchyWindow();
            }
            else
            {
                // 애니메이션 동안 계속 갱신해서 OnHierarchyItemGUI가 호출되도록 함
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        public void OnSelectionChanged() { }

        public void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_isFocusAnimating && instanceID == _focusTargetInstanceID)
                DrawHierarchyFocus(selectionRect);

            if (_defaultIcon == null || _prefabRootIcon == null)
            {
                _defaultIcon = EditorGUIUtility.IconContent("d_greenLight").image as Texture2D;
                _prefabRootIcon = EditorGUIUtility.IconContent("d_orangeLight").image as Texture2D;
            }

            if (_countLabelStyle == null)
            {
                _countLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 7,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                };
                _countLabelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            }

            if (_childToParentMap.TryGetValue(instanceID, out List<ParentInfo> parentInfos))
            {
                bool hasPrefabRootParent = parentInfos.Any(p => p.isPrefabRoot);
                Texture2D iconToDraw = hasPrefabRootParent ? _prefabRootIcon : _defaultIcon;

                Rect iconRect = new Rect(selectionRect.xMax - 20f, selectionRect.y + (selectionRect.height - 12f) / 2, 8f, 8f);
                if (iconToDraw != null) GUI.DrawTexture(iconRect, iconToDraw);

                // 참조가 2개 이상일 때만 숫자 표시
                if (parentInfos.Count >= 2)
                {
                    string countText = parentInfos.Count > 9 ? "9+" : parentInfos.Count.ToString();
                    Rect countRect = new Rect(iconRect.xMax + 1f, iconRect.y - 1f, 14f, iconRect.height);
                    GUI.Label(countRect, countText, _countLabelStyle);
                }

                Event currentEvent = Event.current;
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && iconRect.Contains(currentEvent.mousePosition))
                {
                    currentEvent.Use();

                    // 모든 참조자의 GameObject를 수집하여 하이어라키에서 다중 선택
                    var allParentObjects = new List<UnityEngine.Object>();
                    foreach (var info in parentInfos)
                    {
                        GameObject parentObj = EditorUtility.InstanceIDToObject(info.parentId) as GameObject;
                        if (parentObj != null)
                        {
                            allParentObjects.Add(parentObj);
                            Debug.Log($"[HierarchyMarker] <b>{parentObj.name}</b> 오브젝트의 <b>{info.script.GetType().Name}</b> 컴포넌트가 <b>'{info.fieldName}'</b> 필드를 통해 참조합니다.", parentObj);
                        }
                    }

                    if (allParentObjects.Count > 0)
                    {
                        // 모든 참조 오브젝트를 하이어라키에서 선택 (다중 하이라이트)
                        Selection.objects = allParentObjects.ToArray();
                        // 대표 오브젝트로 스크롤 이동 (프리팹 루트 우선)
                        ParentInfo primary = hasPrefabRootParent ? parentInfos.First(p => p.isPrefabRoot) : parentInfos[0];
                        GameObject primaryObj = EditorUtility.InstanceIDToObject(primary.parentId) as GameObject;
                        if (primaryObj != null)
                        {
                            Selection.activeGameObject = primaryObj;

                            // (1) 폴딩된 상위 Root를 펼쳐서 실제 참조 위치가 보이도록 함
                            ExpandHierarchyAncestors(primaryObj.transform);

                            // (2) 스크롤/프레이밍 + 커스텀 라운딩 하이라이트 피드백
                            int requestVersion = ++_focusRequestVersion;
                            EditorApplication.delayCall += () =>
                            {
                                // 연속 클릭 시 오래된 delay는 무시
                                if (requestVersion != _focusRequestVersion) return;

                                // Transform/GO instanceID는 Unity 버전에 따라 TreeView 항목 ID로 매핑될 수 있어 둘 다 시도
                                bool scrolled = TryScrollToHierarchyItem(primaryObj.transform.GetInstanceID());
                                scrolled |= TryScrollToHierarchyItem(primaryObj.GetInstanceID());
                                if (!scrolled) EditorGUIUtility.PingObject(primaryObj); // 마지막 fallback

                                _focusTargetInstanceID = primaryObj.GetInstanceID();
                                _focusStartTime = EditorApplication.timeSinceStartup;
                                _isFocusAnimating = true;
                                EditorApplication.RepaintHierarchyWindow();
                            };
                        }

                        // 인스펙터에서 해당 필드 하이라이트
                        Highlighter.Stop();
                        EditorApplication.delayCall += () =>
                        {
                            Highlighter.Highlight("Inspector", primary.fieldName, HighlightSearchMode.Auto);
                        };
                    }
                }
            }
        }

        public void OnHierarchyChanged()
        {
            UpdateMarkedObjectsCache();
        }

        public void Dispose()
        {
            _childToParentMap.Clear();
        }

        private void ExpandHierarchyAncestors(Transform target)
        {
            if (target == null) return;

            // TreeView expanded 상태는 Unity 버전에 따라 Transform/GO instanceID를 모두 받을 때가 있어 둘 다 시도
            var expandedIDs = new HashSet<int>();
            var t = target;
            while (t != null)
            {
                expandedIDs.Add(t.GetInstanceID());
                if (t.gameObject != null) expandedIDs.Add(t.gameObject.GetInstanceID());
                t = t.parent;
            }

            var win = _accessor != null ? _accessor.Window : null;
            if (win == null) return;

            foreach (var id in expandedIDs)
                TrySetHierarchyExpanded(win, id, true);
        }

        private bool TryScrollToHierarchyItem(int instanceID)
        {
            var win = _accessor != null ? _accessor.Window : null;
            if (win == null) return false;

            try
            {
                object treeView = GetFieldDeep(win, "m_TreeView") ?? GetFieldDeep(win, "treeView");
                if (treeView == null) return false;

                var treeType = treeView.GetType();

                // Unity 6 방식 후보
                var frameMethod = treeType.GetMethod("Frame",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null, new Type[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) }, null);
                if (frameMethod != null)
                {
                    frameMethod.Invoke(treeView, new object[] { instanceID, true, false, true });
                    return true;
                }

                // Unity 2022 방식 후보
                var frameItemMethod = treeType.GetMethod("FrameItem",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null, new Type[] { typeof(int) }, null);
                if (frameItemMethod != null)
                {
                    frameItemMethod.Invoke(treeView, new object[] { instanceID });
                    return true;
                }
            }
            catch
            {
                // 스크롤 실패는 무시하고 호출부 fallback에서 Ping 처리
            }

            return false;
        }

        private static void TrySetHierarchyExpanded(EditorWindow hierarchyWindow, int instanceID, bool expanded)
        {
            if (hierarchyWindow == null) return;

            try
            {
                object treeViewState = GetFieldDeepStatic(hierarchyWindow, "m_TreeViewState")
                    ?? GetFieldDeepStatic(hierarchyWindow, "treeViewState");
                if (treeViewState == null) return;

                var stateType = treeViewState.GetType();

                // TreeViewState에 SetExpanded가 있는 경우
                var setExpanded = stateType.GetMethod("SetExpanded",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                    null, new Type[] { typeof(int), typeof(bool) }, null);
                if (setExpanded != null)
                {
                    setExpanded.Invoke(treeViewState, new object[] { instanceID, expanded });
                    return;
                }

                // m_ExpandedIDs 배열 직접 조작(내부 구현 버전 대응)
                var expandedIDsField = stateType.GetField("m_ExpandedIDs",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (expandedIDsField == null) return;

                var value = expandedIDsField.GetValue(treeViewState);
                if (value is int[] arr)
                {
                    if (arr.Contains(instanceID)) return;
                    var newArr = new int[arr.Length + 1];
                    arr.CopyTo(newArr, 0);
                    newArr[newArr.Length - 1] = instanceID;
                    expandedIDsField.SetValue(treeViewState, newArr);
                }
                else if (value is List<int> list)
                {
                    if (list.Contains(instanceID)) return;
                    list.Add(instanceID);
                }
            }
            catch
            {
                // 확장 상태 변경 실패는 무시
            }
        }

        private static object GetFieldDeepStatic(object obj, string name)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            while (type != null)
            {
                var f = type.GetField(name,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (f != null) return f.GetValue(obj);
                type = type.BaseType;
            }
            return null;
        }

        private object GetFieldDeep(object obj, string name) => GetFieldDeepStatic(obj, name);

        private void DrawHierarchyFocus(Rect selectionRect)
        {
            if (selectionRect.width <= 1f || selectionRect.height <= 1f) return;

            var elapsed = EditorApplication.timeSinceStartup - _focusStartTime;
            float t = Mathf.Clamp01((float)(elapsed / FocusDurationSec));

            // 초반 펄스 + 후반 페이드
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float alpha = 1f - eased;

            // 라운딩 라디우스는 높이를 따라가게
            float radius = Mathf.Min(6f, selectionRect.height * 0.45f);
            float inset = 1f;
            var r = new Rect(selectionRect.x + inset, selectionRect.y + inset, selectionRect.width - inset * 2f, selectionRect.height - inset * 2f);

            // 펄스(살짝 커졌다가 돌아오기)
            float pulse = 1f + (1f - t) * 0.08f * Mathf.Sin(t * Mathf.PI);
            r = CenterScaleRect(r, pulse);

            Handles.BeginGUI();

            // 채움
            var fill = new Color(0.2f, 0.6f, 1f, 0.18f * alpha);
            DrawRoundedRect(fill, r, radius, isFill: true);

            // 외곽선(두 겹)
            var outline2 = new Color(0.2f, 0.7f, 1f, 0.65f * alpha);
            var outline1 = new Color(0.7f, 0.95f, 1f, 0.95f * alpha);
            DrawRoundedRect(outline2, r, radius, isFill: false, outlineThickness: 1.5f);
            DrawRoundedRect(outline1, r, radius, isFill: false, outlineThickness: 2.5f - 1.5f * eased);

            Handles.EndGUI();
        }

        private static Rect CenterScaleRect(Rect rect, float scale)
        {
            float cx = rect.x + rect.width * 0.5f;
            float cy = rect.y + rect.height * 0.5f;
            float w = rect.width * scale;
            float h = rect.height * scale;
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }

        private void DrawRoundedRect(Color color, Rect rect, float radius, bool isFill, float outlineThickness = 2f)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);

            // 라운딩 라디우스가 거의 0이면 사각형으로 fallback
            if (radius <= 0.001f)
            {
                Handles.color = color;
                if (isFill)
                {
                    EditorGUI.DrawRect(rect, color);
                }
                else
                {
                    Handles.DrawAAPolyLine(outlineThickness, new[]
                    {
                        new Vector3(rect.x, rect.y, 0f),
                        new Vector3(rect.x + rect.width, rect.y, 0f),
                        new Vector3(rect.x + rect.width, rect.y + rect.height, 0f),
                        new Vector3(rect.x, rect.y + rect.height, 0f),
                        new Vector3(rect.x, rect.y, 0f),
                    });
                }
                return;
            }

            FillRoundedRectPoints(rect, radius);

            Handles.color = color;
            if (isFill)
            {
                Handles.DrawAAConvexPolygon(_focusFillPoints);
            }
            else
            {
                Handles.DrawAAPolyLine(outlineThickness, _focusOutlinePoints);
            }
        }

        private void FillRoundedRectPoints(Rect rect, float radius)
        {
            // 시계방향으로 사각형 주변을 따라가는 라운딩 포인트(픽셀 좌표계)
            float xMin = rect.xMin;
            float xMax = rect.xMax;
            float yMin = rect.yMin;
            float yMax = rect.yMax;

            Vector2 tr = new Vector2(xMax - radius, yMin + radius);
            Vector2 br = new Vector2(xMax - radius, yMax - radius);
            Vector2 bl = new Vector2(xMin + radius, yMax - radius);
            Vector2 tl = new Vector2(xMin + radius, yMin + radius);

            int idx = 0;
            idx = AddArcPoints(_focusFillPoints, idx, tr, radius, -90f, 0f, FocusSegPerCorner);
            idx = AddArcPoints(_focusFillPoints, idx, br, radius, 0f, 90f, FocusSegPerCorner);
            idx = AddArcPoints(_focusFillPoints, idx, bl, radius, 90f, 180f, FocusSegPerCorner);
            AddArcPoints(_focusFillPoints, idx, tl, radius, 180f, 270f, FocusSegPerCorner);

            // 외곽선용은 첫 포인트를 마지막에 다시 넣어서 닫기
            for (int i = 0; i < _focusFillPoints.Length; i++)
                _focusOutlinePoints[i] = _focusFillPoints[i];
            _focusOutlinePoints[_focusOutlinePoints.Length - 1] = _focusFillPoints[0];
        }

        private static int AddArcPoints(Vector3[] dst, int startIndex, Vector2 center, float radius, float startDeg, float endDeg, int seg)
        {
            float step = (endDeg - startDeg) / seg;
            int idx = startIndex;
            for (int i = 0; i <= seg; i++)
            {
                float deg = startDeg + step * i;
                float rad = deg * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(rad) * radius;
                float y = center.y + Mathf.Sin(rad) * radius;
                dst[idx++] = new Vector3(x, y, 0f);
            }
            return idx;
        }

        private void UpdateMarkedObjectsCache()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _childToParentMap.Clear();
            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            IEnumerable<MonoBehaviour> scriptsToScan;

            if (currentPrefabStage != null)
            {
                scriptsToScan = currentPrefabStage.scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true));
            }
            else
            {
                scriptsToScan = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt).Where(s => s.isLoaded)
                    .SelectMany(s => s.GetRootGameObjects())
                    .SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true));
            }

            foreach (var script in scriptsToScan)
            {
                if (script == null) continue;

                // 시스템/라이브러리 네임스페이스 필터링
                // Unity 기본 컴포넌트나 TMP 같은 라이브러리 컴포넌트는 건너뜀
                var scriptNamespace = script.GetType().Namespace;
                if (!string.IsNullOrEmpty(scriptNamespace) && (
                    scriptNamespace.StartsWith("UnityEngine") ||
                    scriptNamespace.StartsWith("UnityEditor") ||
                    scriptNamespace.StartsWith("TMPro")))
                {
                    continue;
                }

                GameObject parentObject = script.gameObject;
                int parentID = parentObject.GetInstanceID();
                bool isParentPrefabRoot = currentPrefabStage != null
                    ? parentObject.transform.parent == currentPrefabStage.prefabContentsRoot.transform
                    : PrefabUtility.IsPartOfPrefabInstance(parentObject) && PrefabUtility.GetNearestPrefabInstanceRoot(parentObject) == parentObject;

                // SerializedProperty로 모든 오브젝트 참조 탐색 (중첩 직렬화 클래스, 배열, 리스트 모두 포함)
                var so = new SerializedObject(script);
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;

                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    // m_Script 등 Unity 내부 필드 제외
                    if (prop.propertyPath == "m_Script") continue;

                    UnityEngine.Object refObj = prop.objectReferenceValue;
                    if (refObj == null) continue;

                    GameObject referencedObject = null;
                    if (refObj is GameObject go) referencedObject = go;
                    else if (refObj is Component comp) referencedObject = comp.gameObject;

                    // 루트 필드 이름 추출 (예: "targets.Array.data[2].transform" → "targets")
                    string fieldName = prop.propertyPath;
                    int dotIndex = fieldName.IndexOf('.');
                    if (dotIndex >= 0) fieldName = fieldName.Substring(0, dotIndex);

                    RegisterReference(referencedObject, parentID, isParentPrefabRoot, script, fieldName);
                }
                so.Dispose();
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        private void RegisterReference(GameObject referencedObject, int parentID, bool isParentPrefabRoot, MonoBehaviour script, string fieldName)
        {
            if (referencedObject == null) return;

            int childID = referencedObject.GetInstanceID();
            if (parentID == childID) return;

            var parentInfo = new ParentInfo { parentId = parentID, isPrefabRoot = isParentPrefabRoot, script = script, fieldName = fieldName };
            if (!_childToParentMap.ContainsKey(childID))
                _childToParentMap[childID] = new List<ParentInfo>();

            // 동일 스크립트+필드 중복 등록 방지
            if (!_childToParentMap[childID].Any(p => p.script == script && p.fieldName == fieldName))
                _childToParentMap[childID].Add(parentInfo);
        }
    }
}
