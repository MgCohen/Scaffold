namespace Scaffold.Entities
{
    public interface IEntityFrameInputProvider<TInput>
    {
        TInput GetFrameInput();
    }
}
