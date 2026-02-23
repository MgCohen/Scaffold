namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Configuration for command service sequencing and sender identity.
    /// </summary>
    public class CommandServiceOptions
    {
        public CommandServiceOptions(CommandSource localSource, long firstOutgoingSequence, long firstIncomingSequence, bool bootstrapIncomingFromFirstMessage)
        {
            LocalSource = localSource;
            FirstOutgoingSequence = firstOutgoingSequence;
            FirstIncomingSequence = firstIncomingSequence;
            BootstrapIncomingFromFirstMessage = bootstrapIncomingFromFirstMessage;
        }

        public CommandSource LocalSource { get; }

        public long FirstOutgoingSequence { get; }

        public long FirstIncomingSequence { get; }

        public bool BootstrapIncomingFromFirstMessage { get; }

        public static CommandServiceOptions CreateDefault()
        {
            var localSource = new CommandSource(CommandSourceType.Local, 0);
            var options = new CommandServiceOptions(localSource, 1, 1, true);
            return options;
        }
    }
}
