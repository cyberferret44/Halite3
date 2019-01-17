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
        public override void ProcessTurn() {}

        public int GetCellValue(Ship ship, MapCell cell) {
            int initialVal = cell.IsInspired ? cell.halite * 3 : cell.halite;
            if(ship.CurrentMapCell == cell)
                initialVal *= 3;
            var polr = Navigation.CalculatePathOfLeastResistance(ship.position, cell.position);
            int resistance = polr.Sum(x => (int)(x.halite * .1));
            return initialVal - resistance;
        }

        public override void CommandShips()
        {
            // purge the ships
            PurgeUnavailableShips();

            // select targets
            foreach(var ship in Fleet.AvailableShips) {
                var xLayers = GameInfo.RateLimitXLayers(10);
                var cells = GameInfo.Map.GetXLayers(ship.position, xLayers);
                MapCell target = ShipTargets.ContainsKey(ship.Id) ? GameInfo.CellAt(ShipTargets[ship.Id]) : null;
                int maxVal = target == null ? -100000 : GetCellValue(ship, target);
                do {
                    cells = cells.Where(c => !ShipTargets.Values.Any(v => v.Equals(c.position.AsPoint))).ToList();
                    foreach(var c in cells) {
                        int val = GetCellValue(ship, c);
                        int oppCost = 0;
                        if(target != null) {
                            int distDiff = GameInfo.Distance(ship, c.position) - GameInfo.Distance(ship, target.position);
                            oppCost = distDiff < 0 ? distDiff * (int)(c.halite * .125) : // cell is closet to ship than curTarget
                                distDiff * (int)(target.halite * .125); // distDiff is 0/positive, cell is further than curTarget
                        }
                        if(val - oppCost > maxVal) {
                            maxVal = val;
                            target = c;
                        }
                    }
                    xLayers++;
                    cells = GameInfo.GetXLayersExclusive(ship.position, xLayers);
                } while(target == null && xLayers <= Math.Min(GameInfo.Map.width, GameInfo.RateLimitXLayers(xLayers)));
                if(target != null)
                    ShipTargets[ship.Id] = target.position.AsPoint;
            }

            foreach(var ship in Fleet.AvailableShips.Where(s => ShipTargets.ContainsKey(s.Id))) {
                var targetCell = GameInfo.CellAt(ShipTargets[ship.Id]);
                if(targetCell.position.GetAllDirectionsTo(ship.position).Any(d => IsSafeMove(ship, d))) {
                    var dir = targetCell.position.GetAllDirectionsTo(ship.position).First(d => IsSafeMove(ship, d));
                    MakeMove(ship.Move(dir, $"Moving to best target {targetCell.position.ToString()} End of Game Logic"));
                }
            }
        }

        private void PurgeUnavailableShips() {
            HashSet<int> availableIds = Fleet.AvailableIds.ToHashSet();
            foreach(var id in ShipTargets.Keys.ToList()) {
                var ship = GameInfo.GetMyShip(id);
                if(!availableIds.Contains(id) ||
                        !Navigation.IsAccessible(ship.position, ShipTargets[id].AsPosition)) {
                    ShipTargets.Remove(id);
                }
            }
        }
    }
}