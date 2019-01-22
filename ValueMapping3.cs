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

        public static List<MapCell> RemoveBadCells(List<MapCell> cells) {
            var result = new List<MapCell>();
            foreach(var cell in cells) {
                int areaVal = (int)GameInfo.Map.GetXLayers(cell.position, 2, true).Average(c => ValueMapping3.Mapping[c].Value);
                if(ValueMapping3.Mapping[cell].Value > areaVal / 2) {
                    result.Add(cell);
                }
            }
            return result;
        }

        public static CellValuer FindBestTarget(Ship ship) {
            CellValuer bestCell = Mapping[ship.CurrentMapCell];
            double turnsToFill = bestCell.TurnsToFill(ship);
            int layers = GameInfo.RateLimitXLayers(Math.Min(GameInfo.Map.width, (int)turnsToFill));
            var cells = GameInfo.Map.GetXLayers(ship.position, Math.Min(GameInfo.Map.width, layers));
            cells = RemoveBadCells(cells);
            foreach(var cell in cells) {
                CellValuer tempValuer = Mapping[cell];
                double tempTurnsToFill = tempValuer.TurnsToFill(ship);
                if((bestCell.Target == ship.CurrentMapCell && ship.CellHalite < 25) || tempTurnsToFill < turnsToFill) {
                    if(Navigation.IsAccessible(ship.position, cell.position, true)) {
                        turnsToFill = tempTurnsToFill;
                        bestCell = tempValuer;
                    }
                }
            }
            return bestCell;
        }

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
            this.closestDropDist = DropoffHandler.MyClosestDropDistance(cell.position); // store to save computing resources...
        }
        public MapCell Target => cell;
        public void ReduceValue(int val) { value -= val; }
        public int Value => value;
        private MapCell cell;
        private int value;
        private int closestDropDist;
        public double TurnsToFill(Ship ship) {
            int areaVal = (int)GameInfo.Map.GetXLayers(cell.position, 2, true).Average(c => ValueMapping3.Mapping[c].Value);
            int remainingToFill = (int)MyBot.HParams[Parameters.CARGO_TO_MOVE] - ship.halite;
            int totalTurns = (int)(GameInfo.Distance(ship.position, cell.position) / divisor);
            int remainingCellValue = Value; // value can/should be modified by reduce function
            while(remainingToFill > 0 && remainingCellValue * .25 >= areaVal * .125) {
                int amountMined = (int)(remainingCellValue * .25) + 1;
                remainingCellValue -= amountMined;
                remainingToFill -= amountMined;
                totalTurns++;
            }
            if(remainingToFill > 0) {
                totalTurns += 2 + (int)(remainingToFill / (areaVal * .125 + 1)); // estimate # of turns to fill from nearby, +1 prevents /0
            }
            totalTurns += closestDropDist;

            return (double)totalTurns + (remainingToFill / 1000.0); // differentiate 2 moves of same turns to prevent ships from swapping
        }
    }
}