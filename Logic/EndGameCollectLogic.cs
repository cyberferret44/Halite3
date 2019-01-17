using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class EndGameCollectLogic : Logic
    {
        Dictionary<int, Point> ShipTargets = new Dictionary<int, Point>();
        private int min => /* GameInfo.Me.id.id == 1 ?*/ (int)(GameInfo.AverageHalitePerCell * .3);// : (GameInfo.AverageHalitePerCell > 10 ? 10 : 0);

        public override void ProcessTurn() {

        }

        public int GetCellValue(Ship ship, MapCell cell) {
            int initialVal = cell.IsInspired ? cell.halite * 3 : cell.halite;
            var polr = Navigation.CalculatePathOfLeastResistance(ship.position, cell.position);
            var resistance = polr.Sum(x => (int)(x.halite * .2));
            return initialVal - resistance;
        }

        public override void CommandShips()
        {
            //if(Me.id.id == 1) {
                // purge the ships
                PurgeUnavailableShips();

                // select targets
                foreach(var ship in Fleet.AvailableShips.Where(s => !ShipTargets.ContainsKey(s.Id))) {
                    var xLayers = GameInfo.RateLimitXLayers(10);
                    var cells = GameInfo.Map.GetXLayers(ship.position, xLayers);
                    while(!ShipTargets.ContainsKey(ship.Id) && xLayers < GameInfo.Map.width && xLayers <= GameInfo.RateLimitXLayers(xLayers)) {
                        cells = cells.Where(c =>  c.halite > min && !ShipTargets.Values.Any(v => v.Equals(c.position.AsPoint))).ToList();
                        cells = cells.OrderByDescending(x => GetCellValue(ship, x)).ToList();
                        foreach(var cell in cells) {
                            if(Navigation.IsAccessible(ship.position, cell.position)) {
                                ShipTargets.Add(ship.Id, cell.position.AsPoint);
                                break;
                            }
                        }
                        xLayers++;
                        cells = GameInfo.GetXLayersExclusive(ship.position, xLayers);
                    }
                }

                foreach(var ship in Fleet.AvailableShips.Where(s => ShipTargets.ContainsKey(s.Id))) {
                    var targetCell = GameInfo.CellAt(ShipTargets[ship.Id]);
                    if(targetCell.position.GetAllDirectionsTo(ship.position).Any(d => IsSafeMove(ship, d))) {
                        var dir = targetCell.position.GetAllDirectionsTo(ship.position).First(d => IsSafeMove(ship, d));
                        MakeMove(ship.Move(dir, $"Moving to best target {targetCell.position.ToString()} End of Game Logic"));
                    }
                }
            /* } else {
                // purge the ships
                PurgeUnavailableShips();

                // select targets
                foreach(var ship in Fleet.AvailableShips.Where(s => !ShipTargets.ContainsKey(s.Id))) {
                    var xLayers = GameInfo.RateLimitXLayers(8);
                    var cells = GameInfo.Map.GetXLayers(ship.position, xLayers);
                    while(!ShipTargets.ContainsKey(ship.Id) && xLayers < GameInfo.Map.width && xLayers <= GameInfo.RateLimitXLayers(xLayers)) {
                        cells = cells.Where(c =>  c.halite > min && !ShipTargets.Values.Any(v => v.Equals(c.position.AsPoint))).ToList();
                        cells = cells.OrderByDescending(x => x.halite).ToList();
                        foreach(var cell in cells) {
                            if(Navigation.IsAccessible(ship.position, cell.position)) {
                                ShipTargets.Add(ship.Id, cell.position.AsPoint);
                                break;
                            }
                        }
                        xLayers++;
                        cells = GameInfo.GetXLayersExclusive(ship.position, xLayers);
                    }
                }

                foreach(var ship in Fleet.AvailableShips.Where(s => ShipTargets.ContainsKey(s.Id))) {
                    var targetCell = GameInfo.CellAt(ShipTargets[ship.Id]);
                    if(targetCell.position.GetAllDirectionsTo(ship.position).Any(d => IsSafeMove(ship, d))) {
                        var dir = targetCell.position.GetAllDirectionsTo(ship.position).First(d => IsSafeMove(ship, d));
                        MakeMove(ship.Move(dir, $"old: Moving to best target {targetCell.position.ToString()} End of Game Logic"));
                    }
                }
            }*/
        }

        private void PurgeUnavailableShips() {
            HashSet<int> availableIds = Fleet.AvailableIds.ToHashSet();
            foreach(var id in ShipTargets.Keys.ToList()) {
                var ship = GameInfo.GetMyShip(id);
                if(!availableIds.Contains(id) ||
                        GameInfo.CellAt(ShipTargets[id]).halite <= min ||
                        !Navigation.IsAccessible(ship.position, ShipTargets[id].AsPosition)) {
                    ShipTargets.Remove(id);
                }
            }
        }
    }
}