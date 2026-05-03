using System;
using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>M0 smoke entry node — CardId data output + flow continuation.</summary>
    public sealed class OnPlayRuntime : EntryRuntimeNode<OnPlay, MySmokeRunner>
    {
        public static class Ports
        {
            public const int FlowOut = unchecked((int)0xF001_0001u);
            public const int CardId = unchecked((int)0x4F2A_8B17u);
        }

        [NonSerialized] public int _out_CardId;

        public override Connection GetOutputConnection(int portId) => portId switch
        {
            Ports.CardId => new Connection<int>(this, Ports.CardId, () => _out_CardId),
            _ => throw new ArgumentOutOfRangeException(nameof(portId)),
        };

        public override void BindInput(int portId, Connection connection) =>
            throw new ArgumentOutOfRangeException(nameof(portId));

        public override Task<FlowContinuation> Execute(MySmokeRunner runner)
        {
            if (Payload != null)
                _out_CardId = Payload.CardId;
            return Task.FromResult(FlowContinuation.Next(Ports.FlowOut));
        }
    }

    /// <summary>Mode 1 — self-executing Log via <see cref="IExecutable{MySmokeRunner}"/>.</summary>
    public sealed class LogDispatcherRuntime : RuntimeNode<MySmokeRunner>
    {
        /// <summary>
        /// Fake flow input slot — Log has no BindInput for flow; executor matches this id on <see cref="FlowEdge"/>.
        /// </summary>
        public const int FlowInSlotId = 0;

        public static class Ports
        {
            public const int Message = unchecked((int)0x77E1_3C20u);
        }

        /// <summary>Baked embedded default when Message port is unwired.</summary>
        public string Message = "";

        [NonSerialized] public Connection<string>? _in_Message;

        public override Connection GetOutputConnection(int portId) =>
            throw new ArgumentOutOfRangeException(nameof(portId));

        public override void BindInput(int portId, Connection connection)
        {
            if (portId != Ports.Message)
                throw new ArgumentOutOfRangeException(nameof(portId));
            _in_Message = (Connection<string>)connection;
        }

        public override async Task<FlowContinuation> Execute(MySmokeRunner runner)
        {
            var payload = new Log
            {
                Message = _in_Message != null ? _in_Message.Read() : Message,
            };
            await payload.Execute(runner).ConfigureAwait(false);
            return FlowContinuation.Stop;
        }
    }
}
