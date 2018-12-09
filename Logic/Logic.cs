using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
namespace Halite3.Logic {
    public abstract class Logic {
        // shortcut accessors
        protected static GameMap Map => MyBot.GameMap;
        protected static Player Me => MyBot.Me;
        protected static HashSet<MapCell> CollisionCells => MyBot.CollisionCells;
        protected HyperParameters HParams => MyBot.HParams;

        // Shared Information
        protected List<MapCell> PleaseAvoidCells = new List<MapCell>();

        //abstract methods
        public abstract void Initialize();
        public abstract void ProcessTurn();
        public abstract void CommandShips(List<Ship> ships);

        //concrete methods
        protected virtual bool IsSafeMove(Ship ship, Direction move) {
            MapCell target = Map.At(ship.position.DirectionalOffset(move));
            if(target.IsStructure && !CollisionCells.Contains(target)) {
                return true;
            }
            if(target.IsOccupiedByMe()) {
                var s = target.ship;
                if(s.Neighbors.All(n => CollisionCells.Contains(n) || n.IsOccupiedByOpponent())) {
                    return false;
                }
            }
            return !CollisionCells.Contains(target) && !target.IsOccupiedByOpponent();
        }
    }

    public class EmptyLogic : Logic {
        public override void Initialize() { }
        public override void ProcessTurn() { }
        public override void CommandShips(List<Ship> ships) { }
    }
}