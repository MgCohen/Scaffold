using System;

namespace Scaffold.Maps
{
    public class Holder<TValue>
    {
        public Holder(TValue value)
        {
            EnsureValue(value);
            Value = value;
        }

        public TValue Value { get; set; }

        private void EnsureValue(TValue value)
        {
        }
    }
}

