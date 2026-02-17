namespace Utility.Random
{
    public interface IAmountProvider
    {
        int GetAmount();
        string GetAmountText();
        string GetFlatAmount();
    }
}