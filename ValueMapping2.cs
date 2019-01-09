using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping2 {
        public static readonly Dictionary<MapCell,double> Mapping = new Dictionary<MapCell, double>();
        private static double TAX = .1;
        public static double GetValue(MapCell cell) => Mapping[cell];

        public static void ProcessTurn() {
            // clear and seed the map
            Mapping.Clear();
            foreach(var cell in GameInfo.Map.GetAllCells()) {
                double value = cell.halite;
                var closestDrop = GameInfo.MyClosestDrop(cell.position);
                var polr = Navigation.CalculateGreedyPathOfLeastResistance(cell.position, closestDrop);
                value -= polr.Sum(mc => mc.halite * TAX);
                Mapping.Add(cell, value);
            }
        }

        public static MapCell FindBestTarget(Ship ship, Position previousTarget) {
            double bestVal = Mapping[ship.CurrentMapCell] + ship.CellHalite * 3;
            bestVal *= ship.CurrentMapCell.IsInspired ? 3 : 1;
            MapCell bestCell = ship.CurrentMapCell;
            foreach(var cell in GameInfo.Map.GetAllCells()) {
                double val = Mapping[cell];
                int curDist = GameInfo.Distance(ship.position, bestCell.position);
                int cellDist = GameInfo.Distance(ship.position, cell.position);
                val *= cell.IsInspired ? Math.Max(3.5 - cellDist, 1) : 1;
                double supplement = curDist > cellDist ?
                    supplement = .2 * cell.halite * (curDist - cellDist) :
                    supplement = .2 * bestCell.halite * (curDist - cellDist);
                if(val + supplement > bestVal && Navigation.CalculateGreedyPathOfLeastResistance(cell.position, ship.position, Fleet.OccupiedCells) != null)
                {
                    bestVal = val;
                    bestCell = cell;
                }
            }
            return bestCell;
        }

        // this should be called when the logic has assigned a ship to a particular cell
        public static void AddNegativeShip(Ship ship, MapCell targetCell) {
            double haliteNegative = 1000 - ship.halite;
            var layers = 0;
            while(haliteNegative > 0 && layers < 2) {
                var cells = GameInfo.GetXLayersExclusive(targetCell.position, layers);
                var sum = cells.Sum(c => c.halite);
                var ratio = sum * .7 > haliteNegative ? haliteNegative / sum : .7;
                cells.ForEach(c => Mapping[c] = Mapping[c] * ratio);
                haliteNegative -= sum * .7;
                layers++;
            }
        }
    }
}