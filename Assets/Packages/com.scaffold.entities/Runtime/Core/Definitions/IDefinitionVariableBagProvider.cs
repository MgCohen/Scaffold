namespace Scaffold.Entities
{
    internal interface IDefinitionVariableBagProvider
    {
        VariableBag Bag { get; }

        void RebuildLookup();
    }
}
