using System;
using System.Collections.Generic;
using Scaffold.MVVM;
using UnityEngine;

namespace Scaffold.MVVM.Extensions.DefaultViews
{
    public class GenericPopupViewController : ViewModel
    {
        public Sprite DecorationImage { get; private set; }
        public string Content { get; private set; }
        public string Title { get; private set; }
        public bool ShowClose { get; private set; }
        public IEnumerable<ButtonOption> Options { get; private set; }

        public GenericPopupViewController(string title, string content, bool showClose, params ButtonOption[] options)
        {
            this.Title = title;
            this.Content = content;
            this.ShowClose = showClose;
            this.Options = options;
        }
    }
}
