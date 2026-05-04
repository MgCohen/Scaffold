#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Scaffold.States.Samples.CardGame
{
    public sealed record CardDefId(int Value);

    public sealed record CardDef(CardDefId Id, string Name, int Cost, int Atk, int Hp);

    public sealed class CardCatalog
    {
        private readonly Dictionary<CardDefId, CardDef> byId;

        public CardCatalog(IEnumerable<CardDef> defs)
        {
            byId = defs.ToDictionary(d => d.Id);
        }

        public CardDef Get(CardDefId id) => byId[id];

        public bool TryGet(CardDefId id, out CardDef def) => byId.TryGetValue(id, out def!);
    }
}
