using System;
using System.Collections.Generic;

namespace Scaffold.Types
{
    public interface IDependencyExtractor
    {
        IEnumerable<Type> GetConstructorDependencies(Type type);
    }
}
