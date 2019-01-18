using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // local parameters
        private HashSet<int> MovingTowardsBase = new HashSet<int>();
        private int Spacing;
        private int HaliteCutoff;
        private List<Ship> AvailableShipsMovingToBase => Fleet.AvailableShips.Where(s => MovingTowardsBase.Contains(s.Id)).ToList();
        
        public DropoffLogic() {
            // value initialization
            Spacing = (int)HParams[Parameters.DROPOFF_DISTANCE];
            Log.LogMessage($"Xlayers for dropoff is {GameInfo.DropoffXlayers}");
            Log.LogMessage($"Actual Dropoff Distance is {Spacing}");

            var availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
            while(availableCells.Count > 0) {
                int max = -1;
                Position pos = null;
                foreach(var cell in availableCells) {
                    var virtDrop = new VirtualDropoff(cell.position);
                    if(virtDrop.VirtualDropValue > max || (virtDrop.VirtualDropValue == max && Map.CalculateDistance(cell.position, Me.shipyard.position) < Map.CalculateDistance(pos, Me.shipyard.position))) {
                        pos = cell.position;
                        max = (int)virtDrop.VirtualDropValue;
                    }
                }
                HaliteCutoff = Math.Max(max/3, HaliteCutoff);
                if(max * .7 < HaliteCutoff)
                    break;

                GameInfo.BestDropoffs.Add(new VirtualDropoff(pos));
                availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
                Log.LogMessage($"Best drop-off at ({pos.x},{pos.y}) with a value {max}");
            }
            Log.LogMessage("Halite Cutoff: " + HaliteCutoff);
        }

        public override void ProcessTurn() {
            // Handle return to base flags
            foreach(var ship in Me.ShipsSorted) {
                if(ShouldMoveShip(ship))
                    MovingTowardsBase.Add(ship.Id);
                if(ship.OnDropoff)
                    MovingTowardsBase.Remove(ship.Id);
            }

            // Delete any dropoffs that have been mostly havested
            foreach(var d in GameInfo.BestDropoffs.ToList()) {
                int halite = (int)d.VirtualDropValue;
                if(Map.At(d.Position).IsStructure || halite < HaliteCutoff) {
                    if(GameInfo.NextDropoff == d) {
                        DeleteNextDropoff();
                    }
                    GameInfo.BestDropoffs.Remove(d);
                    Log.LogMessage($"drop-off at {d.Position.x},{d.Position.y} has been deleted... halite {halite}, cutoff {HaliteCutoff}");
                }
            }

            // iterate bestdropoffs to potentially select next dropoff...
            if(GameInfo.ReserveForDropoff && !ShouldCreateDropoff()) {
                GameInfo.ReserveForDropoff = false;
                GameInfo.NextDropoff = null;
            }

            if(GameInfo.BestDropoffs.Any()) {
                GameInfo.BestDropoffs = GameInfo.BestDropoffs.OrderByDescending(d => d.VirtualDropValue * Math.Pow(.95, GameInfo.MyClosestDropDistance(d.Position))).ToList();
                var bestDrop = GameInfo.BestDropoffs[0];
                
                if(!GameInfo.ReserveForDropoff && ShouldCreateDropoff() && bestDrop.Cell.MyClosestShips().Any(s => GameInfo.Distance(s.position, bestDrop.Position) <= s.DistanceToMyDropoff)) {
                    Log.LogMessage("drop-off: save halite for newdropoff has been flagged as true");
                    GameInfo.ReserveForDropoff = true;
                }

                if(GameInfo.ReserveForDropoff && CanCreateDropoff(bestDrop.Position)) {
                    GameInfo.NextDropoff = GameInfo.BestDropoffs[0];
                    Log.LogMessage($"Next dropoff flagged as {GameInfo.NextDropoff.Position.ToString()}");
                }
            }


            // Move the next dropoff if any neighbors have become more valuable
            for(int i=0; i<GameInfo.BestDropoffs.Count; i++) {
                MapCell dropCell = GameInfo.BestDropoffs[i].Cell;
                var curVal = new VirtualDropoff(dropCell.position).VirtualDropValue;
                foreach(var n in dropCell.Neighbors.Where(n => DistanceToClosestVirtualOrRealDropoff(n.position, dropCell) >= Spacing)) {
                    var nVal = new VirtualDropoff(n.position).VirtualDropValue;
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
            // first make dropoffs...
            foreach(var ship in AvailableShipsMovingToBase) {
                if(GameInfo.NextDropoff != null && ship.position.Equals(GameInfo.NextDropoff.Position) && CanCreateDropoff(ship.position)) {
                    MakeMove(ship.MakeDropoff());
                }
            }

            // go through buckets and move the ships...
            var dropoffBuckets = GetBuckets(AvailableShipsMovingToBase);
            foreach(var drop in dropoffBuckets.Keys) {
                var ships = dropoffBuckets[drop].OrderBy(s => Map.CalculateDistance(s.position, drop) * 10000 - s.halite).ToList();
                int maxDist = 0;
                foreach(var ship in ships) {
                    int thisDist = Map.CalculateDistance(ship.position, drop);
                    if(ShouldMineInsteadOfDropoff(ship, ships, dropoffBuckets)) {
                        MakeMove(ship.StayStill("Mining halite because I can"));
                        TwoTurnAvoider.Add(ship, ship.CurrentMapCell, drop.GetAllDirectionsTo(ship.CurrentMapCell));
                    } else if(thisDist > maxDist || ship.CellHalite < 10 || !IsSafeMove(ship, Direction.STILL)) {
                        var cmd = GetBestNavigateCommand(ship, drop);
                        if(cmd != null) {
                            MakeMove(cmd);
                            TwoTurnAvoider.Add(ship, cmd.TargetCell, drop.GetAllDirectionsTo(cmd.TargetCell));
                        }
                    } else {
                        MakeMove(ship.StayStill($"Staying still to stagger ships"));
                        TwoTurnAvoider.Add(ship, ship.CurrentMapCell, drop.GetAllDirectionsTo(ship.CurrentMapCell));
                    }
                    maxDist = Math.Max(maxDist, thisDist);
                }
            }
        }

        public bool ShouldMineInsteadOfDropoff(Ship ship, List<Ship> bucket, Dictionary<Position, List<Ship>> buckets) {
            // verified...
            if(ship.CellHalite < 10)
                return false;
            if(ship.CellHalite * .15 + ship.halite > 1000)
                return false;
            if(ship.CurrentMapCell.IsThreatened)
                return false;
            if(!IsSafeMove(ship, Direction.STILL))
                return false;

            // need to handle the shipyard with special care
            if(GameInfo.IsMyShipyard(ship.ClosestDropoff)) {

            }

            // harder...
            //bool highestOrder = buckets.Where(x => x.Value != bucket).All(b => b[0].DistanceToMyDropoff > ship.DistanceToMyDropoff || b[0].halite < ship.halite);
            if(!MyBot.ShouldSpawnShip(0) && MyBot.ShouldSpawnShip(ship.halite) /* and not 2 ships same dist from drop */)
                return false;
            if(bucket.Count > 1 && bucket[1].DistanceToMyDropoff -1 <= ship.DistanceToMyDropoff) 
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
                var drop = ship.ClosestDropoff; // includes virtual ones
                if(!buckets.ContainsKey(drop)) {
                    buckets.Add(drop, new List<Ship>());
                }
                buckets[drop].Add(ship);
            }
            return buckets;
        }

        private Command GetBestNavigateCommand(Ship ship, Position drop) {
            // new logic, path of least resistance
            var polr = Navigation.CalculatePathOfLeastResistance(ship.position, drop);
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

        private bool ShouldCreateDropoff() => Fleet.ShipCount / Me.GetDropoffs().Count >= 15 ; // need a minimum of ships per drop
        private bool CanCreateDropoff(Position pos) => Me.halite + Map.At(pos).halite + 500 >= 5000;

        private bool ShouldMoveShip(Ship ship) {
            return ship.IsFull() ||
                ship.halite > HParams[Parameters.CARGO_TO_MOVE] + (.3 * ship.CellHalite * (ship.CurrentMapCell.IsInspired ? 3 : 1));
        }

        private void DeleteNextDropoff() {
            Log.LogMessage($"Drop-off {GameInfo.NextDropoff.Position.x},{GameInfo.NextDropoff.Position.y} was deleted.");
            GameInfo.ReserveForDropoff = false;
            GameInfo.NextDropoff = null;
        }
    }
}