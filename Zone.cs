using Halite3;
using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

// This is a class for harvesting data to be used for logic and, more importantly, scoring moves
// Everything should be lazy loaded since the class likely will only be instantiated to get one piece of information
namespace Halite3 {
    public class Zone {
        private Dictionary<int, List<MapCell>> Layers = new Dictionary<int, List<MapCell>>();
        private List<MapCell> Cells = new List<MapCell>();
        public readonly Position Position;
        private readonly int layers;
        public Zone(Position position, int layers) {
            this.Position = GameInfo.Map.Normalize(position);
            this.layers = layers;
        }

        public bool InZone(Ship ship) => GameInfo.Distance(ship.position, Position) <= ZoneMap.Range;
        
        public void Update(GameMap map) {
            Cells = map.GetXLayers(Position, layers);
            foreach(var cell in Cells) {
                int dist = map.CalculateDistance(Position, cell.position);
                if(!Layers.ContainsKey(dist)) {
                    Layers[dist] = new List<MapCell>();
                }
                Layers[dist].Add(cell);
            }
        }

        public int NumEnemyShips => Cells.Count(c => c.IsOccupiedByOpponent());
        public List<Ship> EnemyShips => Cells.Where(c => c.IsOccupiedByOpponent()).Select(c => c.ship).ToList();
        public List<Ship> MyShips => Cells.Where(c => c.IsOccupiedByMe()).Select(c => c.ship).ToList();
        public List<Ship> AllShips => Cells.Where(c => c.IsOccupied()).Select(c => c.ship).ToList();
        public int NumMyShips => Cells.Count(c => c.IsOccupiedByMe());
        public int NumShips => NumMyShips + NumEnemyShips;
        public int MyShipMargin => NumMyShips - NumEnemyShips;
        public double MyShipRatio => NumMyShips / Math.Min(1, NumEnemyShips);
        public int Halite => Cells.Sum(c => c.halite);
        public int HalitePlusShips => Halite + AllShips.Sum(s => s.halite);
        public int NumCells => Cells.Count;
        public int HalitePerCell => Halite / NumCells;
        public List<MapCell> BestNCells(double topPercent) => Cells.OrderByDescending(c => c.halite).Take((int)(Cells.Count * topPercent)).ToList();
        public int HaliteInBestNCells(double topPercent) => BestNCells(topPercent).Sum(c => c.halite);
        public int HalitePerBestNCells(double topPercent) => HaliteInBestNCells(topPercent) / BestNCells(topPercent).Count;
        public int NumMyDropoffs => Cells.Where(c => c.IsStructure && c.structure.IsMine).Count();
        public int NumOpponentDropoffs => Cells.Where(c => c.IsStructure && c.structure.IsOpponents).Count();
        public int MyDropoffMargin => NumMyDropoffs - NumOpponentDropoffs;
        public List<MapCell> AllCells => Cells;
        public int AverageHalitePerCell => (int)Cells.Average(c => c.halite);
        public Ship MyClosestShip() {
            for(int i=0; i<= Layers.Count(); i++) {
                if(Layers[i].Any(cell => cell.IsOccupiedByMe())) {
                    return Layers[i].First(cell => cell.IsOccupiedByMe()).ship;
                }
            }
            return null;
        }

        public int AnticipatedHalite(int numTurns) => (int) Math.Pow(HalitePlusShips - (NumShips * HalitePerCell * .125 / HalitePerCell), numTurns);
    }
}