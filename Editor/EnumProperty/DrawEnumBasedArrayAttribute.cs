using System;
using UnityEngine;

namespace CodeUtils.EditorUtils
{
    /// <summary>
    /// Draws an array of objects by displaying the name of the linked enum value next to it
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class DrawEnumBasedArrayAttribute : PropertyAttribute
    {
        public readonly Type EnumType;

        public DrawEnumBasedArrayAttribute(Type enumType)
        {
            this.EnumType = enumType;
        }
    }
}