using Scaffold.SceneFlow.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scaffold.SceneFlow
{
    public sealed class SceneFlowBootstrapShell : MonoBehaviour, ISceneFlowBootstrapShell
    {
        [SerializeField] private Camera bootstrapCamera;
        [SerializeField] private AudioListener bootstrapAudioListener;
        [SerializeField] private EventSystem bootstrapEventSystem;

        public void SetAdditiveContentActive(bool active)
        {
            if (bootstrapCamera != null)
            {
                bootstrapCamera.enabled = !active;
            }

            if (bootstrapAudioListener != null)
            {
                bootstrapAudioListener.enabled = !active;
            }

            if (bootstrapEventSystem != null)
            {
                bootstrapEventSystem.enabled = !active;
            }
        }
    }
}
