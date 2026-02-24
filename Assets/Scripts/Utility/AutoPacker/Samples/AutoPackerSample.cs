using UnityEngine;

namespace Scaffold.AutoPacker.Samples
{
    /// <summary>
    /// Sample usage of the AutoPacker utility.
    /// </summary>
    public class AutoPackerSample : MonoBehaviour
    {
        private void Start()
        {
            var packer = new AutoPacker();
            packer.Pack();
            Debug.Log("AutoPacker sample executed.");
        }
    }
}
