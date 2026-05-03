using System.Threading.Tasks;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>Entry payload for M0 slice (Mode 1).</summary>
    public sealed class OnPlay : IGraphEntry<MySmokeRunner>
    {
        public int CardId;
    }

    /// <summary>Action payload — self-executing via <see cref="IExecutable{MySmokeRunner}"/>.</summary>
    public sealed class Log : IGraphAction<MySmokeRunner>, IExecutable<MySmokeRunner>
    {
        public string Message = "";

        public Task Execute(MySmokeRunner runner)
        {
            runner.RecordLog(Message);
            return Task.CompletedTask;
        }
    }
}
