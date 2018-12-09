using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    public class CombatLogic : Logic {
        public override void Initialize() { /* TODO */ }
        public override void ProcessTurn() { /* TODO */ }

        public override void CommandShips(List<Ship> ships) {
            foreach(var ship in ships) {
                var info = new XLayersInfo(3, ship.position);
                if(ship.Neighbors.Any(n => n.halite > MyBot.GameMap.AverageHalitePerCell * 20 && n.IsOccupiedByOpponent())) {
                    if(info.MyShipMargin >= 2 || (info.NumMyShips == 2 && info.NumEnemyShips == 1)) {
                        var first = ship.Neighbors.First(n => n.halite > MyBot.GameMap.AverageHalitePerCell * 20 && n.IsOccupiedByOpponent());
                        var dir = ship.position.GetDirectionTo(first.position);
                        if(IsSafeMove(ship, dir)) {
                            MyBot.MakeMove(ship.Move(dir));
                            continue;
                        }
                    }
                }

                if(Me.ships.Count * 1.1 >= MyBot.game.Opponents.Sum(o => o.ships.Count)) {
                    MapCell oCell = ship.Neighbors.FirstOrDefault(n => n.IsOccupiedByOpponent() && n.ship.halite > 300 && n.ship.halite/2 > ship.halite);
                    if(oCell != null && info.MyShipRatio >= 1.5) {
                        MyBot.MakeMove(ship.Move(ship.position.GetDirectionTo(oCell.position)));
                        break;
                    }
                }
            }
        }
    }
}