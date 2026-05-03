using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>M0 smoke entry node — wires CardId output + flow edge.</summary>
    public sealed class OnPlayRuntime : RuntimeNode<MySmokeRunner>, IBindsGraphEntryPayload
    {
        public static class Ports
        {
            /// <summary>Synthetic flow port — carries no meaningful data; ordering only.</summary>
            public const int FlowOut = 0xE001_0001;

            public const int CardId = 0x4F2A_8B17;
        }

        [NonSerialized] public int _out_CardId;

        OnPlay? _pendingPayload;

        public void BindGraphEntryPayload(object payload)
        {
            if (payload is OnPlay p)
                _pendingPayload = p;
        }

        public override Connection GetOutputConnection(int portId) => portId switch
        {
            Ports.FlowOut => new Connection<int>(this, Ports.FlowOut, static () => 0),
            Ports.CardId => new Connection<int>(this, Ports.CardId, () => _out_CardId),
            _ => throw new ArgumentOutOfRangeException(nameof(portId)),
        };

        public override void BindInput(int portId, Connection connection) =>
            throw new ArgumentOutOfRangeException(nameof(portId));

        public override ValueTask Execute(MySmokeRunner runner)
        {
            if (_pendingPayload != null)
                _out_CardId = _pendingPayload.CardId;
            return default;
        }
    }

    /// <summary>
    /// Hand-written dispatcher runtime node — Mode 1 self-execute path from ExecPlan v2.
    /// </summary>
    public sealed class LogDispatcherRuntime : RuntimeNode<MySmokeRunner>
    {
        public static class Ports
        {
            public const int FlowIn = 0xE002_0001;
            public const int Message = 0x77E1_3C20;
        }

        public string Message = "";

        [NonSerialized] public Connection<int>? _flowIn;

        [NonSerialized] public Connection<string>? _in_Message;

        public override Connection GetOutputConnection(int portId) =>
            throw new ArgumentOutOfRangeException(nameof(portId));

        public override void BindInput(int portId, Connection connection)
        {
            switch (portId)
            {
                case Ports.FlowIn:
                    _flowIn = (Connection<int>)connection;
                    return;
                case Ports.Message:
                    _in_Message = (Connection<string>)connection;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(portId));
            }
        }

        public override async ValueTask Execute(MySmokeRunner runner)
        {
            var payload = new Log
            {
                Message = _in_Message != null ? _in_Message.Read() : Message,
            };
            await payload.Execute(runner).ConfigureAwait(false);
        }
    }
}
