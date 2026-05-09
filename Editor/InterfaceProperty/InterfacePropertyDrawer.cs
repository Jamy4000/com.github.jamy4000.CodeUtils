using UnityEngine;
using UnityEditor;
using System;

namespace CodeUtils.EditorUtils
{
    [CustomPropertyDrawer(typeof(InterfaceAttribute))]
    public class InterfacePropertyDrawer : PropertyDrawer
    {
        private bool? _hasSetCorrectValue = null;
        private const int HELP_BOX_SIZE = 2;

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // getting the attribute implementation
            InterfaceAttribute interfaceAttribute = attribute as InterfaceAttribute;

            // getting all allowed types names to display them in editor
            string allowedTypes = interfaceAttribute.Types[0].Name;
            for (int i = 1; i < interfaceAttribute.Types.Length; i++)
            {
                allowedTypes += ", " + interfaceAttribute.Types[i].Name;
            }

            EditorGUI.BeginChangeCheck();
            // Adding the UnityObejct field to the inspector
            Rect rectObject = new Rect(position.min.x, position.min.y, position.size.x,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.ObjectField(rectObject, property, new GUIContent(allowedTypes));

            // if the correct was set wrongly, we show an error helpBox
            if (_hasSetCorrectValue.HasValue && !_hasSetCorrectValue.Value)
            {
                Rect rectHelpBox = new Rect(position.min.x, position.min.y + EditorGUIUtility.singleLineHeight,
                    position.size.x, EditorGUIUtility.singleLineHeight * HELP_BOX_SIZE);
                EditorGUI.HelpBox(rectHelpBox, "The referenced object needs to be of type " + allowedTypes,
                    MessageType.Error);
            }

            if (!EditorGUI.EndChangeCheck())
                return;

            if (property.objectReferenceValue == null)
            {
                return;
            }
            else if (property.objectReferenceValue is GameObject go)
            {
                _hasSetCorrectValue = false;

                var components = go.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var interfaces = component.GetType().GetInterfaces();
                    for (int i = 0; i < interfaceAttribute.Types.Length; i++)
                    {
                        Type type = interfaceAttribute.Types[i];
                        foreach (var iface in interfaces)
                        {
                            if (iface == type)
                            {
                                property.objectReferenceValue = component;
                                _hasSetCorrectValue = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                var interfaces = property.objectReferenceValue.GetType().GetInterfaces();
                for (int i = 0; i < interfaceAttribute.Types.Length; i++)
                {
                    Type type = interfaceAttribute.Types[i];
                    foreach (var iface in interfaces)
                    {
                        if (iface == type)
                        {
                            _hasSetCorrectValue = true;
                            break;
                        }
                    }
                }
            }

            if (_hasSetCorrectValue.HasValue && !_hasSetCorrectValue.Value)
            {
                property.objectReferenceValue = null;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int totalLines = 1;
            if (_hasSetCorrectValue.HasValue && !_hasSetCorrectValue.Value)
            {
                totalLines += HELP_BOX_SIZE;
            }

            return EditorGUIUtility.singleLineHeight * totalLines +
                   EditorGUIUtility.standardVerticalSpacing * (totalLines - 1);
        }
    }
}