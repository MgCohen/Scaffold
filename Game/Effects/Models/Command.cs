using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public abstract class Command
    {
        public abstract Task Execute();
    }

    //###state changing commands###

    //change player resource
    //change entity resource
    //change card position
    //change turn order
    //change turn state
    //add card to pile
    //close effect execution


    //###non-state commands that need to be reacted###

    //get cards in hand
    //get player resource
    //get timer
}
