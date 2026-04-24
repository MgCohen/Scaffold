using System;

namespace LiveOps.DTO.GameApi
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UsesGameApiAttribute : Attribute
    {
    }
}
