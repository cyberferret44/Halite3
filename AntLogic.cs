using Halite3.hlt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Halite3
{
    public class AntLogic : Logic
    {
        private static GameMap gameMap => MyBot.GameMap;
        private Dictionary<Point, double> CellValues = new Dictionary<Point, double>();

        // Magic Numbers that could be tuned
        private static double MoveDegredation => 1.0 - (1.0/(double)gameMap.width);
        private static int XLayers => gameMap.width/16;
        private static int XNeighbors => 4*XLayers/2;
        //private static double CellTurnDegredation = .97;

        public void DoPreProcessing() {
            CellValues = new Dictionary<Point, double>();

            foreach(var row in gameMap.cells) {
                foreach(MapCell cell in row) {
                    CellValues[cell.position.CartesianPosition] = ValueOfCell(cell);
                }
            }
            foreach(var kvp in CellValues.OrderByDescending(k => k.Value)) {
                SetAntScentRecursive(gameMap.At(new Position(kvp.Key.x, kvp.Key.y)), kvp.Value);
            }
        }

        private void SetAntScentRecursive(MapCell cell, double value) {
            if(CellValues[cell.position.CartesianPosition] >= value)
                return;
            
            CellValues[cell.position.CartesianPosition] = value;
            value *= MoveDegredation;
            foreach(var n in gameMap.NeighborsAt(cell.position)) {
                SetAntScentRecursive(n, value);
            }
        }

        private static int ValueOfCell(MapCell cell) {
            var neighbors = gameMap.GetXLayers(cell.position, XLayers); // todo magic number, but probably good, as all map sizes are multiples of 8
            return neighbors.OrderByDescending(n => n.halite).Take(XNeighbors).Sum(n => n.halite);
        }


        public void ProcessTurn() {
            DoPreProcessing();
            //CellValues.Keys.ToList().ForEach(k => CellValues[k] *= CellTurnDegredation);
            // todo get top 10% most valuable and recalculate X random ones for like one second or seomthing
        }

        public List<MapCell> GetBestNeighbors(Position p) {
            return gameMap.GetXLayers(p, 1).OrderByDescending(n => CellValues[n.position.CartesianPosition]).ToList();
        }

        /*public static void WriteToFile() {
            string val = "";
            for(int y=0; y<gameMap.height; y++) {
                for(int x=0; x<gameMap.height; x++) {
                    if(x != 0)
                        val += ",";
                    val += ValueOfCell(gameMap.At(new Position(x, y)));
                }
                val += "\n";
            }

            using(StreamWriter sw = File.AppendText(Guid.NewGuid().ToString() + ".csv")) {
                sw.Write(val);              
            }
        }*/
    }
}
