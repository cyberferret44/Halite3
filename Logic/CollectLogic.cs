using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CollectLogic : Logic {
        private static int NumToIgnore;
        private static double Degradation;
        private HashSet<Point> Wall = new HashSet<Point>();
        private Dictionary<int, Point?> Assignments = new Dictionary<int, Point?>();
        private bool HasChanged = false;

        private int TotalWallCells => Assignments.Values.Where(v => v != null).Count() + Wall.Count;
        private void Assign(Ship ship, Point? point) => Assignments[ship.Id] = point;
        private void Unassign(int shipId) => Assignments.Remove(shipId);

        public override void Initialize() {
            Degradation = HParams[Parameters.CELL_VALUE_DEGRADATION];
            var cellsOrdered = Map.GetAllCells().OrderByDescending(x => x.halite).ToList();
            cellsOrdered = cellsOrdered.Take(cellsOrdered.Count * 2 / 3).ToList();
            NumToIgnore = (int)(cellsOrdered.Average(c => c.halite) * HParams[Parameters.PERCENT_OF_AVERAGE_TO_IGNORE]);
        }

        public override void ProcessTurn() {
            // Reset Variables
            Wall.Clear();
            CreateWall();

            // if cells run out todo consider opponent ship count instead of just my own
            if(!HasChanged && TotalWallCells < Me.ShipsSorted.Count * (MyBot.game.Opponents.Count + 1) && MyBot.game.turnNumber > 30) {
                HasChanged = true;
                NumToIgnore /= 5;
            }

            if(HasChanged && TotalWallCells < Me.ShipsSorted.Count/2) {
                NumToIgnore = 1;
            }
        }

        public override void CommandShips() {
            // add accounted for ships
            UnassignUnavailableShips();

            // We want to swap assignments if a ship is already on someone elses target, it's a waste to try to track elsewhere
            foreach(var id in Assignments.Keys.ToList()) {
                var ship = Me.GetShipById(id);
                foreach(var kvp in Assignments.ToList()) {
                    if(kvp.Value != null && kvp.Value.HasValue && ship.position.AsPoint.Equals(kvp.Value.Value)) {
                        // swap assignments
                        var temp = Assignments[id];
                        Assignments[id] = kvp.Value;
                        Assignments[kvp.Key] = temp;
                    }
                }
            }
            this.ScoreMoves();


            // todo consider the opportunity cost of a move.  if this move was value 100 and it's next best was value 95
            // but another ship valued this at 90 and it's next best was 50, then the later should get the move
            while(Scores.Moves.Count > 0) {
                ScoredMoves moves = Scores.GetBestAvailableMove();
                var best = moves.BestMove;
                var text = $" Collect Command. Value: {best.MoveValue}. Other options were ";
                moves.Scores.ToList().OrderByDescending(x => x.Value).ToList().ForEach(kvp => text += $"... {kvp.Key.ToString("g")}:{kvp.Value}");
                if(Assignments[best.Ship.Id] != null)
                    text += " | target was " + Assignments[best.Ship.Id].Value.x + "," + Assignments[best.Ship.Id].Value.y;
                else
                    text += " | target was null";

                MakeMove(best.Ship.Move(best.Direction), text);
            }
        }

        /// This method will unassign any ships not available in the list
        private void UnassignUnavailableShips() {
            foreach(var key in Assignments.Keys.ToList()) {
                if(!UnusedShips.Any(s => s.Id == key)) {
                    Unassign(key);
                }
            }
        }

        private double CellValue(Position pos, MapCell cell) {
            int dist = Map.CalculateDistance(pos, cell.position);
            var neighbors = Map.GetXLayers(cell.position, 3); // todo magic number
            var vals = neighbors.Select(n => n.halite / (Map.CalculateDistance(n.position, cell.position) + 1));
            var sum = vals.OrderByDescending(v => v).Take(neighbors.Count/2).Sum(v => v);
            return sum * Math.Pow(Degradation, dist);
        }

        private double CellValueForScore(Position pos, MapCell cell) {
            var val = CellValue(pos, cell);
            return val / 12;
        }

        private Position GetBestMoveTarget(Ship ship) {
            // if there's a collided cell nearby, target it
            var cells = Map.GetXLayers(ship.position, 3);
            foreach(var c in cells) {
                if(c.halite > Map.AverageHalitePerCell * 5 && c.halite / 3 > ship.CellHalite && c.ClosestShips(UnusedShips).Contains(ship)) {
                    Assign(ship, c.position.AsPoint);
                    break;
                }
            }

            // if not assigned, assign
            if(!Assignments.ContainsKey(ship.Id) || Assignments[ship.Id] == null) {
                Point? best = null;
                if(Wall.Count > 0) {
                    best = Wall.OrderByDescending(cell => CellValue(ship.position, Map.At(new Position(cell.x,cell.y)))).First();
                    Wall.Remove(best.Value);
                }
                Assign(ship, best);
            }

            // from assignment, move to position
            if(Assignments[ship.Id] != null) {
                var p = Assignments[ship.Id];
                return new Position(p.Value.x, p.Value.y);
            } else {
                return ship.position;
            }
        }

        private void CreateWall() {
            foreach(var cell in Map.GetAllCells()) {
                bool assigned = Assignments.ContainsValue(cell.position.AsPoint);
                if(cell.halite >= NumToIgnore) {
                    if(!assigned) {
                        Wall.Add(cell.position.AsPoint);
                    }
                } else {
                    if(assigned) {
                        var element = Assignments.First(a => a.Value.HasValue && a.Value.Value.Equals(cell.position.AsPoint));
                        Unassign(element.Key);
                    }
                }
            }
        }

        // Make some magic happen
        public override void ScoreMoves() {
            foreach(var ship in UnusedShips) {
                Log.LogMessage($"Scoring {ship.Id}");
                var bestTarget = GetBestMoveTarget(ship);
                var directionsToBestTarget = bestTarget.GetAllDirectionsTo(ship.position);
                var costToMove = ship.CellHalite / 10 * .35;
                double value = 0.0;
                int curDistToTarget = MyBot.GameMap.CalculateDistance(bestTarget, ship.CurrentMapCell.position);
                foreach(var d in DirectionExtensions.ALL_DIRECTIONS) {
                    // some important varibales
                    var target = Map.At(ship, d);

                    // Case one, ship cannot sit on a drop
                    if(d == Direction.STILL && ship.OnDropoff) {
                        Scores.RemoveValue(ship, d);
                        continue;
                    }
                    // todo wont work for 4 players
                    if(target.IsOccupiedByOpponent() && (target.ship.halite < ship.halite)) {
                        Scores.RemoveValue(ship, d);
                        continue;
                    }

                    if(d == Direction.STILL) {
                        value = .25 * target.halite;
                        value *= (value > NumToIgnore ? HParams[Parameters.COLLECT_STICKINESS] : .3);
                    } else {
                        value = (.125 * target.halite) - costToMove;
                    }
                    value = Math.Max(value, 1.0);
                    value += CellValueForScore(target.position, Map.At(bestTarget));

                    if(d == Direction.STILL && curDistToTarget == 0) {
                        value += 10;
                        value *= 5;
                    }
                    int newDistToTarget = MyBot.GameMap.CalculateDistance(target.position, bestTarget);
                    if(newDistToTarget < curDistToTarget) {
                        value *= 2;
                    }

                    if(target.IsInspired) {
                        value *= 5;
                    }

                    if(target.ThreatenedBy.Count > 0 && target.ThreatenedBy.Any(threat => threat.halite < ship.halite)) {
                        value *= .2;
                    }

                    if(target.ThreatenedBy.Count > 0 && target.ThreatenedBy.All(threat => threat.halite > ship.halite + ship.CellHalite * .25)) {
                        value *= 2;
                    }

                    /*if(MyBot.game.Opponents.Count() == 1 && ship.CurrentMapCell.IsThreatened) {
                        var opponent = MyBot.game.Opponents[0];
                        var lowestShip = ship.CurrentMapCell.Neighbors.Where(n => n.IsOccupiedByOpponent()).OrderBy(n => n.ship.halite).First();
                        
                        if(ship.halite + costToMove < lowestShip.halite) {
                            value *= 2 * ((1000 + lowestShip.halite) / (1000 + ship.halite + costToMove));
                        } else {
                            value *= .5 * ((1000 + lowestShip.halite) / (1000 + ship.halite + costToMove));
                        }
                    }*/

                    Log.LogMessage($"Direction: {d.ToString("g")}   Value: {value} besttarget:{bestTarget.x},{bestTarget.y}");
                    Scores.AddMove(ship, d, value);
                }
            }
        }
    }
}