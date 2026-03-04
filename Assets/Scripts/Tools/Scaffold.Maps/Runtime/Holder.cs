namespace Scaffold.Maps
{
    public class Holder<TValue>
    {
        public TValue Value { get; set; }

        public Holder(TValue value)
        {
            Value = value;
        }
    }
}
