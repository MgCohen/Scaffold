using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Scaffold.Types
{
    public class TypeSelectionAttribute : PropertyAttribute
    {
        [NotNull] public Type BaseType;

        public TypeSelectionAttribute([NotNull] Type baseType)
        {
            this.BaseType = baseType;
        }
    }
}
