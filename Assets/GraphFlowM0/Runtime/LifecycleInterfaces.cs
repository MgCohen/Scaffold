namespace Scaffold.GraphFlow.M0
{
    /// <summary>Optional lifecycle hook — populated when M2/M3 need init/listeners.</summary>
    public interface IInitializableNode<TRunner> where TRunner : GraphRunner
    {
        void Initialize(TRunner runner);
    }

    /// <summary>Optional lifecycle hook — pipeline listener refresh (M3).</summary>
    public interface IListenerNode<TRunner> where TRunner : GraphRunner
    {
        void RefreshSubscriptions(TRunner runner);
    }
}
