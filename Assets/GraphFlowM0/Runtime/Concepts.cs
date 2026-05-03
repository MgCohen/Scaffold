namespace Scaffold.GraphFlow.M0
{
    /// <summary>Marker: payload types that belong to this runner package (Mode 1).</summary>
    public interface IGraphEntry<TRunner> where TRunner : GraphRunner { }

    /// <summary>Marker: command/action-shaped payloads (Mode 1).</summary>
    public interface IGraphAction<TRunner> where TRunner : GraphRunner { }

    /// <summary>Optional: payload executes itself instead of DispatcherBase (Mode 1).</summary>
    public interface IExecutable<TRunner> where TRunner : GraphRunner
    {
        System.Threading.Tasks.ValueTask Execute(TRunner runner);
    }
}
