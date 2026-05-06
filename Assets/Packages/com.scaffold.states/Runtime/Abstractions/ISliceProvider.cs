#nullable enable
using System.Collections.Generic;

namespace Scaffold.States
{
    public interface ISliceProvider
    {
        IEnumerable<State> ProvideInitialSlices();
    }
}
