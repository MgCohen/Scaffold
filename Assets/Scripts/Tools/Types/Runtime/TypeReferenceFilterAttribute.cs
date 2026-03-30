using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Types
{
    [AttributeUsage(AttributeTargets.Field)]
    public class TypeReferenceFilterAttribute : Attribute
    {
        public TypeReferenceFilterAttribute(Type typeFilter)
        {
            if (typeFilter is null)
            {
                throw new ArgumentNullException(nameof(typeFilter));
            }
            TypeFilter = typeFilter;
        }

        public Type TypeFilter { get; }
    }
}

