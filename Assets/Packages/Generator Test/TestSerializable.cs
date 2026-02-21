using System;
using UnityEngine;

[SerializableStruct]
public partial class TestSerializable
{
    [Serialized]
    public int value;

    [Serialized(typeof(int))]
    public GameObject target;
}
