using System;
using System.Collections.Generic;

namespace Scaffold.Types.Contracts
{
    public interface IDependencyExtractor
    {
        IEnumerable<Type> GetConstructorDependencies(Type type);
    }
}



