using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
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
                    var vals = ValueMapping.GetMoveValues(ship, ship.CurrentMapCell, projectedTargets[ship]).
                               Where(d => IsSafeAndAvoids2Cells(ship, d.Key.position.GetDirectionTo(ship.position))).
                               OrderByDescending(x => x.Value).ToList();
                    var diff = vals.Count() == 0 ? -1 : vals.Count() == 1 ? int.MaxValue : vals[0].Value - vals[1].Value;
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
            while(true) {
                var vals = ValueMapping.GetMoveValues(ship, targetCell).OrderByDescending(x => x.Value).ToList();
                if(!vals.Any()) {
                    ExceptionHandler.Raise("GetMoveValues didn't return any results, this is unexpected.");
                }
                if(vals[0].Key == targetCell) {
                    break;
                } else {
                    targetCell = vals[0].Key;
                }
            }
            return ship.CurrentMapCell;
        }
    }
}