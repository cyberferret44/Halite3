using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
namespace Halite3.Logic {
    public abstract class Logic {
        // shortcut accessors
        protected static GameMap Map => MyBot.GameMap;
        protected static Player Me => MyBot.Me;
        protected static HashSet<MapCell> CollisionCells => MyBot.CollisionCells;
        protected HyperParameters HParams => MyBot.HParams;

        //abstract methods
        public abstract void Initialize();
        public abstract void ProcessTurn();
        public abstract void CommandShips(List<Ship> ships);

        //concrete methods
        protected virtual bool IsSafeMove(Ship ship, Direction move) {
            MapCell target = Map.At(ship.position.DirectionalOffset(move));
            return !CollisionCells.Contains(target);
        }
    }
}