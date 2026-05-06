#nullable enable
using System;

namespace Scaffold.States
{
    public interface ICatalogged
    {
        Guid Key { get; }
    }
}
