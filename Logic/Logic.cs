using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public abstract class Logic {
        // shortcut accessors
        protected static GameMap Map => GameInfo.Map;
        protected static Player Me => GameInfo.Me;
        protected static Game Game => GameInfo.Game;
        protected HyperParameters HParams => MyBot.HParams;

        //abstract methods
        public abstract void ProcessTurn();
        public abstract void CommandShips();
    }

    public class EmptyLogic : Logic {
        public override void ProcessTurn() { }
        public override void CommandShips() { }
    }
}