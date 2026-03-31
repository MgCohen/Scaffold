namespace Scaffold.SceneFlow.Contracts
{
    /// <summary>
    /// Toggles Bootstrap shell rendering/audio when additive content scenes own the world camera and listener.
    /// </summary>
    public interface ISceneFlowBootstrapShell
    {
        void SetAdditiveContentActive(bool active);
    }
}
