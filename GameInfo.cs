using Halite3.hlt;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class GameInfo {
        // Things to change...
        public static readonly string SPECIMEN_FOLDER = "Specimen8";
        public static readonly string BOT_NAME = "derp6";

        // Determine if we're local
        public static readonly bool IsLocal = Directory.GetCurrentDirectory().StartsWith("/Users/cviolet") ||
                                      Directory.GetCurrentDirectory().StartsWith("C://Users");
        public static bool IsDebug = false;
        public static int BaseShipValue;
        public static int BaseShipValueReducedBy2;
        private static int originalNum = -1;
        private static int getOriginalNum() {
            originalNum = Math.Max(originalNum, GameInfo.NumToIgnore);
            return originalNum;
        }
        public static bool NumToIgnoreAltered => getOriginalNum() != NumToIgnore;

        // Game
        public static Game Game;
        public static void SetInfo(Game game) {
            GameInfo.Game = game;
            CalculateProjectedShipValues();
        }

        // Estimated Ship Values
        private static void CalculateProjectedShipValues() {
            // current ship value per ship
            int numShips = Math.Max(1, GameInfo.TotalShipsCount);
            double ratio = 1.0 - (GameInfo.AverageHalitePerCell * numShips * .1 / Map.HaliteRemaining); // percent leftover each turn
            BaseShipValue = (int)(Map.HaliteRemaining * Math.Pow(ratio, GameInfo.TurnsRemaining) / numShips);
            Log.LogMessage("Base Ship value = " + BaseShipValue);

            // find the ship value if there were 2 less on the board
            int reducedNumShips = Math.Max(1, GameInfo.TotalShipsCount - 2);
            ratio = 1.0 - (GameInfo.AverageHalitePerCell * reducedNumShips * .1 / Map.HaliteRemaining); // percent leftover each turn
            BaseShipValueReducedBy2 = (int)(Map.HaliteRemaining * Math.Pow(ratio, GameInfo.TurnsRemaining) / reducedNumShips);
            Log.LogMessage("baseShipValueReducedBy2 Ship value = " + BaseShipValueReducedBy2);
        }

        // Map Related
        public static GameMap Map => Game.gameMap;
        public static int TotalCellCount => Map.height * Map.width;
        public static int Distance(Position p1, Position p2) => Map.CalculateDistance(p1, p2);
        public static int Distance(Position p1, Entity e2) => Distance(p1, e2.position);
        public static int Distance(Entity e1, Position p2) => Distance(e1.position, p2);
        public static int Distance(Entity e1, Entity e2) => Distance(e1.position, e2.position);
        public static int Distance(MapCell m1, MapCell m2) => Distance(m1.position, m2.position);
        public static int Distance(MapCell m1, Position p2) => Distance(m1.position, p2);
        public static List<MapCell> GetXLayersExclusive(Position position, int distance) {
            var xLayers = Map.GetXLayers(position, distance);
            var subtract = Map.GetXLayers(position, distance-1);
            return xLayers.Where(x => !subtract.Contains(x)).ToList();
        }

        // Dropoff / Shipyard related
        public static Shipyard MyShipyard => Me.shipyard;
        public static List<Position> MyDropoffs => Me.GetDropoffs().ToList();
        public static Position MyClosestDrop(Position p) => MyDropoffs.OrderBy(x => Distance(p, x)).First();
        public static int MyClosestDropDistance(Position p) => GameInfo.Distance(p, MyClosestDrop(p));

        // Halite related
        public static int HaliteRemaining => Map.HaliteRemaining;
        public static int AverageHalitePerCell => HaliteRemaining / TotalCellCount;
        public static int CellValue(MapCell cell, Ship ship) {
            var layers = Math.Min(3, Distance(cell, ship.position));
            double multiplier = 25.0 / ((layers + 1.0)/2.0*4.0*layers + 1.0); // 25 = (3 + 1) / 2 * 4 * 3 + 1
            var xLayers = Map.GetXLayers(cell.position, Math.Min(3, Distance(cell, ship.position)));
            return (int)(multiplier * xLayers.Sum(x => x.halite / (Distance(x, cell) + 1)));
        }
        public static int NumToIgnore;

        // Turn related
        public static int TurnsRemaining => Game.TurnsRemaining;
        public static int TurnNumber => Game.turnNumber;

        // Player Related
        public static int MyId => Me.id.id;
        public static int PlayerCount => Opponents.Count + 1;
        public static Player Me => Game.me;
        public static Player GetPlayer(int id) => Game.players.Single(p => p.id.id == id);
        public static bool Is2Player => PlayerCount == 2;
        public static bool Is4Player => PlayerCount == 4;

        // Ships related
        public static List<Ship> MyShips => Me.ShipsSorted;
        public static int MyShipsCount => Me.ships.Count;
        public static List<Player> Opponents => Game.Opponents;
        public static List<Ship> OpponentShips => Game.Opponents.SelectMany(x => x.ships.Values).ToList();
        public static int OpponentShipsCount => Game.Opponents.Sum(x => x.ships.Count);
        public static int TotalShipsCount => OpponentShipsCount + MyShipsCount;
        public static Ship GetMyShip(int shipId) => Me.GetShipById(shipId);
        public static int LowestNeighboringOpponentHalite(MapCell c) => c.Neighbors.Where(n => n.IsOccupiedByOpponent()).Min(n => n.ship.halite);
        public static int? LowestNeighboringOpponentHaliteWhereNotReturning(MapCell c) {
            var neighbors = c.Neighbors.Where(n => n.IsOccupiedByOpponent() && !EnemyFleet.IsReturningHome(n.ship));
            if(neighbors.Any())
                return neighbors.Min(n => n.ship.halite);
            else
                return null;
        }

        // Position Related
        public static MapCell CellAt(Position p) => Map.At(p);
        public static MapCell CellAt(Position p, Direction d) => Map.At(p.DirectionalOffset(d));
        public static MapCell CellAt(Entity e) => Map.At(e.position);
        public static MapCell CellAt(Entity e, Direction d) => Map.At(e.position.DirectionalOffset(d));
        public static MapCell CellAt(MapCell m, Direction d) => CellAt(m.position, d);
        public static MapCell CellAt(Point p) => CellAt(new Position(p.x, p.y));
        public static MapCell MyShipyardCell => Map.At(Me.shipyard.position);
        

        // Hyper Parameters
        public static string PlayerXSize => PlayerCount + "x" + Map.width;
        public static string HyperParameterFolder => $"{(IsLocal ? "Halite3/" : "")}GeneticTuner/{SPECIMEN_FOLDER}/{PlayerXSize}/";
        public static int OpportunityCost => (int)(.08 * Map.AverageHalitePerCell);
        public static List<VirtualDropoff> BestDropoffs = new List<VirtualDropoff>();
        public static VirtualDropoff NextDropoff = null;
        public static bool ReserveForDropoff = false;
    }

    // Virtual Dropoffs
    public class VirtualDropoff {
        public Position Position;
        public int InitialHalite;
        public VirtualDropoff(Position p, int halite) {
            Position = p;
            InitialHalite = halite;
        }
        public MapCell Cell => GameInfo.CellAt(Position);
    }
}