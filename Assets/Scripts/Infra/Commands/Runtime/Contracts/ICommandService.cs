using System;

namespace Scaffold.Commands
{
    /// <summary>
    /// Public command API for send and subscribe use cases.
    /// </summary>
    public interface ICommandService
    {
        void Send<TMessage>(TMessage message) where TMessage : class, ICommand;

        /// <summary>
        /// Subscribes to a type and receives derived messages assignable to that type.
        /// </summary>
        IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class, ICommand;

        /// <summary>
        /// Subscribes to a type and receives derived messages assignable to that type.
        /// </summary>
        IDisposable Subscribe<TMessage>(Action<TMessage, CommandMetadata> handler) where TMessage : class, ICommand;

        /// <summary>
        /// Subscribes to every command message regardless of concrete type.
        /// </summary>
        IDisposable SubscribeAny(Action<ICommand> handler);

        /// <summary>
        /// Subscribes to every command message regardless of concrete type.
        /// </summary>
        IDisposable SubscribeAny(Action<ICommand, CommandMetadata> handler);
    }
}
