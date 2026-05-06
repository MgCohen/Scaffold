using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Entry payload for M0 slice (Mode 1).</summary>
    public sealed class OnPlay : IGraphEntry
    {
        [GraphPort]
        public int CardId;
    }

    /// <summary>Action payload — self-executing via <see cref="IExecutable{MySmokeRunner}"/>.</summary>
    public sealed class Log : IGraphAction<MySmokeRunner>, IExecutable<MySmokeRunner>
    {
        [GraphPort]
        public string Message = "";

        public Task Execute(MySmokeRunner runner)
        {
            runner.LogSink.Record(Message);
            return Task.CompletedTask;
        }
    }
}
