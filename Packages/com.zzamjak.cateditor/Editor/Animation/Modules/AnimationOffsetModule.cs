using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using AnimUtil = UnityEditor.AnimationUtility;

namespace CAT.AnimationUtility
{
    // Animation Window에 Offset 기능 UI를 추가하는 모듈.
    // - Position, Rotation, Scale에 대해 각각 Offset을 적용.
    // - Offset 단위를 Frame과 Time(초)으로 전환 가능.
    // - 키 추가(+All/+Pos/+Rot/+Sca), 불필요 키 제거(Clean) 기능 포함.
    // 기존 AnimationOffset.cs(CAT.Utility 네임스페이스)를 IAnimationToolModule로 리팩토링.
    public class AnimationOffsetModule : IAnimationToolModule
    {
        // 접힘 상태 저장 키 및 높이 상수
        private const string PrefKeyFolded = "AnimationOffset_IsFolded";
        private const float ExpandedHeight = 42f;
        private const float FoldedHeight = 18f;

        // Offset 관련 인스턴스 필드 (기존 static → 인스턴스로 전환)
        private float _offsetValue = 0f;
        private bool _isTimeInputMode = false;
        private bool _isFolded;
        private VisualElement _parentContainer;

        // UI 너비 상수
        private const float ObjectNameWidth = 120f;
        private const float InputFieldWidth = 100f;
        private const float ModeButtonWidth = 50f;
        private const float ResetButtonWidth = 40f;
        private const float KeyGenButtonWidth = 45f;
        private const float OffsetButtonWidth = 68f;
        private const float CleanButtonWidth = 60f;
        private const float SectionSpacing = 10f;

        [System.Flags]
        private enum PropertyType { Position = 1, Rotation = 2, Scale = 4 }

        private AnimationWindowAccessor _accessor;

        public string ModuleName => "AnimationOffset";
        public int UIOrder => 10;

        public void Initialize(AnimationWindowAccessor accessor)
        {
            _accessor = accessor;
            _isFolded = EditorPrefs.GetBool(PrefKeyFolded, false);
        }

        // Animation Window 우하단에 툴바 UI 주입
        public void InitUI(VisualElement container)
        {
            // 중복 주입 방지
            if (container.Q<VisualElement>("AnimationOffsetContainer") != null) return;

            _parentContainer = new VisualElement
            {
                name = "AnimationOffsetContainer",
                style =
                {
                    position = Position.Absolute,
                    right = 25f,
                    bottom = 15f,
                    width = _isFolded ? 26f : 430f,
                    height = _isFolded ? FoldedHeight : ExpandedHeight,
                    flexDirection = FlexDirection.Row
                }
            };

            var imguiContainer = new IMGUIContainer(DrawGUI);
            imguiContainer.style.flexGrow = 1;
            _parentContainer.Add(imguiContainer);
            container.Add(_parentContainer);
        }

        // Animation Window 위에 삽입될 툴바 UI (접힘/펼침 지원)
        private void DrawGUI()
        {
            if (_isFolded)
            {
                // 접힌 상태: 펼치기 화살표 버튼만 표시 (배경 없음)
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(22), GUILayout.Height(18)))
                    SetFolded(false);
                return;
            }

            // 펼쳐진 상태: 기존 2행 UI
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 선택된 GameObject 이름 표시
            var selectedObject = Selection.activeGameObject;
            string selectedObjectName = (selectedObject != null) ? selectedObject.name : "None";
            EditorGUILayout.LabelField(new GUIContent(selectedObjectName, "Selected GameObject"),
                GUILayout.Width(ObjectNameWidth));

            // Offset 모드(Time/Frame) 버튼
            var originalColor = GUI.backgroundColor;
            string modeText;
            if (_isTimeInputMode)
            {
                GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
                modeText = "Time";
            }
            else
            {
                GUI.backgroundColor = new Color(0.5f, 0.7f, 1.0f);
                modeText = "Frame";
            }

