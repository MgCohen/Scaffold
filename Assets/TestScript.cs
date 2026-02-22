using CommunityToolkit.Mvvm.ComponentModel;
using NaughtyAttributes;
using Sample.States;
using Scaffold.Maps;
using Scaffold.MVVM.Binding;
using Scaffold.States;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using UnityEngine;
using VContainer.Unity;

public class TestScript : MonoBehaviour
{
    public SampleBuilder sample;

    [Button]
    private void Start()
    {
    }
}