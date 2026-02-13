using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ViewEventButton : Button
{
    [SerializeReference, TypeSelection(typeof(ViewEvent))]
    private ViewEvent viewEvent;


    protected override void Start()
    {
        base.Start();
        this.onClick.AddListener(this.SendEvents);
    }

    private void SendEvents()
    {
        this.viewEvent.Restore();
        ViewEvents.Raise(this, this.viewEvent);
    }

    public override void OnPointerClick(PointerEventData eventData) {
        base.OnPointerClick(eventData);
    }
    
    public override void OnPointerEnter(PointerEventData eventData) {
        base.OnPointerEnter(eventData);
    }
    public override void OnPointerDown(PointerEventData eventData) {
        base.OnPointerDown(eventData);
    }
}
