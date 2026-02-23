using System;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Carries command payload and metadata together through transport and ordering layers.
    /// </summary>
    public class CommandEnvelope
    {
        public CommandEnvelope(ICommand command, CommandMetadata metadata)
        {
            var hasCommand = command != null;
            if (!hasCommand)
            {
                throw new ArgumentNullException(nameof(command));
            }
            var hasMetadata = metadata != null;
            if (!hasMetadata)
            {
                throw new ArgumentNullException(nameof(metadata));
            }
            Command = command;
            Metadata = metadata;
        }

        public ICommand Command { get; }

        public CommandMetadata Metadata { get; }
    }
}
