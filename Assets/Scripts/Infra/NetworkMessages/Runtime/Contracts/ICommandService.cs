using System;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Non-static command API for sending commands and subscribing to typed command streams.
    /// </summary>
    public interface ICommandService
    {
        void Send<TCommand>(TCommand command) where TCommand : ICommand;

        IDisposable Subscribe<TCommand>(Action<TCommand, CommandMetadata> handler) where TCommand : ICommand;

        IDisposable Subscribe<TCommand>(Action<TCommand, CommandMetadata> handler, Predicate<CommandMetadata> metadataFilter) where TCommand : ICommand;
    }
}
