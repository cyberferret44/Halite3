using Halite3.hlt;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System;

namespace Halite3 {
    public static class GameInfo {
        // 
        private static Queue<int> HaliteCollected = new Queue<int>();

        // Things to change...
        public static readonly string SPECIMEN_FOLDER = "Specimen9";
        public static string BOT_NAME => Me.id.id + "-Derp8";

        // Turn timer, prevent timeouts
        private static Stopwatch clock = new Stopwatch();
        public static double PercentTurnTimeRemaining => 1.0 - (((double)clock.ElapsedMilliseconds) / 2000.0);
        public static int RateLimitXLayers(int preferredXLayers) {
            if(GameInfo.IsDebug)
                return preferredXLayers;
            return PercentTurnTimeRemaining > .7 ? preferredXLayers :
                   PercentTurnTimeRemaining > .5 ? Math.Max(1, (int)(preferredXLayers * .75)) :
                   PercentTurnTimeRemaining > .3 ? Math.Max(1, (int)(preferredXLayers * .5)) :
                   PercentTurnTimeRemaining > .2 ? Math.Min((int)(preferredXLayers * .5), 15) :
                   PercentTurnTimeRemaining > .1 ? Math.Min((int)(preferredXLayers * .5), 10) :
                   1;
        }

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
        private static int upperThirdAverage = 0;
        public static int UpperThirdAverage => upperThirdAverage;

        // Game
        public static Game Game;
        public static void ProcessTurn(Game game) {
            GameInfo.Game = game;
            CalculateProjectedShipValues();
            clock.Reset();
            clock.Start();
            upperThirdAverage = (int)(.3 * Map.GetAllCells().OrderByDescending(c => c.halite).Take(Map.width * Map.height / 3).Average(c => c.halite));
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
        public static bool IsMyShipyard(Dropoff d) => IsMyShipyard(d.position);
        public static bool IsMyShipyard(Position p) => p.Equals(MyShipyard.position);
        public static int DropoffXlayers =>  Map.width / 4;

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
        // not correct, needs nullablepublic static int LowestNeighboringOpponentHalite(MapCell c) => c.Neighbors.Where(n => n.IsOccupiedByOpponent()).Min(n => n.ship.halite);
        public static int? LowestNeighboringOpponentHaliteWhereNotReturning(MapCell c) {
            var neighbors = c.Neighbors.Where(n => n.IsOccupiedByOpponent() && !EnemyFleet.IsReturningHome(n.ship));
            if(neighbors.Any())
                return neighbors.Min(n => n.ship.halite);
            else
                return null;
        }
        public static int AvailableMoveCounts(Ship ship, bool includeStill) {
            var dirs = includeStill ? DirectionExtensions.ALL_DIRECTIONS : DirectionExtensions.ALL_CARDINALS;
            int count = 0;
            foreach(var d in dirs) {
                if(!Fleet.CollisionCells.Contains(GameInfo.CellAt(ship, d))) {
                    count++;
                }
            }
            return count;
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

        // Combat related...?
        public static double MyStrengthRatio(Position p, int layers) {
            var cells = Map.GetXLayers(p, layers);
            return (double)(cells.Count(x => x.IsOccupiedByMe())+1) / (cells.Count(x => x.IsOccupiedByOpponent())+1);
        }
    }

    // Virtual Dropoffs
    public class VirtualDropoff {
        public Position Position;
        public VirtualDropoff(Position p) {
            Position = p;
        }
        public MapCell Cell => GameInfo.CellAt(Position);
        public double VirtualDropValue => GameInfo.Map.GetXLayers(Cell.position, GameInfo.DropoffXlayers).
                Sum(x => /* (x.IsOccupiedByOpponent() ? x.ship.halite - 1000 : 0) +*/ x.halite / (1 + GameInfo.Distance(Cell, x)));
    }
}