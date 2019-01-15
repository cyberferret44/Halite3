/*/using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Halite3 {
    public static class ValueMapping2 {
        public static readonly Dictionary<MapCell,double> Mapping = new Dictionary<MapCell, double>();
        private static double TAX = .1;
        public static double GetValue(MapCell cell) => Mapping[cell];

        private static Dictionary<int, Position> previousTurn = new Dictionary<int, Position>();
        private static Dictionary<int, Position> thisTurn = new Dictionary<int, Position>();

        public static Position GetPreviousTarget(Ship s) => previousTurn.ContainsKey(s.Id) ? previousTurn[s.Id]: null;

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

            // update the turn memory
            previousTurn = thisTurn;
            thisTurn = new Dictionary<int, Position>();
            foreach(var ship in Fleet.UsedShips.Where(s => !s.CanMove)) {
                if(previousTurn.ContainsKey(ship.Id)) {
                    thisTurn[ship.Id] = previousTurn[ship.Id];
                    AddNegativeShip(ship, GameInfo.CellAt(previousTurn[ship.Id]));
                }
            }
        }

        private static double AddSpecialConsiderations(double val, Ship ship, MapCell cell) {
            // avoiding lower value enemy
            /* if(ship.CurrentMapCell.Neighbors.Contains(cell)) {
                int? nVal = GameInfo.LowestNeighboringOpponentHaliteWhereNotReturning(cell);
                if(nVal.HasValue) {
                    var haliteDiff = nVal.Value - ship.halite;
                    // todo consider ship counts/values...
                    if(haliteDiff < -200) {
                        // very large difference, severly penalize the cell
                        val = 0;
                    }
                    if(haliteDiff < 0) {
                        // my ship has more value, devalue this move
                        val += (haliteDiff - 20) * 2;  // so a diff of 100 would devalue the cell by 240
                    } else if(haliteDiff > 0) {
                        val += haliteDiff * 2;
                    }
                }
            }*

            if(cell.halite > GameInfo.AverageHalitePerCell && GameInfo.MyClosestDropDistance(cell.position) <= 3 && !GameInfo.IsMyShipyard(GameInfo.MyClosestDrop(cell.position))) {
                val += cell.halite * 5;
            }
            // inspired
            int cellDist = GameInfo.Distance(ship.position, cell.position);
            val *= cell.IsInspired ? Math.Max(3 - cellDist * .75, 1) : 1;


            return val;
        }

        public static MapCell FindBestTarget(Ship ship, int layers = 40) {
            // for debugging
            Position prevTarget = previousTurn.ContainsKey(ship.Id) ? previousTurn[ship.Id] : null;
            double prevVal = 0.0;


            double bestVal = Mapping[ship.CurrentMapCell];// + ship.CellHalite * MyBot.HParams[Parameters.STAY_MULTIPLIER];
            bestVal = AddSpecialConsiderations(bestVal, ship, ship.CurrentMapCell);
            MapCell bestCell = ship.CurrentMapCell;
            if(prevTarget != null && prevTarget.Equals(bestCell.position)) {
                prevVal = bestVal;
            }
            int bestDist = 0;
            foreach(var cell in GameInfo.Map.GetXLayers(ship.position, layers)) {
                double val = Mapping[cell];
                val = AddSpecialConsiderations(val, ship, cell);
                int cellDist = GameInfo.Distance(ship.position, cell.position);
                double supplement = bestDist > cellDist ? cell.halite * MyBot.HParams[Parameters.STAY_MULTIPLIER] + .125 * cell.halite * (bestDist - cellDist) :
                                    bestDist < cellDist ? -bestCell.halite * MyBot.HParams[Parameters.STAY_MULTIPLIER] + .125 * bestCell.halite * (bestDist - cellDist) :
                                    0;
                if(val + supplement > bestVal && Navigation.CalculateGreedyPathOfLeastResistance(cell.position, ship.position, Fleet.OccupiedCells) != null)
                {
                    if(prevTarget != null && prevTarget.Equals(bestCell.position))
                        Log.LogMessage($"1. cell.pos {cell.position.ToString()}... bestVal {bestVal}... supplement... {supplement}... bestDist {bestDist}... cellDist {cellDist}... val {val}...");
                    bestVal = val;
                    bestCell = cell;
                    bestDist = cellDist;
                }

                // debugging
                if(prevTarget != null && prevVal == 0 && prevTarget.Equals(cell.position)) {
                    prevVal = val;
                }
                if(prevTarget != null && prevTarget.Equals(cell.position) && bestCell != cell) {
                    Log.LogMessage($"2. bestCell.pos {bestCell.position.ToString()}... bestVal {bestVal}... supplement... {supplement}... bestDist {bestDist}... cellDist {cellDist}... val {val}... can access {Navigation.CalculateGreedyPathOfLeastResistance(cell.position, ship.position, Fleet.OccupiedCells) != null}");
                }
            }
            // also debugging
            if(prevTarget != null)
                Log.LogMessage($"Ship: {ship.Id}, prevTarget: {prevTarget.ToString()} prevVal: {prevVal}... NewTarg: {bestCell.position.ToString()} newVal: {bestVal}");
            else
                Log.LogMessage($"Ship: {ship.Id}, NewTarg: {bestCell.position.ToString()} newVal: {bestVal}");
            if(bestCell != null)
                thisTurn[ship.Id] = bestCell.position;
            return bestCell;
        }

        // this should be called when the logic has assigned a ship to a particular cell
        public static void AddNegativeShip(Ship ship, MapCell targetCell) {
            int haliteNegative = 900 - ship.halite;
            MapCell curCell = targetCell;
            int min = curCell.halite / 3;
            while(haliteNegative > 0 && curCell.halite > min) {
                int reduction = (int)(curCell.halite * .8);
                if(reduction > haliteNegative) {
                    reduction = haliteNegative;
                }
                Mapping[curCell] = Mapping[curCell] - reduction;
                haliteNegative -= reduction;
                var validNeighbors = curCell.Neighbors.Where(n => thisTurn.Values.All(mc => !mc.Equals(n.position)));
                if(validNeighbors.Count() == 0)
                    break;
                curCell = validNeighbors.OrderByDescending(n => Mapping[n]).First();
                /* var cells = GameInfo.GetXLayersExclusive(targetCell.position, layers);
                var sum = cells.Sum(c => c.halite);
                var ratio = sum * .7 > haliteNegative ? haliteNegative / sum : .7;
                cells.ForEach(c => Mapping[c] = Mapping[c] * ratio);
                haliteNegative -= sum * .7;
                layers++;
                *
            }
        }
    }
}*/