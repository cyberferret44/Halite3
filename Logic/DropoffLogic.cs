using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // TODO Meant for Super optomized dropoff logic
        Dictionary<Point, int[]> DropoffQueue = new Dictionary<Point, int[]>();
        public override void ScoreMoves() { }

        // virtual drop off
        private class VirtualDropoff {
            public Position Position;
            public int InitialHalite;
            public VirtualDropoff(Position p, int halite) {
                Position = p;
                InitialHalite = halite;
            }
        }

        // local parameters
        private HashSet<int> MovingTowardsBase = new HashSet<int>();
        private List<VirtualDropoff> BestDropoffs = new List<VirtualDropoff>();
        VirtualDropoff NextDropoff = null;
        private int Xlayers;
        private int MinDropoffValue;
        private int Spacing;
        private double HarvestedPercentToDelete = .7;

        // Abstractc Logic Implementation
        public override void Initialize() {
            // value initialization
            Xlayers = Map.width / 4;
            MinDropoffValue = (int)(((double)Xlayers + 1.0) / 2.0 * 4.0 * (double)Xlayers * 135.0);
            Spacing = (int)HParams[Parameters.DROPOFF_DISTANCE];
            Log.LogMessage($"Spacing is {Spacing}");

            // todo one magic number
            // todo set min to either MinDropoffValue, or like half of the max value so we don't get stuck in too-small of a local minima
            var availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
            while(availableCells.Count > 0) {
                int max = -1;
                Position pos = null;
                foreach(var cell in availableCells) {
                    int val = Map.GetXLayers(cell.position, Xlayers).Sum(x => x.halite);
                    if(val > max || (val == max && Map.CalculateDistance(cell.position, Me.shipyard.position) < Map.CalculateDistance(pos, Me.shipyard.position))) {
                        pos = cell.position;
                        max = val;
                    }
                }
                if(max < MinDropoffValue || (BestDropoffs.Count > 0 && max < BestDropoffs[0].InitialHalite / 1.75))
                    break;
                BestDropoffs.Add(new VirtualDropoff(pos, max));
                availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
                Log.LogMessage($"Best drop-off at ({pos.x},{pos.y}) with a value {max}");
            }
        }

        public override void ProcessTurn() {
            // Handle return to base flags
            foreach(var ship in Me.ShipsSorted) {
                if(ShouldMoveShip(ship))
                    MovingTowardsBase.Add(ship.Id);
                if(ship.OnDropoff)
                    MovingTowardsBase.Remove(ship.Id);
            }

            // Flag MyBot to save 5000 halite if we're close to a virtual dropoff
            if(ShouldCreateDropoff() && !MyBot.ReserveForDropoff && BestDropoffs.Count > 0) {
                foreach(var ship in Me.ShipsSorted) {
                    var closestVirtual = GetClosestVirtualDropoff(ship.position);
                    if(Map.CalculateDistance(ship.position, closestVirtual.Position) <= ship.DistanceToDropoff) {
                        Log.LogMessage("drop-off bot save for drop has been flagged");
                        MyBot.ReserveForDropoff = true;
                        break;
                    }
                }
            }

            // Delete any dropoffs that have been mostly havested
            foreach(var d in BestDropoffs.ToList()) {
                int halite = Map.GetXLayers(d.Position, Xlayers).Sum(x => x.halite);
                if(Map.At(d.Position).IsStructure || halite < d.InitialHalite * HarvestedPercentToDelete) {
                    if(NextDropoff == d) {
                        DeleteNextDropoff();
                    }
                    BestDropoffs.Remove(d);
                    Log.LogMessage($"drop-off at {d.Position.x},{d.Position.y} has been deleted...");
                }
            }

            // Flag our next dropoff if not defined
            if(NextDropoff == null && MyBot.ReserveForDropoff && BestDropoffs.Count > 0) {
                int max = 0;
                VirtualDropoff best = null;
                foreach(var ship in AllShips) {
                    var closestVirtual = GetClosestVirtualDropoff(ship.position);
                    int dist = Map.CalculateDistance(ship.position, closestVirtual.Position);
                    if(CanCreateDropoff(closestVirtual.Position) &&  dist <= ship.DistanceToDropoff) {
                        int halite = Map.GetXLayers(closestVirtual.Position, Xlayers).Sum(x => x.halite);
                        if(max < halite) {
                            max = halite;
                            best = closestVirtual;
                        }
                    }
                }

                if(best != null && AllShips.Any(s => Map.CalculateDistance(s.position, best.Position) <= s.DistanceToDropoff && MovingTowardsBase.Contains(s.Id))) {
                    NextDropoff = best;
                    Log.LogMessage($"best drop-off has been selected at {best.Position.x},{best.Position.y}");
                }
            }
        }

        public override void CommandShips() {
            // Sort the ships based on distances to the virtual dropoff
            var ships = UnusedShips.Where(s => MovingTowardsBase.Contains(s.Id)).ToList();
            ships = ships.OrderBy(s => Map.CalculateDistance(s.position, GetClosestDropoff(s))).ToList();

            foreach(var ship in ships) {
                if(NextDropoff != null && ship.position.Equals(NextDropoff.Position) && CanCreateDropoff(ship.position)) {
                    MakeMove(ship.MakeDropoff(), "make into drop-off");
                } else {
                    Position closestDrop = GetClosestDropoff(ship);
                    NavigateToDropoff(ship, closestDrop);
                }
            }
        }

        private void NavigateToDropoff(Ship ship, Position drop) {
            List<Direction> directions = drop.GetAllDirectionsTo(ship.position);
            directions = directions.OrderBy(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
            if(directions.Count == 1 && Map.At(ship.position.DirectionalOffset(directions[0])).IsOccupiedByOpponent()) {
                if(directions[0] == Direction.NORTH)
                    directions.AddRange(new List<Direction>{ Direction.EAST, Direction.WEST});
                if(directions[0] == Direction.SOUTH)
                    directions.AddRange(new List<Direction>{ Direction.EAST, Direction.WEST});
                if(directions[0] == Direction.EAST)
                    directions.AddRange(new List<Direction>{ Direction.NORTH, Direction.SOUTH});
                if(directions[0] == Direction.WEST)
                    directions.AddRange(new List<Direction>{ Direction.NORTH, Direction.SOUTH});
            } else if(directions.Count == 2 && directions.Where(d => Map.At(ship.position.DirectionalOffset(d)).IsThreatened).Count() == 1) {
                if(Map.At(ship.position.DirectionalOffset(directions[0])).IsThreatened) {
                    var temp = directions[1];
                    directions[1] = directions[0];
                    directions[0] = temp;
                }
            } else if(directions.Count == 2 && directions.All(d => Map.At(ship.position.DirectionalOffset(d)).IsThreatened)) {
                // someone blocking a corner, keep options open by moving closest to the hypotenouse 
                var pos0 = Map.At(ship.position.DirectionalOffset(directions[0])).position;
                var pos1 = Map.At(ship.position.DirectionalOffset(directions[1])).position;
                int delta0 = Math.Abs(  Math.Abs(pos0.x-drop.x) - Math.Abs(pos0.y-drop.y)); // i.e. 7-1 = 6
                int delta1 = Math.Abs(  Math.Abs(pos1.x-drop.x) - Math.Abs(pos1.y-drop.y)); // i.e. 8-0 = 8
                if(delta1 < delta0) {
                    var temp = directions[1];
                    directions[1] = directions[0];
                    directions[0] = temp;
                }
            }
            directions.Add(Direction.STILL);
            foreach(Direction d in directions) {
                if(IsSafeMove(ship, d)) {
                    MakeMove(ship.Move(d),  "moving to dropoff");
                    break;
                }
            }
        }

        private int DistanceToClosestVirtualOrRealDropoff(Position position) {
            if(BestDropoffs.Count == 0)
                return int.MaxValue;
            int closestReal = Me.GetDropoffs().Min(x => Map.CalculateDistance(position, x.position));
            int closestVirtual = BestDropoffs.Min(x => Map.CalculateDistance(x.Position, position));
            return Math.Min(closestReal, closestVirtual);
        }

        private VirtualDropoff GetClosestVirtualDropoff(Position position) {
            if(BestDropoffs.Count == 0)
                return null;
            return BestDropoffs.OrderBy(d => Map.CalculateDistance(position, d.Position)).First();
        }

        // Forecasting!!!
        private Position GetClosestDropoff(Ship ship) {
            if(NextDropoff != null && Map.CalculateDistance(ship.position, NextDropoff.Position) <= ship.DistanceToDropoff)
                return NextDropoff.Position;
            return ship.ClosestDropoff.position;
        }

        private bool ShouldCreateDropoff() => Me.ShipsSorted.Count / Me.GetDropoffs().Count > 15 ; // need a minimum of ships per drop
        private bool CanCreateDropoff(Position pos) => Me.halite + Map.At(pos).halite + 500 >= 5000 && MyBot.game.turnNumber >= 40;

        // todo consider opponents
        private bool ShouldMoveShip(Ship ship) {
            return ship.IsFull() ||
                ship.halite > HParams[Parameters.CARGO_TO_MOVE] * Constants.MAX_HALITE + .4 * ship.CurrentMapCell.halite;
        }

        private void DeleteNextDropoff() {
            Log.LogMessage($"Drop-off {NextDropoff.Position.x},{NextDropoff.Position.y} was deleted.");
            MyBot.ReserveForDropoff = false;
            NextDropoff = null;
        }
    }
}