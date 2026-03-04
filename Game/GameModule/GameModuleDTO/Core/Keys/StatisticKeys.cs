namespace GameModuleDTO.Keys
{
    /// <summary>
    /// Defines literal string keys used to identify core player tracking statistics.
    /// </summary>
    public static class StatisticKeys
    {
        /// <summary>Total number of successful matches.</summary>
        public const string Wins = "Wins";
        /// <summary>Total number of defeated matches.</summary>
        public const string Losses = "Losses";
        /// <summary>Total number of tied matches.</summary>
        public const string Draws = "Draws";
        /// <summary>Amount of game-specific rounds or turns performed.</summary>
        public const string Turns = "Turns";
        /// <summary>Current matchmaking ranking rating.</summary>
        public const string Elo = "Elo";
    }
}
