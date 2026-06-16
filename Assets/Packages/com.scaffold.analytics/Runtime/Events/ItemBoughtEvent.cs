namespace Scaffold.Analytics
{
    public class ItemBoughtEvent : AnalyticsEvent
    {
        public ItemBoughtEvent(string itemName) : base("boughtItem")
        {
            SetParameter("itemName", itemName);
        }
    }
}
