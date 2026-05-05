using Scaffold.GraphFlow.M0.Smoke;
using TMPro;
using UnityEngine;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.PlayerSmoke
{
    public sealed class M0PlayerSmokeVerifier : MonoBehaviour
    {
        const string PrefsKey = "GraphFlow.M0.LastResult";

        [SerializeField] MySmokeGraphAsset graph = null!;
        [SerializeField] TextMeshProUGUI before;
        [SerializeField] TextMeshProUGUI after;

        void Awake()
        {
            var runner = new MySmokeBuilder().Build(graph);
            RunAsync(runner);
        }

        async void RunAsync(MySmokeRunner runner)
        {
            int id = Random.Range(0, 50);
            Debug.Log($"[M0] Player smoke: Id ={id}");
            before.text = id.ToString();
            await runner.Run(new OnPlay { CardId = id });
            PlayerPrefs.SetString(PrefsKey, runner.LastLogMessage);
            PlayerPrefs.Save();
            after.text = runner.LastLogMessage;
            Debug.Log($"[M0] Player smoke: {PrefsKey}={runner.LastLogMessage}");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}
