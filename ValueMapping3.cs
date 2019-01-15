using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping3 {
        public static readonly Dictionary<MapCell, CellValuer> Mapping = new Dictionary<MapCell, CellValuer>();

        public static void ProcessTurn() {
            // clear and seed the map
            Mapping.Clear();
            GameInfo.Map.GetAllCells().ForEach(c => Mapping.Add(c, new CellValuer(c)));
        }

        public static CellValuer FindBestTarget(Ship ship) {
            CellValuer bestCell = Mapping[ship.CurrentMapCell];
            int bestTime = bestCell.TurnsToFill(ship);
            int layers = Math.Min(GameInfo.Map.width, bestTime);
            var cells = GameInfo.Map.GetXLayers(ship.position, Math.Min(GameInfo.Map.width, layers));
            //var cells = GetCellsExcludingShipCurrent(ship);
            foreach(var cell in cells) {
                CellValuer tempValuer = Mapping[cell];
                int tempTurnsToFill = tempValuer.TurnsToFill(ship);
                // todo if equal take further one
                if(tempTurnsToFill < bestTime) {
                    bestTime = tempTurnsToFill;
                    bestCell = tempValuer;
                }
            }
            return bestCell;
        }

        /* private static List<MapCell> GetCellsExcludingShipCurrent(Ship ship) {
            HashSet<MapCell> results = new HashSet<MapCell>{ ship.CurrentMapCell };
            Stack<MapCell> stack = new Stack<MapCell>(ship.Neighbors.Union(GameInfo.MyDropoffs.Select(c => GameInfo.CellAt(c))));
            while(stack.Any()) {
                var n = stack.Pop();
                if(!ShouldStay(n)) {
                    var nToAdd = n.Neighbors.Where(neighbor => !results.Contains(neighbor));
                    nToAdd.ToList().ForEach(x => stack.Push(x));
                }
                results.Add(n);
            }
            return results.ToList();
        }*/

        // this should be called when the logic has assigned a ship to a particular cell
        public static void AddNegativeShip(Ship ship, MapCell targetCell) {
            int haliteNegative = (int)(1000 - ship.halite);
            int layers = 0;
            while(haliteNegative > 0 && layers < 2) {
                var cells = GameInfo.GetXLayersExclusive(targetCell.position, layers);
                int sum = cells.Sum(c => Mapping[c].Value);
                var ratio = Math.Min(((double)haliteNegative / (double)sum), .7);
                cells.ForEach(c => Mapping[c].ReduceValue((int)(Mapping[c].Value * ratio)));
                haliteNegative -= (int)(sum * .7)+1;
                layers++;
            }
        }
    }


    public class CellValuer {
        private static double divisor = .71428; // It's 5 / 7
        public CellValuer(MapCell cell) {
            this.cell = cell;
            this.value = cell.halite;
            this.closestDropDist = GameInfo.MyClosestDropDistance(cell.position); // store to save computing resources...
        }
        public MapCell Target => cell;
        public void ReduceValue(int val) { value -= val; }
        public int Value => value;
        private MapCell cell;
        private int value;
        private int closestDropDist;
        public int TurnsToFill(Ship ship) {
            int areaVal = (int)GameInfo.Map.GetXLayers(cell.position, 2, true).Average(c => ValueMapping3.Mapping[c].Value);
            int remainingToFill = 900 - ship.halite;
            int totalTurns = (int)(GameInfo.Distance(ship.position, cell.position) / divisor);
            int remainingCellValue = Value; // value can/should be modified by reduce function
            while(remainingToFill > 0 && remainingCellValue * .25 >= areaVal * .125) {
                int amountMined = (int)(remainingCellValue * .25) + 1;
                remainingCellValue -= amountMined;
                remainingToFill -= amountMined;
                totalTurns++;
            }
            if(remainingToFill > 0) {
                totalTurns += (int)(remainingToFill / (areaVal * .125 + 1)); // estimate # of turns to fill from nearby, +1 prevents /0
            }
            totalTurns += closestDropDist;
            return totalTurns;
        }
    }
}