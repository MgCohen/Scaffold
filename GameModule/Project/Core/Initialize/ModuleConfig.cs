/*using GameModule.ActionHistory;
using GameModule.Analytics;
using GameModule.ApiMissions;
using GameModule.Deck;
using GameModule.GameMode;
using GameModule.Inventory;
using GameModule.Mission;
using GameModule.Shop;
using GameModule.Tutorial;
using GameModule.User;
using GameModule.ItemDatabaseModule;
using GameModule.Leaderboard;
using GameModule.SeasonPass;
using GameModule.Quest;
using GameModule.Rest;
using GameModule.RestApi;
using CampaignGameModeModule = GameModule.Campaign.CampaignGameModeModule;
using GameModule.Reward;
using GameModule.Xp;*/

using GameModule.Authentication;
using GameModule.GameModule;
using GameModule.Initialize;
using GameModule.ModuleFetchData;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        IGameApiClient gameApiClient = GameApiClient.Create();
        ModuleServices.GameApiClient = gameApiClient;
        
        config.Dependencies.AddSingleton(gameApiClient);
        PushClient pushClient = PushClient.Create();
        config.Dependencies.AddSingleton(pushClient);

        RegisterScoped<PlayerData>(config);
        RegisterScoped<GameState>(config);
        RegisterScoped<RemoteConfig>(config);
        
        /*RegisterSingleton<CloudCodeRestApi>(config);
        RegisterScoped<UnityAnalyticsApi>(config);*/
        RegisterScoped<AuthenticationModule>(config);
        //RegisterScoped<LeaderboardAdminHelper>(config);
        RegisterScoped<ConfigFetcher>(config);
        /*RegisterModuleScoped<RestApiModule>(config);
        
        //Missions
        RegisterSingleton<RestApiMissions>(config);
        
        //Game
        RegisterModuleScoped<AppSettingsModule>(config);
        RegisterModuleScoped<RarityPayoutModule>(config);
        RegisterModuleScoped<UrlModule>(config);
        //RegisterModuleScoped<NewsModule>(config);
        RegisterModuleScoped<ItemDatabaseModule>(config);

        //GameMode
        RegisterModuleScoped<ActionHistoryModule>(config);
        RegisterModuleScoped<CampaignGameModeModule>(config);
        RegisterModuleScoped<MatchmakingGameModeClientModule>(config);
        RegisterModuleScoped<MatchmakingGameModeServerModule>(config);
        RegisterModuleScoped<PrivateRoomGameModeModule>(config);
        RegisterModuleScoped<AugmentationGameModeModule>(config);
        RegisterModuleScoped<DailyChallengeGameModeModule>(config);
        //User
        RegisterModuleScoped<UserModule>(config);
        RegisterModuleScoped<UserSettingsModule>(config);
        RegisterModuleScoped<UserStatisticModule>(config);
        RegisterModuleScoped<InventoryModule>(config);
        RegisterModuleScoped<EquippedInventoryModule>(config);
        RegisterModuleScoped<DeckModule>(config);
        //Progress
        RegisterModuleScoped<TutorialModule>(config);
        RegisterModuleScoped<MissionModule>(config);
        RegisterModuleScoped<CampaignGameModeModule>(config);
        RegisterModuleScoped<ClientQuestModule>(config);
        RegisterModuleScoped<ServerQuestModule>(config);
        RegisterModuleScoped<SeasonPassModule>(config);
        RegisterModuleScoped<XpModule>(config);
        //Purchase
        RegisterModuleScoped<ShopModule>(config);
        //Leaderboard
        RegisterModuleScoped<LeaderboardModule>(config);
        RegisterModuleScoped<ServerLeaderboardModule>(config);*/

        //RegisterScoped<RewardMultiplier>(config);
        /*
        //RewardMap
        RegisterRewardHandler<RewardHandler>(config);
        RegisterRewardHandler<OptionRewardHandler>(config);
        RegisterRewardHandler<SelectFactionRewardHandler>(config);
        RegisterRewardHandler<MatchRewardHandler>(config);
        RegisterScoped<RewardHandlerResolver>(config);
        //GiveReward
        RegisterGiveRewardHandler<ItemGiveRewardHandler>(config);
        RegisterGiveRewardHandler<LeaderboardN3musGiveRewardHandler>(config);
        RegisterGiveRewardHandler<LeaderboardGiveRewardHandler>(config);
        RegisterGiveRewardHandler<LeaderboardGiveGuildRewardHandler>(config);
        RegisterGiveRewardHandler<BoosterCardGiveRewardHandler>(config);
        RegisterGiveRewardHandler<RandomCardGiveRewardHandler>(config);
        RegisterGiveRewardHandler<RandomItemGiveRewardHandler>(config);
        RegisterGiveRewardHandler<XPGiveRewardHandler>(config);
        RegisterGiveRewardHandler<XpKeyChallengeGiveRewardHandler>(config);
        RegisterGiveRewardHandler<NFTCallFunctionRewadHandler>(config);
        RegisterGiveRewardHandler<NFTDataChipGiveRewardHandler>(config);
        RegisterGiveRewardHandler<NFTItemGiveRewardHandler>(config);
        //RegisterGiveRewardHandler<NFTRandomItemGiveRewardHandler>(config);
        RegisterGiveRewardHandler<UseNFTGiveRewardHandler>(config);
        RegisterScoped<GiveRewardHandlerResolver>(config);*/
    }
    
    private void RegisterSingleton<T>(ICloudCodeConfig config) where T : class
    {
        config.Dependencies.AddSingleton<T>();
    }
    
    private void RegisterScoped<T>(ICloudCodeConfig config) where T : class
    {
        config.Dependencies.AddScoped<T>();
    }
    
    private void RegisterModuleScoped<T>(ICloudCodeConfig config) where T : class, IGameModule
    {
        config.Dependencies.AddScoped<IGameModule, T>();
        RegisterScoped<T>(config);
    }
    /*
    private void RegisterRewardHandler<T>(ICloudCodeConfig config) where T : class, IRewardHandler
    {
        config.Dependencies.AddScoped<IRewardHandler, T>();
        RegisterScoped<T>(config);
    }
    
    private void RegisterGiveRewardHandler<T>(ICloudCodeConfig config) where T : class, IGiveRewardHandler
    {
        config.Dependencies.AddScoped<IGiveRewardHandler, T>();
        RegisterScoped<T>(config);
    }*/
}