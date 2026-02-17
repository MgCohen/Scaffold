using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Keys;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameModule
{
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