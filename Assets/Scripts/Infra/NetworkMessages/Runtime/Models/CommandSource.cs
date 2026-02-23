using System;

namespace Scaffold.NetworkMessages
{
    /// <summary>
    /// Sender identity used as the ordering stream key.
    /// </summary>
    public readonly struct CommandSource : IEquatable<CommandSource>
    {
        public CommandSource(CommandSourceType type, ulong id)
        {
            Type = type;
            Id = id;
        }

        public CommandSourceType Type { get; }

        public ulong Id { get; }

        public bool Equals(CommandSource other)
        {
            return Type == other.Type && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            var hasValue = obj is CommandSource source;
            if (hasValue)
            {
                return Equals(source);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = (int)Type;
            hash = (hash * 397) ^ Id.GetHashCode();
            return hash;
        }
    }
}
