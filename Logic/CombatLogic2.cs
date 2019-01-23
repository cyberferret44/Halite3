using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CombatLogic2 : Logic
    {
        public override void ProcessTurn()
        {
            FleetCombatScores.RecoveryScores.Clear();
            foreach(var ship in Fleet.AllShips) {
                FleetCombatScores.RecoveryScores[ship.Id] = new CombatScores(ship);
                foreach(var n in ship.CurrentMapCell.NeighborsAndSelf.Where(x => x.IsThreatened)) {
                    var zone = new Zone(n.position, 5);
                    var lowestEnemy = GameInfo.LowestNeighboringOpponentShip(n);
                    var d = n.position.GetDirectionTo(ship.position);
                    FleetCombatScores.RecoveryScores[ship.Id].Scores[d] = zone.CargoRecoveryLikelihood(ship, lowestEnemy);
                }
            }
        }
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

    public class CombatScores {
        public CombatScores(Ship ship) {
            this.Ship = ship;
            this.Scores = new Dictionary<Direction, double>();
            foreach(var d in DirectionExtensions.ALL_DIRECTIONS) {
                Scores.Add(d, 1.0);
            }
        }
        public Ship Ship;
        public Dictionary<Direction, double> Scores;
    }

    public static class FleetCombatScores {
        public static Dictionary<int, CombatScores> RecoveryScores = new Dictionary<int, CombatScores>();
        public static double RecoveryChance(Ship s, Direction d) => asdf(s, d);
        private static double asdf(Ship s, Direction d) {
            if(!RecoveryScores.ContainsKey(s.Id)) {
                Log.LogMessage("asdf");
            }
            var r1 = RecoveryScores[s.Id];
            return r1.Scores[d];
        }
    }
}