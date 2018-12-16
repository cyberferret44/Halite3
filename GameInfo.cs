using Halite3.hlt;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Halite3 {
    /// I found myself needing a lot of global infomration for pretty much every class, and also found myself repeating
    /// a number of verious lambda functions.  So I created this class to give a universal simplified interface
    /// for accessing all of these various pieces of information.
    public static class GameInfo {
        // Determine if we're local
        public static readonly bool IsLocal = Directory.GetCurrentDirectory().StartsWith("/Users/cviolet") ||
                                      Directory.GetCurrentDirectory().StartsWith("C://Users");
        public static bool IsDebug = false;

        // Game
        public static Game Game;
        public static void SetInfo(Game game) {
            GameInfo.Game = game;
        }

        // Map Related
        public static GameMap Map => Game.gameMap;
        public static int TotalCellCount => Map.height * Map.width;
        public static int Distance(Position p1, Position p2) => Map.CalculateDistance(p1, p2);
        public static int Distance(Position p1, Entity e2) => Distance(p1, e2.position);
        public static int Distance(Entity e1, Position p2) => Distance(e1.position, p2);
        public static int Distance(Entity e1, Entity e2) => Distance(e1.position, e2.position);
        public static List<MapCell> GetXLayersExclusive(Position position, int distance) {
            var xLayers = Map.GetXLayers(position, distance);
            var subtract = Map.GetXLayers(position, distance-1);
            return xLayers.Where(x => !subtract.Contains(x)).ToList();
        }

        // Dropoff / Shipyard related
        public static Shipyard MyShipyard => Me.shipyard;
        public static List<Position> MyDropoffs => Me.GetDropoffs().ToList();
        public static Position MyClosestDrop(Position p) => MyDropoffs.OrderBy(x => Distance(p, x)).First();

        // Halite related
        public static int HaliteRemaining => Map.HaliteRemaining;
        public static int AverageHalitePerCell => HaliteRemaining / TotalCellCount;

        // Turn related
        public static int TurnsRemaining => Game.TurnsRemaining;
        public static int TurnNumber => Game.turnNumber;

        // Player Related
        public static int MyId => Me.id.id;
        public static int PlayerCount => Game.players.Count;
        public static Player Me => Game.me;
        public static Player GetPlayer(int id) => Game.players.Single(p => p.id.id == id);

        // Ships related
        public static List<Ship> MyShips => Me.ShipsSorted;
        public static int MyShipsCount => Me.ships.Count;
        public static List<Player> Opponents => Game.Opponents;
        public static List<Ship> OpponentShips => Game.Opponents.SelectMany(x => x.ships.Values).ToList();
        public static int OpponentShipsCount => Game.Opponents.Sum(x => x.ships.Count);
        public static int TotalShipsCount => OpponentShipsCount + MyShipsCount;

        // Position Related
        public static MapCell CellAt(Position p) => Map.At(p);
        public static MapCell CellAt(Position p, Direction direction) => Map.At(p.DirectionalOffset(direction));
        public static MapCell CellAt(Entity e) => Map.At(e.position);
        public static MapCell CellAt(Entity e, Direction direction) => Map.At(e.position.DirectionalOffset(direction));
        public static MapCell CellAt(Position p, Direction d, int iterations) {
            for(int i=0; i< iterations; i++) {
                p = p.DirectionalOffset(d);
            }
            return CellAt(p);
        }
        public static MapCell MyShipyardCell => Map.At(Me.shipyard.position);
        

        // Miscellaneous...
       
        public static List<VirtualDropoff> BestDropoffs = new List<VirtualDropoff>();
        public static VirtualDropoff NextDropoff = null;
        
    }

    // Virtual Dropoffs
    public class VirtualDropoff {
        public Position Position;
        public int InitialHalite;
        public VirtualDropoff(Position p, int halite) {
            Position = p;
            InitialHalite = halite;
        }
    }
}