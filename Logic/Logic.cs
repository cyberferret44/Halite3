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

        // Make move method, and all it's details...
        //public static void MakeMove(Command command) {
        //    Fleet.AddMove(command);
        //    TwoTurnAvoider.Remove(command.TargetCell);
       // }
    }

    public class EmptyLogic : Logic {
        public override void ProcessTurn() { }
        public override void CommandShips() { }
    }
}