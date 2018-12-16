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

    // TODO if target cell value < targettoignore, prioritize it for less loss
    // TODO if moving off of a base, prioritize any square value < 10; but onlu glightly
        private int TotalWallCells => Assignments.Values.Where(v => v != null).Count() + Wall.Count;
        private void Assign(Ship ship, Point? point)  {
            if(point.HasValue && IsAssigned(point.Value)) {
                throw new Exception("a ship was attempted to be assigned to the same location twice");
            }
            Assignments[ship.Id] = point;
            if(point.HasValue && Wall.Contains(point.Value)) {
                Wall.Remove(point.Value);
            }
            if(point.HasValue) {
                Log.LogMessage($"ship {ship.Id} was assigned to {point.Value.x},{point.Value.y}");
            }
        }
        private void Unassign(int shipId) => Assignments.Remove(shipId);
        private bool IsAssigned(Point p) => Assignments.Values.Any(v => v.HasValue && v.Value.Equals(p));
        private void Swap(int one, int two) {
            Log.LogMessage($"Swapping ships {one} and {two}");
            var temp = Assignments[one];
            Assignments[one] = Assignments[two];
            Assignments[two] = temp;
        }

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
            if(!HasChanged && TotalWallCells < Me.ShipsSorted.Count * (GameInfo.Opponents.Count + 1) && GameInfo.TurnNumber > 30) {
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

            foreach(var ship in UnusedShips) {
                GetBestMoveTarget(ship);
            }

            foreach(var enemy in GameInfo.OpponentShips) {
                Scores.TapCell(enemy.CurrentMapCell);
            }

            // We want to swap assignments if a ship is already on someone elses target, it's a waste to try to track elsewhere
            foreach(var id in Assignments.Keys.ToList()) {
                var ship = Me.GetShipById(id);
                var targetPos = Assignments[id].HasValue ? new Position(Assignments[id].Value.x, Assignments[id].Value.y) : null;

                // if the current cell is more valuable than our target cell, reassign
                if((targetPos != null && !ship.OnDropoff && ship.CellHalite * 1.1 > Map.At(targetPos).halite) && Wall.Contains(ship.position.AsPoint)) {
                    targetPos = ship.position;
                    Assign(ship, targetPos.AsPoint);
                }

                // if on someone elses assignment, swap the assignments
                foreach(var kvp in Assignments.ToList()) {
                    if(kvp.Value != null && kvp.Key != ship.Id && kvp.Value.HasValue && ship.position.AsPoint.Equals(kvp.Value.Value)) {
                        // swap assignments
                        Assignments[kvp.Key] = null;
                        Assign(ship, kvp.Value);
                        break;
                    }
                }
            }
            this.ScoreMoves();


            // todo consider the opportunity cost of a move.  if this move was value 100 and it's next best was value 95
            // but another ship valued this at 90 and it's next best was 50, then the later should get the move
            while(Scores.Moves.Count > 0) {
                ScoredMoves moves = Scores.GetBestAvailableMove();
                var best = moves.BestMove();
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
            //if(cell.halite > NumToIgnore)
            //    dist = Math.Min(dist, 1 + GameInfo.Distance(cell.position, GameInfo.MyClosestDrop(cell.position))); // prioritize valuable cells near my dropoffs
            var neighbors = Map.GetXLayers(cell.position, 3); // todo magic number
            var vals = neighbors.Select(n => n.halite / (Map.CalculateDistance(n.position, cell.position) + 1));
            var sum = vals.OrderByDescending(v => v).Take(neighbors.Count/2).Sum(v => v);
            return sum * Math.Pow(Degradation, dist);
        }

        private double CellValueForScore(Position pos, MapCell cell) {
            var val = CellValue(pos, cell);
            return val / 12;
        }

        private bool IsAccessible(Ship ship, Point targetPoint) {
            if(GameInfo.Distance(ship, new Position(targetPoint.x, targetPoint.y)) <= 1)
                return true;
            var target = GameInfo.CellAt(new Position(targetPoint.x, targetPoint.y));
            var dirsToShip = ship.position.GetAllDirectionsTo(target.position);
            var neighbors = dirsToShip.Select(d => GameInfo.CellAt(target.position.DirectionalOffset(d)));
            if(neighbors.All(n => n.IsOccupiedByMe() || Assignments.Values.Any(x => x.HasValue && x.Value.Equals(n.position.AsPoint)))) {
                return false;
            }
            return true;
        }

        private Position GetBestMoveTarget(Ship ship) {
            if(Assignments.ContainsKey(ship.Id) && Assignments[ship.Id].HasValue && !IsAccessible(ship, Assignments[ship.Id].Value)) {
                Log.LogMessage(ship.Id + $" was unassigned from new block ({Assignments[ship.Id].Value.x},{Assignments[ship.Id].Value.y})");
                Unassign(ship.Id);
            }

            // if there's a collided cell nearby, target it
            var cells = Map.GetXLayers(ship.position, 3);
            foreach(var c in cells) {
                if(c.halite > Map.AverageHalitePerCell * 5 && c.halite / 3 > ship.CellHalite && c.ClosestShips(UnusedShips).Contains(ship) && !IsAssigned(c.position.AsPoint)) {
                    Assign(ship, c.position.AsPoint);
                    break;
                }
            }

            // if not assigned, assign
            if(!Assignments.ContainsKey(ship.Id) || Assignments[ship.Id] == null) {
                Point? best = null;
                bool anyAccessible = false;
                double maxVal = -1;
                foreach(var point in Wall) {
                    double value =  CellValue(ship.position, Map.At(new Position(point.x,point.y)));
                    bool accessible = IsAccessible(ship, point);
                    if(accessible) {
                        anyAccessible = true;
                    }
                    if(value > maxVal && (!anyAccessible || accessible)) {
                        best = point;
                        maxVal = value;
                    }
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
                int curDistToTarget = GameInfo.Distance(bestTarget, ship);
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

                    // set the initial value
                    if(d == Direction.STILL) {
                        value = .25 * target.halite;
                        value *= (value > NumToIgnore ? HParams[Parameters.COLLECT_STICKINESS] : .6);
                    } else {
                        value = (.125 * target.halite) - costToMove;
                    }
                    value = Math.Max(value, 1.0);
                    value += CellValueForScore(target.position, Map.At(bestTarget));


                    if(d == Direction.STILL && curDistToTarget == 0) {
                        value += 10;
                        value *= 4;
                    }
                    /* int newDistToTarget = GameInfo.Distance(target.position, bestTarget);
                    if(newDistToTarget < curDistToTarget) {
                        value *= 2;
                    }*/

                    // prioritize inspired cells
                    if(target.IsInspired) {
                        value *= 15;
                    }

                    // avoid a cell if we're more valuable than the worst opponent
                    if(target.ThreatenedBy.Count > 0 && target.ThreatenedBy.Any(threat => threat.halite < ship.halite)) {
                        value *= .2;
                    }

                    // prefer a cell if we're less valuable than opponents
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