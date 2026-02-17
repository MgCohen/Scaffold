namespace Utility.Random
{
    public class FlatProvider : IAmountProvider
    {
        public int amount = 1;

        public FlatProvider(int amount)
        {
            this.amount = amount;
        }

        public int GetAmount()
        {
            return amount;
        }

        public string GetAmountText()
        {
            return amount.ToString();
        }

        public string GetFlatAmount()
        {
            return amount <= 1 ? "" : amount.ToString();
        }
    }
}