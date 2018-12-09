using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CollectLogic : Logic {
        private static int NumToIgnore;
        private static double Degradation;
        private HashSet<Point> IgnoredCells = new HashSet<Point>();
        private HashSet<Point> Wall = new HashSet<Point>();
        private Dictionary<int, Point?> Assignments = new Dictionary<int, Point?>();
        private bool HasChanged = false;

        private HashSet<int> ShipsAccountedFor = new HashSet<int>();

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
            // unassign ships not accounted for
            // todo this is not optimum, but it is cleaner
            UnassignUnavailableShips();

            // Reset Variables
            ShipsAccountedFor.Clear();
            IgnoredCells.Clear();
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

        public override void CommandShips(List<Ship> ships) {
            // add accounted for ships
            ships.ForEach(s => ShipsAccountedFor.Add(s.Id));

            // command the ships
            foreach(var ship in ships) {
                if(!ship.CanMove) {
                    MyBot.MakeMove(ship.Move(Direction.STILL));
                    continue;
                }
                var target = GetBestMoveTarget(ship);
                var directions = GetDirections(target, ship);
                bool cont = false;

                // Special logic for a ship on a dropoff
                if(ship.OnDropoff) {
                    directions.Remove(Direction.STILL);
                    directions.ForEach(d => Log.LogMessage(d.ToString("g")));
                    var returningNeighbors = ship.CurrentMapCell.Neighbors.Where(n => n.IsOccupied() && n.ship.IsMine 
                                            && n.ship.halite > 500).Select(n => n.ship).ToList();
                    returningNeighbors = returningNeighbors.OrderBy(s => ships.IndexOf(s)).ToList();
                    returningNeighbors.ForEach(s => Log.LogMessage(s.id.id.ToString()));
                    foreach(var d in directions) {
                        var nCell = Map.At(ship.position.DirectionalOffset(d));
                        var nnCell = Map.At(ship.position.DirectionalOffset(d).DirectionalOffset(d));
                        var nnnCell = Map.At(ship.position.DirectionalOffset(d).DirectionalOffset(d).DirectionalOffset(d));
                        bool couldMove = nCell.halite < 10;
                        bool nnOccupiedAndReturning = nnCell.IsOccupied() && nnCell.ship.halite > 500; //todo weird logic
                        bool nnnOccupiedAndReturning = nnnCell.IsOccupied() && nnnCell.ship.halite > 500; //todo weird logic
                        if(IsSafeMove(ship, d) && !nnOccupiedAndReturning && (couldMove || !nnnOccupiedAndReturning) && !(nCell.IsOccupied() && nCell.ship.IsMine && returningNeighbors.Count > 1)) {
                            cont = true;
                            MyBot.MakeMove(ship.Move(d));
                            break;
                        }
                    }
                }

                if(cont)
                    continue;

                for(int i=0; i< directions.Count && directions[i] != Direction.STILL; i++) {
                    if(IsSafeMove(ship, directions[i])) {
                        MyBot.MakeMove(ship.Move(directions[i]));
                        cont = true;
                        break;
                    }
                }

                if(cont)
                    continue;

                if(directions.Any(m => IsSafeMove(ship, m))) {
                    MyBot.MakeMove(ship.Move(directions.First(m => IsSafeMove(ship, m)))); //todo fix possible collisions here
                    continue;
                }
            }
        }

        private List<Direction> GetDirections(Position target, Ship ship) {
            if(ship.CurrentMapCell.position.Equals(target)) {
                var directions = DirectionExtensions.ALL_CARDINALS.ToList();
                directions = directions.OrderByDescending(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
                directions.Insert(0, Direction.STILL);
                return directions;
            } else {
                List<Direction> directions = target.GetAllDirectionsTo(ship.position);
                directions = directions.OrderByDescending(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
                if(directions.Count == 1) {
                    List<Direction> extraDirections = null;
                    if(directions[0] == Direction.NORTH)
                        extraDirections = new List<Direction>{ Direction.EAST, Direction.WEST};
                    if(directions[0] == Direction.SOUTH)
                        extraDirections = new List<Direction>{ Direction.EAST, Direction.WEST};
                    if(directions[0] == Direction.EAST)
                        extraDirections = new List<Direction>{ Direction.NORTH, Direction.SOUTH};
                    if(directions[0] == Direction.WEST)
                        extraDirections = new List<Direction>{ Direction.NORTH, Direction.SOUTH};
                    extraDirections = extraDirections.OrderByDescending(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
                    directions.Add(extraDirections[0]);
                    directions.Add(extraDirections[1]);
                }

                foreach(var d in directions.ToList()) {
                    if(Map.At(ship.position.DirectionalOffset(d)).IsStructure) {
                        directions.Remove(d);
                    }
                }

                // todo reassign the cell
                if(!directions.Contains(Direction.STILL) && ship.CurrentMapCell.halite >= NumToIgnore) {
                    directions.Insert(0, Direction.STILL);
                }
                directions = AddRemaining(directions, !ship.OnDropoff);
                return directions;
            }
        }

        /// This method will unassign any ships not available in the list
        private void UnassignUnavailableShips() {
            foreach(var key in Assignments.Keys.ToList()) {
                if(!ShipsAccountedFor.Contains(key)) {
                    Unassign(key);
                }
            }
        }

        private List<Direction> AddRemaining(List<Direction> directions, bool stillFirst = true) {
            if(stillFirst) {
                if(!directions.Contains(Direction.STILL))
                    directions.Add(Direction.STILL);
            }
            if(directions.Contains(Direction.NORTH)) {
                if(!directions.Contains(Direction.EAST))
                    directions.Add(Direction.EAST);
                if(!directions.Contains(Direction.WEST))
                    directions.Add(Direction.WEST);
            }
            if(directions.Contains(Direction.EAST)) {
                if(!directions.Contains(Direction.NORTH))
                    directions.Add(Direction.NORTH);
                if(!directions.Contains(Direction.SOUTH))
                    directions.Add(Direction.SOUTH);
            }
            if(directions.Contains(Direction.SOUTH)) {
                if(!directions.Contains(Direction.EAST))
                    directions.Add(Direction.EAST);
                if(!directions.Contains(Direction.WEST))
                    directions.Add(Direction.WEST);
            }
            if(directions.Contains(Direction.WEST)) {
                if(!directions.Contains(Direction.NORTH))
                    directions.Add(Direction.NORTH);
                if(!directions.Contains(Direction.SOUTH))
                    directions.Add(Direction.SOUTH);
            }
            if(!directions.Contains(Direction.NORTH))
                directions.Add(Direction.NORTH);
            if(!directions.Contains(Direction.SOUTH))
                directions.Add(Direction.SOUTH);
            if(!directions.Contains(Direction.EAST))
                directions.Add(Direction.EAST);
            if(!directions.Contains(Direction.WEST))
                directions.Add(Direction.WEST);
            if(!stillFirst) {
                if(!directions.Contains(Direction.STILL))
                    directions.Add(Direction.STILL);
            }
            return directions;
        }

        private double CellValue(Ship ship, MapCell cell) {
            int dist = Map.CalculateDistance(ship.position, cell.position);
            var neighbors = Map.GetXLayers(cell.position, 3); // todo magic number
            var vals = neighbors.Select(n => n.halite / (Map.CalculateDistance(n.position, cell.position) + 1));
            var sum = vals.OrderByDescending(v => v).Take(neighbors.Count/2).Sum(v => v);
            return sum * Math.Pow(Degradation, dist);
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
                    best = Wall.OrderByDescending(cell => CellValue(ship, Map.At(new Position(cell.x,cell.y)))).First();
                    Wall.Remove(best.Value);
                }
                Assign(ship, best);
            }

            // from assignment, move to position (which can be still be null).....
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
                    IgnoredCells.Add(cell.position.AsPoint);
                    if(assigned) {
                        var element = Assignments.First(a => a.Value.HasValue && a.Value.Value.Equals(cell.position.AsPoint));
                        Unassign(element.Key);
                    }
                }
            }
        }

        private void Assign(Ship ship, Point? point) {
            if(point.HasValue)
                Log.LogMessage($"ship {ship.Id} has been assigned to ({point.Value.x},{point.Value.y})");
            Assignments[ship.Id] = point;
        }
        private void Unassign(int shipId) {
            if(Assignments[shipId] != null)
                Log.LogMessage($"ship {shipId} has been unassigned");
            Assignments[shipId] = null;
        }
    }
}