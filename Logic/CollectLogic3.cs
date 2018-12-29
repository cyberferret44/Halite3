using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class CollectLogic3 : Logic
    {
        public CollectLogic3()
        {
            var cellsOrdered = Map.GetAllCells().OrderByDescending(x => x.halite).ToList();
            cellsOrdered = cellsOrdered.Take(cellsOrdered.Count * 2 / 3).ToList();
            GameInfo.NumToIgnore = (int)(cellsOrdered.Average(c => c.halite) * HParams[Parameters.PERCENT_OF_AVERAGE_TO_IGNORE]);
            Log.LogMessage("Num to Ignore: " + GameInfo.NumToIgnore);
        }

        public override void ProcessTurn()
        {
            // adjust the NumToIgnore if need be
            var notEnoughCells = GameInfo.Map.GetAllCells().Where(c => c.halite > GameInfo.NumToIgnore).Count() < GameInfo.TotalShipsCount * GameInfo.Map.width / 16;
            if(notEnoughCells) {
                GameInfo.NumToIgnore = GameInfo.NumToIgnoreAltered ? 1 : GameInfo.NumToIgnore /= 5;
            }
        }

        public override void CommandShips()
        {
            var map = ValueMapping.Mapping; // maps cells to their values
            var projectedTargets = ProjectShipDestinations(Fleet.AvailableShips);

            List<Ship> shipsNearDest = projectedTargets.Where(kvp => GameInfo.Distance(kvp.Key.CurrentMapCell, kvp.Value) <= 2).Select(kvp => kvp.Key).ToList();
            while(shipsNearDest.Count > 0) {
                // find best ship...
                Ship bestShip = null;
                double bestValue = -1.0; // must be negative in case a ship has 2 moves of equal value
                var bestVals = new List<KeyValuePair<MapCell, double>>();
                foreach(var ship in shipsNearDest) {
                    // get vals
                    var vals = GetAdjustedValues(ship, projectedTargets[ship]); // this isn't working correctly, the values
                    // it's producing is in contratrary to the values from projection
                    // this is becasue the value mapping on line 113 is including negative values (post projection) that it #endregion
                    // didn't include on the original iteration...

                    // find diff
                    double diff;
                    if(vals.Count == 0)
                        diff = -1.0;
                    else if (vals.Count == 1) {
                        diff = int.MaxValue;
                    } else {
                        vals = vals.OrderByDescending(x => x.Value).ToList();
                        diff = vals[0].Value - vals[1].Value;
                    }

                    // set best
                    if(diff > bestValue) {
                        bestValue = diff;
                        bestShip = ship;
                        bestVals = vals;
                    }
                }

                if(bestShip == null) {
                    break;
                }
                string msg = $"Moving ship {bestShip.Id} to its best target {bestVals[0].Key.position.ToString()}.  Moves were... ";
                bestVals.ForEach(d => msg += $"{d.Key.position.GetDirectionTo(bestShip.position).ToString("g")}: {d.Value.ToString("0.##")} ...");
                MakeMove(bestShip.Move(bestVals[0].Key.position.GetDirectionTo(bestShip.position), msg));
                shipsNearDest.Remove(bestShip);
            }

            foreach(var ship in Fleet.AvailableShips) {
                // navigate to destination...
                var target = projectedTargets[ship];
                var dirs = target.position.GetAllDirectionsTo(ship.position);
                dirs = dirs.OrderBy(d => GameInfo.CellAt(ship, d).halite).ToList();
                dirs.Add(Direction.STILL);
                if(dirs.Any(d => IsSafeAndAvoids2Cells(ship, d))) {
                    MakeMove(ship.Move(dirs.First(d => IsSafeAndAvoids2Cells(ship, d)), "Moving from collect to destination " + target.position.ToString()));
                }
            }
        }

        public Dictionary<Ship, MapCell> ProjectShipDestinations(List<Ship> ships) {
            var map = ValueMapping.Mapping; // maps cells to their values
            var list = ships.OrderByDescending(s => map[s.CurrentMapCell].GetValue()); // order them by dist from dropoff as they are probably closest to target locations

            var dict = new Dictionary<Ship, MapCell>();
            foreach(var s in list) {
                MapCell target = ProjectNextStillCell(s);
                ValueMapping.AddNegativeShip(s, target);
                dict.Add(s, target);
            }
            return dict;
        }

        public MapCell ProjectNextStillCell(Ship ship) {
            var targetCell = ship.CurrentMapCell;
            Log.LogMessage($"Projecting {ship.Id}...  Available ships {Fleet.AvailableShips.Count}");
            while(true) {
                var vals = ValueMapping.GetMoveValues(ship, targetCell).OrderByDescending(x => x.Value).ToList();
                if(vals[0].Key == targetCell) {
                    break;
                } else {
                    targetCell = vals[0].Key;
                }
            }
            Log.LogMessage($"Ship {ship.Id} projects to {targetCell.position.ToString()}");
            return ship.CurrentMapCell;
        }

        public List<KeyValuePair<MapCell, double>> GetAdjustedValues(Ship ship, MapCell projectedTarget) {
            var vals = ValueMapping.GetMoveValues(ship, ship.CurrentMapCell, projectedTarget).
                        Where(d => IsSafeAndAvoids2Cells(ship, d.Key.position.GetDirectionTo(ship.position))).ToList();

            for(int i=0; i<vals.Count; i++) {
                int? lowestNeighbor = GameInfo.LowestNeighboringOpponentHaliteWhereNotReturning(vals[i].Key);
                if(lowestNeighbor.HasValue) {
                    var diff = lowestNeighbor.Value - ship.halite;
                    if(GameInfo.Is4Player)
                        diff -= 300;
                    else
                        diff += (GameInfo.MyShipsCount - GameInfo.OpponentShipsCount) * 10;
                    vals[i] = new KeyValuePair<MapCell, double>(vals[i].Key, vals[i].Value + (Math.Abs(vals[i].Value) * (lowestNeighbor.Value - ship.halite)/1000.0));
                }

                // this shoudl offset the negative additions from the project logic
                //if(vals[i].Key == projectedTarget) {
                  //  vals[1] = new KeyValuePair<MapCell, double>(vals[i].Key, vals[i].Value + Math.Abs(vals[i].Value) * .1);
                //}
            }
            return vals;
        }
    }
}