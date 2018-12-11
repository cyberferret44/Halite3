using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    public class CombatLogic : Logic {
        public override void Initialize() { /* TODO */ }
        public override void ProcessTurn() { /* TODO */ }
        public override void ScoreMoves() { /* TODO */ }

        public override void CommandShips() {
            foreach(var ship in UnusedShips) {
                var info = new XLayersInfo(3, ship.position);
                if(ship.Neighbors.Any(n => n.halite > MyBot.GameMap.AverageHalitePerCell * 20 && n.IsOccupiedByOpponent())) {
                    if(info.MyShipMargin >= 2 || (info.NumMyShips == 2 && info.NumEnemyShips == 1)) {
                        var first = ship.Neighbors.First(n => n.halite > MyBot.GameMap.AverageHalitePerCell * 20 && n.IsOccupiedByOpponent());
                        var dir = ship.position.GetDirectionTo(first.position);
                        if(IsSafeMove(ship, dir)) {
                            MakeMove(ship.Move(dir), "combat logic from first block");
                            continue;
                        }
                    }
                }

                // Logic to avoid being collided with
                if(Me.ships.Count * 1.1 >= MyBot.game.Opponents.Sum(o => o.ships.Count) && ship.halite > 300) {
                    MapCell oCell = ship.Neighbors.FirstOrDefault(n => n.IsOccupiedByOpponent() && n.ship.halite/2 > ship.halite && IsSafeMove(ship, n.position.GetDirectionTo(ship.position)));
                    if(oCell != null && info.MyShipRatio >= 1.5) {
                        MakeMove(ship.Move(ship.position.GetDirectionTo(oCell.position)), "combat logic from 2nd block");
                        break;
                    }
                }
            }
        }
    }
}