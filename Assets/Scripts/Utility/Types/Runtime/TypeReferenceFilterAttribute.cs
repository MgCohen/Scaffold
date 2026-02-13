using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class TypeReferenceFilterAttribute : Attribute
{
    public TypeReferenceFilterAttribute(Type typeFilter)
    {
        TypeFilter = typeFilter;
    }

    public Type TypeFilter { get; }
}
