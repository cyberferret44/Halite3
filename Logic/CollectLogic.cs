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

        public override void Initialize() {
            Degradation = HParams[Parameters.CELL_VALUE_DEGRADATION];
            var cellsOrdered = Map.GetAllCells().OrderByDescending(x => x.halite).ToList();
            cellsOrdered = cellsOrdered.Take(cellsOrdered.Count * 2 / 3).ToList();
            NumToIgnore = (int)(cellsOrdered.Average(c => c.halite) * HParams[Parameters.PERCENT_OF_AVERAGE_TO_IGNORE]);
            Log.LogMessage($"Degradation: {Degradation}");
            Log.LogMessage($"NumToIgnore: {NumToIgnore}");
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
            this.ScoreMoves();

            while(Scores.Moves.Count > 0) {
                ScoredMove move = Scores.GetBestAvailableMove();
                MakeMove(move.Ship.Move(move.Direction), $" command from new thing with a value of {move.MoveValue}");
            }
        }

        /// This method will unassign any ships not available in the list
        private void UnassignUnavailableShips() {
            foreach(var key in Assignments.Keys.ToList()) {
                if(UsedShips.Contains(key)) {
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
            var info = new XLayersInfo(3, ship.position);
            foreach(var cell in info.AllCells) {
                if(cell.halite > Map.AverageHalitePerCell * 20 && info.MyClosestShip().Id == ship.Id) {
                    Assign(ship, cell.position.AsPoint);
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

        private void Assign(Ship ship, Point? point) {
            Assignments[ship.Id] = point;
        }
        private void Unassign(int shipId) {
            Assignments[shipId] = null;
        }

        // Make some magic happen
        public override void ScoreMoves() {
            foreach(var ship in UnusedShips) {
                Log.LogMessage($"Scoring {ship.Id}");
                var bestTarget = GetBestMoveTarget(ship);
                var directionsToBestTarget = bestTarget.GetAllDirectionsTo(ship.position);
                var costToMove = ship.CurrentMapCell.halite / 10 * .35;
                double value = 0.0;
                int curDistToTarget = MyBot.GameMap.CalculateDistance(bestTarget, ship.CurrentMapCell.position);
                foreach(var d in DirectionExtensions.ALL_DIRECTIONS) {
                    // some important varibales
                    var target = Map.At(ship.position.DirectionalOffset(d));

                    // Case one, ship cannot sit on a drop
                    if(d == Direction.STILL && ship.OnDropoff) {
                        Scores.RemoveValue(ship, d);
                        continue;
                    }
                    // todo wont work for 4 players
                    if(target.IsOccupiedByOpponent() && target.ship.halite < ship.halite) {
                        Scores.RemoveValue(ship, d);
                        continue;
                    }

                    if(d == Direction.STILL) {
                        value = .25 * target.halite;
                        value *= (value > NumToIgnore ? 30 : .5);
                        
                    } else {
                        value = (.1 * target.halite) - costToMove;
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
                    } else if(newDistToTarget > curDistToTarget) {
                        value *= .5;
                    }

                    if(target.IsInspired) {
                        value *= 3;
                    }

                    if(target.ThreatenedBy.Count > 0 && target.ThreatenedBy.Any(threat => threat.halite < ship.halite)) {
                        value *= .5;
                    }

                    if(target.ThreatenedBy.Count > 0 && target.ThreatenedBy.All(threat => threat.halite > ship.halite)) {
                        value *= .5;
                    }

                    /* if(MyBot.game.Opponents.Count() == 1) {
                        var opponent = MyBot.game.Opponents[0];
                        if((Me.halite - opponent.halite) / 1000 + (Me.ShipsSorted.Count - opponent.ships.Values.Count()) > 0) {
                            value *= 2;
                        } else  {
                            value *= .5;
                        }
                    }*/

                    Log.LogMessage($"Direction: {d.ToString("g")}   Value: {value} besttarget:{bestTarget.x},{bestTarget.y}");
                    Scores.AddMove(ship, d, value);
                }
            }
        }
    }
}