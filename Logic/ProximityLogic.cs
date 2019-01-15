/* using Halite3;
using System.Linq;
using System.Collections.Generic;
using Halite3.hlt;

namespace Halite3.Logic {
    public class ProximityLogic : Logic
    {
        public override void ProcessTurn()
        {
            // do nothing...
        }

        public override void CommandShips()
        {
            /var shipsToUse = Fleet.AvailableShips.Where(s => s.CurrentMapCell.Neighbors.Any(n => n.IsInspired)).ToHashSet();
            foreach(var s in Fleet.AvailableShips) {
                foreach(var n in s.CurrentMapCell.NeighborsAndSelf) {
                    var cv = new CellValuer(n);
                    if(cv.TurnsToFill(s) <= (1000 - s.halite) / (GameInfo.AverageHalitePerCell * .125)) {
                        shipsToUse.Add(s);
                        break;
                    }
                }
            }

            var scoredMoves = new List<ScoredMoves>();
            foreach(var s in shipsToUse) {
                scoredMoves.Add(new ScoredMoves(s));
            }

            while(scoredMoves.Count > 0) {
                scoredMoves = scoredMoves.OrderByDescending(sm => sm.Discrepency).ToList();
                var nextMove = scoredMoves[0].GetTarget();
                if(nextMove != null) {
                    MakeMove(nextMove);
                    ValueMapping3.AddNegativeShip(nextMove.Ship, nextMove.TargetCell);
                }
                scoredMoves.Remove(scoredMoves[0]);
            }

            // todo when moving, add negative value to value mapping
            // todo update value mapping to cut consumption by 200% when cell is inspired...
        }

        private class ScoredMoves {
            private Dictionary<MapCell, double> Scores;
            public Ship ship;
            public ScoredMoves(Ship s) {
                this.Scores = new Dictionary<MapCell, double>();
                this.ship = s;

                // TODO add logic here...
                foreach(var c in s.CurrentMapCell.NeighborsAndSelf) {
                    double val = c == ship.CurrentMapCell && c.halite > GameInfo.UpperThirdAverage ? c.halite * 3 : c.halite;
                    if(c.IsInspired)
                        val *= 3;
                    if(c.IsThreatened) {
                        var nHal = GameInfo.LowestNeighboringOpponentHaliteWhereNotReturning(c);
                        if(nHal.HasValue)
                            val *= nHal.Value / (ship.halite + 1);
                        val *= GameInfo.MyStrengthRatio(c.position, 8);
                    }
                        
                    Scores.Add(c, val);
                }
            }

            public double Discrepency => discrepency();

            private double discrepency() {
                var tmp = Scores.OrderByDescending(kvp => kvp.Value).ToList();
                tmp = tmp.Where(kvp => !Fleet.CollisionCells.Contains(kvp.Key)).ToList();
                if(tmp.Count < 2)
                    return int.MaxValue;
                else
                    return tmp[0].Value - tmp[1].Value;
            }

            public Command GetTarget() {
                var tmp = Scores.OrderByDescending(kvp => kvp.Value).ToList();
                tmp = tmp.Where(kvp => !Fleet.CollisionCells.Contains(kvp.Key)).ToList();
                if(tmp.Count > 0)
                    return ship.Move(tmp[0].Key.position.GetDirectionTo(ship.position), "Moving from proximity logic to best target");
                return null;
            }
        }
    }
}*/