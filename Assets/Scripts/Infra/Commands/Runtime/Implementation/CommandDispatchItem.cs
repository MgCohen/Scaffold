namespace Scaffold.Commands
{
    /// <summary>
    /// Queue output item containing dispatch-ready payload and metadata.
    /// </summary>
    internal class CommandDispatchItem
    {
        public CommandDispatchItem(ICommand message, CommandMetadata metadata)
        {
            Message = message;
            Metadata = metadata;
        }

        public ICommand Message { get; }

        public CommandMetadata Metadata { get; }
    }
}
