using System;
using System.Threading;

namespace Scaffold.Entities
{
    /// <summary>
    /// Runtime-unique id for an entity instance (monotone integer per process).
    /// </summary>
    public record InstanceId(int Id)
    {
        public override string ToString()
        {
            return Id.ToString();
        }
    }
}
