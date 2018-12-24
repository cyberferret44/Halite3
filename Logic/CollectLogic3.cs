using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class CollectLogic3 : Logic
    {
        private bool HasChanged = false;
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
            var notEnoughCells = GameInfo.Map.GetAllCells().Where(c => c.halite > GameInfo.NumToIgnore).Count() < GameInfo.TotalShipsCount* 2;
            if(notEnoughCells) {
                GameInfo.NumToIgnore = HasChanged ? 1 : GameInfo.NumToIgnore /= 5;
                HasChanged = true;
            }
        }

        public override void CommandShips()
        {
            var map = ValueMapping.Mapping; // maps cells to their values
            while(Fleet.AvailableShips.Count > 0) {
                // find best ship...
                Ship bestShip = null;
                double bestValue = 0.0;
                foreach(var ship in Fleet.AvailableShips) {
                    var list = ValueMapping.GetMoveValues(ship.CurrentMapCell, ship).Where(d => IsSafeAndAvoids2Cells(ship, d.Key.position.GetDirectionTo(ship.position))).ToArray();
                    list = list.OrderByDescending(x => x.Value).ToArray();
                    var diff = list.Count() == 0 ? -1 : list.Count() == 1 ? int.MaxValue : list[0].Value - list[1].Value;
                    if(diff > bestValue) {
                        bestValue = diff;
                        bestShip = ship;
                    }
                }

                if(bestShip == null) {
                    break;
                }

                var vals = ValueMapping.GetMoveValues(bestShip.CurrentMapCell, bestShip).Where(d => IsSafeAndAvoids2Cells(bestShip, d.Key.position.GetDirectionTo(bestShip.position))).ToList();
                vals = vals.OrderByDescending(x => x.Value).ToList();
                string msg = $"Moving ship {bestShip.Id} to its best target.  Moves were... ";
                vals.ForEach(d => msg += $"{d.Key.position.GetDirectionTo(bestShip.position).ToString("g")}: {d.Value.ToString("0.##")} ...");
                
                MakeMove(bestShip.Move(vals[0].Key.position.GetDirectionTo(bestShip.position), msg));
            }
        }
    }
}