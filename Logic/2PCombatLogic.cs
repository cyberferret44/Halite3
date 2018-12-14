using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class TwoPlayerCombatLogic : Logic {
        public override void Initialize() { /* TODO */ }
        private class EnemyShipInformation {
            int shipId => ship.Id;
            bool wasFull = false;
            bool endGame = false;
            int cumulitiveMovesTowardsBase = 0;
            public bool IsReturningToBase => endGame || wasFull || cumulitiveMovesTowardsBase >= 3;
            public Direction PredictedMove;
            public MapCell PredictedTargetCell => GameInfo.CellAt(ship, PredictedMove);
            private Ship ship;

            public void Process(Ship enemyShip) {
                ship = enemyShip;
                if(enemyShip.halite == 0)
                    wasFull = false;
                if(enemyShip.halite > 980)
                    wasFull = true;
                if(GameInfo.TurnsRemaining < enemyShip.DistanceToOwnerDropoff * 1.5)
                    endGame = true;

                if(enemyShip.PreviousPosition != null) {
                    if(enemyShip.DistanceToOwnerDropoff < GameInfo.Distance(enemyShip.PreviousPosition, enemyShip.ClosestOwnerDropoff)) {
                        cumulitiveMovesTowardsBase++;
                    } else if(enemyShip.DistanceToOwnerDropoff > GameInfo.Distance(enemyShip.PreviousPosition, enemyShip.ClosestOwnerDropoff)) {
                        cumulitiveMovesTowardsBase = 0;
                    }
                }

                PredictedMove = PredictEnemyMove(enemyShip);
            }

            private Direction PredictEnemyMove(Ship enemy) {
                if(!IsReturningToBase && enemy.CellHalite > GameInfo.AverageHalitePerCell) {
                    return Direction.STILL;
                }
                if(IsReturningToBase) {
                    var directionsToBase = enemy.ClosestOwnerDropoff.position.GetAllDirectionsTo(enemy.position).Where(d => !GameInfo.CellAt(ship, d).IsOccupiedByMe());
                    directionsToBase = directionsToBase.OrderBy(x => GameInfo.CellAt(enemy, x).halite).ToList();
                    if(directionsToBase.Any())
                        return directionsToBase.First();
                }
                if(enemy.CellHalite * 2 < GameInfo.AverageHalitePerCell) {
                    var eNeighbors = enemy.Neighbors.Where(n => !n.IsOccupied()).OrderByDescending(x => x.halite);
                    var best = eNeighbors.FirstOrDefault(x => x.halite > enemy.CellHalite);
                    if(best != null) {
                        return best.position.GetDirectionTo(enemy.position);
                    }
                }
                /* TODO */
                return Direction.STILL;
            }
        }

        public int baseShipValue;
        public int baseShipValueReducedBy2;
        private Dictionary<int, EnemyShipInformation> EnemyShipInfo = new Dictionary<int, EnemyShipInformation>();
        private int CorrectPredictions = 0;
        private int TotalPredictions = 0;

        public override void ProcessTurn() { 
            CalculateProjectedShipValues();
            
            // process new information for each enemy ship
            foreach(var enemyShip in GameInfo.OpponentShips) {
                if(!EnemyShipInfo.ContainsKey(enemyShip.Id)) {
                    EnemyShipInfo.Add(enemyShip.Id, new EnemyShipInformation());
                } else {
                    TotalPredictions++;
                    if(EnemyShipInfo[enemyShip.Id].PredictedMove == enemyShip.PreviousMove) {
                        CorrectPredictions++;
                    }
                }

                EnemyShipInfo[enemyShip.Id].Process(enemyShip);
            }
        }
        public override void ScoreMoves() { /* TODO */ }

        public override void CommandShips() {
            Log.LogMessage("combat logic");
            if(TotalPredictions > 0) {
                Log.LogMessage($"Correct Predictions {CorrectPredictions} out of a total {TotalPredictions} for a percent {((double)CorrectPredictions)/((double)TotalPredictions)}");
            }
            foreach(var ship in UnusedShips) {
                var Xcells = GameInfo.Map.GetXLayers(ship.position, 2).Where(c => c.IsOccupiedByOpponent()); // all crashable opponents
                Xcells = Xcells.Where(x => GetCrashValue(ship, x.ship) > 500);
                Xcells = Xcells.Where(x => !CollisionCells.Contains(EnemyShipInfo[x.ship.Id].PredictedTargetCell)); // eliminate collision cells
                Xcells = Xcells.OrderByDescending(x => GetCrashValue(ship, x.ship));
                Xcells.ToList().ForEach(x => Log.LogMessage($"ship {x.ship.Id} has a crash value of {GetCrashValue(ship, x.ship)} and is predicted to move {EnemyShipInfo[x.ship.Id].PredictedMove.ToString("g")}"));

                // at this point, we have all ships within 2 units worth crashing and their predicted target cells
                var neighbors = ship.Neighbors;
                var best = Xcells.FirstOrDefault(x => neighbors.Contains(EnemyShipInfo[x.ship.Id].PredictedTargetCell)); // narrow it down to expected moves

                if(best != null) {
                    var info = EnemyShipInfo[best.ship.Id];
                    MakeMove(ship.Move(EnemyShipInfo[best.ship.Id].PredictedTargetCell.position.GetDirectionTo(ship.position)), $"attempting to crash ship {Xcells.First().ship.Id} whose predicted target is {info.PredictedTargetCell.position.x},{info.PredictedTargetCell.position.y}");
                }
            }
        }

        // I don't actually care how much halite each player has
        // todo, doesn't consider if opponent could get my cargo
        private int GetCrashValue(Ship mine, Ship enemy) {
            //if(mine.halite > 500)
                //return false;
            var opponent = GameInfo.GetPlayer(enemy.owner.id);
            var myValue = Me.ships.Count * baseShipValue;  // net est value of fleet
            var oppValue = opponent.ships.Count * baseShipValue; // net est value of fleet
            var myReducedValue = (Me.ships.Count-1) * baseShipValueReducedBy2;
            var oppReducedValue = (opponent.ships.Count-1) * baseShipValueReducedBy2;
            var myLoss = (myValue - myReducedValue) + mine.halite; // new value loss for me
            var oppLoss = (oppValue - oppReducedValue) + enemy.halite; // the net value loss for opponent
            //tood vqlue is also 25% of cell

            return oppLoss - myLoss;
        }
        

        private void CalculateProjectedShipValues() {
            int numShips = GameInfo.TotalShipsCount;
            numShips = Math.Max(numShips, 1);
            int numCells = Map.width * Map.height;
            int haliteRemaining = Map.HaliteRemaining;
            // crashing a ship makes all remaining ships more valuable
            for(int i=0; i<GameInfo.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .08 * haliteRemaining / numCells);
                haliteRemaining -= haliteCollectable;
            }
            baseShipValue = (Map.HaliteRemaining - haliteRemaining) / numShips;
            Log.LogMessage("Base Ship value = " + baseShipValue);

            numShips -= 2;
            numShips = Math.Max(numShips, 1);
            int haliteRemaining2 = Map.HaliteRemaining;
            for(int i=0; i<GameInfo.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .08 * haliteRemaining2 / numCells);
                haliteRemaining2 -= haliteCollectable;
            }
            baseShipValueReducedBy2 = (Map.HaliteRemaining - haliteRemaining2) / numShips;
            Log.LogMessage("baseShipValueReducedBy2 Ship value = " + baseShipValueReducedBy2);
        }
    }
}





                // Logic to defend a good space
                /* if(ship.DistanceToDropoff <= 6 && ship.Neighbors.Any(x => x.halite > Map.AverageHalitePerCell && x.IsOccupiedByOpponent())) {
                    var info = new XLayersInfo(4, ship.position);
                    foreach(var n in ship.CurrentMapCell.Neighbors.Where(x => x.IsOccupiedByOpponent()).OrderByDescending(x => x.ship.halite + x.halite)) {
                        bool shipIsWorthCrashing = n.ship.halite + n.halite * .25 > ship.halite * ship.CellHalite * .25;
                        bool iHaveMoreShips = info.MyShipMargin >= 0;
                        bool iHaveMoreMoneyAndEnoughShips = info.MyShipRatio >= .8 && (Me.ships.Sum(s => s.Value.halite + 1000) + Me.halite) * 1.1 > 
                                                            GameInfo.Opponents[0].ships.Sum(s => s.Value.halite + 1000) + GameInfo.Opponents[0].halite;
                        if(shipIsWorthCrashing && (iHaveMoreShips || iHaveMoreMoneyAndEnoughShips)) {
                            var d = n.position.GetDirectionTo(ship.position);
                            if(IsSafeMove(ship, d, true)) {
                                MakeMove(ship.Move(d), " purposefully trying to crash enemy ship");
                                cont = true;
                                break;
                            }
                        }
                    }
                }*/

                                // other method...
                /* if(ship.Neighbors.Any(n => n.halite > MyBot.GameMap.AverageHalitePerCell * 20 && n.IsOccupiedByOpponent())) {
                    var info = new XLayersInfo(4, ship.position);
                    if(info.MyShipMargin >= 2 || (info.NumMyShips == 2 && info.NumEnemyShips == 1)) {
                        var first = ship.Neighbors.First(n => n.halite > MyBot.GameMap.AverageHalitePerCell * 20 && n.IsOccupiedByOpponent());
                        var dir = first.position.GetDirectionTo(ship.position);
                        if(IsSafeMove(ship, dir, true)) {
                            MakeMove(ship.Move(dir), "combat logic from first block");
                            continue;
                        }
                    }
                }*/

                // Logic to avoid being collided with
                /* if(Me.ships.Count * 1.1 >= MyBot.game.Opponents.Sum(o => o.ships.Count) && ship.halite > 300) {
                    var info = new XLayersInfo(4, ship.position);
                    MapCell oCell = ship.Neighbors.FirstOrDefault(n => n.IsOccupiedByOpponent() && n.ship.halite/2 > ship.halite && IsSafeMove(ship, n.position.GetDirectionTo(ship.position)));
                    if(oCell != null && info.MyShipRatio >= 1.5) {
                        MakeMove(ship.Move(ship.position.GetDirectionTo(oCell.position)), "combat logic from 2nd block");
                        continue;
                    }
                }*/

                // todo consider whether cell is an empty collision cell, and also sort the enemy ships by the crash value
                /* var bestNeighborToCrash = ship.Neighbors.FirstOrDefault(n => n.IsOccupiedByOpponent() && ShouldCrashShip(ship, n.ship) && PredictedEnemyMove(n.ship) == Direction.STILL);
                if(bestNeighborToCrash != null) {
                    MakeMove(ship.Move(bestNeighborToCrash.position.GetDirectionTo(ship.position)), "simple but awesome.. crash command");
                    cont = true;
                    break;
                }
                if(cont)
                    continue;

                var allEnemies = ship.CurrentMapCell.Corners.Where(c => c.IsOccupiedByOpponent() && ShouldCrashShip(ship, c.ship)).Select(x => x.ship);
                foreach(var enemy in allEnemies) {
                    var enemyDrop = ship.ClosestEnemyDropoff(enemy.owner.id);
                    int dx = Map.DeltaX(ship, enemyDrop);
                    int dy = Map.DeltaY(ship, enemyDrop);
                    int ex = Map.DeltaX(ship, enemy);
                    int ey = Map.DeltaY(ship, enemy);
                    var directions = enemy.position.GetAllDirectionsTo(ship.position);

                    // check if my ship separates the enemy and the drop
                    if((ex < 0 && dx >= 0 || ex > 0 && dx <= 0) && (ey < 0 && dy >= 0 || ey > 0 && dy <= 0)) {
                        directions = directions.OrderBy(x => Map.At(ship.position.DirectionalOffset(x)).halite).ToList();
                        foreach(var d in directions) {
                            if(IsSafeMove(ship, d, true)) {
                                MakeMove(ship.Move(d), "combat logic, trying to intersect a ship where we split corners");
                                cont = true;
                                break;
                            }
                        }
                    } else if((ex < 0 && dx >= 0 || ex > 0 && dx <= 0) && Map.DeltaY(enemy, enemyDrop) == 0) {
                        // in this case, we can intersect a ship moving horizontally
                        directions = directions.Where(d => d == Direction.NORTH || d == Direction.SOUTH).ToList();
                        foreach(var d in directions) {
                            if(IsSafeMove(ship, d, true)) {
                                MakeMove(ship.Move(d), "combat logic, trying to intersect a ship moving horizontally");
                                cont = true;
                                break;
                            }
                        }
                    } else if((ey < 0 && dy >= 0 || ey > 0 && dy <= 0) && Map.DeltaX(enemy, enemyDrop) == 0) {
                        // in this case, we can intersect a ship moving vertically
                        directions = directions.Where(d => d == Direction.EAST || d == Direction.WEST).ToList();
                        foreach(var d in directions) {
                            if(IsSafeMove(ship, d, true)) {
                                MakeMove(ship.Move(d), "combat logic, trying to intersect a ship moving vertically");
                                cont = true;
                                break;
                            }
                        }
                    }
                    if(cont)
                        break;
                }
                if(cont)
                    continue;*/