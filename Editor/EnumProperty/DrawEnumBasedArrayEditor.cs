using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CodeUtils.EditorUtils
{
    /// <summary>
    /// Draws an array of objects by displaying the name of the linked enum value next to it
    /// </summary>
    [CustomPropertyDrawer(typeof(DrawEnumBasedArrayAttribute))]
    public sealed class DrawEnumBasedArrayEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DrawEnumBasedArrayAttribute enumAttribute = attribute as DrawEnumBasedArrayAttribute;

            //propertyPath returns the actual data, not the array, so we get the index from there
            // https://answers.unity.com/questions/1589226/showing-an-array-with-enum-as-keys-in-the-property.html
            int index = System.Convert.ToInt32(property.propertyPath.Substring(property.propertyPath.IndexOf("[")).Replace("[", "").Replace("]", ""));

            if (index < 0)
                return;

            //change the label
            label.text = System.Enum.GetName(enumAttribute.EnumType, index);

            // getting the potential tooltip linked to the enum value
            TooltipAttribute tooltipAttribute = enumAttribute.EnumType.GetField(label.text).GetCustomAttribute(typeof(TooltipAttribute), true) as TooltipAttribute;
            label.tooltip = tooltipAttribute != null ? tooltipAttribute.tooltip : string.Empty;

            //draw field
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}