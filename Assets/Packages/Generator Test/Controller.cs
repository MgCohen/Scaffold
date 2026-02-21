using PlasticPipe.PlasticProtocol.Messages;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class Controller: MonoBehaviour
{
    public void Setup()
    {
        //FastBufferWriter writer = new FastBufferWriter();
        //NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("test", 0, )
        TestSerializable.Serializable serializable = default;
        Setup(serializable);
    }

    public void Setup<T>(T thing) where T: unmanaged, ISerializedStruct
    {
        //ForceNetworkSerializeByMemcpy<T> thing2 = new ForceNetworkSerializeByMemcpy<T>(thing);
        //int size = Marshal.SizeOf(thing);
        //var writer = new FastBufferWriter(size, Allocator.Temp);
        //writer.WriteValueSafe(thing);
    }
}