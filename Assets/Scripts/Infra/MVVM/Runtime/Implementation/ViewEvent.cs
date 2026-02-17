using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

[Serializable]
public abstract class ViewEvent
{
    public ViewEvent(PointerEventData pointer)
    {
        PointerData = pointer;
    }


    public ViewEvent()
    {

    }

    //Event Base Variables
    public PointerEventData PointerData {  get; private set; }

    //Event Path
    public Transform Source { get; private set; }
    public Transform Current { get; private set; }
    public List<Transform> History { get; private set; } = new List<Transform>();

    //event State
    public Transform Consumer { get; private set; }
    public bool IsConsumed { get; private set; }

    //Event Settings
    public int maxRange { get; private set; } = -1;


    public void Consume()
    {
        IsConsumed = true;
        Consumer = Current;

        if(History.LastOrDefault() != Current)
        {
            History.Add(Current);
        }
        Current = null;
    }

    public void Restore()
    {
        IsConsumed = false;
        Current = null;
        Consumer = null;
        History.Clear();
    }

    public void LogNext(Transform next)
    {
        if(Current != null)
        {
            History.Add(Current);
        }
        Current = next;
    }
}
