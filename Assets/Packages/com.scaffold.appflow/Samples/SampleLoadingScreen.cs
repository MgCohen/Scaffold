using System.Threading.Tasks;
using Scaffold.AppFlow;
using UnityEngine;

namespace Scaffold.AppFlow.Samples
{
    /// <summary>
    /// Sample: subscribe to <see cref="AppFlowRoot.Progress"/> before startup completes.
    /// Put <see cref="SampleAppFlowRoot"/> earlier in execution order so Configure runs first.
    /// </summary>
    public sealed class SampleLoadingScreen : MonoBehaviour
    {
        [SerializeField]
        private AppFlowRoot appFlowRoot;

        private void Awake()
        {
            if (appFlowRoot == null)
            {
                Debug.LogWarning("[SampleLoadingScreen] Assign AppFlowRoot in the inspector.");
                return;
            }

            appFlowRoot.Progress.Changed += OnProgress;
            _ = ObserveStartupAsync();
        }

        private void OnDestroy()
        {
            if (appFlowRoot != null)
            {
                appFlowRoot.Progress.Changed -= OnProgress;
            }
        }

        private void OnProgress(AppFlowSession session)
        {
            Debug.Log($"[SampleLoadingScreen] progress session='{session.Name}' complete={session.IsComplete} layers={session.Entries.Count}");
        }

        private async Task ObserveStartupAsync()
        {
            if (appFlowRoot == null)
            {
                return;
            }

            AppFlowOutcome outcome = await appFlowRoot.Progress.WhenSessionCompleted();
            Debug.Log($"[SampleLoadingScreen] startup outcome succeeded={outcome.Succeeded}");
        }
    }
}
