using CommunityToolkit.Mvvm.ComponentModel;
using Scaffold.MVVM;
using Scaffold.States;
using System;
using VContainer;

public partial class SampleViewModel : ViewModel
{
    public SampleViewModel(string sample)
    {
        Sample = sample;
        parent = BindChildViewModel(new ParentViewModel());
    }

    [ObservableProperty] protected string sample;
    [ObservableProperty] protected int currentTurn;
    [ObservableProperty] protected ParentViewModel parent;

    [Inject] private ITurnHandler turn;
    [Inject] private Store store;


    protected override void Initialize()
    {
        base.Initialize();
        store.Subscribe<TurnState>(UpdateTurn);
    }

    private void UpdateTurn(IReference reference, TurnState state)
    {
        CurrentTurn = state.CurrentTurn;
    }

    public void Pass()
    {
        turn.Temporary_PassTurn();
    }

    internal void NewParent()
    {
        Parent = BindChildViewModel(new ParentViewModel());
    }
}

public partial class ParentViewModel: ViewModel
{
    public ParentViewModel()
    {
        child = BindChildViewModel(new ChildViewModel());
    }
    [ObservableProperty] private ChildViewModel child;

    internal void NewChild()
    {
        Child = BindChildViewModel(new ChildViewModel());
    }
}


public partial class ChildViewModel: ViewModel
{
    public ChildViewModel()
    {
        value = UnityEngine.Random.Range(0, 10);
    }

    [ObservableProperty] private int value;
}