using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class EndGameCollectLogic : Logic
    {
        List<Assignment> Assignments = new List<Assignment>();
        //private Dictionary<int, Point> ShipTargets = new Dictionary<int, Point>();
        //private Dictionary<Point, int> CellTargets = new Dictionary<Point, int>();
        public override void ProcessTurn() {}

        public int GetCellValue(Ship ship, MapCell cell) {
            int initialVal = cell.IsInspired ? cell.halite * 3 : cell.halite;
            if(ship.CurrentMapCell == cell)
                initialVal *= 3;
            var polr = Navigation.CalculatePathOfLeastResistance(ship.position, cell.position);
            int resistance = polr.Sum(x => (int)(x.halite * .1));
            return initialVal - resistance;
        }

        public Assignment AssignAndReturnPrevAssignIfAny(Ship ship, MapCell cell) {
            Assignments.RemoveAll(a => a.Ship.Id == ship.Id);
            Assignment prevAssign = Assignments.FirstOrDefault(a => a.Target.position.Equals(cell.position));
            if(prevAssign != null && GameInfo.Distance(ship, cell.position) >= GameInfo.Distance(prevAssign.Ship, cell.position))
                throw new Exception("this shouldn't happen...");
            if(prevAssign != null)
                Assignments.Remove(prevAssign);

            Assignments.Add(new Assignment(ship, cell));
            return prevAssign;
        }

        public override void CommandShips()
        {
            // purge the ships
            PurgeUnavailableShips();

            // select targets
            Queue<Ship> queue = new Queue<Ship>();
            Fleet.AvailableShips.ForEach(s => queue.Enqueue(s));

            while(queue.Count > 0) {
                var s = queue.Dequeue();
                var xLayers = GameInfo.RateLimitXLayers(20);
                var cells = GameInfo.Map.GetXLayers(s.position, xLayers);
                var prevTarget = Assignments.FirstOrDefault(a => a.Ship.Id == s.Id);
                MapCell target = null; //prevTarget.Target;
                int maxVal = -1000000; //target == null ? -100000 : GetCellValue(s, target);
                do {
                    foreach(var c in cells) {
                        var otherAssign = Assignments.FirstOrDefault(a => a.Target.position.Equals(c.position));
                        if(otherAssign != null && otherAssign.Distance < GameInfo.Distance(s, c.position)) {
                            continue;
                        }

                        // value calculation...
                        int val = GetCellValue(s, c);
                        int oppCost = 0;
                        if(target != null) {
                            int distDiff = GameInfo.Distance(s, c.position) - GameInfo.Distance(s, target.position);
                            oppCost = distDiff < 0 ? distDiff * (int)(c.halite * .125) : // cell is closet to ship than curTarget
                                distDiff * (int)(target.halite * .125); // distDiff is 0/positive, cell is further than curTarget
                        }
                        if(val - oppCost > maxVal) {
                            maxVal = val;
                            target = c;
                        }
                    }
                    xLayers++;
                    cells = GameInfo.GetXLayersExclusive(s.position, xLayers);
                } while(target == null && xLayers <= Math.Min(GameInfo.Map.width, GameInfo.RateLimitXLayers(xLayers)));
                /* if(target != null) {
                    Log.LogMessage($"Ship {s.Id} value of sitting still at {s.position.ToString()} is {GetCellValue(s, s.CurrentMapCell)}");
                    if(prevTarget != null)
                        Log.LogMessage($"prev target for ship {s.Id} was {prevTarget.position.ToString()} with value of {GetCellValue(s, prevTarget)}");
                    Log.LogMessage($"New target is {target.position.ToString()} with value {GetCellValue(s, target)}");
                }*/
                if(target != null) {
                    Assignments.Add(new Assignment(s, target));
                    var otherTarget = AssignAndReturnPrevAssignIfAny(s, target);
                    if(otherTarget != null) {
                        Assignments.Remove(otherTarget);
                        Log.LogMessage($"Ship {otherTarget.Ship.Id} was requeued");
                        queue.Enqueue(otherTarget.Ship);
                    }
                }
            }

            foreach(var a in Assignments) {
                var dirs = a.Target.position.GetAllDirectionsTo(a.Ship.position);
                dirs = dirs.OrderBy(d => GameInfo.CellAt(a.Ship, d).halite).ToList();
                if(dirs.Any(d => Safety.IsSafeMove(a.Ship, d))) {
                    var dir = dirs.First(d => Safety.IsSafeMove(a.Ship, d));
                    Fleet.AddMove(a.Ship.Move(dir, $"Moving to best target {a.Target.position.ToString()} End Game Collect Logic"));
                }
            }
        }

        private void PurgeUnavailableShips() {
            foreach(var a in Assignments.ToList()) {
                if(Fleet.IsDead(a.Ship.Id) || !Navigation.IsAccessible(a.Ship.position, a.Target.position)) {
                    Assignments.Remove(a);
                }
            }
        }

        public class Assignment {
            public readonly Ship Ship;
            public readonly MapCell Target;
            public Assignment(Ship ship, MapCell target) {
                this.Target = target;
                this.Ship = ship;
            }
            public int Distance => GameInfo.Distance(Ship.position, Target.position);
        }
    }
}