namespace Scaffold.Entities
{
    /// <summary>
    /// Supplies per-frame input (or context) for an <see cref="EntityBehaviorRunner{TData,TInput}"/>.
    /// </summary>
    public interface IEntityFrameInputProvider<TInput>
    {
        TInput GetFrameInput();
    }
}
