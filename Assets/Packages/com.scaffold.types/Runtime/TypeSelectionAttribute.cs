using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Scaffold.Types
{
    public class TypeSelectionAttribute : PropertyAttribute
    {
        public TypeSelectionAttribute([NotNull] Type baseType)
        {
            if (baseType is null)
            {
                throw new ArgumentNullException(nameof(baseType));
            }
            this.BaseType = baseType;
        }

        [NotNull] public Type BaseType;
    }
}

