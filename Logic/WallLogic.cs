using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class WallLogic : Logic {
        private static int NumToIgnore = 100;
        private static double Degredation => .8;
        private HashSet<Point> IgnoredCells = new HashSet<Point>();
        private HashSet<Point> Wall = new HashSet<Point>();
        private Dictionary<int, Point?> Assignments = new Dictionary<int, Point?>();
        private bool HasChanged = false;

        private HashSet<int> ShipsAccountedFor = new HashSet<int>();

        private int TotalWallCells => Assignments.Values.Where(v => v != null).Count() + Wall.Count;

        public override void Initialize() {
            ExpandWall(Map.At(MyBot.Me.shipyard.position));
        }

        public override void ProcessTurn() {
            // unassign ships not accounted for
            // todo this is not optimum, but it is cleaner
            UnassignUnavailableShips();

            // clear the ships accounted for set
            ShipsAccountedFor.Clear();

            // if cells run out 
            if(!HasChanged && TotalWallCells < Me.ShipsSorted.Count && MyBot.game.turnNumber > 30) {
                HasChanged = true;
                NumToIgnore /= 5;
                IgnoredCells.Clear();
                foreach(var d in MyBot.Me.GetDropoffs()) {
                    ExpandWall(Map.At(d.position));
                }
            }

            // redefine our wall
            IgnoredCells.Clear();
            foreach(var d in MyBot.Me.GetDropoffs()) {
                ExpandWall(MyBot.GameMap.At(d.position));
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

                if(ship.OnDropoff && directions.Any(m => IsSafeMove(ship, m) && !Map.At(ship.position.DirectionalOffset(m)).IsOccupied())) {
                    MyBot.MakeMove(ship.Move(directions.First(m => IsSafeMove(ship, m) && !Map.At(ship.position.DirectionalOffset(m)).IsOccupied())));
                    continue;
                }
                if(directions.Any(m => IsSafeMove(ship, m))) {
                    MyBot.MakeMove(ship.Move(directions.First(m => IsSafeMove(ship, m)))); //todo fix possible collisions here
                    continue;
                }
            }
        }

        private List<Direction> GetDirections(Position target, Ship ship) {
            List<Direction> directions = target.GetAllDirectionsTo(ship.position);
            directions = directions.OrderBy(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();

            // todo reassign the cell
            if(!directions.Contains(Direction.STILL) && ship.CurrentMapCell.halite >= NumToIgnore) {
                directions.Insert(0, Direction.STILL);
            }
            directions = AddRemaining(directions);
            return directions;
        }

        /// This method will unassign any ships not available in the list
        private void UnassignUnavailableShips() {
            foreach(var key in Assignments.Keys.ToList()) {
                if(!ShipsAccountedFor.Contains(key)) {
                    Assignments[key] = null;
                }
            }
        }

        private List<Direction> AddRemaining(List<Direction> directions) {
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
            if(!directions.Contains(Direction.STILL))
                directions.Add(Direction.STILL);
            return directions;
        }

        private double CellValue(Ship ship, MapCell cell) {
            int dist = Map.CalculateDistance(ship.position, cell.position);
            var neighbors = Map.GetXLayers(cell.position, 3); // todo magic number
            var sum = neighbors.OrderByDescending(n => n.halite).Take(neighbors.Count/2).Sum(n => n.halite);
            return sum * Math.Pow(Degredation, dist);
        }

        private Position GetBestMoveTarget(Ship ship) {
            // if not assigned, assign
            if(!Assignments.ContainsKey(ship.Id) || Assignments[ship.Id] == null) {
                Point? best = null;
                if(Wall.Count > 0) {
                    best = Wall.OrderByDescending(cell => CellValue(ship, Map.At(new Position(cell.x,cell.y)))).First();
                    Wall.Remove(best.Value);
                }
                Assignments[ship.Id] = best;
            }

            // from assignment, move to position (which can be still be null).....
            if(Assignments[ship.Id] != null) {
                var p = Assignments[ship.Id];
                return new Position(p.Value.x, p.Value.y);
            } else {
                return ship.position;
            }
        }

        private void ExpandWall(MapCell cell) {
            if(IgnoredCells.Contains(cell.position.AsPoint))
                return;

            if(cell.halite >= NumToIgnore) {
                if(!Assignments.ContainsValue(cell.position.AsPoint) && !Wall.Contains(cell.position.AsPoint)) {
                    Wall.Add(cell.position.AsPoint);
                }
                return;
            }

            // less than target halite, skip and add neighbors
            IgnoredCells.Add(cell.position.AsPoint);
            if(Assignments.Any(x => x.Value.HasValue && x.Value.Value.Equals(cell.position.AsPoint))) {
                var kvp = Assignments.First(x => x.Value.HasValue && x.Value.Value.Equals(cell.position.AsPoint));
                Assignments[kvp.Key] = null;
            }
            if(Wall.Contains(cell.position.AsPoint)) {
                Wall.Remove(cell.position.AsPoint);
            }
            var neighbors = Map.NeighborsAt(cell.position);
            foreach(var n in neighbors) {
                ExpandWall(n);
            }
        }
    }
}