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
            var sink = new CollectingLogSink();
            var runner = new MySmokeBuilder(sink).Build(graph);
            RunAsync(runner, sink);
        }

        async void RunAsync(MySmokeRunner runner, CollectingLogSink sink)
        {
            int id = Random.Range(0, 50);
            Debug.Log($"[M0] Player smoke: Id ={id}");
            before.text = id.ToString();
            await runner.Run(new OnPlay { CardId = id });
            var last = sink.Messages.Count > 0 ? sink.Messages[^1] : "";
            PlayerPrefs.SetString(PrefsKey, last);
            PlayerPrefs.Save();
            after.text = last;
            Debug.Log($"[M0] Player smoke: {PrefsKey}={last}");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}
