namespace Scaffold.Entities
{
    /// <summary>
    /// Ordered entity view behavior; first <see cref="TryAcceptControl"/> wins for the frame.
    /// </summary>
    public interface IEntityBehavior<TData, TInput> where TData : Entity
    {
        bool TryAcceptControl(TData data, in TInput input);

        void Execute(TData data, in TInput input, float deltaTime);

        void OnQuit(TData data);
    }
}
