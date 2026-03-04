using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Sample.States
{
    public class SampleMonoInstaller : MonoBehaviour, IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            throw new System.NotImplementedException();
        }
    }
}
