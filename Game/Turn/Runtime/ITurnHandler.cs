using System.Collections;
using System.Collections.Generic;
using Scaffold.Tools;

public interface ITurnHandler
{
    void SetActivePlayer(IPlayer player);
    IPlayer GetActivePlayer();
    IPlayer GetNextPlayer(IPlayer player);
    void Temporary_PassTurn();
}
