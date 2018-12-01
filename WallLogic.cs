
using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
public class WallLogic : Logic {
    private static int NumToIgnore = 100;
    private static double Degredation => .8;

    private static GameMap gameMap => MyBot.GameMap;
    HashSet<Point> IgnoredCells = new HashSet<Point>();
    HashSet<Point> Wall = new HashSet<Point>();
    Dictionary<int, Point?> Assignments = new Dictionary<int, Point?>();
    public bool HasChanged = false;

    public void DoPreProcessing() {
        ProcessRecursive(gameMap.At(MyBot.Me.shipyard.position));
    }

    public void ProcessTurn() {
        // if cells run out 
        if(!HasChanged && Assignments.Where(kvp => kvp.Value != null).ToList().Count + Wall.Count < MyBot.Me.ShipsSorted.Count) {
            HasChanged = true;
            NumToIgnore /= 10;
            IgnoredCells.Clear();
            foreach(var d in MyBot.Me.GetDropoffs()) {
                ProcessRecursive(gameMap.At(d.position));
            }
        }

        // add cells if a ship collided there
        var cellsToAdd = IgnoredCells.Where(c => gameMap.At(new Position(c.x, c.y)).halite >= NumToIgnore).ToList();
        foreach(var c in cellsToAdd) {
            IgnoredCells.Remove(c);
            Wall.Add(c);
        }

        // todo if ship returning to home, unassign
        foreach(var ship in MyBot.Me.ShipsSorted) {
            if(MyBot.MovingTowardsBase.Contains(ship.Id) && Assignments[ship.Id] != null) {
                Wall.Add(Assignments[ship.Id].Value);
                Assignments[ship.Id] = null;
            }
        }

        // todo if ship destroyed, unassign cell
        HashSet<int> ShipsRemaining = new HashSet<int>();
        MyBot.Me.ShipsSorted.ForEach(s => ShipsRemaining.Add(s.Id));
        foreach(var kvp in Assignments.ToList()) {
            if(!ShipsRemaining.Contains(kvp.Key)) {
                if(kvp.Value.HasValue)
                    Wall.Add(kvp.Value.Value);
                Assignments.Remove(kvp.Key);
            }
        }

        // add cells if depleted
        foreach(var val in Assignments.Values.ToList().Where(v => v != null)) {
            var cell = gameMap.At(new Position(val.Value.x, val.Value.y));
            ProcessRecursive(cell);
        }
        foreach(var val in Wall.ToList()) {
            var cell = gameMap.At(new Position(val.x, val.y));
            ProcessRecursive(cell);
        }
    }

    public List<Direction> GetBestMoves(Ship ship) {
        var target = GetBestMoveTarget(ship);
        List<Direction> directions = target.GetAllDirectionsTo(ship.position);
        directions = directions.OrderBy(d => gameMap.At(ship.position.DirectionalOffset(d)).halite).ToList();
        if(!directions.Contains(Direction.STILL) && ship.CurrentMapCell.halite >= NumToIgnore) {
            directions.Insert(0, Direction.STILL);
        }
        directions = AddRemaining(directions);

        string recommended = "";
        directions.ForEach(d => recommended += d.ToString() + " ");
        var val = Assignments[ship.Id];
        string pt = val.HasValue ? $"({val.Value.x},{val.Value.y})" : "null";
        Log.LogMessage($"{ship.Id}, target cell is {pt} recommended moves are... {recommended}");

        return directions;
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
        int dist = gameMap.CalculateDistance(ship.position, cell.position);
        var neighbors = gameMap.GetXLayers(cell.position, 3); // todo magic number, but probably good, as all map sizes are multiples of 8
        var sum = neighbors.OrderByDescending(n => n.halite).Take(neighbors.Count/2).Sum(n => n.halite);
        return sum * Math.Pow(Degredation, dist);
    }

    public Position GetBestMoveTarget(Ship ship) {
        // if not assigned, assign
        if(!Assignments.ContainsKey(ship.Id) || Assignments[ship.Id] == null) {
            Point? best = null;
            if(Wall.Count > 0) {
                best = Wall.OrderByDescending(cell => CellValue(ship, gameMap.At(new Position(cell.x,cell.y)))).First();
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

    private void ProcessRecursive(MapCell cell) {
        if(IgnoredCells.Contains(cell.position.AsPoint))
            return;

        if(cell.halite >= NumToIgnore) {
            if(!Assignments.ContainsValue(cell.position.AsPoint) && !Wall.Contains(cell.position.AsPoint)) {
                Wall.Add(cell.position.AsPoint);
            }
            return;
        }

        // less than 100 halite, skip and add neighbors
        IgnoredCells.Add(cell.position.AsPoint);
        if(Assignments.Any(x => x.Value.HasValue && x.Value.Value.Equals(cell.position.AsPoint))) {
            var kvp = Assignments.First(x => x.Value.HasValue && x.Value.Value.Equals(cell.position.AsPoint));
            Assignments[kvp.Key] = null;
        }
        if(Wall.Contains(cell.position.AsPoint)) {
            Wall.Remove(cell.position.AsPoint);
        }
        var neighbors = gameMap.NeighborsAt(cell.position);
        foreach(var n in neighbors) {
            ProcessRecursive(n);
        }
    }
}