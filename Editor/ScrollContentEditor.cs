using UnityEngine;
using UnityEngine.UI.EX;

namespace UnityEditor.UI.EX
{
    [CustomEditor(typeof(ScrollContent), true)]
    [CanEditMultipleObjects]
    public class ScrollContentEditor : ScrollListEditor
    {
        protected SerializedProperty m_Alignment;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_Alignment = serializedObject.FindProperty("m_Alignment");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Viewport, new GUIContent(m_Viewport.objectReferenceValue ? "Viewport" : "Viewport (Def-Parent)"), true);
            EditorGUILayout.PropertyField(m_LayoutAxis, true);
            EditorGUILayout.PropertyField(m_Alignment, true);
            EditorGUILayout.PropertyField(m_Padding, true);
            EditorGUILayout.PropertyField(m_Spacing, true);
            EditorGUILayout.PropertyField(m_ChildAlignment, true);
            EditorGUILayout.PropertyField(m_ReverseArrangement, true);

            Rect rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, -1, EditorGUIUtility.TrTextContent("Control Child Size"));
            rect.width = Mathf.Max(60, (rect.width - 4) / 3);
            EditorGUIUtility.labelWidth = 60;
            ToggleLeft(rect, m_ChildControl, EditorGUIUtility.TrTextContent(m_LayoutAxis.enumValueIndex == 1 ? "Width" : "Height"));
            rect.x += rect.width + 2;
            ToggleLeft(rect, m_ChildControlLayout, EditorGUIUtility.TrTextContent(m_LayoutAxis.enumValueIndex == 0 ? "Width" : "Height"));
            EditorGUIUtility.labelWidth = 0;

            rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, -1, EditorGUIUtility.TrTextContent("Use Child Scale"));
            rect.width = Mathf.Max(60, (rect.width - 4) / 3);
            EditorGUIUtility.labelWidth = 60;
            ToggleLeft(rect, m_ChildScale, EditorGUIUtility.TrTextContent(m_LayoutAxis.enumValueIndex == 1 ? "Width" : "Height"));
            rect.x += rect.width + 2 + 17;
            EditorGUI.LabelField(rect, m_LayoutAxis.enumValueIndex == 0 ? "Width" : "Height");
            EditorGUIUtility.labelWidth = 0;

            rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, -1, EditorGUIUtility.TrTextContent("Child Force Expand"));
            rect.width = Mathf.Max(60, (rect.width - 4) / 3);
            EditorGUIUtility.labelWidth = 60;
            ToggleLeft(rect, m_ChildForceExpand, EditorGUIUtility.TrTextContent(m_LayoutAxis.enumValueIndex == 1 ? "Width" : "Height"));
            rect.x += rect.width + 2;
            ToggleLeft(rect, m_ChildForceExpandLayout, EditorGUIUtility.TrTextContent(m_LayoutAxis.enumValueIndex == 0 ? "Width" : "Height"));
            EditorGUIUtility.labelWidth = 0;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
