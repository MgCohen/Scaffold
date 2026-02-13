using Scaffold.MVVM;
using System;
using UnityEngine;
using UnityEngine.UI;

public class NavigateViewButton: MonoBehaviour
{
    [SerializeField] private NavigateViewEvent navigateEvent;
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(Navigate);
    }

    private void Navigate()
    {
        ViewEvents.Raise(this, navigateEvent);
    }
}