using System.Threading.Tasks;
using Scaffold.AppFlow;
using Scaffold.SceneFlow;
using UnityEngine;

namespace Scaffold.AppFlow.Samples
{
    /// <summary> sample: drives <see cref="LoadingView"/> from <see cref="AppFlowRoot.Progress"/> (weighted bar).
    /// Put <see cref="SampleAppFlowRoot"/> earlier in execution order so Configure runs first. </summary>
    public sealed class SampleLoadingScreen : MonoBehaviour
    {
        [SerializeField]
        private AppFlowRoot appFlowRoot;

        [SerializeField]
        private LoadingView loadingView;

        private void Awake()
        {
            if (appFlowRoot == null || loadingView == null)
            {
                Debug.LogWarning("[SampleLoadingScreen] Assign AppFlowRoot and LoadingView in the inspector.");
                return;
            }

            loadingView.Show();
            ApplyProgress(appFlowRoot.Progress.Current);
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
            ApplyProgress(session);
        }

        private void ApplyProgress(AppFlowSession session)
        {
            float total = Mathf.Max(1, session.TotalLayers);
            float current = session.Current.HasValue ? session.Current.Value.SubProgress : 0f;
            float normalized = Mathf.Clamp01((session.CompletedLayers + current) / total);
            loadingView.SetProgress(normalized);
        }

        private async Task ObserveStartupAsync()
        {
            if (appFlowRoot == null || loadingView == null)
            {
                return;
            }

            AppFlowOutcome outcome = await appFlowRoot.Progress.WhenSessionCompleted();
            loadingView.SetProgress(1f);
            loadingView.Hide();
            Debug.Log($"[SampleLoadingScreen] startup outcome succeeded={outcome.Succeeded}");
        }
    }
}
