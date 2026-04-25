using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace LiveOps.ServerAuth
{

    public sealed class GameStateServerAuth : IServerAuth
    {
        public const string DefaultDatabaseKey = "AccessKey";
        public const string DefaultItemKey = "ServerKey";

        private readonly ILogger<GameStateServerAuth> _logger;

        public GameStateServerAuth(ILogger<GameStateServerAuth> logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsValidForServerAccessAsync(
            IGameState gameState,
            IExecutionContext context,
            string guid,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(guid) || gameState is null)
            {
                return false;
            }

            string stored = await gameState
                .Get<string>(context, DefaultDatabaseKey, DefaultItemKey, string.Empty)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(stored))
            {
                return false;
            }

            byte[] a = Encoding.UTF8.GetBytes(stored);
            byte[] b = Encoding.UTF8.GetBytes(guid);
            if (a.Length != b.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
