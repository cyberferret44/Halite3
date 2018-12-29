using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping {
        public static readonly Dictionary<MapCell,Value> Mapping = new Dictionary<MapCell, Value>();
        public static readonly HashSet<Ship> NegativeShips = new HashSet<Ship>();
        private static double Exponent => MyBot.HParams[Parameters.TOUCH_RATIO];

        public static double NegativeHalite(Ship ship) {
            double haliteNegative = Math.Max(0, 950 - ship.halite) / 3;
            double haliteMax = GameInfo.Map.GetXLayers(ship.position, 2).Sum(x => Math.Max(0, x.halite - GameInfo.NumToIgnore));
            haliteNegative = Math.Min(haliteMax, haliteNegative);
            haliteNegative = haliteNegative * Math.Min(1.0, (ship.DistanceToMyDropoff + 1.0) / 5.0);
            return haliteNegative;
        }

        public static void ProcessTurn() {
            // clear and seed the map
            Mapping.Clear();
            NegativeShips.Clear();
            foreach(var c in GameInfo.Map.GetAllCells()) {
                double value = Math.Max(0, c.halite - GameInfo.NumToIgnore);
                // increase value for things near a dropoff
                if(GameInfo.MyClosestDropDistance(c.position) < 10) {
                    var extraVal = (10-GameInfo.MyClosestDropDistance(c.position))/2;
                    value *= extraVal;
                }
                Mapping.Add(c, new Value(value));
            }

            // order the cells by current value, then grab xLayers and add the values
            foreach(var kvp in Mapping.OrderByDescending(kvp => kvp.Value.ValueOnly)) {
                var cell = kvp.Key;
                var value = kvp.Value.ValueOnly;
                var xLayers = GameInfo.Map.GetXLayers(cell.position, 6);
                xLayers.Remove(cell);
                foreach(var c in xLayers) {
                    var cVal = Mapping[c].ValueOnly;
                    var dist = GameInfo.Distance(c, cell);
                    var ratio = Math.Pow(Exponent, dist);
                    var newVal = cVal * (1.0 - ratio) + value * ratio;
                    if(cVal < newVal) {
                        Mapping[c].SetValue(newVal);
                    }
                }
            }
        }

        public static void AddNegativeShip(Ship ship, MapCell finalTarget) {
            // Adds ships as negative values... I may not need the if statement after new logic...
            //if(!GameInfo.NumToIgnoreAltered) {
            NegativeShips.Add(ship);
            var haliteNegative = NegativeHalite(ship);
            var layer1 = finalTarget.Neighbors;
            var layer2 = GameInfo.Map.GetXLayersExclusive(finalTarget.position, 2);
            Mapping[finalTarget].negative += haliteNegative;
            layer1.ForEach(l => Mapping[l].negative += haliteNegative/layer1.Count); //todo add negative values to value period... delete negative variable
            layer2.ForEach(l => Mapping[l].negative += haliteNegative/layer2.Count);
            //}
        }

        public static Dictionary<MapCell, double> GetMoveValues(Ship ship, MapCell projectedShipCell, MapCell finalTarget = null) {
            //todo multiply value by 1.0 + .01 * ship.DistanceToMyDropoff * (600 - ship.halite)/1000 
            Dictionary<MapCell, double> vals = new Dictionary<MapCell, double>(); // self and neighbors...
            foreach(var c in projectedShipCell.NeighborsAndSelf) {
                double val = Mapping[c].GetValueForShipMovement(ship, projectedShipCell, c.position.GetDirectionTo(projectedShipCell.position),  finalTarget);
                val += projectedShipCell == c && c.halite > GameInfo.NumToIgnore ? c.halite : 0; // todo play with this...
                //val += c.IsInspired ? c.halite : 0;
                vals.Add(c, val);
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

        public double GetValueForShipMovement(Ship ship, MapCell shipPosition, Direction move, MapCell finalTarget = null) {
            var moveTarget = shipPosition.GetNeighbor(move);
            if(moveTarget.IsMyStructure) {
                //Log.LogMessage("Ship " + ship.Id + " was on a structure");
                return -100000.0; // highly discourage sitting on a structure
            }
            double val = GetValue();
            if(finalTarget != null) {
                int dist = GameInfo.Distance(finalTarget, moveTarget);
                if(dist <= 2 && ValueMapping.NegativeShips.Contains(ship)) { // todo this is hacky...) {
                    var haliteNegative = ValueMapping.NegativeHalite(ship);
                    int divisor = dist == 0 ? 1 : dist * 4;
                    val += haliteNegative / divisor;
                }
            }
            return val;
        }
        public void AddValue(double val) => value += val;
        public double ValueOnly => value;
        public double GetValue() => value - negative; // + addedValue;
        private double value;
        public double negative;
        public MapCell cell;
    }
}