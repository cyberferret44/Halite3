using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // local parameters
        private HashSet<int> MovingTowardsBase = new HashSet<int>();
        private List<Ship> AvailableShipsMovingToBase => Fleet.AvailableShips.Where(s => MovingTowardsBase.Contains(s.Id)).ToList();

        public override void ProcessTurn() {
            // Handle return to base flags
            foreach(var ship in Me.ShipsSorted) {
                if(ShouldMoveShip(ship))
                    MovingTowardsBase.Add(ship.Id);
                if(ship.OnDropoff)
                    MovingTowardsBase.Remove(ship.Id);
            }
        }

        private bool ShouldMoveShip(Ship ship) {
            if(ship.IsFull())
                return true;
            if(ship.ClosestDropoff.Equals(GameInfo.Me.shipyard.position)) {
                return ship.halite > MyBot.HParams[Parameters.CARGO_TO_MOVE] * .95;
            } else {
                return ship.halite > MyBot.HParams[Parameters.CARGO_TO_MOVE] + (.3 * ship.CellHalite * (ship.CurrentMapCell.IsInspired ? 3 : 1));
            }
        }

        public override void CommandShips() {
            // go through buckets and move the ships...
            var dropoffBuckets = GetBuckets(AvailableShipsMovingToBase);
            
            var ShipPositions = new List<List<Ship>>();
            for(int i=0; i<GameInfo.Map.width * 2; i++) {
                ShipPositions.Add(new List<Ship>());
            }
            
            if(dropoffBuckets.Any(kvp => kvp.Key.Equals(GameInfo.Me.shipyard.position))) {
                var yardDrop = dropoffBuckets.Single(kvp => kvp.Key.Equals(GameInfo.Me.shipyard.position));
                foreach(var s in yardDrop.Value) {
                    var dist = GameInfo.Distance(s, yardDrop.Key);
                    ShipPositions[dist].Add(s);
                }

                // iterate....
                bool frontOccupied = false;
                for(int i=0; i<ShipPositions.Count; i++) {
                    if(ShipPositions[i].Count >= 1) {
                        var ships = ShipPositions[i];
                        ships = ships.OrderByDescending(s => s.halite).ToList();
                        foreach(var s in ships) {
                            if(frontOccupied && !s.IsFull() && s.CellHalite >= 10) {
                                Fleet.AddMove(s.StayStill("Mining halite because I can"));
                                Safety.TwoTurnAvoider.Add(s, s.CurrentMapCell, yardDrop.Key.GetAllDirectionsTo(s.CurrentMapCell));
                            } else {
                                var cmd = GetBestNavigateCommand(s, yardDrop.Key);
                                if(cmd != null) {
                                    Fleet.AddMove(cmd);
                                    Safety.TwoTurnAvoider.Add(s, cmd.TargetCell, yardDrop.Key.GetAllDirectionsTo(cmd.TargetCell));
                                }
                            }
                            frontOccupied = true;
                        }
                    }
                    frontOccupied = ShipPositions[i].Count > 0;
                }

                // remove the key
                dropoffBuckets.Remove(yardDrop.Key);
            }

            foreach(var drop in dropoffBuckets.Keys) {
                var ships = dropoffBuckets[drop].OrderBy(s => Map.CalculateDistance(s.position, drop) * 10000 - s.halite).ToList();
                int maxDist = 0;
                foreach(var ship in ships) {
                    int thisDist = Map.CalculateDistance(ship.position, drop);
                    if(ShouldMineInsteadOfDropoff(ship, ships, dropoffBuckets)) {
                        Fleet.AddMove(ship.StayStill("Mining halite because I can"));
                        Safety.TwoTurnAvoider.Add(ship, ship.CurrentMapCell, drop.GetAllDirectionsTo(ship.CurrentMapCell));
                    } else if(thisDist > maxDist || ship.CellHalite < 10 || !Safety.IsSafeMove(ship, Direction.STILL)) {
                        var cmd = GetBestNavigateCommand(ship, drop);
                        if(cmd != null) {
                            Fleet.AddMove(cmd);
                            Safety.TwoTurnAvoider.Add(ship, cmd.TargetCell, drop.GetAllDirectionsTo(cmd.TargetCell));
                        }
                    } else {
                        Fleet.AddMove(ship.StayStill($"Staying still to stagger ships"));
                        Safety.TwoTurnAvoider.Add(ship, ship.CurrentMapCell, drop.GetAllDirectionsTo(ship.CurrentMapCell));
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
            if(!Safety.IsSafeMove(ship, Direction.STILL))
                return false;
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
                var drop = ship.ClosestAccessibleDropoff; // includes virtual ones
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
            if(Safety.IsCompletelySafeMove(ship, polr[0].position.GetDirectionTo(ship.position))) {
                var best = polr[0].position.GetDirectionTo(ship.position);
                return ship.Move(best, "Moving from path of least resistance2");
            }

            // old logic, pick any safe direction
            List<Direction> directions = drop.GetAllDirectionsTo(ship.position);
            if(directions.All(x => Map.At(ship, x).IsOccupiedByOpponent || (Map.At(ship, x).IsThreatened) && !Map.At(ship, x).IsMyStructure)) {
                directions = DirectionExtensions.ALL_DIRECTIONS.ToList(); // add all
            }
            directions = directions.OrderBy(d => Map.At(ship, d).IsOpponentsStructure && Map.At(ship, d).IsThreatened ? ship.halite * 3 :
                    Map.At(ship, d).IsThreatened || Map.At(ship, d).IsOccupiedByOpponent ? ship.halite - Map.At(ship, d).SmallestEnemyValue :
                    Map.At(ship, d).IsOccupiedByMe ? Map.At(ship, d).halite * .45 :
                    ship.CellHalite * .1).ToList();
            foreach(var d in directions) {
                if(Safety.IsSafeMove(ship, d)) {
                    return ship.Move(d, "moving to dropoff");
                }
            }
            if(Safety.IsCompletelySafeMove(ship, Direction.STILL)) {
                return ship.StayStill("staying still because nothing else available...");
            }
            return null;
        }
    }
}