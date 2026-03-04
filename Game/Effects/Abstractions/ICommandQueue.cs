namespace Scaffold.Effects
{
    public interface ICommandQueue
    {
        void QueueCommand(Command command);
    }
}
