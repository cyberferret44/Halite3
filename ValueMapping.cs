using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping {
        public static Dictionary<MapCell,Value> Mapping = new Dictionary<MapCell, Value>();
        public static int numToIgnore = 0;

        public static double NegativeHalite(Ship ship) {
            double haliteNegative = Math.Max(0, 950 - ship.halite) / 3;
            double haliteMax = GameInfo.Map.GetXLayers(ship.position, 2).Sum(x => Math.Max(0, x.halite - GameInfo.NumToIgnore));
            haliteNegative = Math.Min(haliteMax, haliteNegative);
            haliteNegative = haliteNegative * Math.Min(1.0, (ship.DistanceToMyDropoff + 1.0) / 5.0);
            return haliteNegative;
        }

        public static void ProcessTurn() {
            numToIgnore = Math.Max(numToIgnore, GameInfo.NumToIgnore);

            // clear and seed the map
            Mapping.Clear();
            foreach(var c in GameInfo.Map.GetAllCells()) {
                Mapping.Add(c, new Value(c.halite));
            }

            foreach(var cell in GameInfo.Map.GetAllCells().OrderByDescending(c => c.halite)) {
                cell.Neighbors.ForEach(n => TouchCell(n, cell));
            }

            // because they have cargo and they crash my ships partially negating their values;
            if(numToIgnore == GameInfo.NumToIgnore) {
                foreach(var ship in Fleet.AllShips) {
                    var haliteNegative = NegativeHalite(ship);
                    var layer1 = ship.Neighbors;
                    var layer2 = GameInfo.Map.GetXLayersExclusive(ship.position, 2);
                    Mapping[ship.CurrentMapCell].negative += haliteNegative;
                    layer1.ForEach(l => Mapping[l].negative += haliteNegative/layer1.Count);
                    layer2.ForEach(l => Mapping[l].negative += haliteNegative/layer2.Count);
                }
            }

            // IF NUM TO IGNORE CHANGED ADD THIS LOGIC..... nemy ships should be factored in as positives
            /* foreach(var ship in GameInfo.OpponentShips) {
                var val = ship.halite;
                var layer1 = ship.Neighbors;
                var layer2 = GameInfo.Map.GetXLayers(ship.position, 2).Where(x => !layer1.Contains(x)).ToList();
                Mapping[ship.CurrentMapCell].AddValue(val/3);
                layer1.ForEach(l => Mapping[l].AddValue(val/3/layer1.Count));
                layer2.ForEach(l => Mapping[l].AddValue(val/3/layer2.Count));
            }*/
        }

        private static void TouchCell(MapCell cell, MapCell toucher) {
            double newValue = cell.halite * (1.0 - MyBot.HParams[Parameters.TOUCH_RATIO]) + Mapping[toucher].ValueOnly * MyBot.HParams[Parameters.TOUCH_RATIO];
            if(newValue > Mapping[cell].ValueOnly) {
                Mapping[cell].SetValue(newValue);
                cell.Neighbors.ForEach(n => TouchCell(n, cell)); // todo order by desc might be faster...
            }
        }

        public static Dictionary<MapCell, double> GetMoveValues(MapCell cell, Ship ship) {
            Dictionary<MapCell, double> vals = new Dictionary<MapCell, double>();
            double val = Mapping[cell].GetValueForShip(cell, ship);
            val += cell.halite > GameInfo.NumToIgnore ? cell.halite * 2 : 0; // todo toy with the x2 here
            val += cell.IsInspired ? cell.halite : 0;
            vals.Add(cell, val);
            foreach(var n in cell.Neighbors) {
                val = Mapping[n].GetValueForShip(n, ship);
                val += n.IsInspired ? n.halite : 0;
                vals.Add(n, val);
            }
            return vals;
        }

        public static void MoveShip(Ship ship, Direction d) => MoveShip(ship, GameInfo.CellAt(ship, d));
        public static void MoveShip(Ship ship, MapCell target) {
            if(target == ship.CurrentMapCell)
                return; // do nothing...

            // undo ship's current move
            var haliteNegative = NegativeHalite(ship);
            var layer1 = ship.Neighbors;
            var layer2 = GameInfo.Map.GetXLayersExclusive(ship.position, 2);
            Mapping[ship.CurrentMapCell].negative -= haliteNegative;
            layer1.ForEach(l => Mapping[l].negative -= haliteNegative/layer1.Count);
            layer2.ForEach(l => Mapping[l].negative -= haliteNegative/layer2.Count);

            // Add negative to new position...
            layer1 = GameInfo.Map.GetXLayersExclusive(target.position, 1);
            layer2 = GameInfo.Map.GetXLayersExclusive(target.position, 2);
            Mapping[target].negative += haliteNegative;
            layer1.ForEach(l => Mapping[l].negative += haliteNegative/layer1.Count);
            layer2.ForEach(l => Mapping[l].negative += haliteNegative/layer2.Count);

        }
    }

    public class Value {
        public Value(double val) {
            value = val;
        }

        public void SetValue(double newValue) {
            value = newValue;
        }

        public double GetValueForShip(MapCell cell, Ship ship) {
            double val = GetValue();
            if(ValueMapping.numToIgnore == GameInfo.NumToIgnore) {
                var haliteNegative = ValueMapping.NegativeHalite(ship);
                int dist = GameInfo.Distance(cell, ship.CurrentMapCell);
                int divisor = dist == 0 ? 1 : dist * 4;
                val += haliteNegative / divisor;
            }
            return val;
        }
        public double ValueOnly => value;
        public double GetValue() => value - negative; // + addedValue;
        private double value;
        public double negative;
        public MapCell cell;
        //public int returnValue;
        //private int addedValue;
    }
}