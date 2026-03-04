using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace GameModule.Authentication
{
    /// <summary>
    /// Manages Unity Cloud authentication and token exchange mechanisms.
    /// </summary>
    public class AuthenticationModule
    {
        private ILogger _logger;
        private readonly HttpClient _httpClient = new HttpClient();
        private string _keyId;
        private string _secretKey;
        private string _statelessToken;
        private string _base64EncodedApiKey;

        /// <summary>
        /// Initializes the authentication system with necessary logging capabilities.
        /// </summary>
        /// <param name="logger">The system logger module.</param>
        public AuthenticationModule(ILogger<AuthenticationModule> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates or returns the Base64 formatted string representing the Service Account keys.
        /// </summary>
        /// <param name="context">The caller context instance.</param>
        /// <param name="gameState">The active configuration storage.</param>
        /// <returns>The encoded string configured securely.</returns>
        /// <exception cref="Exception">Thrown if keys resolve missing representations.</exception>
        public async Task<string> GetBase64EncodedApiKey(IExecutionContext context, GameState gameState)
        {
            if (!string.IsNullOrEmpty(_base64EncodedApiKey))
            {
                return _base64EncodedApiKey;
            }

            if (gameState == null)
            {
                throw new Exception("[AuthenticationModule.GetBase64EncodedApiKey] Empty GameState, cannot fetch keys without it.");
            }

            // Fetch these from RemoteConfig or constants for now.
            if (string.IsNullOrEmpty(_keyId))
            {
                _keyId = gameState != null ? await gameState.GetAdminFunctionsKeyId(context) : null;
                if (string.IsNullOrEmpty(_keyId))
                {
                    throw new Exception("[AuthenticationModule.GetBase64EncodedApiKey] Service Account Keys missing in Cloud Code Configuration.");
                }
            }

            if (string.IsNullOrEmpty(_secretKey))
            {
                _secretKey = gameState != null ? await gameState.GetAdminFunctionsSecretKey(context) : null;
                if (string.IsNullOrEmpty(_secretKey))
                {
                    throw new Exception("[AuthenticationModule.GetBase64EncodedApiKey] Service Account Secret Keys missing in Cloud Code Configuration.");
                }
            }

            return _base64EncodedApiKey = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_keyId}:{_secretKey}"));
        }

        /// <summary>
        /// Orchestrates the token exchange executing a REST call securely fetching Unity tokens.
        /// </summary>
        /// <param name="context">The functional network caller instance.</param>
        /// <param name="gameState">The active environment data component.</param>
        /// <returns>The active stateless string format representation.</returns>
        /// <exception cref="Exception">Thrown matching failed REST transactions.</exception>
        public async Task<string> GetStatelessToken(IExecutionContext context, GameState gameState)
        {
            if (!string.IsNullOrEmpty(_statelessToken))
            {
                return _statelessToken;
            }

            // Docs: https://services.docs.unity.com/auth/v1/#tag/Token-Exchange
            string basicAuth = await GetBase64EncodedApiKey(context, gameState);

            // Note: Adding environmentId is best practice for scope, though projectId is strictly required
            string url = $"https://services.api.unity.com/auth/v1/token-exchange?projectId={context.ProjectId}&environmentId={context.EnvironmentId}";

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

                using (HttpResponseMessage response = await _httpClient.SendAsync(request))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Token Exchange Failed: {responseString}");
                    }

                    TokenExchangeResponse? data = JsonConvert.DeserializeObject<TokenExchangeResponse>(responseString);
                    return _statelessToken = data.accessToken;
                }
            }
        }
    }
}
