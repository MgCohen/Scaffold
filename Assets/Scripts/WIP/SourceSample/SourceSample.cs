using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class SourceSampleAttribute: Attribute
{

}

public class SourceSerializeAttribute: Attribute
{

}

[SourceSample]
public partial class SourceSample
{
    [SourceSerialize] public int myValue;
    [SourceSerialize] public float myValue2;
    
    public bool myValue3;
}

public interface ICustomSerializable
{
    public ICustomSerialized Serialize();
}

public interface ICustomSerialized
{
    public Type SerializableType { get; }
}

public partial class SourceSample: ICustomSerializable
{
    public SourceSample(SourceSample.Serializable serializable)
    {
        myValue = serializable.myValue;
        myValue2 = serializable.myValue2;
    }

    public ICustomSerialized Serialize()
    {
        return new Serializable(this);
    }

    public struct Serializable: INetworkSerializeByMemcpy, ICustomSerialized
    {
        public Type SerializableType => typeof(SourceSample);

        public Serializable(SourceSample sample)
        {
            myValue = sample.myValue;
            myValue2 = sample.myValue2;
        }

        public int myValue;
        public float myValue2;
    }
}
