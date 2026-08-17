using System;
using UnityEngine;
using UnityEditor;

namespace CAT.Utility
{
    // ========== Transform (일반) ==========
    [CustomEditor(typeof(Transform))]
    [CanEditMultipleObjects]
    public class TransformResetter : Editor
    {
        // FindProperty와 GUIContent 생성은 인스펙터가 그려질 때마다 발생하면 안 되므로 OnEnable에서 1회만 준비한다.
        private static readonly GUIContent PositionLabel = new GUIContent("Position");
        private static readonly GUIContent RotationLabel = new GUIContent("Rotation");
        private static readonly GUIContent ScaleLabel = new GUIContent("Scale");
        private static readonly GUIContent ResetLabel = new GUIContent("R", "기본값으로 되돌리기");
        private static readonly GUILayoutOption[] ResetButtonWidth = { GUILayout.Width(30f) };

        private SerializedProperty _position;
        private SerializedProperty _rotation;
        private SerializedProperty _scale;

        private void OnEnable()
        {
            _position = serializedObject.FindProperty("m_LocalPosition");
            _rotation = serializedObject.FindProperty("m_LocalRotation");
            _scale = serializedObject.FindProperty("m_LocalScale");
        }

        private void OnDisable()
        {
            _position = null;
            _rotation = null;
            _scale = null;
        }

        public override void OnInspectorGUI()
        {
            if (_position == null) return;

            serializedObject.Update();

            DrawFieldWithResetButton(_position, PositionLabel, ResetKind.Position);
            DrawFieldWithResetButton(_rotation, RotationLabel, ResetKind.Rotation);
            DrawFieldWithResetButton(_scale, ScaleLabel, ResetKind.Scale);

            serializedObject.ApplyModifiedProperties();
        }

        private enum ResetKind { Position, Rotation, Scale }

        private void DrawFieldWithResetButton(SerializedProperty property, GUIContent label, ResetKind kind)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(property, label);

            if (GUILayout.Button(ResetLabel, ResetButtonWidth))
            {
                Undo.RecordObjects(targets, $"{label.text} Reset");
                foreach (var t in targets)
                {
                    if (!(t is Transform transform)) continue;

                    switch (kind)
                    {
                        case ResetKind.Position: transform.localPosition = Vector3.zero; break;
                        case ResetKind.Rotation: transform.localRotation = Quaternion.identity; break;
                        case ResetKind.Scale: transform.localScale = Vector3.one; break;
                    }

                    EditorUtility.SetDirty(transform);
                }

                serializedObject.Update();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // ========== RectTransform (UI) — 기본 앵커/레이아웃 유지 + Pos 0 / Rot 0 / Scale 0 버튼 ==========
    [CustomEditor(typeof(RectTransform))]
    [CanEditMultipleObjects]
    public class RectTransformResetter : Editor
    {
        // Type.GetType은 문자열 어셈블리 탐색이라 선택할 때마다 호출하지 않도록 1회만 해석한다.
        private static readonly Type DefaultEditorType = Type.GetType("UnityEditor.RectTransformEditor, UnityEditor");
        private static readonly GUILayoutOption[] ButtonWidth = { GUILayout.MinWidth(50f) };

        private Editor _defaultEditor;

        private void OnEnable()
        {
            if (DefaultEditorType != null)
                _defaultEditor = CreateEditor(targets, DefaultEditorType);
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

            if (GUILayout.Button("Pos 0", ButtonWidth))
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

            if (GUILayout.Button("Rot 0", ButtonWidth))
            {
                Undo.RecordObjects(targets, "RectTransform Rotation Reset");
                foreach (var t in targets)
                {
                    var rt = (RectTransform)t;
                    rt.localRotation = Quaternion.identity;
                    EditorUtility.SetDirty(rt);
                }
            }

            if (GUILayout.Button("Scale 0", ButtonWidth))
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
