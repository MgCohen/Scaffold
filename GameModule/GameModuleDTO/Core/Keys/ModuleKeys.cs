namespace GameModuleDTO.Keys
{
    public static class ModuleKeys
    {
        public const string GameState = "GameState";
        
        public const string Inventory = "Inventory";
        public const string EquippedInventory = "Inventory_Equipped";
        public const string FallbackInventory = "Inventory_Fallback";
        
        public const string AppSettings = "AppSettings";
        public const string RemoteSettingsVersion = "RemoteSettingsVersion";
        
        public const string Url = "Url";
        public const string News = "News";
        
        public const string ItemDatabaseDefault = "Database_Item_Default";
        public const string ItemDatabase = "Database_Item";
        public const string LootDatabase = "Database_Loot";
        
        public const string User = "User";
        public const string UserSettings = "User_Settings";
        public const string UserStatistic = "User_Statistic";
        
        public const string Leaderboard = "Leaderboard";
        public const string LeaderboardIdGuild = "Guild";
        public const string DailyReward = "DailyReward";
        public const string Assignment = "Assigment";
        public const string Deck = "Deck";
        public const string Battle = "Battle";
        public const string Shop = "Shop";
        public const string RarityPayout = "RarityPayout";
        public const string ActionHistory = "ActionHistory";
        
        public const string IdempotencyGate = "IdempotencyGate";
        public const string IdempotencyGateDefaultTag = "Default";

        public static string DailyChallengeGameMode = "GameMode_DailyChallenge";
        public static string MatchmakingGameMode = "GameMode_Matchmaking";
        public const string CampaignGameMode = "GameMode_Campaign";
        public static string PrivateRoomGameMode = "GameMode_PrivateRoom";
        public static string AugmentationGameMode = "GameMode_Augmentation";
        
        public const string Tutorial = "Tutorial";
        public const string Mission = "Mission";
        public const string Quest = "Quest";
        public const string SeasonPass = "SeasonPass";
        public const string Xp = "Xp";
        
        public static string RestApi = "RestApi";

        #region GameState
        //Keys
        public const string FirebaseBearerToken = "FirebaseBearerToken";
        public const string UnityToken = "UnityToken";
        public const string AdminFunctionsKey = "AdminFunctionsKey";
        public const string AdminFunctionsSecretKey = "AdminFunctionsSecretKey";
        //Id
        public const string Auth = "Auth";
        public const string Guild = "Guild";
        public const string EmailId = "EmailId";
        public const string WalletId = "WalletId";
        public const string UsernameId = "UsernameId";
        #endregion
    }
}