            if (GUILayout.Button(new GUIContent(modeText, "Switch input: Frames vs Seconds"),
                EditorStyles.toolbarButton, GUILayout.Width(ModeButtonWidth)))
            {
                if (_offsetValue != 0)
                {
                    var activeClip = _accessor.ActiveClip;
                    float frameRate = (activeClip != null) ? activeClip.frameRate : 60f;
                    if (_isTimeInputMode) _offsetValue *= frameRate;
                    else _offsetValue /= frameRate;
                }
                _isTimeInputMode = !_isTimeInputMode;
                GUI.FocusControl(null);
            }

            GUI.backgroundColor = originalColor;

            string inputTooltip = _isTimeInputMode ? "Time offset" : "Frame offset";
            _offsetValue = EditorGUILayout.FloatField(new GUIContent("", inputTooltip), _offsetValue,
                EditorStyles.toolbarTextField, GUILayout.Width(InputFieldWidth));

            // [R] Reset 버튼
            if (GUILayout.Button(new GUIContent("R", "Reset to 0"), EditorStyles.toolbarButton,
                GUILayout.Width(ResetButtonWidth)))
            {
                _offsetValue = 0f;
                GUI.FocusControl(null);
            }

            GUILayout.Space(SectionSpacing);

            // [Clean] 불필요한 키 제거 버튼
            var cleanButtonColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
            if (GUILayout.Button(new GUIContent("Clean", "Remove unnecessary keys for all objects"),
                EditorStyles.toolbarButton, GUILayout.Width(CleanButtonWidth)))
            {
                CleanAllUnnecessaryKeys();
            }
            GUI.backgroundColor = cleanButtonColor;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 키 생성 버튼 [+All], [+Pos], [+Rot], [+Sca]
            var keyGenButtonColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 1.0f, 0.6f);

            if (GUILayout.Button(new GUIContent("+All", "Add keys for all Transform properties"),
                EditorStyles.toolbarButton, GUILayout.Width(KeyGenButtonWidth)))
                AddTransformKeys(PropertyType.Position | PropertyType.Rotation | PropertyType.Scale);
            if (GUILayout.Button(new GUIContent("+Pos", "Add keys for Position"),
                EditorStyles.toolbarButton, GUILayout.Width(KeyGenButtonWidth)))
                AddTransformKeys(PropertyType.Position);
            if (GUILayout.Button(new GUIContent("+Rot", "Add keys for Rotation"),
                EditorStyles.toolbarButton, GUILayout.Width(KeyGenButtonWidth)))
                AddTransformKeys(PropertyType.Rotation);
            if (GUILayout.Button(new GUIContent("+Sca", "Add keys for Scale"),
                EditorStyles.toolbarButton, GUILayout.Width(KeyGenButtonWidth)))
                AddTransformKeys(PropertyType.Scale);

            GUI.backgroundColor = keyGenButtonColor;

            GUILayout.Space(SectionSpacing);

            // Offset 적용 버튼 [Position], [Rotation], [Scale]
            if (GUILayout.Button(new GUIContent("Position", "Apply offset to Position"),
                EditorStyles.toolbarButton, GUILayout.Width(OffsetButtonWidth)))
                ApplyLoopOffset(PropertyType.Position);
            if (GUILayout.Button(new GUIContent("Rotation", "Apply offset to Rotation"),
                EditorStyles.toolbarButton, GUILayout.Width(OffsetButtonWidth)))
                ApplyLoopOffset(PropertyType.Rotation);
            if (GUILayout.Button(new GUIContent("Scale", "Apply offset to Scale"),
                EditorStyles.toolbarButton, GUILayout.Width(OffsetButtonWidth)))
                ApplyLoopOffset(PropertyType.Scale);

            // 접기 버튼
            if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(22)))
                SetFolded(true);

            EditorGUILayout.EndHorizontal();
        }

        private void SetFolded(bool folded)
        {
            _isFolded = folded;
            EditorPrefs.SetBool(PrefKeyFolded, _isFolded);
            if (_parentContainer != null)
            {
                _parentContainer.style.height = _isFolded ? FoldedHeight : ExpandedHeight;
                // 접힌 상태에서 배경 프레임이 보이지 않도록 너비도 축소
                _parentContainer.style.width = _isFolded ? 26f : 430f;
            }
            _accessor.Window?.Repaint();
        }

        public void OnUpdate() { }
        public void OnSelectionChanged() { }

        // 선택된 GameObject의 Transform 속성에 대한 키를 추가
        private void AddTransformKeys(PropertyType propertyTypes)
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null) { Debug.LogError("Select a GameObject."); return; }

            var activeClip = _accessor.ActiveClip;
            if (activeClip == null) { Debug.LogError("Select an Animation Clip."); return; }

            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath)) { Debug.LogError("Cannot find asset path for the clip."); return; }

            var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null) { Debug.LogError($"Failed to load clip from path: {clipPath}"); return; }

            var rootObject = _accessor.ActiveRoot;
            if (rootObject == null) { Debug.LogError("Cannot find animation root GameObject."); return; }

            string selectedObjectPath = AnimUtil.CalculateTransformPath(
                selectedObject.transform, rootObject.transform);
            float clipDuration = sourceClip.length;

            var rectTransform = selectedObject.GetComponent<RectTransform>();
            bool isRectTransform = rectTransform != null;

            Undo.RecordObject(sourceClip, "Add Transform Keys");

            bool anyKeyAdded = false;
            string addedProperties = "";

            // Position 키 추가
            if ((propertyTypes & PropertyType.Position) != 0)
            {
                if (isRectTransform)
                {
                    if (AddRectTransformPositionKeys(sourceClip, selectedObjectPath, rectTransform.anchoredPosition, clipDuration))
                    {
                        anyKeyAdded = true;
                        addedProperties += "Position ";
                    }
                }
                else
                {
                    if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalPosition",
                        selectedObject.transform.localPosition, clipDuration, typeof(Transform)))
                    {
                        anyKeyAdded = true;
                        addedProperties += "Position ";
                    }
                }
            }

            // Rotation 키 추가
            if ((propertyTypes & PropertyType.Rotation) != 0)
            {
                string rotationPropertyType = DetectRotationPropertyType(sourceClip, selectedObjectPath);
                if (rotationPropertyType == "quaternion")
                {
                    var rotation = selectedObject.transform.localRotation;
                    if (AddQuaternionKeys(sourceClip, selectedObjectPath, rotation, clipDuration, isRectTransform))
                    {
                        anyKeyAdded = true;
                        addedProperties += "Rotation ";
                        InitializeRotationProperty(sourceClip, selectedObjectPath,
                            selectedObject.transform.localEulerAngles.z, isRectTransform);
                    }
                    RemoveEulerRotationCurves(sourceClip, selectedObjectPath, isRectTransform);
                }
                else
                {
                    RemoveQuaternionRotationCurves(sourceClip, selectedObjectPath, isRectTransform);
                    var eulerAngles = selectedObject.transform.localEulerAngles;
                    if (isRectTransform)
                    {
                        if (AddRectTransformRotationKeys(sourceClip, selectedObjectPath, eulerAngles.z, clipDuration))
                        {
                            anyKeyAdded = true;
                            addedProperties += "Rotation ";
                            InitializeRotationProperty(sourceClip, selectedObjectPath, eulerAngles.z, isRectTransform);
                        }
                    }
                    else
                    {
                        if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalEulerAngles",
                            eulerAngles, clipDuration, typeof(Transform)))
                        {
                            anyKeyAdded = true;
                            addedProperties += "Rotation ";
                            InitializeRotationProperty(sourceClip, selectedObjectPath, eulerAngles.z, isRectTransform);
                        }
                    }
                }
            }

            // Scale 키 추가
            if ((propertyTypes & PropertyType.Scale) != 0)
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
                if (AddPropertyKeys(sourceClip, selectedObjectPath, "m_LocalScale",
                    selectedObject.transform.localScale, clipDuration, bindingType))
                {
                    anyKeyAdded = true;
                    addedProperties += "Scale ";
                }
            }

            if (anyKeyAdded)
            {
                Debug.Log($"Transform keys added for: {addedProperties.Trim()} at frame 0 and {(clipDuration > 0 ? "end" : "0")}");
                _accessor.ForceRefresh();
            }
            else
            {
                Debug.LogWarning("Failed to add transform keys.");
            }
        }

        // 현재 클립의 rotation 속성 타입 감지 (quaternion 또는 euler)
        private string DetectRotationPropertyType(AnimationClip clip, string objectPath)
        {
            var bindings = AnimUtil.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.path == objectPath)
                {
                    if (binding.propertyName.Contains("m_LocalRotation")) return "quaternion";
                    if (binding.propertyName.Contains("localEulerAngles") ||
                        binding.propertyName.Contains("m_LocalEulerAngles") ||
                        binding.propertyName.Contains("localEulerAnglesRaw"))
                        return "euler";
                }
            }
            return "euler";
        }

        private bool AddQuaternionKeys(AnimationClip clip, string objectPath, Quaternion rotation, float duration, bool isRectTransform)
        {
            try
            {
                string[] components = { ".x", ".y", ".z", ".w" };
                float[] values = { rotation.x, rotation.y, rotation.z, rotation.w };
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);

                for (int i = 0; i < 4; i++)
                {
                    string fullPropertyName = "m_LocalRotation" + components[i];
                    var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, fullPropertyName);
                    var curve = AnimUtil.GetEditorCurve(clip, binding) ?? new AnimationCurve();
                    curve.AddKey(0f, values[i]);
                    if (duration > 0f) curve.AddKey(duration, values[i]);
                    AnimUtil.SetEditorCurve(clip, binding, curve);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add quaternion keys: {e.Message}");
                return false;
            }
        }

        private void RemoveQuaternionRotationCurves(AnimationClip clip, string objectPath, bool isRectTransform)
        {
            try
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
                string[] quaternionComponents = { ".x", ".y", ".z", ".w" };
                foreach (string component in quaternionComponents)
                {
                    var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + component);
                    if (AnimUtil.GetEditorCurve(clip, binding) != null)
                        AnimUtil.SetEditorCurve(clip, binding, null);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to remove Quaternion rotation curves: {e.Message}");
            }
        }

        private void RemoveEulerRotationCurves(AnimationClip clip, string objectPath, bool isRectTransform)
        {
            try
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
                if (isRectTransform)
                {
                    var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "localEulerAnglesRaw.z");
                    if (AnimUtil.GetEditorCurve(clip, binding) != null)
                        AnimUtil.SetEditorCurve(clip, binding, null);
                }
                else
                {
                    string[] eulerComponents = { ".x", ".y", ".z" };
                    foreach (string component in eulerComponents)
                    {
                        var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalEulerAngles" + component);
                        if (AnimUtil.GetEditorCurve(clip, binding) != null)
                            AnimUtil.SetEditorCurve(clip, binding, null);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to remove Euler rotation curves: {e.Message}");
            }
        }

        private bool AddPropertyKeys(AnimationClip clip, string objectPath, string propertyName,
            Vector3 value, float duration, System.Type bindingType)
        {
            try
            {
                string[] components = { ".x", ".y", ".z" };
                float[] values = { value.x, value.y, value.z };
                for (int i = 0; i < 3; i++)
                {
                    string fullPropertyName = propertyName + components[i];
                    var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, fullPropertyName);
                    var curve = AnimUtil.GetEditorCurve(clip, binding) ?? new AnimationCurve();
                    curve.AddKey(0f, values[i]);
                    if (duration > 0f) curve.AddKey(duration, values[i]);
                    AnimUtil.SetEditorCurve(clip, binding, curve);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add keys for {propertyName}: {e.Message}");
                return false;
            }
        }

        private bool AddRectTransformPositionKeys(AnimationClip clip, string objectPath, Vector2 anchoredPosition, float duration)
        {
            try
            {
                string[] components = { ".x", ".y" };
                float[] values = { anchoredPosition.x, anchoredPosition.y };
                for (int i = 0; i < 2; i++)
                {
                    string fullPropertyName = "m_AnchoredPosition" + components[i];
                    var binding = EditorCurveBinding.FloatCurve(objectPath, typeof(RectTransform), fullPropertyName);
                    var curve = AnimUtil.GetEditorCurve(clip, binding) ?? new AnimationCurve();
                    curve.AddKey(0f, values[i]);
                    if (duration > 0f) curve.AddKey(duration, values[i]);
                    AnimUtil.SetEditorCurve(clip, binding, curve);
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add RectTransform position keys: {e.Message}");
                return false;
            }
        }

        private bool AddRectTransformRotationKeys(AnimationClip clip, string objectPath, float rotationZ, float duration)
        {
            try
            {
                var binding = EditorCurveBinding.FloatCurve(objectPath, typeof(RectTransform), "localEulerAnglesRaw.z");
                var curve = AnimUtil.GetEditorCurve(clip, binding) ?? new AnimationCurve();
                curve.AddKey(0f, rotationZ);
                if (duration > 0f) curve.AddKey(duration, rotationZ);
                AnimUtil.SetEditorCurve(clip, binding, curve);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to add RectTransform rotation keys: {e.Message}");
                return false;
            }
        }

        // Rotation 프로퍼티 초기화: 0프레임 Z값 +0.1 후 원래 값으로 복원하여 에디터 업데이트 트리거
        private void InitializeRotationProperty(AnimationClip clip, string objectPath, float originalZValue, bool isRectTransform)
        {
            try
            {
                System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
                string eulerPropertyName = isRectTransform ? "localEulerAnglesRaw.z" : "m_LocalEulerAngles.z";
                var eulerBinding = EditorCurveBinding.FloatCurve(objectPath, bindingType, eulerPropertyName);
                var eulerCurve = AnimUtil.GetEditorCurve(clip, eulerBinding);

                var quatBinding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation.x");
                var quatCurve = AnimUtil.GetEditorCurve(clip, quatBinding);

                if (quatCurve != null && quatCurve.keys.Length > 0)
                    InitializeQuaternionRotationProperty(clip, objectPath, originalZValue, isRectTransform);
                else if (eulerCurve != null && eulerCurve.keys.Length > 0)
                    InitializeEulerRotationProperty(clip, eulerBinding, eulerCurve, originalZValue);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to initialize rotation property: {e.Message}");
            }
        }

        private void InitializeEulerRotationProperty(AnimationClip clip, EditorCurveBinding binding,
            AnimationCurve curve, float originalZValue)
        {
            int keyIndex = -1;
            for (int i = 0; i < curve.keys.Length; i++)
            {
                if (Mathf.Approximately(curve.keys[i].time, 0f)) { keyIndex = i; break; }
            }
            if (keyIndex == -1) return;

            var key = curve.keys[keyIndex];
            var newKey = new Keyframe(key.time, key.value + 0.1f, key.inTangent, key.outTangent, key.inWeight, key.outWeight)
                { weightedMode = key.weightedMode };
            curve.MoveKey(keyIndex, newKey);
            AnimUtil.SetEditorCurve(clip, binding, curve);

            var restoredKey = new Keyframe(key.time, originalZValue, key.inTangent, key.outTangent, key.inWeight, key.outWeight)
                { weightedMode = key.weightedMode };
            curve.MoveKey(keyIndex, restoredKey);
            AnimUtil.SetEditorCurve(clip, binding, curve);
        }

        private void InitializeQuaternionRotationProperty(AnimationClip clip, string objectPath,
            float originalZValue, bool isRectTransform)
        {
            System.Type bindingType = isRectTransform ? typeof(RectTransform) : typeof(Transform);
            string[] quatComponents = { ".x", ".y", ".z", ".w" };
            float[] quatValues = new float[4];

            for (int i = 0; i < 4; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + quatComponents[i]);
                var curve = AnimUtil.GetEditorCurve(clip, binding);
                if (curve == null || curve.keys.Length == 0) return;

                int keyIndex = -1;
                for (int j = 0; j < curve.keys.Length; j++)
                {
                    if (Mathf.Approximately(curve.keys[j].time, 0f)) { keyIndex = j; break; }
                }
                if (keyIndex == -1) return;
                quatValues[i] = curve.keys[keyIndex].value;
            }

            var quat = new Quaternion(quatValues[0], quatValues[1], quatValues[2], quatValues[3]);
            var euler = quat.eulerAngles;
            var modifiedQuat = Quaternion.Euler(new Vector3(euler.x, euler.y, euler.z + 0.1f));
            float[] modifiedQuatValues = { modifiedQuat.x, modifiedQuat.y, modifiedQuat.z, modifiedQuat.w };

            // +0.1 적용
            for (int i = 0; i < 4; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + quatComponents[i]);
                var curve = AnimUtil.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                int keyIndex = -1;
                for (int j = 0; j < curve.keys.Length; j++)
                {
                    if (Mathf.Approximately(curve.keys[j].time, 0f)) { keyIndex = j; break; }
                }
                if (keyIndex == -1) continue;
                var key = curve.keys[keyIndex];
                curve.MoveKey(keyIndex, new Keyframe(key.time, modifiedQuatValues[i], key.inTangent, key.outTangent, key.inWeight, key.outWeight)
                    { weightedMode = key.weightedMode });
                AnimUtil.SetEditorCurve(clip, binding, curve);
            }

            // 원래 값으로 복원
            for (int i = 0; i < 4; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(objectPath, bindingType, "m_LocalRotation" + quatComponents[i]);
                var curve = AnimUtil.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                int keyIndex = -1;
                for (int j = 0; j < curve.keys.Length; j++)
                {
                    if (Mathf.Approximately(curve.keys[j].time, 0f)) { keyIndex = j; break; }
                }
                if (keyIndex == -1) continue;
                var key = curve.keys[keyIndex];
                curve.MoveKey(keyIndex, new Keyframe(key.time, quatValues[i], key.inTangent, key.outTangent, key.inWeight, key.outWeight)
                    { weightedMode = key.weightedMode });
                AnimUtil.SetEditorCurve(clip, binding, curve);
            }
        }

        // 모든 객체의 불필요한 키 제거 (값이 변하지 않는 중간 키프레임)
        private void CleanAllUnnecessaryKeys()
        {
            var activeClip = _accessor.ActiveClip;
            if (activeClip == null) { Debug.LogError("Select an Animation Clip."); return; }

            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath)) { Debug.LogError("Cannot find asset path for the clip."); return; }

            var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null) { Debug.LogError($"Failed to load clip from path: {clipPath}"); return; }

            if (_accessor.ActiveRoot == null) { Debug.LogError("Cannot find animation root GameObject."); return; }

            Undo.RecordObject(sourceClip, "Clean All Unnecessary Keys");
            var bindings = AnimUtil.GetCurveBindings(sourceClip);
            bool anyCurveModified = false;
            int totalKeysRemoved = 0;
            int objectsProcessed = 0;
            var processedObjects = new HashSet<string>();

            foreach (var binding in bindings)
            {
                if (!processedObjects.Contains(binding.path))
                {
                    processedObjects.Add(binding.path);
                    objectsProcessed++;
                }

                var curve = AnimUtil.GetEditorCurve(sourceClip, binding);
                if (curve == null || curve.keys.Length <= 2) continue;

                var cleanedCurve = RemoveUnnecessaryKeys(curve);
                if (cleanedCurve != null && cleanedCurve.keys.Length != curve.keys.Length)
                {
                    AnimUtil.SetEditorCurve(sourceClip, binding, cleanedCurve);
                    anyCurveModified = true;
                    totalKeysRemoved += curve.keys.Length - cleanedCurve.keys.Length;
                }
            }

            if (anyCurveModified)
            {
                Debug.Log($"All Clean completed: {totalKeysRemoved} keys removed from {objectsProcessed} objects");
                _accessor.ForceRefresh();
            }
            else
            {
                Debug.LogWarning("No unnecessary keys found in the animation clip.");
            }
        }

        // 이전/이후 키와 값이 동일하고 커브 구간이 평탄한 중간 키만 제거
        // 값이 같아도 탄젠트에 의해 커브 형태가 달라지는 키는 유지
        private AnimationCurve RemoveUnnecessaryKeys(AnimationCurve originalCurve)
        {
            if (originalCurve.keys.Length <= 2) return originalCurve;

            var keys = new List<Keyframe>(originalCurve.keys);
            var cleanedKeys = new List<Keyframe>();
            cleanedKeys.Add(keys[0]);

            const float tangentEpsilon = 0.001f;

            for (int i = 1; i < keys.Count - 1; i++)
            {
                // 조건 1: 이전, 현재, 다음 키의 값이 모두 동일
                bool sameValues = Mathf.Approximately(keys[i].value, keys[i - 1].value) &&
                                  Mathf.Approximately(keys[i].value, keys[i + 1].value);

                if (!sameValues)
                {
                    cleanedKeys.Add(keys[i]);
                    continue;
                }

                // 조건 2: A→B 구간이 평탄 (A의 outTangent ≈ 0, B의 inTangent ≈ 0)
                bool flatBefore = Mathf.Abs(keys[i - 1].outTangent) < tangentEpsilon &&
                                  Mathf.Abs(keys[i].inTangent) < tangentEpsilon;

                // 조건 3: B→C 구간이 평탄 (B의 outTangent ≈ 0, C의 inTangent ≈ 0)
                bool flatAfter = Mathf.Abs(keys[i].outTangent) < tangentEpsilon &&
                                 Mathf.Abs(keys[i + 1].inTangent) < tangentEpsilon;

                // 모든 구간이 평탄할 때만 제거 (커브 형태에 영향을 주지 않는 경우)
                bool isUnnecessary = flatBefore && flatAfter;

                if (!isUnnecessary) cleanedKeys.Add(keys[i]);
            }
            cleanedKeys.Add(keys[keys.Count - 1]);

            if (cleanedKeys.Count == originalCurve.keys.Length) return originalCurve;

            var newCurve = new AnimationCurve(cleanedKeys.ToArray());
            newCurve.preWrapMode = originalCurve.preWrapMode;
            newCurve.postWrapMode = originalCurve.postWrapMode;
            return newCurve;
        }

        // 루프 애니메이션 오프셋 적용
        private void ApplyLoopOffset(PropertyType propertyType)
        {
            if (_offsetValue == 0) { Debug.LogWarning("Offset value is 0."); return; }

            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null) { Debug.LogError("Select a GameObject."); return; }

            var activeClip = _accessor.ActiveClip;
            if (activeClip == null) { Debug.LogError("Select an Animation Clip."); return; }

            string clipPath = AssetDatabase.GetAssetPath(activeClip);
            if (string.IsNullOrEmpty(clipPath)) { Debug.LogError("Cannot find asset path for the clip."); return; }

            var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (sourceClip == null) { Debug.LogError($"Failed to load clip from path: {clipPath}"); return; }

            var rootObject = _accessor.ActiveRoot;
            if (rootObject == null) { Debug.LogError("Cannot find animation root GameObject."); return; }

            float loopDurationSecs = sourceClip.length;
            if (loopDurationSecs <= 0) { Debug.LogError("Clip length must be greater than 0."); return; }

            float timeOffset = _isTimeInputMode ? _offsetValue : (_offsetValue / sourceClip.frameRate);
            timeOffset %= loopDurationSecs;
            if (timeOffset < 0) timeOffset += loopDurationSecs;

            Undo.RecordObject(sourceClip, "Apply Loop Animation Offset");
            var bindings = AnimUtil.GetCurveBindings(sourceClip);
            bool anyCurveModified = false;

            string selectedObjectPath = AnimUtil.CalculateTransformPath(
                selectedObject.transform, rootObject.transform);

            foreach (var binding in bindings)
            {
                if (binding.path == selectedObjectPath && IsPropertyTypeMatch(binding.propertyName, propertyType))
                {
                    var curve = AnimUtil.GetEditorCurve(sourceClip, binding);
                    if (curve == null || curve.keys.Length == 0) continue;

                    var newCurve = CreateOffsetCurve(curve, timeOffset, loopDurationSecs);
                    if (newCurve != null)
                    {
                        AnimUtil.SetEditorCurve(sourceClip, binding, newCurve);
                        anyCurveModified = true;
                    }
                }
            }

            if (anyCurveModified)
            {
                Debug.Log($"Loop offset applied successfully: {_offsetValue} " + (_isTimeInputMode ? "s" : "frames"));
                _accessor.ForceRefresh();
            }
            else
            {
                Debug.LogWarning($"No '{propertyType}' curves found for '{selectedObject.name}'.");
            }
        }

        private AnimationCurve CreateOffsetCurve(AnimationCurve originalCurve, float timeOffset, float loopDuration)
        {
            if (originalCurve.keys.Length == 0) return null;

            var offsetKeys = new List<Keyframe>();
            foreach (var originalKey in originalCurve.keys)
            {
                float newTime = (originalKey.time + timeOffset) % loopDuration;
                if (newTime < 0) newTime += loopDuration;
                offsetKeys.Add(new Keyframe(newTime, originalKey.value, originalKey.inTangent, originalKey.outTangent,
                    originalKey.inWeight, originalKey.outWeight) { weightedMode = originalKey.weightedMode });
            }
            offsetKeys.Sort((a, b) => a.time.CompareTo(b.time));

            var finalKeys = new List<Keyframe>();
            const float epsilon = 0.0001f;

            if (offsetKeys.Count > 0)
            {
                finalKeys.Add(offsetKeys[0]);
                for (int i = 1; i < offsetKeys.Count; i++)
                {
                    if (Mathf.Abs(offsetKeys[i].time - offsetKeys[i - 1].time) > epsilon)
                        finalKeys.Add(offsetKeys[i]);
                }
            }

            float originalTimeAt0 = (0 - timeOffset + loopDuration * 100) % loopDuration;
            float valueAt0 = originalCurve.Evaluate(originalTimeAt0);
            float tangentAt0 = CalculateTangent(originalCurve, originalTimeAt0);

            bool hasKeyAtStart = finalKeys.Count > 0 && Mathf.Abs(finalKeys[0].time) < epsilon;
            if (!hasKeyAtStart)
                finalKeys.Insert(0, new Keyframe(0f, valueAt0, tangentAt0, tangentAt0));
            else
            {
                var key = finalKeys[0];
                key.value = valueAt0; key.inTangent = tangentAt0; key.outTangent = tangentAt0;
                finalKeys[0] = key;
            }

            bool hasKeyAtEnd = finalKeys.Count > 0 && Mathf.Abs(finalKeys[finalKeys.Count - 1].time - loopDuration) < epsilon;
            if (!hasKeyAtEnd)
                finalKeys.Add(new Keyframe(loopDuration, valueAt0, tangentAt0, tangentAt0));
            else
            {
                var key = finalKeys[finalKeys.Count - 1];
                key.value = valueAt0; key.inTangent = tangentAt0; key.outTangent = tangentAt0;
                finalKeys[finalKeys.Count - 1] = key;
            }

            var newCurve = new AnimationCurve(finalKeys.ToArray());
            newCurve.preWrapMode = originalCurve.preWrapMode;
            newCurve.postWrapMode = originalCurve.postWrapMode;
            return newCurve;
        }

        private float CalculateTangent(AnimationCurve curve, float time)
        {
            const float deltaTime = 0.0001f;
            float divisor = 2 * deltaTime;
            if (divisor == 0) return 0;
            return (curve.Evaluate(time + deltaTime) - curve.Evaluate(time - deltaTime)) / divisor;
        }

        private bool IsPropertyTypeMatch(string propertyName, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.Position:
                    return propertyName.Contains("Position") || propertyName.Contains("m_AnchoredPosition");
                case PropertyType.Rotation:
                    return propertyName.Contains("Euler") || propertyName.Contains("Rotation") ||
                           propertyName.Contains("localEulerAngles") || propertyName.Contains("localEulerAnglesRaw");
                case PropertyType.Scale:
                    return propertyName.Contains("Scale");
                default:
                    return false;
            }
        }

        public void Dispose() { }
    }
}
