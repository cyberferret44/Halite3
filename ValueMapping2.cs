using Halite3.hlt;
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

        public static MapCell FindBestTarget(Ship ship, Position previousTarget, int layers = 40) {
            // for debugging
            Position prevTarget = previousTurn.ContainsKey(ship.Id) ? previousTurn[ship.Id] : null;
            double prevVal = 0.0;


            double bestVal = Mapping[ship.CurrentMapCell] + ship.CellHalite * MyBot.HParams[Parameters.STAY_MULTIPLIER];
            bestVal *= ship.CurrentMapCell.IsInspired ? 2 : 1;
            MapCell bestCell = ship.CurrentMapCell;
            int curDist = 0;
            foreach(var cell in GameInfo.Map.GetXLayers(ship.position, layers)) {
                double val = Mapping[cell];
                int cellDist = GameInfo.Distance(ship.position, cell.position);
                val *= cell.IsInspired ? Math.Max(3.5 - cellDist, 1) : 1;
                double supplement = curDist > cellDist ? .2 * cell.halite * (curDist - cellDist) :
                                    curDist < cellDist ? .2 * bestCell.halite * (curDist - cellDist) :
                                    0;
                if(val + supplement > bestVal && Navigation.CalculateGreedyPathOfLeastResistance(cell.position, ship.position, Fleet.OccupiedCells) != null)
                {
                    if(prevTarget != null && prevTarget.Equals(bestCell.position))
                        Log.LogMessage($"1. cell.pos {cell.position.ToString()}... bestVal {bestVal}... supplement... {supplement}... curDist {curDist}... cellDist {cellDist}... val {val}...");
                    bestVal = val;
                    bestCell = cell;
                    curDist = cellDist;
                }

                // debugging
                if(prevTarget != null && prevTarget.Equals(cell.position)) {
                    prevVal = val;
                }
                if(prevTarget != null && prevTarget.Equals(cell.position) && bestCell != cell) {
                    Log.LogMessage($"2. bestCell.pos {bestCell.position.ToString()}... bestVal {bestVal}... supplement... {supplement}... curDist {curDist}... cellDist {cellDist}... val {val}... can access {Navigation.CalculateGreedyPathOfLeastResistance(cell.position, ship.position, Fleet.OccupiedCells) != null}");
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