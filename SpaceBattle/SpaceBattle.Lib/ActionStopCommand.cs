using App;
using System.Collections.Generic;

namespace SpaceBattle.Lib
{
    public class ActionStopCommand : ICommand
    {
        private readonly IDictionary<string, object> _order;

        public ActionStopCommand(IDictionary<string, object> order)
        {
            _order = order ?? throw new System.ArgumentNullException(nameof(order));
        }

        public void Execute()
        {

        }
    }
}
