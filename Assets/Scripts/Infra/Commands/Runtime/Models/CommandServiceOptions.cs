namespace Scaffold.Commands
{
    /// <summary>
    /// Service configuration for local source identity and sequence behavior.
    /// </summary>
    public class CommandServiceOptions
    {
        public CommandServiceOptions(CommandSourceType localSourceType, ulong localSourceId, long firstOutgoingSequence, long firstIncomingSequence, bool bootstrapIncomingFromFirstMessage)
        {
            LocalSourceType = localSourceType;
            LocalSourceId = localSourceId;
            FirstOutgoingSequence = firstOutgoingSequence;
            FirstIncomingSequence = firstIncomingSequence;
            BootstrapIncomingFromFirstMessage = bootstrapIncomingFromFirstMessage;
        }

        public CommandSourceType LocalSourceType { get; }

        public ulong LocalSourceId { get; }

        public long FirstOutgoingSequence { get; }

        public long FirstIncomingSequence { get; }

        public bool BootstrapIncomingFromFirstMessage { get; }

        public static CommandServiceOptions CreateDefault()
        {
            var options = new CommandServiceOptions(CommandSourceType.Local, 0, 1, 1, true);
            return options;
        }
    }
}
