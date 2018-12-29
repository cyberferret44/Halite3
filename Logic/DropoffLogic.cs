using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // local parameters
        private HashSet<int> MovingTowardsBase = new HashSet<int>();
        private int Xlayers;
        private int Spacing;
        private double HarvestedPercentToDelete = .7;
        private double CellValue(MapCell cell) => Map.GetXLayers(cell.position, Xlayers).
                Sum(x => (x.IsOccupiedByOpponent() ? x.ship.halite - 1000 : 0) + x.halite / (1 + GameInfo.Distance(cell, x)));
        
        public DropoffLogic() {
            // value initialization
            Xlayers = Map.width / 4;
            Spacing = (int)HParams[Parameters.DROPOFF_DISTANCE];
            Log.LogMessage($"Xlayers for dropoff is {Xlayers}");
            Log.LogMessage($"Actual Dropoff Distance is {Spacing}");

            var availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
            while(availableCells.Count > 0) {
                int max = -1;
                Position pos = null;
                foreach(var cell in availableCells) {
                    int val = (int)CellValue(cell);
                    if(val > max || (val == max && Map.CalculateDistance(cell.position, Me.shipyard.position) < Map.CalculateDistance(pos, Me.shipyard.position))) {
                        pos = cell.position;
                        max = val;
                    }
                }
                if((GameInfo.BestDropoffs.Count > 0 && max < GameInfo.BestDropoffs[0].InitialHalite / 1.75))
                    break;

                GameInfo.BestDropoffs.Add(new VirtualDropoff(pos, max));
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
            /* if(ShouldCreateDropoff() && !GameInfo.ReserveForDropoff && GameInfo.BestDropoffs.Count > 0) {
                foreach(var ship in Me.ShipsSorted) {
                    var closestVirtual = GetClosestVirtualDropoff(ship.position);
                    if(Map.CalculateDistance(ship.position, closestVirtual.Position) <= ship.DistanceToMyDropoff) {
                        Log.LogMessage("drop-off bot save for drop has been flagged");
                        GameInfo.ReserveForDropoff = true;
                        break;
                    }
                }
            }*/

            // Delete any dropoffs that have been mostly havested
            foreach(var d in GameInfo.BestDropoffs.ToList()) {
                int halite = (int)CellValue(d.Cell);
                if(Map.At(d.Position).IsStructure || halite < d.InitialHalite * HarvestedPercentToDelete) {
                    if(GameInfo.NextDropoff == d) {
                        DeleteNextDropoff();
                    }
                    GameInfo.BestDropoffs.Remove(d);
                    Log.LogMessage($"drop-off at {d.Position.x},{d.Position.y} has been deleted...");
                }
            }

            // iterate bestdropoffs to potentially select next dropoff...
            if(GameInfo.ReserveForDropoff && !ShouldCreateDropoff()) {
                GameInfo.ReserveForDropoff = false;
                GameInfo.NextDropoff = null;
            }

            if(GameInfo.BestDropoffs.Any()) {
                GameInfo.BestDropoffs = GameInfo.BestDropoffs.OrderByDescending(d => CellValue(d.Cell) * Math.Pow(.95, GameInfo.MyClosestDropDistance(d.Position))).ToList();
                var bestDrop = GameInfo.BestDropoffs[0];
                
                if(ShouldCreateDropoff() && bestDrop.Cell.MyClosestShips().Any(s => GameInfo.Distance(s.position, bestDrop.Position) <= s.DistanceToMyDropoff)) {
                    Log.LogMessage("drop-off bot save for drop has been flagged");
                    GameInfo.ReserveForDropoff = true;
                }


                if(GameInfo.ReserveForDropoff && CanCreateDropoff(bestDrop.Position)) {
                    GameInfo.NextDropoff = GameInfo.BestDropoffs[0];
                }
            }


            // Move the next dropoff if any neighbors have become more valuable
            for(int i=0; i<GameInfo.BestDropoffs.Count; i++) {
                MapCell dropCell = GameInfo.BestDropoffs[i].Cell;
                var curVal = CellValue(dropCell);
                foreach(var n in dropCell.Neighbors.Where(n => DistanceToClosestVirtualOrRealDropoff(n.position, dropCell) >= Spacing)) { // todo where closestreal or virtual dropoff
                    var nVal = CellValue(n);
                    if(nVal > curVal) {
                        curVal = nVal;
                        dropCell = n;
                    }
                }
                if(dropCell != GameInfo.BestDropoffs[i].Cell) {
                    Log.LogMessage($"drop location was changed from {GameInfo.BestDropoffs[i].Position.ToString()} to {dropCell.position.ToString()}");
                    GameInfo.BestDropoffs[i].Position = dropCell.position;
                }
            }
        }

        public override void CommandShips() {
            // get the ships to use
            var ships = Fleet.AvailableShips.Where(s => MovingTowardsBase.Contains(s.Id)).ToList();

            // first make dropoffs...
            foreach(var ship in ships.ToList()) {
                if(GameInfo.NextDropoff != null && ship.position.Equals(GameInfo.NextDropoff.Position) && CanCreateDropoff(ship.position)) {
                    MakeMove(ship.MakeDropoff());
                    ships.Remove(ship);
                }
            }

            // go through buckets and move the ships...
            var dropoffBuckets = GetBuckets(ships);
            foreach(var bucket in dropoffBuckets) {
                var drop = bucket.Key;
                int maxDist = 0; // the bucket values are sorted by dist from bucket key.  this lets know if we have dups on distance.
                foreach(var ship in bucket.Value) {
                    int thisDist = Map.CalculateDistance(ship.position, drop);
                    // first, check that this ship is closer than any other and its cargo wont go over and it wont make a difference
                    if(ShouldMineInsteadOfDropoff(ship, bucket.Value, dropoffBuckets)) {
                        MakeMove(ship.StayStill(" Mining halite because I can"));
                        TwoTurnAvoider.Add(ship, ship.CurrentMapCell, drop.GetAllDirectionsTo(ship.CurrentMapCell));
                    } else if(thisDist > maxDist || ship.CellHalite < 10 || !IsSafeMove(ship, Direction.STILL)) {
                        var cmd = GetBestNavigateCommand(ship, drop);
                        if(cmd != null) {
                            MakeMove(cmd);
                            TwoTurnAvoider.Add(ship, cmd.TargetCell, drop.GetAllDirectionsTo(cmd.TargetCell));
                        }
                    } else {
                        MakeMove(ship.StayStill($" staying still to stagger ships"));
                        TwoTurnAvoider.Add(ship, ship.CurrentMapCell, drop.GetAllDirectionsTo(ship.CurrentMapCell));
                    }
                    maxDist = Math.Max(maxDist, thisDist);
                }
            }
        }

        public bool ShouldMineInsteadOfDropoff(Ship ship, List<Ship> bucket, Dictionary<Position, List<Ship>> buckets) {
            if(ship.CellHalite < 10)
                return false;
            if(ship.CellHalite * .15 + ship.halite > 1000)
                return false;
            if((int)((ship.halite + GameInfo.Me.halite)/1000) > (int)(GameInfo.Me.halite/1000))
                return false;
            if(bucket.Count > 1 && bucket[1].DistanceToMyDropoff -1 <= ship.DistanceToMyDropoff) 
                return false;
            if(ship.CurrentMapCell.IsThreatened)
                return false;
            if(!IsSafeMove(ship, Direction.STILL))
                return false;
            foreach(var b in buckets.Where(x => x.Value != bucket)) {
                if(b.Value.First().DistanceToMyDropoff <= ship.DistanceToMyDropoff) {
                    return false;
                }
            }
            return true;
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

        private Command GetBestNavigateCommand(Ship ship, Position drop) {
            // new logic, path of least resistance
            var polr = GameInfo.CalculatePathOfLeastResistance(ship.position, drop);
            if(IsCompletelySafeMove(ship, polr[0].position.GetDirectionTo(ship.position))) {
                var best = polr[0].position.GetDirectionTo(ship.position);
                return ship.Move(best, "Moving from path of least resistance");
            }

            // old logic, pick any safe direction
            List<Direction> directions = drop.GetAllDirectionsTo(ship.position);
            if(directions.All(x => Map.At(ship, x).IsOccupiedByOpponent() || (Map.At(ship, x).IsThreatened) && !Map.At(ship, x).IsMyStructure)) {
                directions = DirectionExtensions.ALL_DIRECTIONS.ToList(); // add all
            }
            directions = directions.OrderBy(d => Map.At(ship, d).IsOpponentsStructure && Map.At(ship, d).IsThreatened ? ship.halite * 3 :
                    Map.At(ship, d).IsThreatened || Map.At(ship, d).IsOccupiedByOpponent() ? ship.halite - Map.At(ship, d).SmallestEnemyValue :
                    Map.At(ship, d).IsOccupiedByMe() ? Map.At(ship, d).halite * .45 :
                    ship.CellHalite * .1).ToList();
            foreach(var d in directions) {
                if(IsSafeMove(ship, d)) {
                    return ship.Move(d, "moving to dropoff");
                }
            }
            if(IsCompletelySafeMove(ship, Direction.STILL)) {
                return ship.StayStill("staying still because nothing else available...");
            }
            return null;
        }

        private int DistanceToClosestVirtualOrRealDropoff(Position position, MapCell exclude = null) {
            if(GameInfo.BestDropoffs.Count == 0)
                return int.MaxValue;
            int closestReal = Me.GetDropoffs().Min(x => Map.CalculateDistance(position, x));
            var best = GameInfo.BestDropoffs;
            if(exclude != null)
                best = best.Where(d => !d.Cell.position.AsPoint.Equals(exclude.position.AsPoint)).ToList();
            int closestVirtual = best.Any() ? best.Min(x => Map.CalculateDistance(x.Position, position)) : int.MaxValue;
            return Math.Min(closestReal, closestVirtual);
        }

        private VirtualDropoff GetClosestVirtualDropoff(Position position) {
            if(GameInfo.BestDropoffs.Count == 0)
                return null;
            int closest = GameInfo.BestDropoffs.Min(x => Map.CalculateDistance(position, x.Position));
            return GameInfo.BestDropoffs.First(d => Map.CalculateDistance(position, d.Position) == closest);
        }

        // Forecasting!!!
        private Position GetClosestDropoff(Ship ship) {
            //if(GameInfo.NextDropoff != null && Map.CalculateDistance(ship.position, GameInfo.NextDropoff.Position) <= ship.DistanceToMyDropoff)
            //    return GameInfo.NextDropoff.Position;
            return ship.ClosestDropoff;
        }

        private bool ShouldCreateDropoff() => Fleet.AllShipIds.Count / Me.GetDropoffs().Count >= 15 ; // need a minimum of ships per drop
        private bool CanCreateDropoff(Position pos) => Me.halite + Map.At(pos).halite + 500 >= 5000;

        private bool ShouldMoveShip(Ship ship) {
            return ship.IsFull() ||
                ship.halite > HParams[Parameters.CARGO_TO_MOVE] * Constants.MAX_HALITE + (.3 * ship.CellHalite * (ship.CurrentMapCell.IsInspired ? 3 : 1));
        }

        private void DeleteNextDropoff() {
            Log.LogMessage($"Drop-off {GameInfo.NextDropoff.Position.x},{GameInfo.NextDropoff.Position.y} was deleted.");
            GameInfo.ReserveForDropoff = false;
            GameInfo.NextDropoff = null;
        }
    }
}