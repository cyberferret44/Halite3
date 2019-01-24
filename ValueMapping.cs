using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping {
        public static readonly Dictionary<MapCell, CellValuer> Mapping = new Dictionary<MapCell, CellValuer>();
        private static Dictionary<int, Position> previousTargets = new Dictionary<int, Position>();
        private static Dictionary<int, Position> theseTargets = new Dictionary<int, Position>();
        public static bool IsPreviousTarget(int shipId, Position p) => previousTargets.ContainsKey(shipId) && previousTargets[shipId].Equals(p);

        public static void ProcessTurn() {
            // clear and seed the map
            previousTargets = theseTargets;
            theseTargets = new Dictionary<int, Position>();
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
            var sameAsPrevious = previousTargets.ContainsKey(ship.Id) && previousTargets[ship.Id].Equals(ship.position);
            double turnsToFill = bestCell.TurnsToFill(ship, sameAsPrevious);
            int layers = GameInfo.RateLimitXLayers(Math.Min(GameInfo.Map.width, (int)turnsToFill));
            var cells = GameInfo.Map.GetXLayers(ship.position, Math.Min(GameInfo.Map.width, layers));
            cells = RemoveBadCells(cells);
            foreach(var cell in cells) {
                sameAsPrevious = previousTargets.ContainsKey(ship.Id) && previousTargets[ship.Id].Equals(cell.position);
                CellValuer tempValuer = Mapping[cell];
                double tempTurnsToFill = tempValuer.TurnsToFill(ship, sameAsPrevious);
                if((bestCell.Target == ship.CurrentMapCell && ship.CellHalite < 25) || tempTurnsToFill < turnsToFill) {
                    if(Navigation.IsAccessible(ship.position, cell.position, true)) {
                        turnsToFill = tempTurnsToFill;
                        bestCell = tempValuer;
                    }
                }
            }
            theseTargets[ship.Id] = bestCell.Target.position;
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
            this.closestDropDist = GameInfo.MyClosestDropDistance(cell.position); // store to save computing resources...
        }
        public MapCell Target => cell;
        public void ReduceValue(int val) { value -= val; }
        public int Value => value;
        private MapCell cell;
        private int value;
        private int closestDropDist;
        public double TurnsToFill(Ship ship, bool IsPrevious) {
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
                int extraTurns = 1 + (int)(remainingToFill / (areaVal * .125 + 1));
                totalTurns += 2 + extraTurns; // estimate # of turns to fill from nearby, +1 prevents /0
                remainingToFill -= (int)(extraTurns * (areaVal * .125 + 1));
            }
            totalTurns += closestDropDist;
            double res = totalTurns + (remainingToFill / 1000.0);
            if(IsPrevious) {
                res *= .95; // bias to previous move
            }

            return (double)totalTurns + (remainingToFill / 1000.0); // differentiate 2 moves of same turns to prevent ships from swapping
        }
    }
}