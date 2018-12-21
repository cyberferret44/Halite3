using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CollectLogic2 : Logic
    {
        private bool HasChanged = false;
        private bool ExceedsNum(MapCell cell) => cell.halite > GameInfo.NumToIgnore;
        private bool ExceedsNum(int halite) => halite > GameInfo.NumToIgnore;
        private ShipAssignments Assignments = new ShipAssignments();
        public CollectLogic2()
        {
            var cellsOrdered = Map.GetAllCells().OrderByDescending(x => x.halite).ToList();
            cellsOrdered = cellsOrdered.Take(cellsOrdered.Count * 2 / 3).ToList();
            GameInfo.NumToIgnore = (int)(cellsOrdered.Average(c => c.halite) * HParams[Parameters.PERCENT_OF_AVERAGE_TO_IGNORE]);
            Log.LogMessage("Num to Ignore: " + GameInfo.NumToIgnore);
        }

        public override void ProcessTurn()
        {
            // clean up dead ships
            ShipAssignments.CleanupDeadShips();

            // unassign any ship on a space with less than target halite
            foreach(var ship in Fleet.AllShips) {
                var assignment = ShipAssignments.Get(ship);
                if(assignment != null && !ExceedsNum(assignment.halite))
                    ShipAssignments.Unassign(ship, "assignment " + assignment.position.ToString() + " has less than target halite.");
            }

            // adjust the NumToIgnore if need be
            var notEnoughCells = GameInfo.Map.GetAllCells().Where(c => c.halite > GameInfo.NumToIgnore).Count() < GameInfo.TotalShipsCount* 2;
            if(notEnoughCells) {
                GameInfo.NumToIgnore = HasChanged ? 1 : GameInfo.NumToIgnore /= 5;
                HasChanged = true;
            }
        }

        public override void CommandShips()
        {
            // assign unassigned ships
            foreach(var ship in Fleet.AvailableShips) {
                if(!ShipAssignments.IsAssigned(ship)) {
                    ShipAssignments.Assign(ship, GetBestAssignment(ship), "Best Ship assignment.");
                }
            }

            // assign to a good neighbor, only if already on assignment
            foreach(var ship in Fleet.AvailableShips.Where(s => ShipAssignments.IsAssigned(s) && s.CurrentMapCell == Assignments[s])) {
                var neighbors = ship.Neighbors.OrderByDescending(n => n.halite);
                var best = neighbors.FirstOrDefault(n => !ShipAssignments.IsAssigned(n) && IsSafeMove(ship, n) &&
                        n.halite / 4 * (n.IsInspired ? 2.5 : 1) > ship.CellHalite * (ship.CurrentMapCell.IsInspired ? 2.5 : 1));
                if(best != null) {
                    ShipAssignments.Assign(ship, best, "Assigning to an awesome neighbor.");
                }
            }

            // if current cell is an assignment, swap it
            foreach(var ship in Fleet.AvailableShips)
            {
                if(ShipAssignments.IsAssigned(ship.CurrentMapCell) && Assignments[ship.CurrentMapCell] != ship) {
                    var assignment = ShipAssignments.Get(ship);
                    var otherShip = Assignments[ship.CurrentMapCell];
                    if(otherShip == null)
                        throw new Exception($"stole an assignment from a null ship???? {ship.Id} {ship.CurrentMapCell.position.ToString()}");
                    ShipAssignments.Assign(ship, ship.CurrentMapCell, $"Stealing assignment {ship.CurrentMapCell.position.ToString()} from {otherShip.Id}");
                    if(assignment != null && otherShip.CurrentMapCell == assignment) {
                        ShipAssignments.Assign(otherShip, assignment, "Taking cell from ship that stole this ship's cell");
                    }
                }
            }

            // move towards assignment...  Ignore other ships
            foreach(var ship in Fleet.AvailableShips) {
                var assignment = ShipAssignments.Get(ship);
                if(assignment != null && assignment != ship.CurrentMapCell && (!ExceedsNum(ship.CurrentMapCell) || !IsSafeMove(ship, Direction.STILL))) {
                    var dirs = assignment.position.GetAllDirectionsTo(ship.position).OrderBy(d => GameInfo.CellAt(ship, d).halite).ToList();
                    string msg = $"Moving towards assignment {assignment.position.ToString()} ignoring other ships.";
                    if(dirs.Any(d => IsSafeAndAvoids2Cells(ship, d) && !GameInfo.CellAt(ship, d).IsOccupied())) {
                        MakeMove(ship.Move(dirs.First(d => IsSafeAndAvoids2Cells(ship, d) && !GameInfo.CellAt(ship, d).IsOccupied()), msg + "1"));
                        continue;
                    }
                    if(dirs.Any(d => IsSafeAndAvoids2Cells(ship, d))) {
                        dirs = dirs.Where(d => IsSafeAndAvoids2Cells(ship,d)).ToList();
                        MakeMove(ship.Move(dirs.First(), msg + "2"));
                        continue;
                    }
                }
            }

            // sit still if on assignment
            foreach(var ship in Fleet.AvailableShips) {
                // sit still if on assignment
                if(ExceedsNum(ship.CellHalite) && IsSafeMove(ship, Direction.STILL)) {
                    MakeMove(ship.StayStill("Collecting Halite from current position " + ship.position.ToString()));
                    continue;
                }
            }

            // sit still if on assignment
            foreach(var ship in Fleet.AvailableShips) {
                // sit still if on assignment
                var assignment = ShipAssignments.Get(ship);
                if(assignment != null && ship.CurrentMapCell == assignment && IsSafeMove(ship, Direction.STILL)) {
                    Log.LogMessage("ERROR!!! This shouldn't happenasdfas " + ship.Id);
                }
            }

            // move to best neighbor if sitting still isn't available
            foreach(var ship in Fleet.AvailableShips) {
                var dirs = DirectionExtensions.ALL_CARDINALS;
                dirs = dirs.OrderByDescending(d => CellValueWithPath(ship, GameInfo.CellAt(ship, d))).ToArray();
                if(dirs.Any(d => IsSafeMove(ship, d))) {
                    var dir = dirs.First(d => IsSafeMove(ship, d));
                    MakeMove(ship.Move(dir, "Moving to best neighbor.... "));
                    continue;
                }
            }

            // ideally, these blocks should never happen
            foreach(var ship in Fleet.AvailableShips) {
                var assignment = ShipAssignments.Get(ship);
                if(IsSafeMove(ship, Direction.STILL)) {
                    string msg = "Staying still from collect, can't move towards assignment " +
                            (assignment == null ? "null" : assignment.position.ToString());
                    MakeMove(ship.StayStill(msg));
                    continue;
                }

                foreach(var dir in DirectionExtensions.ALL_DIRECTIONS) {
                    if(IsSafeMove(ship, dir)) {
                        MakeMove(ship.Move(dir, "Making a random move because I don't know what else to do."));
                        break;
                    }
                }
            }
        }

        public MapCell GetBestAssignment(Ship ship) {
            double bestValue = -1;
            MapCell bestCell = null;
            foreach(var cell in ZoneMap.Zones[ship].AllCells.Where(c => ExceedsNum(c.halite))) {
                var val = CellValueWithPath(ship, cell);
                if(!ShipAssignments.IsAssigned(cell) && val > bestValue) {
                    bestValue = val;
                    bestCell = cell;
                }
            }
            return bestCell;
        }

        public double CellValueWithPath(Ship ship, MapCell cell) {
            var cellVal = cell.halite; //GameInfo.CellValue(cell, ship);
            var path = GameInfo.CalculatePathOfLeastResistance(cell.position, ship.position);
            path.ForEach(p => cellVal = cellVal * (1000 - p.halite)/(1000 + GameInfo.OpportunityCost));
            cellVal *= (int)(cell.IsInspired ? 2.5 : 1.0);
            return cellVal;
        }
    }

    public class ShipAssignments {
        private static Dictionary<Point, int> CellToShip = new Dictionary<Point, int>();
        private static Dictionary<int, Point?> ShipToCell = new Dictionary<int, Point?>();

        public static void CleanupDeadShips() {
            var dead = ShipToCell.Keys.ToList().Where(s => Fleet.IsDead(s));
            foreach(var shipId in dead) {
                Point? p = ShipToCell[shipId];
                ShipToCell.Remove(shipId);
                if(p.HasValue)
                    CellToShip.Remove(p.Value);
            }
        }

        public static void Assign(Ship ship, MapCell cell, string note) {
            if(cell == null) {
                ShipToCell[ship.Id] = null;
                return;
            }
            // clean up existing assignment if exists
            if(CellToShip.ContainsKey(cell.position.AsPoint)) {
                Unassign(CellToShip[cell.position.AsPoint], "Unassigning, as a different ship took this assignment");
            }
            // remove the existing assignment. Must check key in case this is the ship's first assignment
            if(ShipToCell.ContainsKey(ship.Id) && ShipToCell[ship.Id].HasValue) {
                CellToShip.Remove(ShipToCell[ship.Id].Value);
            }
            Log.LogMessage($"Ship {ship.Id} was assigned to {cell.position.ToString()}. Note: {note}");
            CellToShip[cell.position.AsPoint] = ship.Id;
            ShipToCell[ship.Id] = cell.position.AsPoint;
        }

        public static void Unassign(Ship ship, string note) => Unassign(ship.Id, note);
        public static void Unassign(int shipId, string note) {
            if(!ShipToCell.ContainsKey(shipId))
                return;
            Point? p = ShipToCell[shipId];
            ShipToCell[shipId] = null;
            if(p.HasValue) {
                CellToShip.Remove(p.Value);
                Log.LogMessage($"Ship {shipId} was unassigned from {p.Value.ToString()}. Note: {note}");
            }
        }

        public MapCell this[Ship s]
        {
            get { return Get(s); }
        }

        public Ship this[MapCell c]
        {
            get { return Get(c); }
        }

        public static bool IsAssigned(MapCell cell) => CellToShip.ContainsKey(cell.position.AsPoint);
        public static bool IsAssigned(Ship ship) => ShipToCell.ContainsKey(ship.Id) && ShipToCell[ship.Id].HasValue;
        public static HashSet<MapCell> AssignedCells => CellToShip.Keys.Select(k => GameInfo.CellAt(k)).ToHashSet();
        public static MapCell Get(Ship ship) => ShipToCell.ContainsKey(ship.Id) && ShipToCell[ship.Id].HasValue ? GameInfo.CellAt(ShipToCell[ship.Id].Value) : null;
        public static Ship Get(MapCell cell) => CellToShip.ContainsKey(cell.position.AsPoint) ? GameInfo.GetMyShip(CellToShip[cell.position.AsPoint]) : null;
    }
}