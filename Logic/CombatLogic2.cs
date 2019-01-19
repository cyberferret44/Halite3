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

            if(FleetCombatScores.RecoveryScores.Count < Fleet.ShipCount)
                throw new Exception("this absoltuely positively should not happen");
        }
        public override void CommandShips()
        {
            foreach(var s in Fleet.AvailableShips) {

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