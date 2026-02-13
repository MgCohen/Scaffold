using VContainer.Unity;
using Scaffold.Navigation;

namespace Sample.States
{
    public class SampleGameManager : IInitializable
    {
        public SampleGameManager(INavigation navigation)
        {
            this.navigation = navigation;
        }

        private INavigation navigation;

        public void Initialize()
        {
            SampleViewModel vm = new SampleViewModel("haha");
            navigation.Open(vm);
        }
    }
}


//private void Start()
//{
//    Match match = BuildMatch();

//    PlayerState p1 = new PlayerState()
//    {
//        Variables = match.Players[0].Variables,
//    };
//    PlayerCardsState pc1 = new PlayerCardsState()
//    {
//        CardLookUp = match.Players[0].Cards.Cards.ToDictionary(c => c, c => new Zone())
//    };

//    PlayerState p2 = new PlayerState()
//    {
//        Variables = match.Players[1].Variables,
//    };
//    PlayerCardsState pc2 = new PlayerCardsState()
//    {
//        CardLookUp = match.Players[1].Cards.Cards.ToDictionary(c => c, c => new Zone())
//    };

//    TurnState turnState = new TurnState()
//    {
//        CurrentTurn = 0,
//        ActivePlayer = match.Players[0],
//        PriorityPlayer = match.Players[0],

//        CurrentPhase = match.Phases[0],
//        CurrentStep = match.Phases[0],
//    };

//    StoreBuilder builder = new StoreBuilder();

//    Store store = builder.BuildSlice(match.Players[0], p1)
//                         .BuildSlice(match.Players[0], pc1)
//                         .BuildSlice(match.Players[1], p2)
//                         .BuildSlice(match.Players[1], pc2)
//                         .BuildSlice(match, turnState)
//                         .Build();

//    PlayerState pState = store.Get<PlayerState>(match.Players[0]);
//    Debug.Log(pState.Variables.Count);
//    PlayerCardsState pcState = store.Get<PlayerCardsState>(match.Players[0]);
//    Debug.Log(pcState.CardLookUp.First().Key.Id);
//    store.Subscribe<PlayerState>(match.Players[0], (r, s) => Debug.Log("State changed, Variable count: " + s.Variables.Count));
//    store.Execute(match.Players[0], new PlayerVariablesMutator());
//}

//private Match BuildMatch()
//{
//    Player player1 = new Player()
//    {
//        Variables = new Dictionary<string, int>() { { "Hp", 10 } },
//        Cards = new PlayerCards() { Cards = new List<Card>() { new Card(0.ToString()), new Card(1.ToString()) } }
//    };

//    Player player2 = new Player()
//    {
//        Variables = new Dictionary<string, int>() { { "Hp", 10 } },
//        Cards = new PlayerCards() { Cards = new List<Card>() { new Card(2.ToString()), new Card(3.ToString()) } }
//    };

//    TurnPhase upKeep = new TurnPhase();
//    TurnPhase battlePhase = new TurnPhase();
//    TurnPhase endPhase = new TurnPhase();

//    Match match = new Match()
//    {
//        Players = new List<Player>() { player1, player2 },
//        Phases = new List<TurnPhase> { upKeep, battlePhase, endPhase }
//    };
//    return match;
//}