using System;

namespace LiveOps.Core.DTO.GameApi
{
    /// <summary>
    /// Marks a request type that is routed through the unified <c>GameApi</c> Cloud Code entry point.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UsesGameApiAttribute : Attribute
    {
    }
}
