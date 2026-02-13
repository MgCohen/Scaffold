//Checks for player-actions, Like Playing / Passing / Moving / Activating / Drawing
//This is not effects, commands or parts of it - but more high level, closer to inputs but not necessarily player initiated
public interface IActionHandler
{
    void PushAction(IPlayerAction action);
    IPlayerAction CheckPreviousAction();
}

public class ActionHandler : IActionHandler
{
    public ActionHandler(IStackHandler stack)
    {
        this.stack = stack;
    }

    private IStackHandler stack;

    public IPlayerAction CheckPreviousAction()
    {
        throw new System.NotImplementedException();
    }

    public void PushAction(IPlayerAction action)
    {
        throw new System.NotImplementedException();
    }

    public bool ValidatePlayerAction(IPlayerAction action)
    {
        //run through all state checks 
        return true;
    }

    public void ExecutePlayerAction(IPlayerAction action)
    {
        //add to queue or run
    }
}

//check if step has a valid Play Action
//check if there is a override
//check for reactions 

public class PassAction : IPlayerAction
{

}

public interface IPlayerAction
{

}