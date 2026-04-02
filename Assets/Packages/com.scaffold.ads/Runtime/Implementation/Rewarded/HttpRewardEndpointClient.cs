using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Scaffold.Ads
{
    public class HttpRewardEndpointClient : IRewardEndpointClient
    {
        private readonly string endpointUrl;

        public HttpRewardEndpointClient(string endpointUrl)
        {
            this.endpointUrl = endpointUrl;
        }

        public async Task<bool> CallRewardEndpointAsync(string unityUserId, string placementId, string rewardAdId)
        {
            try
            {
                Debug.Log($"Calling raw reward endpoint with user: {unityUserId}, placement: {placementId}, adId: {rewardAdId}");
                string jsonPayload = CreatePayloadJson(unityUserId, rewardAdId);
                return await SendRewardRequestAsync(jsonPayload);
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception calling reward endpoint: {e.Message}");
                return false;
            }
        }

        private string CreatePayloadJson(string unityUserId, string rewardAdId)
        {
            RewardRequestPayload payload = new RewardRequestPayload
            {
                unityUserId = unityUserId,
                rewardAdId = rewardAdId
            };
            return JsonUtility.ToJson(payload);
        }

        private async Task<bool> SendRewardRequestAsync(string jsonPayload)
        {
            using (UnityWebRequest www = new UnityWebRequest(endpointUrl, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                var operation = www.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"Raw reward endpoint fail: {www.error}");
                    return false;
                }

                return true;
            }
        }
    }
}
