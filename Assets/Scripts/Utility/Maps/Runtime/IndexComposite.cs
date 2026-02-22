namespace Scaffold.Maps
{
    public record Index<TPrimary, TSecondary>(TPrimary primary, TSecondary secondary) : Index<TPrimary>(primary);
}
