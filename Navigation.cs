namespace Halite3 {
    public class Config {

    }

    public static class Navigation {
        
    }

    // This class merely keeps track of moving ships and verifies that no ship interrups their movement
    // by moving into a space where they would be stuck the following turn
    public class TwoTurnAvoid {
        Dictionary<int, int> ShipOptionsCount = new Dictionary<int, int>();
        Dictionary<Point, List<int>> CellToShipMapping = new Dictionary<Point, List<int>>();

        public void Clear() {
            ShipOptionsCount.Clear();
            CellToShipMapping.Clear();
        }
        // This method expects to be passed the cell that should be avoided
        public void Add(Ship ship, Position cell) {
            Log.LogMessage($"Added Position {cell.x},{cell.y} for ship {ship.Id} for 2 turn avoid");
            if(!ShipOptionsCount.ContainsKey(ship.Id)) {
                ShipOptionsCount[ship.Id] = 1;
            } else {
                ShipOptionsCount[ship.Id] += 1;
            }

            if(!CellToShipMapping.ContainsKey(cell.AsPoint)) {
                CellToShipMapping[cell.AsPoint] = new List<int> { ship.Id };
            } else {
                CellToShipMapping[cell.AsPoint].Add(ship.Id);
            }
        }
        public void Add(Ship ship, MapCell cell) => Add(ship, cell.position);
        public void Add(Ship ship, MapCell target, List<Direction> dirs) => dirs.ForEach(d => Add(ship, GameInfo.CellAt(target, d)));

        public void Remove(Position cell) {
            if(!CellToShipMapping.ContainsKey(cell.AsPoint))
                return;
            foreach(var id in CellToShipMapping[cell.AsPoint]) {
                ShipOptionsCount[id] -= 1;
            }
            CellToShipMapping.Remove(cell.AsPoint);
        }
        public void Remove(MapCell cell) => Remove(cell.position);

        public bool IsOkay(Position cell) {
            if(!CellToShipMapping.ContainsKey(cell.AsPoint))
                return true;
            if(CellToShipMapping[cell.AsPoint].Any(x => ShipOptionsCount[x] == 0))
                ExceptionHandler.Raise("We have a cell reserved in CellToShipMapping for two-turn avoid with no ship attached. This is unexpected.");
            return CellToShipMapping[cell.AsPoint].All(shipId => ShipOptionsCount[shipId] > 1);
        }
        public bool IsOkay(MapCell cell) => IsOkay(cell.position);
        public bool IsOkay(Ship s, Direction d) => IsOkay(GameInfo.CellAt(s, d));
    }
}