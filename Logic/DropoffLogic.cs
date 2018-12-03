using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // local parameters
        private bool CreatedDropoff = false;
        private HashSet<int> MovingTowardsBase = new HashSet<int>();

        // Abstractc Logic Implementation
        public override void Initialize() { /* TODO */ }

        public override void ProcessTurn() {
            foreach(var ship in Me.ShipsSorted) {
                if(ship.halite > HParams[Parameters.CARGO_TO_MOVE])
                    MovingTowardsBase.Add(ship.Id);
                if(ship.OnDropoff)
                    MovingTowardsBase.Remove(ship.Id);
            }
        }

        public override void CommandShips(List<Ship> ships) {
            // todo alter this to be more advanced
            Ship exclude = null;
            if(Me.halite > 5000 && !CreatedDropoff && ships.Count > 0) {
                var dropoffship = ships.OrderBy(s => Map.GetXLayers(s.position, 3).Sum(n => n.halite)).Last();
                MyBot.MakeMove(dropoffship.MakeDropoff());
                CreatedDropoff = true;
                exclude = dropoffship;
            }

            foreach(var ship in ships.Where(s => MovingTowardsBase.Contains(s.Id) && s != exclude)) {
                Entity closestDrop = ship.ClosestDropoff;
                List<Direction> directions = closestDrop.position.GetAllDirectionsTo(ship.position);
                directions = directions.OrderBy(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
                foreach(Direction d in directions) {
                    if(IsSafeMove(ship, d)) {
                        MyBot.MakeMove(ship.Move(d));
                        break;
                    }
                }
            }
        } 
    }
}