using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // TODO Meant for Super optomized dropoff logic
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
            Log.LogMessage($"Actual Dropoff Distance is {Spacing}");

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
            // get the ships to use
            var ships = UnusedShips.Where(s => MovingTowardsBase.Contains(s.Id)).ToList();
            Log.LogMessage($"Drop-off ships are " + string.Join(", ", ships.Select(s => s.Id)));

            // first make dropoffs...
            foreach(var ship in ships.ToList()) {
                if(NextDropoff != null && ship.position.Equals(NextDropoff.Position) && CanCreateDropoff(ship.position)) {
                    MakeMove(ship.MakeDropoff(), "make into drop-off");
                    ships.Remove(ship);
                }
            }

            // go through buckets and move the ships...
            var dropoffBuckets = GetBuckets(ships);
            foreach(var bucket in dropoffBuckets) {
                Log.LogMessage($"{bucket.Key.x},{bucket.Key.y}: {string.Join(", ", bucket.Value.Select(x => x.Id))}");
                var drop = bucket.Key;
                int maxDist = 0; // the bucket values are sorted by dist from bucket key.  this lets know if we have dups on distance.
                foreach(var ship in bucket.Value) {
                    int thisDist = Map.CalculateDistance(ship.position, drop);
                    if(thisDist > maxDist || ship.CellHalite < 10 || !IsSafeMove(ship, Direction.STILL)) {
                        NavigateToDropoff(ship, drop);
                    } else {
                        ship.StayStill();
                    }
                    maxDist = Math.Max(maxDist, thisDist);
                }
            }
        }

        Dictionary<Position, List<Ship>> GetBuckets(List<Ship> ships) {
            var buckets = new Dictionary<Position, List<Ship>>();
            foreach(var ship in ships) {
                var drop = GetClosestDropoff(ship); // includes virtual ones
                if(!buckets.ContainsKey(drop)) {
                    buckets.Add(drop, new List<Ship>());
                }
                buckets[drop].Add(ship);
            }
            foreach(var key in buckets.Keys.ToList()) {
                // ordering them by 10k * distance minus halite, which prioritizes moving full ships
                buckets[key] = buckets[key].OrderBy(s => Map.CalculateDistance(s.position, key) * 10000 - s.halite).ToList();
            }
            return buckets;
        }

        private void NavigateToDropoff(Ship ship, Position drop) {
            // todo if ship is closest to dropoff and cargo plus me.halite isn't enough to bump above 1k, just wait on current cell, no rush
            List<Direction> directions = drop.GetAllDirectionsTo(ship.position);
            directions = directions.OrderBy(d => Map.At(ship, d).IsOccupiedByMe() ? Map.At(ship, d).halite * .45 : Map.At(ship, d).halite * .1).ToList();
            if(directions.Count == 1 && Map.At(ship, directions[0]).IsOccupiedByOpponent()) {
                if(directions[0] == Direction.NORTH)
                    directions.AddRange(new List<Direction>{ Direction.EAST, Direction.WEST});
                if(directions[0] == Direction.SOUTH)
                    directions.AddRange(new List<Direction>{ Direction.EAST, Direction.WEST});
                if(directions[0] == Direction.EAST)
                    directions.AddRange(new List<Direction>{ Direction.NORTH, Direction.SOUTH});
                if(directions[0] == Direction.WEST)
                    directions.AddRange(new List<Direction>{ Direction.NORTH, Direction.SOUTH});
            } else if(directions.Count == 2 && directions.Where(d => Map.At(ship, d).IsThreatened).Count() == 1) {
                if(Map.At(ship, directions[0]).IsThreatened) {
                    var temp = directions[1];
                    directions[1] = directions[0];
                    directions[0] = temp;
                }
            } else if(directions.Count == 2 && directions.All(d => Map.At(ship, d).IsThreatened || Map.At(ship, d).Neighbors.Any(n => n.IsStructure && n.structure.IsOpponents))) {
                // someone blocking a corner, keep options open by moving closest to the hypotenouse 
                var pos0 = Map.At(ship, directions[0]).position;
                var pos1 = Map.At(ship, directions[1]).position;
                int delta0 = Math.Abs(  Math.Abs(pos0.x-drop.x) - Math.Abs(pos0.y-drop.y)); // i.e. 7-1 = 6
                int delta1 = Math.Abs(  Math.Abs(pos1.x-drop.x) - Math.Abs(pos1.y-drop.y)); // i.e. 8-0 = 8
                if(delta1 < delta0) {
                    var temp = directions[1];
                    directions[1] = directions[0];
                    directions[0] = temp;
                }
            }
            directions.Add(Direction.STILL);
            for(int i=0; i< directions.Count; i++) { //foreach(Direction d in directions) {
                if(IsSafeMove(ship, directions[i])) {
                    MakeMove(ship.Move(directions[i]),  "moving to dropoff");
                    if((ship.position.x == drop.x || ship.position.y == drop.y) && i == 0) {
                        TwoTurnAvoid.Add(Map.At(ship.position.DirectionalOffset(directions[i]).DirectionalOffset(directions[i])));
                    }
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

        private bool ShouldMoveShip(Ship ship) {
            return ship.IsFull() ||
                ship.halite > HParams[Parameters.CARGO_TO_MOVE] * Constants.MAX_HALITE + (.3 * ship.CellHalite * (ship.CurrentMapCell.IsInspired ? 3 : 1))
                || ship.halite > 500 && ship.CurrentMapCell.IsThreatened; // todo this should be more robust
        }

        private void DeleteNextDropoff() {
            Log.LogMessage($"Drop-off {NextDropoff.Position.x},{NextDropoff.Position.y} was deleted.");
            MyBot.ReserveForDropoff = false;
            NextDropoff = null;
        }
    }
}