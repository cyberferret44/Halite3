using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class CombatLogic : Logic {
        public override void Initialize() { /* TODO */ }

        public int baseShipValue;
        public int baseShipValueReducedBy2;
        public override void ProcessTurn() { 
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
        public override void ScoreMoves() { /* TODO */ }

        public override void CommandShips() {
            Log.LogMessage("combat logic");
            bool cont = false;
            foreach(var ship in UnusedShips) {
                // Logic to defend a good space
                if(GameInfo.Opponents.Count == 1 && ship.DistanceToDropoff <= 6 && ship.Neighbors.Any(x => x.halite > Map.AverageHalitePerCell && x.IsOccupiedByOpponent())) {
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
                }
                if(cont)
                    continue;

                if(ship.CurrentMapCell.Corners.Any(c => c.IsOccupiedByOpponent() && ShouldCrashShip(ship, c.ship))) {
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
                }
                if(cont)
                    continue;

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
            }
        }

        // I don't actually care how much halite each player has
        // todo, doesn't consider if opponent could get my cargo
        private bool ShouldCrashShip(Ship mine, Ship enemy) {
            //if(mine.halite > 500)
                //return false;
            var opponent = GameInfo.GetPlayer(enemy.owner.id);
            var myValue = Me.ships.Count * baseShipValue;  // net est value of fleet
            var oppValue = opponent.ships.Count * baseShipValue; // net est value of fleet
            var myReducedValue = (Me.ships.Count-1) * baseShipValueReducedBy2;
            var oppReducedValue = (opponent.ships.Count-1) * baseShipValueReducedBy2;
            var myLoss = (myValue - myReducedValue) + mine.halite; // new value loss for me
            var oppLoss = (oppValue - oppReducedValue) + enemy.halite; // the net value loss for opponent

            if(oppLoss - myLoss > 500) {
                Log.LogMessage($"Ship {mine.Id} was told to crash {enemy.Id} with a net loss positive as {oppLoss - myLoss}");
                return true;
            }

            return false;
        }
    }
}