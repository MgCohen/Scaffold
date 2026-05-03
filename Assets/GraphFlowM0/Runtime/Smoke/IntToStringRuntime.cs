using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Pure data node — converts int to string via lazy <see cref="Connection{T}"/> reads.</summary>
    public sealed class IntToStringRuntime : RuntimeNode<MySmokeRunner>
    {
        public static class Ports
        {
            public const int InValue = 0xA001_0001;
            public const int OutString = 0xA001_0002;
        }

        [NonSerialized] Connection<int>? _inValue;

        public override Connection GetOutputConnection(int portId) => portId switch
        {
            Ports.OutString => new Connection<string>(
                this,
                Ports.OutString,
                () => (_inValue != null ? _inValue.Read() : 0).ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(portId)),
        };

        public override void BindInput(int portId, Connection connection)
        {
            switch (portId)
            {
                case Ports.InValue:
                    _inValue = (Connection<int>)connection;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(portId));
            }
        }

        public override ValueTask Execute(MySmokeRunner runner) => default;
    }
}
