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
    public class AuthenticationModule
    {
        private ILogger logger;
        private readonly HttpClient httpClient = new HttpClient();
        private string keyId;
        private string secretKey;
        private string statelessToken;
        private string base64EncodedApiKey;

        public AuthenticationModule(ILogger<AuthenticationModule> logger)
        {
            this.logger = logger;
        }

        public async Task<string> GetBase64EncodedApiKey(IExecutionContext context, GameState gameState)
        {
            if (!string.IsNullOrEmpty(base64EncodedApiKey))
            {
                return base64EncodedApiKey;
            }
            if (gameState == null)
            {
                throw new Exception("[AuthenticationModule.GetBase64EncodedApiKey] Empty GameState, cannot fetch keys without it.");
            }
            
            // Fetch these from RemoteConfig or constants for now.
            if (string.IsNullOrEmpty(keyId))
            {
                keyId = gameState != null ? await gameState.GetAdminFunctionsKeyID(context) : null;
                if (string.IsNullOrEmpty(keyId))
                {
                    throw new Exception("[AuthenticationModule.GetBase64EncodedApiKey] Service Account Keys missing in Cloud Code Configuration.");
                }
            }

            if (string.IsNullOrEmpty(secretKey))
            {
                secretKey = gameState != null ? await gameState.GetAdminFunctionsSecretKey(context) : null;
                if (string.IsNullOrEmpty(secretKey))
                {
                    throw new Exception("[AuthenticationModule.GetBase64EncodedApiKey] Service Account Secret Keys missing in Cloud Code Configuration.");
                }
            }
            
            return base64EncodedApiKey = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{keyId}:{secretKey}"));
        }

        public async Task<string> GetStatelessToken(IExecutionContext context, GameState gameState)
        {
            if (!string.IsNullOrEmpty(statelessToken))
            {
                return statelessToken;
            }

            // Docs: https://services.docs.unity.com/auth/v1/#tag/Token-Exchange
            string basicAuth = await GetBase64EncodedApiKey(context, gameState);
            
            // Note: Adding environmentId is best practice for scope, though projectId is strictly required
            string url = $"https://services.api.unity.com/auth/v1/token-exchange?projectId={context.ProjectId}&environmentId={context.EnvironmentId}";

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

                using (HttpResponseMessage response = await httpClient.SendAsync(request))
                {
                    string responseString = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Token Exchange Failed: {responseString}");
                    }

                    TokenExchangeResponse? data = JsonConvert.DeserializeObject<TokenExchangeResponse>(responseString);
                    return statelessToken = data.accessToken;
                }
            }
        }
    }
}