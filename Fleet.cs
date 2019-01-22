using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3 {
    public static class Fleet {
        private static Dictionary<Ship, List<Direction>> availableShipMoves = new Dictionary<Ship, List<Direction>>();
        private static Dictionary<Ship, Command> usedShips = new Dictionary<Ship, Command>();
        private static HashSet<MapCell> collisionCells = new HashSet<MapCell>();
        private static HashSet<int> availableIds = new HashSet<int>();
        public static List<Ship> AvailableShips => availableShipMoves.Keys.ToList();
        public static List<Ship> UsedShips => usedShips.Keys.ToList();
        public static bool IsDead(int shipId) => !availableIds.Contains(shipId);
        public static List<int> AvailableIds => availableIds.ToList();
        private static List<Ship> allShips;
        public static List<Ship> AllShips => allShips;
        public static bool CellAvailable(MapCell c) => !collisionCells.Contains(c);
        public static HashSet<MapCell> CollisionCells => collisionCells;
        private static int shipCount;
        public static int ShipCount => shipCount;
        public static HashSet<MapCell> ProbablyOccupiedCells => CollisionCells.Union(AvailableShips.Select(s => s.CurrentMapCell)).ToHashSet();
        public static bool ShipAvailable(Ship ship) => !usedShips.ContainsKey(ship);
        private static bool shouldSpawnShip = false;
        public static void UpdateFleet(List<Ship> ships) {
            shouldSpawnShip = false;
            allShips = ships.ToList();
            shipCount = ships.Count;
            availableIds.Clear();
            availableShipMoves.Clear();
            usedShips.Clear();
            collisionCells.Clear();
            Log.LogMessage("Ship count: " + ships.Count);
            Log.LogMessage("Opp  count: " + GameInfo.OpponentShipsCount);
            Log.LogMessage("My halite: " + GameInfo.Me.halite);
            GameInfo.Opponents.ForEach(o => Log.LogMessage("Op halite: " + o.halite + "... ships " + o.ships.Count));

            foreach(var ship in ships) {
                if(ship.CanMove) {
                    availableShipMoves.Add(ship, ship.OnDropoff ? DirectionExtensions.ALL_CARDINALS.ToList() : 
                                DirectionExtensions.ALL_DIRECTIONS.ToList());
                } else {
                    collisionCells.Add(ship.CurrentMapCell); // ship must stay still, so flag it now, but allow other logic to process it
                }
                availableIds.Add(ship.Id);
            }
        }
        
        public static void AddMove(Command command) {
            availableShipMoves.Remove(command.Ship);
            Safety.TwoTurnAvoider.Remove(command.TargetCell);
            usedShips[command.Ship] = command;
            collisionCells.Add(command.TargetCell);
            Log.LogMessage(command.Comment);

            if(availableShipMoves.Any(kvp => GameInfo.AvailableMoveCounts(kvp.Key, !command.Ship.OnDropoff) == 1)) {
                var shipToMove = availableShipMoves.First(kvp => GameInfo.AvailableMoveCounts(kvp.Key, !command.Ship.OnDropoff) == 1).Key;
                var dirs = shipToMove.OnDropoff ? DirectionExtensions.ALL_CARDINALS : DirectionExtensions.ALL_DIRECTIONS;
                if(dirs.Any(d => !CollisionCells.Contains(GameInfo.CellAt(shipToMove,d)))) {
                    var dir = dirs.First(d => !CollisionCells.Contains(GameInfo.CellAt(shipToMove,d)));
                    AddMove(shipToMove.Move(dir, $"Moving ship {shipToMove.Id} because there were no other moves remaining"));
                }
            }
        }

        public static List<Command> GenerateCommandQueue() {
            foreach(var ship in AvailableShips) {
                var dirs = DirectionExtensions.ALL_CARDINALS.ToList();
                if(ship.CellHalite > 0)
                    dirs.Insert(0, Direction.STILL);
                else
                    dirs.Add(Direction.STILL);
                if(dirs.Any(d => Safety.IsSafeMove(ship, d))) {
                    AddMove(ship.Move(dirs.First(d => Safety.IsSafeMove(ship, d)), "Left-over ship, making move from fleet logic."));
                } else if(dirs.Any(d => !CollisionCells.Contains(GameInfo.CellAt(ship, d)))) {
                    AddMove(ship.Move(dirs.First(d => !CollisionCells.Contains(GameInfo.CellAt(ship, d))), "Left-over ship, Moving towards an enemy instead of crashing myself..."));
                }
            }
            var commands = usedShips.Values.ToList();;
            if(shouldSpawnShip) {
                commands.Add(GameInfo.Me.shipyard.Spawn());
            }
            return commands;
        }

        public static void SpawnShip() {
            collisionCells.Add(GameInfo.CellAt(GameInfo.Me.shipyard.position));
            shouldSpawnShip = true;
        }
    }
}