namespace Scaffold.Maps
{
    public class Holder<TValue>
    {
        public Holder(TValue value)
        {
            Value = value;
        }

        public TValue Value { get; set; }
    }
}
