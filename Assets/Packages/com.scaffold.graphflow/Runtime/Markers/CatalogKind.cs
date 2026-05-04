using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// What roles a type plays inside a package's generator-emitted catalog. Flags so a single
    /// type can appear in multiple roles (e.g., a class could be both a <see cref="Command"/>
    /// payload and a <see cref="Return"/> result type).
    /// </summary>
    [Flags]
    public enum CatalogKind
    {
        None    = 0,
        Event   = 1 << 0,
        Return  = 1 << 1,
        Command = 1 << 2,
        Entry   = 1 << 3,
    }
}
