using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Keys;
/*
using GameModuleDTO.ActionHistory;
using GameModuleDTO.Deck;
using GameModuleDTO.GameMode.Campaign.Progress;
using GameModuleDTO.GameMode.Config;
using GameModuleDTO.GameMode.Progress;
using GameModuleDTO.Idempotency;
using GameModuleDTO.Inventory;
using GameModuleDTO.ItemDatabase;
using GameModuleDTO.Leaderboard;
using GameModuleDTO.Mission;
using GameModuleDTO.Quest;
using GameModuleDTO.RestApi;
using GameModuleDTO.SeasonPass;
using GameModuleDTO.Settings;
using GameModuleDTO.ShopData;
using GameModuleDTO.Tutorial;
using GameModuleDTO.Url;
using GameModuleDTO.User;
using GameModuleDTO.Xp;*/
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace GameModule.GameModule
{
    //using ItemDatabase = ItemDatabase;
    
    public static class GameModuleExtensions
    {
        public static T GetTData<T>(this IGameModuleData gameModuleData)
        {
            if (gameModuleData is T result)
            {
                return result;
            }
            return default;
        }

        /*
        #region PlayerData
        public static async Task<ActionHistoryData> GetActionHistoryData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.ActionHistory, new ActionHistoryData());
        }

        public static async Task<ActionHistoryData> GetOrSetActionHistoryData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.ActionHistory, new ActionHistoryData());
        }

        public static async Task SetActionHistoryData(this PlayerData playerData, IExecutionContext context, ActionHistoryData actionHistoryData)
        {
            await playerData.Set(context, ModuleKeys.ActionHistory, actionHistoryData);
        }

        public static async Task<UserData> GetUserData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.User, new UserData());
        }

        public static async Task SetUserData(this PlayerData playerData, IExecutionContext context, UserData userData)
        {
            await playerData.Set(context, ModuleKeys.User, userData);
        }

        public static async Task<UserSettingsData> GetOrSetUserSettingsData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.UserSettings, new UserSettingsData());
        }

        public static async Task SetUserSettingsData(this PlayerData playerData, IExecutionContext context, UserSettingsData userSettingsData)
        {
            await playerData.Set(context, ModuleKeys.UserSettings, userSettingsData);
        }

        public static async Task<UserStatisticData> GetOrSetUserStatistics(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.UserStatistic, new UserStatisticData());
        }

        public static async Task<DeckListData> GetOrSetDeckData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.Deck, new DeckListData());
        }

        public static async Task<DeckListData> GetDeckData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.Deck, new DeckListData());
        }

        public static async Task<IdempotencyGateData> GetOrSetIdempotencyGate(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.IdempotencyGate, new IdempotencyGateData());
        }

        public static async Task<IdempotencyGateData> GetIdempotencyGate(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.IdempotencyGate, new IdempotencyGateData());
        }

        public static string GetIdempotencyGateWriteLock(this PlayerData playerData)
        {
            return playerData.GetWriteLock(ModuleKeys.IdempotencyGate);
        }

        public static async Task SetIdempotencyGate(this PlayerData playerData, IExecutionContext context, IdempotencyGateData idempotencyGateData, bool useWriteLock)
        {
            await playerData.Set(context, ModuleKeys.IdempotencyGate, idempotencyGateData, useWriteLock);
        }

        public static async Task SetTutorial(this PlayerData playerData, IExecutionContext context, TutorialData tutorialData)
        {
            await playerData.Set(context, ModuleKeys.Tutorial, tutorialData);
        }

        public static async Task SetMission(this PlayerData playerData, IExecutionContext context, MissionData missionData)
        {
            await playerData.Set(context, ModuleKeys.Mission, missionData);
        }

        public static async Task SetQuest(this PlayerData playerData, IExecutionContext context, QuestData questData)
        {
            await playerData.Set(context, ModuleKeys.Quest, questData);
        }

        public static async Task SetCampaign(this PlayerData playerData, IExecutionContext context, CampaignData campaignData)
        {
            await playerData.Set(context, ModuleKeys.CampaignGameMode, campaignData);
        }

        public static async Task<TutorialData> GetTutorialData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.Tutorial, new TutorialData());
        }

        public static async Task<MissionData> GetMissionData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.Mission, new MissionData());
        }

        public static async Task<XpModuleData> GetXpData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.Xp, new XpModuleData());
        }

        public static async Task<XpModuleData> GetOrSetXpData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.Xp, new XpModuleData());
        }

        public static async Task<QuestData> GetQuestData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.Quest, new QuestData());
        }

        public static async Task<CampaignData> GetCampaignData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.CampaignGameMode, new CampaignData());
        }

        public static async Task<InventoryData> GetInventory(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.Inventory, new InventoryData());
        }

        public static async Task<InventoryData> GetOrSetInventory(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.Inventory, new InventoryData());
        }

        public static async Task<EquippedInventoryData> GetEquippedInventory(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.EquippedInventory, new EquippedInventoryData());
        }

        public static async Task SetInventory(this PlayerData playerData, IExecutionContext context, InventoryData inventoryData)
        {
            await playerData.Set(context, ModuleKeys.Inventory, inventoryData);
        }

        public static async Task<PrivateRoomData> GetPrivateRoomGameModeData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.PrivateRoomGameMode, new PrivateRoomData());
        }

        public static async Task<DailyChallengeData> GetDailyChallengeGameMode(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.DailyChallengeGameMode, new DailyChallengeData());
        }

        public static async Task<MatchmakingData> GetMatchmakingGameModeData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.MatchmakingGameMode, new MatchmakingData());
        }

        public static async Task SetMatchmakingGameModeData(this PlayerData playerData, IExecutionContext context, MatchmakingData matchmakingData)
        {
            await playerData.Set(context, ModuleKeys.MatchmakingGameMode, matchmakingData);
        }

        public static async Task<CampaignData> GetCampaignGameMode(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.CampaignGameMode, new CampaignData());
        }

        public static async Task<PrivateRoomData> GetPrivateRoomGameMode(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.PrivateRoomGameMode, new PrivateRoomData());
        }

        public static async Task<AugmentationData> GetAgumentationGameMode(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.AugmentationGameMode, new AugmentationData());
        }

        public static async Task<SeasonPassData> GetSeasonPassData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.Get(context, ModuleKeys.SeasonPass, new SeasonPassData());
        }

        public static async Task<SeasonPassData> GetOrSetSeasonPassData(this PlayerData playerData, IExecutionContext context)
        {
            return await playerData.GetOrSet(context, ModuleKeys.SeasonPass, new SeasonPassData());
        }

        public static async Task SetSeasonPassData(this PlayerData playerData, IExecutionContext context, SeasonPassData seasonPassData)
        {
            await playerData.Set(context, ModuleKeys.SeasonPass, seasonPassData);
        }
        #endregion

        #region  Remote
        public static async Task<ShopData> GetShop(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.Shop, new ShopData());
        }

        public static async Task<AppSettingsData> GetAppSettings(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.AppSettings, new AppSettingsData());
        }

        public static async Task<LeaderboardRegistryData> GetLeaderboardConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.Leaderboard, new LeaderboardRegistryData());
        }

        public static async Task<TutorialConfigData> GetTutorialConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.Tutorial, new TutorialConfigData());
        }

        public static async Task<RarityPayoutData> GetRarityPayout(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.RarityPayout, new RarityPayoutData());
        }

        public static async Task<MissionConfigData> GetMissionConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.Mission, new MissionConfigData());
        }

        public static async Task<QuestConfigData> GetQuestConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.Quest, new QuestConfigData());
        }

        public static async Task<(LootDatabase, ItemDatabase)> GetFullLootDatabase(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            ItemDatabase itemDatabase = await remoteConfig.GetItemDatabase(context);
            LootDatabase lootDatabase = await remoteConfig.Get(context, ModuleKeys.LootDatabase, new LootDatabase());
            lootDatabase.Initialize(itemDatabase);
            return (lootDatabase, itemDatabase);
        }

        public static async Task<LootDatabase> GetLootDatabase(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            ItemDatabase itemDatabase = await remoteConfig.GetItemDatabase(context);
            LootDatabase lootDatabase = await remoteConfig.Get(context, ModuleKeys.LootDatabase, new LootDatabase());
            lootDatabase.Initialize(itemDatabase);
            return lootDatabase;
        }

        public static async Task<ItemDatabase> GetItemDatabase(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            ItemDatabase itemDatabase = await remoteConfig.Get(context, ModuleKeys.ItemDatabase, new ItemDatabase());
            itemDatabase.Initialize();
            return itemDatabase;
        }

        public static async Task<DefaultItemDatabase> GetDefaultItemDatabase(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            DefaultItemDatabase defaultItemDatabase = await remoteConfig.Get(context, ModuleKeys.ItemDatabaseDefault, new DefaultItemDatabase());
            defaultItemDatabase.Initialize();
            return defaultItemDatabase;
        }

        #region GameMode
        public static async Task<UrlData> GetUrl(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.Url, new UrlData());
        }

        public static async Task<RestApiData> GetRestApi(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.RestApi, new RestApiData());
        }

        public static async Task<DailyChallengeConfigData> GetDailyChallengeGameModeConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.DailyChallengeGameMode, new DailyChallengeConfigData());
        }

        public static async Task<MatchmakingConfigData> GetMatchmakingGameModeConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.MatchmakingGameMode, new MatchmakingConfigData());
        }

        public static async Task<CampaignConfigData> GetCampaignGameModeConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.CampaignGameMode, new CampaignConfigData());
        }

        public static async Task<PrivateRoomConfigData> GetPrivateRoomGameModeConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.PrivateRoomGameMode, new PrivateRoomConfigData());
        }

        public static async Task<AugmentationConfigData> GetAgumentationGameModeConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.AugmentationGameMode, new AugmentationConfigData());
        }
        #endregion

        public static async Task<RemoteSettingsVersionData> GetRemoteSettingData(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.RemoteSettingsVersion, new RemoteSettingsVersionData());
        }

        public static async Task<RemoteInventoryData> GetFallbackInventoryData(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.FallbackInventory, new RemoteInventoryData());
        }

        public static async Task<SeasonPassModuleConfig> GetSeasonPassModuleConfig(this RemoteConfig remoteConfig, IExecutionContext context)
        {
            return await remoteConfig.Get(context, ModuleKeys.SeasonPass, new SeasonPassModuleConfig());
        }
        #endregion*/
        
        #region GameData
        public static async Task<string> GetUnityAuth(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.UnityToken);
        }
        
        public static async Task<string> GetFirebaseAuth(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.FirebaseBearerToken);
        }

        public static async Task<string> GetAdminFunctionsSecretKey(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.AdminFunctionsSecretKey);
        }

        public static async Task<string> GetAdminFunctionsKeyID(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.Auth, ModuleKeys.AdminFunctionsKey);
        }

        public static async Task<string> GetEmailId(this GameState gameState, IExecutionContext context, string key)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.EmailId, key);
        }
        
        public static async Task SetEmailId(this GameState gameState, IExecutionContext context, string email, string playerId)
        {
            await gameState.Set(context, ModuleKeys.EmailId, email, playerId);
        }
        
        public static async Task<string> GetWalletId(this GameState gameState, IExecutionContext context, string key)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.WalletId, key);
        }
        
        public static async Task SetWalletId(this GameState gameState, IExecutionContext context, string walletAddress, string playerId)
        {
            await gameState.Set(context, ModuleKeys.WalletId, walletAddress, playerId);
        }
        
        public static async Task<string> GetUsername(this GameState gameState, IExecutionContext context, string key)
        {
            return await gameState.GetAllGameValue<string>(context, ModuleKeys.UsernameId, key);
        }
        
        public static async Task SetUsername(this GameState gameState, IExecutionContext context, string username, string playerId)
        {
            await gameState.Set(context, ModuleKeys.UsernameId, username, playerId);
        }
        
        public static async Task<Dictionary<string, string>> GetAllWalletValues(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValues<string>(context, ModuleKeys.WalletId);
        }

        public static async Task UnregisterWallet(this GameState gameState, IExecutionContext context, string walletAddress)
        {
            if (string.IsNullOrEmpty(walletAddress))
            {
                return;
            }

            await gameState.Delete(context, ModuleKeys.WalletId, walletAddress);
        }

        #endregion

        #region Guild
        /// <summary>
        /// Gets all guilds and members id from Cloud Save.
        /// </summary>
        public static async Task<Dictionary<string, List<string>>> GetAllGuildsAndMembers(this GameState gameState, IExecutionContext context)
        {
            return await gameState.GetAllGameValues<List<string>>(context, ModuleKeys.Guild);
        }
        
        /// <summary>
        /// Gets the all members id for a specific guild.
        /// </summary>
        public static async Task<List<string>> GetGuildMembers(this GameState gameState, IExecutionContext context, string guildId)
        {
            if (string.IsNullOrEmpty(guildId))
            {
                return new List<string>();
            }

            // Get the list from the "Guild" Custom ID, using the guildId as the Key
            List<string>? members = await gameState.GetAllGameValue<List<string>>(context, ModuleKeys.Guild, guildId);
            return members ?? new List<string>();
        }

        /// <summary>
        /// Adds a player to the guild list in Cloud Save.
        /// </summary>
        public static async Task AddGuildMember(this GameState gameState, IExecutionContext context, string guildId, string playerId)
        {
            if (string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(playerId))
            {
                return;
            }

            List<string> members = await gameState.GetGuildMembers(context, guildId);

            if (!members.Contains(playerId))
            {
                members.Add(playerId);

                await gameState.Set(context, ModuleKeys.Guild, guildId, members);
            }
        }

        /// <summary>
        /// Removes a player from the guild list in Cloud Save.
        /// </summary>
        public static async Task RemoveGuildMember(this GameState gameState, IExecutionContext context, string guildId, string playerId)
        {
            if (string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(playerId))
            {
                return;
            }

            List<string> members = await gameState.GetGuildMembers(context, guildId);

            if (members.Contains(playerId))
            {
                members.Remove(playerId);
                await gameState.Set(context, ModuleKeys.Guild, guildId, members);
            }
        }

        #endregion
    }
}