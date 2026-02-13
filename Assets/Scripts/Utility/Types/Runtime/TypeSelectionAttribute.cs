using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

public class TypeSelectionAttribute : PropertyAttribute
{
    [NotNull] public Type baseType;

    public TypeSelectionAttribute([NotNull] Type baseType)
    {
        this.baseType = baseType;
    }
}