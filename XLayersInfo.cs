using Halite3;
using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

// This is a class for harvesting data to be used for logic and, more importantly, scoring moves
// Everything should be lazy loaded since the class likely will only be instantiated to get one piece of information
namespace Halite3 {
    public class XLayersInfo {
        private Dictionary<int, List<MapCell>> Layers = new Dictionary<int, List<MapCell>>();
        private List<MapCell> Cells = new List<MapCell>();
        private readonly Position position;
        public  XLayersInfo(int layers, Position position) {
            this.position = position;
            Cells = GameInfo.Map.GetXLayers(position, layers);
            foreach(var cell in Cells) {
                int dist = GameInfo.Map.CalculateDistance(position, cell.position);
                if(!Layers.ContainsKey(dist)) {
                    Layers[dist] = new List<MapCell>();
                }
                Layers[dist].Add(cell);
            }
        }

        public int NumEnemyShips => Cells.Where(c => c.IsOccupiedByOpponent()).Count();
        public List<Ship> EnemyShips => Cells.Where(c => c.IsOccupiedByOpponent()).Select(c => c.ship).ToList();
        public int NumMyShips => Cells.Where(c => c.IsOccupiedByMe()).Count();
        public int MyShipMargin => NumMyShips - NumEnemyShips;
        public double MyShipRatio => NumMyShips / Math.Min(1, NumEnemyShips);
        public int Halite => Cells.Sum(c => c.halite);
        public int NumCells => Cells.Count;
        public int HalitePerCell => Halite / NumCells;
        public List<MapCell> BestNCells(double topPercent) => Cells.OrderByDescending(c => c.halite).Take((int)(Cells.Count * topPercent)).ToList();
        public int HaliteInBestNCells(double topPercent) => BestNCells(topPercent).Sum(c => c.halite);
        public int HalitePerBestNCells(double topPercent) => HaliteInBestNCells(topPercent) / BestNCells(topPercent).Count;
        public int NumMyDropoffs => Cells.Where(c => c.IsStructure && c.structure.IsMine).Count();
        public int NumOpponentDropoffs => Cells.Where(c => c.IsStructure && c.structure.IsOpponents).Count();
        public int MyDropoffMargin => NumMyDropoffs - NumOpponentDropoffs;
        public List<MapCell> AllCells => Cells;
        public Ship MyClosestShip() {
            for(int i=0; i<= Layers.Count(); i++) {
                if(Layers[i].Any(cell => cell.IsOccupiedByMe())) {
                    return Layers[i].First(cell => cell.IsOccupiedByMe()).ship;
                }
            }
            return null;
        }

    }
}