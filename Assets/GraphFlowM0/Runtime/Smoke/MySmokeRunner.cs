using System.Threading;

namespace Scaffold.GraphFlow.M0.Smoke
{
    /// <summary>M0 smoke runner — holds services used by the vertical slice.</summary>
    public sealed class MySmokeRunner : GraphRunner
    {
        /// <summary>Test hook: last message passed to Log payload execution.</summary>
        public string LastLogMessage { get; private set; }

        public void RecordLog(string message) => LastLogMessage = message;

        public MySmokeRunner()
        {
            CancellationToken = default;
        }

        public MySmokeRunner(CancellationToken token)
        {
            CancellationToken = token;
        }
    }
}
