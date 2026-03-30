using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Scaffold.MVVM.BaseEvents
{
    public class ClickViewEvent : ViewEvent
    {
        public ClickViewEvent(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new System.ArgumentException("Id cannot be null or whitespace.", nameof(id));
            }
            this.id = id;
        }

        public ClickViewEvent()
        {

        }

        public string Id => id;
        [SerializeField] private string id;
    }
}




