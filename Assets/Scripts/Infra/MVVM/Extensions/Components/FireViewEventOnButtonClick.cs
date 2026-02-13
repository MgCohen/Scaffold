using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FireViewEventOnButtonClick :  MonoBehaviour
{

    [SerializeReference, TypeSelection(typeof(ViewEvent))]
    private ViewEvent viewEvent;
    
    private Button button;

    protected void Awake()
    {
        button = GetComponent<Button>();
        
        button.onClick.AddListener(SendEvents);
    }
    private void OnDestroy() {
        if(button!= null)
            button.onClick.RemoveListener(SendEvents);
    }

    private void SendEvents()
    {
        this.viewEvent.Restore();
        ViewEvents.Raise(this, this.viewEvent);
    }
}