using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    public class CombatLogic : Logic {
        private bool Is2Player = true;
        public override void Initialize() { 
            if(MyBot.game.Opponents.Count() > 1) {
                Is2Player = false;
            }
        }
        public override void ProcessTurn() { /* TODO */ }
        public override void ScoreMoves() { /* TODO */ }

        public override void CommandShips() {
            Log.LogMessage("combat logic");
            bool cont = false;
            foreach(var ship in UnusedShips) {
                // Logic to defend a good space
                if(MyBot.game.Opponents.Count == 1 && ship.DistanceToDropoff <= 6 && ship.Neighbors.Any(x => x.halite > Map.AverageHalitePerCell && x.IsOccupiedByOpponent())) {
                    var info = new XLayersInfo(4, ship.position);
                    foreach(var n in ship.CurrentMapCell.Neighbors.Where(x => x.IsOccupiedByOpponent()).OrderByDescending(x => x.ship.halite + x.halite)) {
                        bool shipIsWorthCrashing = n.ship.halite + n.halite * .25 > ship.halite * ship.CellHalite * .25;
                        bool iHaveMoreShips = info.MyShipMargin >= 0;
                        bool iHaveMoreMoneyAndEnoughShips = info.MyShipRatio >= .8 && (Me.ships.Sum(s => s.Value.halite + 1000) + Me.halite) * 1.1 > 
                                                            MyBot.game.Opponents[0].ships.Sum(s => s.Value.halite + 1000) + MyBot.game.Opponents[0].halite;
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

        private bool ShouldCrashShip(Ship mine, Ship enemy) {
            return Is2Player && enemy.halite > 700 && mine.halite/2 < enemy.halite; //
        }
    }
}