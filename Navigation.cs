using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3 {
    public class Config {

    }

    public static class Navigation {
        struct Path {
            public int resistance;
            public List<MapCell> path;
        }

        public static int PathCost(Position start, Position end, HashSet<MapCell> CellsToAvoid = null) {
            var polr = CalculatePathOfLeastResistance(start, end, CellsToAvoid);
            return polr.Sum(p => (int)(p.halite/10));
        }


        // excludes start and end nodes
        public static List<MapCell> CalculatePathOfLeastResistance(Position start, Position end, HashSet<MapCell> CellsToAvoid = null) {
            HashSet<MapCell> visited = new HashSet<MapCell>();
            List<Path> Paths = new List<Path>();
            foreach(var d in end.GetAllDirectionsTo(start)) {
                var cell = GameInfo.CellAt(start, d);
                if(CellsToAvoid == null || !CellsToAvoid.Contains(cell))
                    Paths.Add(new Path { resistance = cell.halite/10, path = new List<MapCell> { cell } });
            }
            while(true) {
                if(Paths.Count == 0)
                    return null;
                int shortest = Paths.Min(x => x.resistance);
                var shortestPath = Paths.First(x => x.resistance == shortest);
                var last = shortestPath.path.Last();
                
                foreach(var d in end.GetAllDirectionsTo(last.position)) {
                    var cell = GameInfo.CellAt(last.position, d);
                    if(cell.position.AsPoint.Equals(end.AsPoint)) {
                        shortestPath.path.Add(cell);
                        return shortestPath.path;
                    }
                    if(!visited.Contains(cell) && (CellsToAvoid == null || !CellsToAvoid.Contains(cell))) {
                        var newPath = shortestPath.path.ToList();
                        newPath.Add(cell);
                        Paths.Add(new Path { path = newPath, resistance = shortestPath.resistance + (cell.halite/10)});
                        visited.Add(cell);
                    }
                }
                Paths.Remove(shortestPath);
            }
        }

        // excludes start and end nodes
        public static List<MapCell> CalculateGreedyPathOfLeastResistance(Position start, Position end, HashSet<MapCell> CellsToAvoid = null) {
            List<MapCell> path = new List<MapCell>();
            Position next = start;
            while(true) {
                var cells = end.GetAllDirectionsTo(next).Select(d => GameInfo.CellAt(next, d));
                cells = cells.OrderBy(c => c.halite);
                var cell = cells.ElementAt(0);
                if(cell.position.Equals(end)) {
                    return path;
                }
                if(CellsToAvoid != null)
                    cells = cells.Where(c => !CellsToAvoid.Contains(c));
                if(cells.Count() == 0) {
                    return null;
                }
                next = cell.position;
                path.Add(cell);
            }
        }

        public static bool IsAccessible(Position start, Position end) {
            HashSet<MapCell> used = Fleet.ProbablyOccupiedCells;
            Stack<Position> nexts = new Stack<Position>();
            nexts.Push(start);
            while(nexts.Any()) {
                var n = nexts.Pop();
                if(end.Equals(n))
                    return true;
                used.Add(GameInfo.CellAt(n));
                var cells = end.GetAllDirectionsTo(n).Select(d => GameInfo.CellAt(n , d));
                cells = cells.Where(c => !used.Contains(c));
                cells.ToList().ForEach(c => nexts.Push(c.position));
            }
            return false;
        }
    }
}