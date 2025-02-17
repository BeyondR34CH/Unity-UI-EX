using UnityEngine;
using UnityEngine.UI.EX;

namespace UnityEditor.UI.EX
{
    [CustomEditor(typeof(ScrollList), true)]
    [CanEditMultipleObjects]
    public class ScrollListEditor : Editor
    {
        protected SerializedProperty m_Viewport;
        protected SerializedProperty m_LayoutAxis;
        protected SerializedProperty m_Padding;
        protected SerializedProperty m_Spacing;
        protected SerializedProperty m_ChildAlignment;
        protected SerializedProperty m_ReverseArrangement;
        protected SerializedProperty m_ChildControl;
        protected SerializedProperty m_ChildControlLayout;
        protected SerializedProperty m_ChildScale;
        protected SerializedProperty m_ChildForceExpand;
        protected SerializedProperty m_ChildForceExpandLayout;

        protected virtual void OnEnable()
        {
            m_Viewport = serializedObject.FindProperty("m_Viewport");
            m_LayoutAxis = serializedObject.FindProperty("m_LayoutAxis");
            m_Padding = serializedObject.FindProperty("m_Padding");
            m_Spacing = serializedObject.FindProperty("m_Spacing");
            m_ChildAlignment = serializedObject.FindProperty("m_ChildAlignment");
            m_ReverseArrangement = serializedObject.FindProperty("m_ReverseArrangement");
            m_ChildControl = serializedObject.FindProperty("m_ChildControl");
            m_ChildControlLayout = serializedObject.FindProperty("m_ChildControlLayout");
            m_ChildScale = serializedObject.FindProperty("m_ChildScale");
            m_ChildForceExpand = serializedObject.FindProperty("m_ChildForceExpand");
            m_ChildForceExpandLayout = serializedObject.FindProperty("m_ChildForceExpandLayout");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Viewport, new GUIContent(m_Viewport.objectReferenceValue ? "Viewport" : "Viewport (Def-Parent)"), true);
            EditorGUILayout.PropertyField(m_LayoutAxis, true);
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

        protected void ToggleLeft(Rect position, SerializedProperty property, GUIContent label)
        {
            bool toggle = property.boolValue;
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            toggle = EditorGUI.ToggleLeft(position, label, toggle);
            EditorGUI.indentLevel = oldIndent;
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = property.hasMultipleDifferentValues ? true : !property.boolValue;
            }
            EditorGUI.EndProperty();
        }
    }
}
