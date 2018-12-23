using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping {
        public static Dictionary<MapCell,Value> Mapping = new Dictionary<MapCell, Value>();

        public static void ProcessTurn() {
            // clear and seed the map
            Mapping.Clear();
            foreach(var c in GameInfo.Map.GetAllCells()) {
                Mapping.Add(c, new Value(c.halite));
            }

            foreach(var cell in GameInfo.Map.GetAllCells().OrderByDescending(c => c.halite)) {
                cell.Neighbors.ForEach(n => TouchCell(n, cell));
            }

            // enemy ships should be factored in as positives
            // because they have cargo and they crash my ships partially negating their values;
            foreach(var ship in Fleet.AllShips) {
                var haliteNegative = Math.Max(0, 950 - ship.halite); // todo maybe
                var layer1 = ship.Neighbors;
                var layer2 = GameInfo.Map.GetXLayers(ship.position, 2).Where(x => !layer1.Contains(x)).ToList();
                Mapping[ship.CurrentMapCell].negative += haliteNegative/3;
                layer1.ForEach(l => Mapping[l].negative += haliteNegative/3/layer1.Count);
                layer2.ForEach(l => Mapping[l].negative += haliteNegative/3/layer2.Count);
            }

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
            int newValue = (int)(cell.halite * (1.0 - MyBot.HParams[Parameters.TOUCH_RATIO])) + (int)(Mapping[toucher].ValueOnly * MyBot.HParams[Parameters.TOUCH_RATIO]);
            if(newValue > Mapping[cell].ValueOnly) {
                Mapping[cell].SetValue(newValue);
                cell.Neighbors.ForEach(n => TouchCell(n, cell)); // todo order by desc might be faster...
            }
        }

        public static Dictionary<MapCell, int> GetVMoveValues(MapCell cell, Ship ship) {
            Dictionary<MapCell, int> vals = new Dictionary<MapCell, int>();
            int val = (int)Mapping[cell].GetValueForShip(cell, ship);
            val += cell.halite > GameInfo.NumToIgnore ? cell.halite * 2 : 0;
            val += cell.IsInspired ? cell.halite : 0;
            vals.Add(cell, val);
            foreach(var n in cell.Neighbors) {
                val = (int)Mapping[n].GetValueForShip(n, ship);
                val += n.IsInspired ? n.halite : 0;
                vals.Add(n, val);
            }
            return vals;
        }
    }

    public class Value {
        public Value(int val) {
            value = val;
        }
        public void AddValue(int val) {
            addedValue += val;
        }
        private Value() {}
        public int ValueOnly => value;
        public Value NewVal(double reduce) {
            return new Value {
                value = (int)(this.value - reduce - this.negative),
                returnValue = this.returnValue,
                cell = this.cell
            };
        }

        public void SetValue(int newValue) {
            value = newValue;
        }

        public int GetValueForShip(MapCell cell, Ship ship) {
            var haliteNegative = Math.Max(0, 950 - ship.halite) / 3; // todo maybe
            int val = GetValue();
            int dist = GameInfo.Distance(cell, ship.CurrentMapCell);
            int divisor = dist == 0 ? 1 : dist * 4;
            val += haliteNegative / divisor;
            return val;
        }
        public int GetValue() => value + addedValue - negative;
        private int value;
        public int returnValue;
        public int negative;
        public MapCell cell;
        private int addedValue;
    }
}