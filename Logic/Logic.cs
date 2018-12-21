using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public abstract class Logic {
        // shortcut accessors
        protected static GameMap Map => GameInfo.Map;
        protected static Player Me => GameInfo.Me;
        protected static Game Game => GameInfo.Game;
        protected HyperParameters HParams => MyBot.HParams;

        // Shared Information
        protected static TwoTurnAvoid TwoTurnAvoider = new TwoTurnAvoid();

        //abstract methods
        public abstract void ProcessTurn();
        public abstract void CommandShips();

        // New Turn Method
        public static void InitializeNewTurn() {
            TwoTurnAvoider.Clear();
        }

        // Make move method, and all it's details...
        public static void MakeMove(Command command) {
            Fleet.AddMove(command);
            TwoTurnAvoider.Remove(command.TargetCell);
        }

        // Safety based moves
        public static bool IsSafeMove(Ship ship, MapCell neighbor) => IsSafeMove(ship, neighbor.position.GetDirectionTo(ship.position));
        public static bool IsSafeMove(Ship ship, Direction direction, bool IngoreEnemy = false) {
            MapCell target = Map.At(ship, direction);
            if(target.IsOccupiedByMe() && !target.ship.CanMove)
                return false;
            return Fleet.CellAvailable(target) && (IngoreEnemy || !target.IsOccupiedByOpponent());
        }
        public static bool IsCompletelySafeMove(Ship s, Direction d) => IsSafeMove(s, d) && !Map.At(s, d).IsThreatened;
        public static bool IsSafeAndAvoids2Cells(Ship s, Direction d) => IsSafeMove(s, d) && (d == Direction.STILL || 
                (s.halite - (s.CellHalite * .1) >= GameInfo.CellAt(s, d).halite * .1) || TwoTurnAvoider.IsOkay(s, d));
        public static bool IsSafeAndAvoids2Cells(Ship s, MapCell m) => IsSafeMove(s, m.position.GetDirectionTo(s.position));
    }

    public class EmptyLogic : Logic {
        public override void ProcessTurn() { }
        public override void CommandShips() { }
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
                throw new Exception("this should not happen.;..");
            return CellToShipMapping[cell.AsPoint].All(shipId => ShipOptionsCount[shipId] > 1);
        }
        public bool IsOkay(MapCell cell) => IsOkay(cell.position);
        public bool IsOkay(Ship s, Direction d) => IsOkay(GameInfo.CellAt(s, d));
    }
}