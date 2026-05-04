using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Entry payload for M0 slice (Mode 1).</summary>
    [GraphEntry(FlowOutPortId = unchecked((int)0xF001_0001u))]
    public sealed class OnPlay : IGraphEntry
    {
        [GraphPort(Id = unchecked((int)0x4F2A_8B17u))]
        public int CardId;
    }

    /// <summary>Action payload — self-executing via <see cref="IExecutable{MySmokeRunner}"/>.</summary>
    public sealed class Log : IGraphAction<MySmokeRunner>, IExecutable<MySmokeRunner>
    {
        [GraphPort(Id = unchecked((int)0x77E1_3C20u))]
        public string Message = "";

        public Task Execute(MySmokeRunner runner)
        {
            runner.RecordLog(Message);
            return Task.CompletedTask;
        }
    }
}
