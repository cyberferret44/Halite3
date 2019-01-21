using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using Halite3.Logic;

namespace Halite3 {
    public static class Safety {
        // New Turn logic
        public static void InitializeNewTurn() {
            Safety.TwoTurnAvoider.Clear();
        }

        // Shared Information
        public static TwoTurnAvoid TwoTurnAvoider = new TwoTurnAvoid();

        // Safety based moves
        public static bool IsSafeMove(Ship ship, MapCell neighbor) => IsSafeMove(ship, neighbor.position.GetDirectionTo(ship.position));
        public static bool IsSafeMove(Ship ship, Direction direction) {
            MapCell target = GameInfo.CellAt(ship, direction);
            if(Fleet.CollisionCells.Contains(target))
                return false;
            
            double recoveryChance = FleetCombatScores.RecoveryChance(ship, direction);
            if(recoveryChance != 1.0) {
                Log.LogMessage($"Ship {ship.Id}, rChance: {recoveryChance}, target: {target.position.ToString()}");
            }
            return recoveryChance > MyBot.HParams[Parameters.SAFETY_THRESHOLD];
        }
        public static bool IsCompletelySafeMove(Ship s, Direction d) => IsSafeMove(s, d); // && (!GameInfo.CellAt(s, d).IsThreatened || s.DistanceToMyDropoff <= 3) ;
        public static bool IsSafeAndAvoids2Cells(Ship s, Direction d) => IsSafeMove(s, d) && (d == Direction.STILL || 
                (s.halite - (s.CellHalite * .1) >= GameInfo.CellAt(s, d).halite * .1) || TwoTurnAvoider.IsOkay(s, d));
        public static bool IsSafeAndAvoids2Cells(Ship s, MapCell m) => IsSafeMove(s, m.position.GetDirectionTo(s.position));
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
            return CellToShipMapping[cell.AsPoint].All(shipId => ShipOptionsCount[shipId] > 1);
        }
        public bool IsOkay(MapCell cell) => IsOkay(cell.position);
        public bool IsOkay(Ship s, Direction d) => IsOkay(GameInfo.CellAt(s, d));
    }
}