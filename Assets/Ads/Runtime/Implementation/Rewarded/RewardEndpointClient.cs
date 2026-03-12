using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Ads.Endpoint
{
    public class RewardEndpointClient : IRewardEndpointClient
    {
        public async Task<bool> CallRewardEndpointAsync(string unityUserId, string rewardAdId, string customEndpointUrl)
        {
            try
            {
                Debug.Log($"Calling raw reward endpoint with user: {unityUserId}, adId: {rewardAdId}");

                RewardRequestPayload payload = new RewardRequestPayload
                {
                    unityUserId = unityUserId,
                    rewardAdId = rewardAdId
                };

                string jsonPayload = JsonUtility.ToJson(payload);

                using (UnityWebRequest www = new UnityWebRequest(customEndpointUrl, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");

                    var operation = www.SendWebRequest();

                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogError($"Failed to call raw reward endpoint: {www.error} - {www.downloadHandler.text}");
                        return false;
                    }

                    Debug.Log($"Endpoint call result: {www.downloadHandler.text}");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception calling reward endpoint: {e.Message}");
                return false;
            }
        }
    }
}
