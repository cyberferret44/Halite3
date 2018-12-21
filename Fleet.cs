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
        public static List<int> AllShipIds => availableIds.ToList();
        public static List<Ship> AllShips => UsedShips.Union(AvailableShips).ToList();
        public static bool CellAvailable(MapCell c) => !collisionCells.Contains(c);
        public static List<MapCell> CollisionCells => collisionCells.ToList();

        public static void UpdateFleet(List<Ship> ships) {
            availableIds.Clear();
            availableShipMoves.Clear();
            usedShips.Clear();
            collisionCells.Clear();
            foreach(var ship in ships) {
                if(ship.CanMove) {
                    availableShipMoves.Add(ship, DirectionExtensions.ALL_DIRECTIONS.ToList());
                } else {
                    AddMove(ship.StayStill($"ship {ship.Id} can't move, making it stay still from Fleet logic"));
                }
                availableIds.Add(ship.Id);
            }
        }
        
        public static void AddMove(Command command) {
            availableShipMoves.Remove(command.Ship);
            usedShips.Add(command.Ship, command);
            collisionCells.Add(command.TargetCell);
            Log.LogMessage(command.Comment);
        }

        public static List<Command> GenerateCommandQueue() {
            foreach(var ship in availableShipMoves.Keys.ToList()) {
                AddMove(ship.StayStill($"ship {ship.Id} was leftover, just making it stay still"));
            }
            return usedShips.Values.ToList();
        }
    }
}