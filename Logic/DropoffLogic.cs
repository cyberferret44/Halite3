using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // local parameters
        private bool CreatedDropoff = false;
        private HashSet<int> MovingTowardsBase = new HashSet<int>();

        // Abstractc Logic Implementation
        public override void Initialize() { /* TODO */ }

        public override void ProcessTurn() {
            foreach(var ship in Me.ShipsSorted) {
                if(ShouldMoveShip(ship))
                    MovingTowardsBase.Add(ship.Id);
                if(ship.OnDropoff)
                    MovingTowardsBase.Remove(ship.Id);
            }
        }

        public override void CommandShips(List<Ship> ships) {
            // todo alter this to be more advanced
            Ship exclude = null; // prevents sending 2 commands to the same ship
            if(Me.halite >= 5000 && !CreatedDropoff && ships.Count > 0 && MyBot.game.turnNumber > 100 && MyBot.GameMap.PercentHaliteCollected < Math.Min(.5, ((double)MyBot.game.turnNumber)/((double)MyBot.game.TotalTurns))) {
                var dropoffship = ships.OrderBy(s => Map.GetXLayers(s.position, 3).Sum(n => n.halite)).Last();
                MyBot.MakeMove(dropoffship.MakeDropoff());
                CreatedDropoff = true;
                exclude = dropoffship;
            }

            // todo, if bot in way can't move, then wait
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

        // todo make this logic more robust to consider opponents, and dynamic values
        private bool ShouldMoveShip(Ship ship) {
            return ship.halite > HParams[Parameters.CARGO_TO_MOVE];
        }

        private Position ForecastLocation() {
            return null; // todo
        }
    }
}