using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CombatLogic : Logic
    {
        public override void ProcessTurn() { }

        public override void CommandShips() {
            // see if we can disrupt an opponent
            foreach(var s in Fleet.AvailableShips) {
                var occupiedNeighbors = s.CurrentMapCell.Neighbors.Where(n => n.IsOccupiedByOpponent && !EnemyFleet.IsReturningHome(n.ship));
                MapCell bestMove = null;
                double best = 0.0;
                foreach(var n in occupiedNeighbors) {
                    var zone = new Zone(n.position, 5);
                    if(zone.SafetyRatio < MyBot.HParams[Parameters.SAFETY_RATIO])
                        continue;
                    if(GameInfo.MyShipsCount * 1.1 < GameInfo.OpponentShipsCount)
                        continue;
                    if(n.halite < s.CellHalite)
                        continue;
                    if(GameInfo.LowestNeighboringOpponentHalite(n) < s.halite)
                        continue;
                    if(Safety.IsSafeMove(s, n)) {
                        var val = (n.halite * .25 + n.ship.halite) - (s.CellHalite * .25 + s.halite);
                        if(val > best) {
                            bestMove = n;
                            best = val;
                        }
                    }
                }
                if(bestMove != null) {
                    Fleet.AddMove(s.Move(bestMove, "trying to disrupt opponent from Combat logic"));
                }
            }
        }
    }
}