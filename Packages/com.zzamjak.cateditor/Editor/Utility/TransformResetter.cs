using System;
using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    // ========== Transform (일반) ==========
    [CustomEditor(typeof(Transform))]
    public class TransformResetter : Editor
    {
        private Transform _transform;

        private void OnEnable()
        {
            _transform = (Transform)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawFieldWithResetButton("Local Position", () => _transform.localPosition = Vector3.zero);
            DrawFieldWithResetButton("Local Rotation", () => _transform.localRotation = Quaternion.identity);
            DrawFieldWithResetButton("Local Scale", () => _transform.localScale = Vector3.one);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFieldWithResetButton(string label, Action resetAction)
        {
            EditorGUILayout.BeginHorizontal();

            if (label == "Local Position")
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalPosition"), new GUIContent("Position"));
            else if (label == "Local Rotation")
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalRotation"), new GUIContent("Rotation"));
            else if (label == "Local Scale")
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_LocalScale"), new GUIContent("Scale"));

            if (GUILayout.Button("R", GUILayout.Width(30)))
            {
                Undo.RecordObject(_transform, $"{label} Reset");
                resetAction.Invoke();
                EditorUtility.SetDirty(_transform);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // ========== RectTransform (UI) — 기본 앵커/레이아웃 유지 + Pos 0 / Rot 0 / Scale 0 버튼 ==========
    [CustomEditor(typeof(RectTransform))]
    [CanEditMultipleObjects]
    public class RectTransformResetter : Editor
    {
        private Editor _defaultEditor;

        private void OnEnable()
        {
            Type editorType = Type.GetType("UnityEditor.RectTransformEditor, UnityEditor");
            if (editorType != null)
                _defaultEditor = CreateEditor(targets, editorType);
        }

        private void OnDisable()
        {
            if (_defaultEditor != null)
            {
                DestroyImmediate(_defaultEditor);
                _defaultEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            DrawResetButtons();
            EditorGUILayout.Space(4f);

            if (_defaultEditor != null)
                _defaultEditor.OnInspectorGUI();
            else
                DrawDefaultInspector();
        }

        private void DrawResetButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Pos 0", GUILayout.MinWidth(50)))
            {
                Undo.RecordObjects(targets, "RectTransform Position Reset");
                foreach (var t in targets)
                {
                    var rt = (RectTransform)t;
                    rt.anchoredPosition = Vector2.zero;
                    Vector3 localPos = rt.localPosition;
                    rt.localPosition = new Vector3(localPos.x, localPos.y, 0f);
                    EditorUtility.SetDirty(rt);
                }
            }

            if (GUILayout.Button("Rot 0", GUILayout.MinWidth(50)))
            {
                Undo.RecordObjects(targets, "RectTransform Rotation Reset");
                foreach (var t in targets)
                {
                    var rt = (RectTransform)t;
                    rt.localRotation = Quaternion.identity;
                    EditorUtility.SetDirty(rt);
                }
            }

            if (GUILayout.Button("Scale 0", GUILayout.MinWidth(50)))
            {
                Undo.RecordObjects(targets, "RectTransform Scale Reset");
                foreach (var t in targets)
                {
                    var rt = (RectTransform)t;
                    rt.localScale = Vector3.one;
                    EditorUtility.SetDirty(rt);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
