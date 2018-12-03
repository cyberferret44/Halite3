using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    public class EndOfGameLogic : Logic {
        private HashSet<int> FinalReturnToHome = new HashSet<int>();

        public override void Initialize() { /* Nothing to be done */ }

        public override void ProcessTurn() {
            foreach(var ship in Me.ShipsSorted) {
                if(ship.DistanceToDropoff * 1.5 > MyBot.game.TurnsRemaining) {
                    FinalReturnToHome.Add(ship.Id);
                }
            }
        }

        public override void CommandShips(List<Ship> ships) {
            foreach(var ship in ships.Where(s => FinalReturnToHome.Contains(s.Id))) {
                var directions = ship.ClosestDropoff.position.GetAllDirectionsTo(ship.position);
                directions = directions.OrderBy(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
                directions.Add(Direction.STILL);
                foreach(var d in directions) {
                    if(IsSafeMove(ship, d)) {
                        MyBot.MakeMove(ship.Move(d));
                        break;
                    }
                }
            }
        }

        // override methods
        protected override bool IsSafeMove(Ship ship, Direction move) {
            MapCell target = Map.At(ship.position.DirectionalOffset(move));
            if(target.structure != null)
                return true;
            return !CollisionCells.Contains(target);
        }
    }
}