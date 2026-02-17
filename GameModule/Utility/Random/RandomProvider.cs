namespace Utility.Random
{
    public class RandomProvider : IAmountProvider
    {
        public int min = 1;
        public int max = 1;

        public RandomProvider(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public int GetAmount()
        {
            return RandomExtensions.GetRandom(min, max);
        }

        public string GetAmountText()
        {
            return $"{min}-{max}";
        }

        public string GetFlatAmount()
        {
            return "";
        }
    }
}