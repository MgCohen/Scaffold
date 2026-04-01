using System;
using System.Collections.Generic;
using VContainer;

namespace Scaffold.Scope.Contracts
{
    /// <summary>
    /// Computes topological levels (parallel-safe groups) for IStartupOrderParticipant types. Ordering only; does not run initialization.
    /// </summary>
    public interface IStartupOrderGroupProvider
    {
        IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(IObjectResolver resolver);

        IReadOnlyList<IReadOnlyList<Type>> GetOrderedGroups(IReadOnlyList<IStartupOrderParticipant> participants, IObjectResolver resolver);
    }
}
