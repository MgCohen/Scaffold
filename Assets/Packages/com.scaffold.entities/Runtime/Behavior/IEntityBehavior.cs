namespace Scaffold.Entities
{
    public interface IEntityBehavior<TData, TInput> where TData : EntityComponent
    {
        bool TryAcceptControl(TData data, in TInput input);

        void Execute(TData data, in TInput input, float deltaTime);

        void OnQuit(TData data);
    }
}
