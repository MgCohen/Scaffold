using System;

namespace Scaffold.MVVM.Extensions.DefaultViews
{
    public class ButtonOption
    {
        public ButtonOption(string key, Action callback)
        {
            Key = key;
            Callback = callback;
        }

        public string Key { get; private set; }
        public Action Callback { get; private set; }
    }
}
