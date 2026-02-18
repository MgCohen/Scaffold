using UnityEngine;

namespace Scaffold.MVVM
{
    public class ClickViewEvent : ViewEvent
    {
    public ClickViewEvent(string id)
    {
        this.id = id;
    }

    public ClickViewEvent()
    {

    }

    public string Id => id;
    [SerializeField] private string id;
    }
}

