using System;
using System.Threading;

namespace Scaffold.Entities
{
    public record InstanceId(int Id)
    {
        public override string ToString()
        {
            return Id.ToString();
        }
    }
}
