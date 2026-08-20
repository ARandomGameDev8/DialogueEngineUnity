#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// When a framework serializes DialoguePlayActionNode as a property, this keeps
/// Save State hidden unless Interruptible is enabled, as required by the node UI.
/// </summary>
[CustomPropertyDrawer(typeof(DialoguePlayActionNode))]
public sealed class DialoguePlayActionNodeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
        SerializedProperty interruptible = property.FindPropertyRelative("Interruptible");
        int rows = interruptible != null && interruptible.boolValue ? 4 : 3;
        return rows * EditorGUIUtility.singleLineHeight +
               (rows - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float h = EditorGUIUtility.singleLineHeight;
        float gap = EditorGUIUtility.standardVerticalSpacing;
        Rect row = new Rect(position.x, position.y, position.width, h);

        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            row.y += h + gap;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("DslPath"));
            row.y += h + gap;
            SerializedProperty interruptible = property.FindPropertyRelative("Interruptible");
            EditorGUI.PropertyField(row, interruptible);
            if (interruptible.boolValue)
            {
                row.y += h + gap;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("SaveState"));
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
#endif
