using System;

using Scaffold.States;

namespace Scaffold.Entities
{
    public record InstanceId(int Id) : IReference
    {
        public override string ToString()
        {
            return Id.ToString();
        }
    }
}
