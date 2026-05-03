using Scaffold.GraphFlow.M0.Smoke;
using UnityEngine;

namespace Scaffold.GraphFlow.M0.PlayerSmoke
{
    /// <summary>
    /// Assign the baked <see cref="MySmokeGraphAsset"/> from a .gfmsmoke import. Running the scene
    /// proves [SerializeReference] nodes survive a player build (M0 sign-off).
    /// </summary>
    public sealed class M0PlayerSmokeVerifier : MonoBehaviour
    {
        const string PrefsKey = "GraphFlow.M0.LastResult";

        [SerializeField] MySmokeGraphAsset graph = null!;

        void Awake()
        {
            var runner = new MySmokeRunner();
            var controller = new GraphController<MySmokeRunner>(graph);
            controller.Initialize(runner);

            _ = RunAsync(controller, runner);
        }

        static async void RunAsync(GraphController<MySmokeRunner> controller, MySmokeRunner runner)
        {
            await controller.Run(new OnPlay { CardId = 42 });
            PlayerPrefs.SetString(PrefsKey, runner.LastLogMessage);
            PlayerPrefs.Save();
            Debug.Log($"[M0] Player smoke: {PrefsKey}={runner.LastLogMessage}");
#if !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}
