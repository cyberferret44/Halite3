using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping {
        public static readonly Dictionary<MapCell,Value> Mapping = new Dictionary<MapCell, Value>();
        public static readonly HashSet<Ship> NegativeShips = new HashSet<Ship>();

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
                Mapping.Add(c, new Value(c.halite));
            }

            foreach(var cell in GameInfo.Map.GetAllCells().OrderByDescending(c => c.halite)) {
                cell.Neighbors.ForEach(n => TouchCell(n, cell));
            }

            /* if(numToIgnore == GameInfo.NumToIgnore) {
                foreach(var ship in Fleet.AllShips) {
                    var haliteNegative = NegativeHalite(ship);
                    var layer1 = ship.Neighbors;
                    var layer2 = GameInfo.Map.GetXLayersExclusive(ship.position, 2);
                    Mapping[ship.CurrentMapCell].negative += haliteNegative;
                    layer1.ForEach(l => Mapping[l].negative += haliteNegative/layer1.Count);
                    layer2.ForEach(l => Mapping[l].negative += haliteNegative/layer2.Count);
                }
            }*/

            // IF NUM TO IGNORE CHANGED ADD THIS LOGIC..... nemy ships should be factored in as positives
            // because they have cargo and they crash my ships partially negating their values;
            /* foreach(var ship in GameInfo.OpponentShips) {
                var val = ship.halite;
                var layer1 = ship.Neighbors;
                var layer2 = GameInfo.Map.GetXLayers(ship.position, 2).Where(x => !layer1.Contains(x)).ToList();
                Mapping[ship.CurrentMapCell].AddValue(val/3);
                layer1.ForEach(l => Mapping[l].AddValue(val/3/layer1.Count));
                layer2.ForEach(l => Mapping[l].AddValue(val/3/layer2.Count));
            }*/
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

        private static void TouchCell(MapCell cell, MapCell toucher) {
            double newValue = cell.halite * (1.0 - MyBot.HParams[Parameters.TOUCH_RATIO]) + Mapping[toucher].ValueOnly * MyBot.HParams[Parameters.TOUCH_RATIO];
            if(newValue > Mapping[cell].ValueOnly) {
                Mapping[cell].SetValue(newValue);
                cell.Neighbors.ForEach(n => TouchCell(n, cell));
            }
        }

        /* public static Dictionary<MapCell, double> GetMoveValues(MapCell finalTarget, Ship ship, MapCell projectedShipCell = null) {
            projectedShipCell = projectedShipCell == null ? ship.CurrentMapCell : projectedShipCell; //todo multiply value by 1.0 + .01 * ship.DistanceToMyDropoff * (600 - ship.halite)/1000 
            Dictionary<MapCell, double> vals = new Dictionary<MapCell, double>(); // self and neighbors...
            foreach(var c in projectedShipCell.NeighborsAndSelf) {
                double val = Mapping[c].GetValueForShip(finalTarget, c, ship);
                val += ship.CurrentMapCell == c && c.halite > GameInfo.NumToIgnore ? c.halite : 0; // todo play with this...
                val += c.IsInspired ? c.halite : 0;
                vals.Add(c, val);
            }
            return vals;
        }*/

        public static Dictionary<MapCell, double> GetMoveValues(Ship ship, MapCell projectedShipCell, MapCell finalTarget = null) {
            //todo multiply value by 1.0 + .01 * ship.DistanceToMyDropoff * (600 - ship.halite)/1000 
            Dictionary<MapCell, double> vals = new Dictionary<MapCell, double>(); // self and neighbors...
            foreach(var c in projectedShipCell.NeighborsAndSelf) {
                double val = Mapping[c].GetValueForShipMovement(ship, projectedShipCell, c.position.GetDirectionTo(projectedShipCell.position),  finalTarget);
                val += ship.CurrentMapCell == c && c.halite > GameInfo.NumToIgnore ? c.halite : 0; // todo play with this...
                val += c.IsInspired ? c.halite : 0;
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
            if(moveTarget == ship.CurrentMapCell && moveTarget.IsMyStructure) {
                Log.LogMessage("Ship " + ship.Id + " was on a structure");
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
        public double ValueOnly => value;
        public double GetValue() => value - negative; // + addedValue;
        private double value;
        public double negative;
        public MapCell cell;
    }
}