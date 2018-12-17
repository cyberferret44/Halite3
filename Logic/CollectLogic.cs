using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CollectLogic : Logic {
        private static int NumToIgnore;
        private Dictionary<int, Point?> Assignments = new Dictionary<int, Point?>();
        private CollectCalculator ValueCalculator = new CollectCalculator();
        private bool HasChanged = false;
        private HashSet<Point> AssignedCells = new HashSet<Point>();
        //private bool IsAssigned(Point p) => Assignments.Values.Any(v => v.HasValue && v.Value.Equals(p));


        // TODO if target cell value < targettoignore, prioritize it for less loss
        // TODO if moving off of a base, prioritize any square value < 10; but onlu glightly
        private int TotalWallCells => Assignments.Values.Where(v => v != null).Count() + ValueCalculator.Count;
        private void Assign(Ship ship, Point? point)  {
            if(point.HasValue && AssignedCells.Contains(point.Value)) {
                //throw new Exception("a ship was attempted to be assigned to the same location twice");
            }
            Assignments[ship.Id] = point;
            /* if(point.HasValue && Wall.Contains(point.Value)) {
                Wall.Remove(point.Value);
            }*/
            if(point.HasValue) {
                AssignedCells.Add(point.Value);
            }
            if(point.HasValue) {
                Log.LogMessage($"ship {ship.Id} was assigned to {point.Value.x},{point.Value.y}");
            } else {
                Log.LogMessage($"ship {ship.Id} was assigned to null");
            }
        }
        private void Unassign(int shipId, string comment) {
            if(Assignments[shipId] != null) {
                Log.LogMessage($"Ship {shipId} was removed from {Assignments[shipId].Value.x},{Assignments[shipId].Value.y} with comment {comment}");
                AssignedCells.Remove(Assignments[shipId].Value);
            }
            Assignments[shipId] = null;
        }

        public override void Initialize() {
            var cellsOrdered = Map.GetAllCells().OrderByDescending(x => x.halite).ToList();
            cellsOrdered = cellsOrdered.Take(cellsOrdered.Count * 2 / 3).ToList();
            NumToIgnore = (int)(cellsOrdered.Average(c => c.halite) * HParams[Parameters.PERCENT_OF_AVERAGE_TO_IGNORE]);
        }

        public override void ProcessTurn() {
            if(!HasChanged && TotalWallCells < Me.ShipsSorted.Count + GameInfo.OpponentShipsCount && GameInfo.TurnNumber > 30) {
                HasChanged = true;
                NumToIgnore /= 5;
            }

            if(HasChanged && TotalWallCells < Me.ShipsSorted.Count + GameInfo.OpponentShipsCount) {
                NumToIgnore = 1;
            }

            foreach(var ship in GameInfo.MyShips) {
                if(!Assignments.ContainsKey(ship.Id)) {
                    Assign(ship, null);
                }
            }

            // clean up dead ships
            foreach(var key in Assignments.Keys.ToList()) {
                if(GameInfo.GetMyShip(key) == null) {
                    Assignments.Remove(key);
                }
            }
        }

        public override void CommandShips() {
            // ****************************************
            // **  First set up all ship assignments **
            // ****************************************

            // Unassign ships which aren't in unused
            var set = UnusedShips.Select(x => x.Id).ToHashSet();
            Assignments.Keys.Where(k => !set.Contains(k) && GameInfo.GetMyShip(k).CanMove).ToList().ForEach(k => Unassign(k, " cell used in other logic"));

            // unassign ships whose targets are below numtoignore
            foreach(var ship in UnusedShips) {
                if(Assignments[ship.Id].HasValue && GameInfo.CellAt(Assignments[ship.Id].Value.AsPosition).halite < NumToIgnore) {
                    Unassign(ship.Id, " cell value less than " + NumToIgnore);
                }
            }

            foreach(var enemy in GameInfo.OpponentShips) {
                Scores.TapCell(enemy.CurrentMapCell);
            }

            // Reset Variables
            ValueCalculator.Recalculate(AssignedCells, NumToIgnore);

            foreach(var ship in UnusedShips) {
                GetBestMoveTarget(ship);
            }

            // We want to swap assignments if a ship is already on someone elses target, it's a waste to try to track elsewhere
            foreach(var id in Assignments.Keys.ToList()) {
                var ship = Me.GetShipById(id);
                var targetPos = Assignments[id].HasValue ? new Position(Assignments[id].Value.x, Assignments[id].Value.y) : null;

                if(targetPos == null)
                    continue;
                // if the current cell is more valuable than our target cell, reassign
                if((!ship.OnDropoff && ship.CellHalite * 1.1 > Map.At(targetPos).halite) && !AssignedCells.Contains(ship.position.AsPoint)) {
                    targetPos = ship.position;
                    Assign(ship, targetPos.AsPoint);
                }

                // if on someone elses assignment, swap the assignments
                /* foreach(var kvp in Assignments.ToList()) {
                    if(kvp.Value != null && kvp.Key != ship.Id && kvp.Value.HasValue && ship.position.AsPoint.Equals(kvp.Value.Value)) {
                        // swap assignments
                        Unassign(kvp.Key);
                        Assign(ship, kvp.Value);
                        break;
                    }
                }*/

                // steal an assignment...
                var dirs = targetPos.GetAllDirectionsTo(ship.position).Where(d => GameInfo.CellAt(ship, d).IsOccupiedByMe() && GameInfo.CellAt(ship, d).ship.CanMove);
                int count = 0;
                foreach(var d in dirs) {
                    var cell = GameInfo.CellAt(ship, d);
                    if(Assignments[cell.ship.Id].HasValue && Assignments[cell.ship.Id].Value.Equals(cell.position.AsPoint)) {
                        count ++;
                    }
                }
                if(count > 0 && count == dirs.Count()) {
                    // steal one
                    int max = dirs.Max(d => GameInfo.CellAt(ship, d).halite);
                    var cellToSteal = GameInfo.CellAt(ship, dirs.First(d => max == GameInfo.CellAt(ship, d).halite));
                    Unassign(cellToSteal.ship.Id, "unassigning so I can reassign it to " + ship.Id);
                    Assign(ship, cellToSteal.position.AsPoint);
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

        /* /private double CellValue(Position pos, MapCell cell) {
            int dist = Map.CalculateDistance(pos, cell.position);
            //if(cell.halite > NumToIgnore)
            //    dist = Math.Min(dist, 1 + GameInfo.Distance(cell.position, GameInfo.MyClosestDrop(cell.position))); // prioritize valuable cells near my dropoffs
            var neighbors = Map.GetXLayers(cell.position, 3); // todo magic number
            var vals = neighbors.Select(n => n.halite / (Map.CalculateDistance(n.position, cell.position) + 1));
            var sum = vals.OrderByDescending(v => v).Take(neighbors.Count/2).Sum(v => v);
            return sum * Math.Pow(MyBot.HParams[Parameters.CELL_VALUE_DEGRADATION], dist);
        }*/

        private double CellValueForScore(Position pos, MapCell cell) {
            var val = CollectCalculator.CellValue(pos, cell);
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
            // Unassign the ship if its target is no longer available, must be here so it's current while assignments are being added
            if(Assignments[ship.Id].HasValue && !IsAccessible(ship, Assignments[ship.Id].Value)) {
                Unassign(ship.Id, " not accessbile");
            }

            // if there's an extremely valuable nearby cell (i.e. from a collision) reassign to it
            var cells = Map.GetXLayers(ship.position, 4);
            foreach(var c in cells) {
                if(c.halite > Map.AverageHalitePerCell * 5 && c.halite / 3 > ship.CellHalite && c.ClosestShips(UnusedShips).Contains(ship) && !AssignedCells.Contains(c.position.AsPoint)) {
                    Assign(ship, c.position.AsPoint);
                    break;
                }
            }

            // next, assign to a valuable neighbor
            if(!Assignments[ship.Id].HasValue) {
                cells = Map.GetXLayers(ship.position, 3);
                foreach(var cell in cells.OrderByDescending(c => CollectCalculator.CellValue(ship.position, c))) {
                    if(!AssignedCells.Contains(cell.position.AsPoint) && cell.halite > NumToIgnore && IsAccessible(ship, cell.position.AsPoint)) {
                        Assign(ship, cell.position.AsPoint);
                        break;
                    }
                }
            }
            

            // if not assigned, assign
            if(!Assignments[ship.Id].HasValue) {
                var cell = ValueCalculator.GetBest(AssignedCells, ship);
                if(cell == null) {
                    Log.LogMessage("attempted to assign ship " + ship.Id + " but returned null");
                }
                if(cell != null) {
                    Assign(ship, cell.position.AsPoint);
                }
            }
            /* if(!Assignments.ContainsKey(ship.Id) || Assignments[ship.Id] == null) {
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
            }*/

            // from assignment, move to position
            if(Assignments[ship.Id] != null) {
                var p = Assignments[ship.Id];
                return new Position(p.Value.x, p.Value.y);
            } else {
                return ship.position;
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

                // check if we're blocked from our target
                bool isBlocked =bestTarget.GetAllDirectionsTo(ship.position).All(d => GameInfo.CellAt(ship, d).IsOccupied() && GameInfo.CellAt(ship, d).halite >= NumToIgnore);

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

                    if(target.IsStructure) {
                        value *= .01;
                    }

                    // set the initial value
                    if(d == Direction.STILL) {
                        value = .25 * target.halite;
                        value *= (value > NumToIgnore ? HParams[Parameters.COLLECT_STICKINESS] : .6);
                    } else {
                        value = (.125 * target.halite) - costToMove;
                    }
                    value = Math.Max(value, 1.0);
                    value += CellValueForScore(bestTarget, target);

                    if(d == Direction.STILL && curDistToTarget == 0) {
                        value += 10;
                        value *= 4;
                    }

                    if(d == Direction.STILL && IsBlockingNeighbor(ship)) {
                        value *= .1;
                    }

                    int newDistToTarget = GameInfo.Distance(target.position, bestTarget);
                    if(newDistToTarget < curDistToTarget) {
                        value *= isBlocked ? 10 : 2;
                        if(target.halite < NumToIgnore) {
                            value *= 10 / ((int)(target.halite/10)+1);
                        }
                    }

                    if(ship.OnDropoff && target.halite < 10) {
                        value *= 1.5;
                    }

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
        private bool IsBlockingNeighbor(Ship ship) {
            var neighbors = ship.Neighbors.Where(n => n.IsOccupiedByMe() && n.ship.CanMove && Assignments[n.ship.Id].HasValue);
            foreach(Ship s in neighbors.Select(n => n.ship)) {
                // todo optimization to check if target is even split
                var path = GameInfo.CalculatePathOfLeastResistance(Assignments[s.Id].Value.AsPosition, s.position);
                if(path.Any(p => p == ship.CurrentMapCell)) {
                    return true;
                }
            }
            return false;
        }
    }

    public class CollectCalculator {
        Dictionary<Point, LinkedList<MapCell>> ValueBuckets = new Dictionary<Point, LinkedList<MapCell>>();
        private int size = 0;
        public int Count => size;

        public static double CellValue(Position pos, MapCell cell) {
            //int dist = GameInfo.Map.CalculateDistance(pos, cell.position);
            //if(cell.halite > NumToIgnore)
            //    dist = Math.Min(dist, 1 + GameInfo.Distance(cell.position, GameInfo.MyClosestDrop(cell.position))); // prioritize valuable cells near my dropoffs
            var neighbors = GameInfo.Map.GetXLayers(cell.position, 3); // todo magic number
            var vals = neighbors.Select(n => n.halite / (GameInfo.Map.CalculateDistance(n.position, cell.position) + 1));
            var sum = vals.OrderByDescending(v => v).Take(neighbors.Count/2).Sum(v => v);
            //return sum * Math.Pow(MyBot.HParams[Parameters.CELL_VALUE_DEGRADATION], dist);
            //var val = cell.halite;
            var path = GameInfo.CalculatePathOfLeastResistance(cell.position, pos);
            path.ForEach(p => sum = sum * (1000 - GameInfo.OpportunityCost - p.halite)/1000);
            return sum;
        }

        private bool IsAccessible(Ship ship, MapCell target, HashSet<Point> inaccessible) {
            if(GameInfo.Distance(ship, target.position) <= 1)
                return true;
            var dirsToShip = ship.position.GetAllDirectionsTo(target.position);
            var neighbors = dirsToShip.Select(d => GameInfo.CellAt(target.position.DirectionalOffset(d)));
            if(neighbors.All(n => n.IsOccupiedByMe() || inaccessible.Contains(n.position.AsPoint))) { // todo could maybe remove first condition
                return false;
            }
            return true;
        }

        public void Recalculate(HashSet<Point> ignore, int numToIgnore) {
            // reset variables
            ValueBuckets.Clear();
            size = 0;

            // determine which cells are valid
            var ValidCells = GameInfo.Map.GetAllCells().Where(c => !ignore.Contains(c.position.AsPoint) && c.halite >= numToIgnore);
            
            // create the buckets, where key is a dropoff location and value is cell value
            foreach(var cell in ValidCells) {
                Point closestDrop = GameInfo.MyClosestDrop(cell.position).AsPoint;
                if(!ValueBuckets.ContainsKey(closestDrop)) {
                    ValueBuckets.Add(closestDrop, new LinkedList<MapCell>());
                }
                size++;
                ValueBuckets[closestDrop].AddFirst(cell);
            }

            // sort the buckets by most valuable cells
            foreach(var kvp in ValueBuckets.ToList()) {
                var sortedVals = kvp.Value.OrderByDescending(cell => CellValue(kvp.Key.AsPosition, cell));
                ValueBuckets[kvp.Key] = new LinkedList<MapCell>(sortedVals);
            }
        }

        public MapCell GetBest(HashSet<Point> assigned, Ship ship) {
            // determine which bucket the ship should be in
            var closestDrop = GameInfo.MyClosestDrop(ship.position).AsPoint;
            double maxVal = -1;
            MapCell bestCell = null;
            LinkedList<MapCell> bestList = null;
            foreach(var kvp in ValueBuckets) {
                // find the best target in this bucket
                MapCell best = null;
                int distToDrop = GameInfo.Distance(ship, kvp.Key.AsPosition);
                foreach(MapCell possibleTarget in kvp.Value) {
                    if(IsAccessible(ship, possibleTarget, assigned) && (kvp.Key.Equals(closestDrop) || GameInfo.Distance(ship, possibleTarget.position) <= distToDrop)) {
                        best = possibleTarget;
                        break;
                    }
                }

                // no luck with this bucket, try the next
                if(best == null)
                    continue;

                // bias the ship to our current drop location
                double val = CellValue(kvp.Key.AsPosition, best);
                if(kvp.Key.Equals(closestDrop)) {
                    val *= 2; // todo magic number
                }
                if(val > maxVal) {
                    maxVal = val;
                    bestCell = best;
                    bestList = kvp.Value;
                }
            }
            if(bestCell != null) {
                size--;
                bestList.Remove(bestCell);
            }
            return bestCell;
        }
    }
}