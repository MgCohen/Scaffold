using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    /// <summary>Marker: entry payload — invokable via controller.Run or subscribable via host's bus.</summary>
    public interface IGraphEntry { }

    /// <summary>Marker: command/action-shaped payloads (Mode 1). Stays runner-typed.</summary>
    public interface IGraphAction<TRunner> where TRunner : GraphRunner { }

    /// <summary>Optional: payload executes itself instead of DispatcherBase (Mode 1).</summary>
    public interface IExecutable<TRunner> where TRunner : GraphRunner
    {
        Task Execute(TRunner runner);
    }
}
