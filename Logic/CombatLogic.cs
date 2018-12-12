using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    public class CombatLogic : Logic {
        public override void Initialize() { /* TODO */ }
        public override void ProcessTurn() { /* TODO */ }
        public override void ScoreMoves() { /* TODO */ }

        public override void CommandShips() {
            Log.LogMessage("combat logic");
            foreach(var ship in UnusedShips) {
                // Logic to defend a good space
                if(MyBot.game.Opponents.Count == 1 && ship.DistanceToDropoff <= 6 && ship.Neighbors.Any(x => x.halite > Map.AverageHalitePerCell && x.IsOccupiedByOpponent())) {
                    var info = new XLayersInfo(4, ship.position);
                    bool cont = false;
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
                    if(cont)
                        continue;
                }

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
    }
}