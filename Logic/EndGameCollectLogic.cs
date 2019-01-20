using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class EndGameCollectLogic : Logic
    {
        Dictionary<Point, Assignment> PointAssignments = new Dictionary<Point, Assignment>();
        Dictionary<Ship, Assignment> ShipAssignments = new Dictionary<Ship, Assignment>();
        public override void ProcessTurn() {}

        public int GetCellValue(Ship ship, MapCell cell) {
            int initialVal = cell.IsInspired ? cell.halite * 3 : cell.halite;
            if(ship.CurrentMapCell.position.Equals(cell.position))
                initialVal *= 3;
            var polr = Navigation.CalculatePathOfLeastResistance(ship.position, cell.position);
            int resistance = polr.Sum(x => (int)(x.halite * .1));
            return initialVal - resistance;
        }

        public Assignment AssignAndReturnPrevAssignIfAny(Ship ship, MapCell cell) {
            Assignment otherAssignment = null;
            if(PointAssignments.ContainsKey(cell.position.AsPoint)) {
                otherAssignment = PointAssignments[cell.position.AsPoint];
                ShipAssignments.Remove(otherAssignment.Ship);
                PointAssignments.Remove(otherAssignment.Target.position.AsPoint);
            }
            var newAssignment = new Assignment(ship, cell);
            ShipAssignments[ship] = newAssignment;
            PointAssignments[cell.position.AsPoint] = newAssignment;
            return otherAssignment;
        }

        public override void CommandShips()
        {
            ShipAssignments.Clear();
            PointAssignments.Clear();

            // stay still...
            foreach(var s in Fleet.AvailableShips.Where(s => !s.CanMove)) {
                Fleet.AddMove(s.StayStill("Ship cannot move, forcing it to stay still..."));
            }

            // select targets
            Queue<Ship> queue = new Queue<Ship>();
            Fleet.AvailableShips.ForEach(s => queue.Enqueue(s));

            while(queue.Count > 0) {
                var s = queue.Dequeue();
                var xLayers = GameInfo.RateLimitXLayers(15);
                var cells = GameInfo.Map.GetXLayers(s.position, xLayers);
                MapCell target = s.CurrentMapCell;
                int maxVal = GetCellValue(s, target);
                do {
                    foreach(var c in cells) {
                        var otherAssign = PointAssignments.ContainsKey(c.position.AsPoint) ? PointAssignments[c.position.AsPoint] : null; //.FirstOrDefault(a => a.Target.position.Equals(c.position));
                        if(otherAssign != null && GameInfo.Distance(s, c.position) >= otherAssign.Distance) {
                            continue;
                        }

                        // value calculation...
                        int val = GetCellValue(s, c);
                        int distDiff = GameInfo.Distance(s, c.position) - GameInfo.Distance(s, target.position);
                        int oppCost = distDiff < 0 ? distDiff * (int)(c.halite * .125) : // cell is closet to ship than curTarget
                            distDiff * (int)(target.halite * .125); // distDiff is 0/positive, cell is further than curTarget
                        if(val - oppCost > maxVal && Navigation.IsAccessible(s.position, c.position)) {
                            maxVal = val;
                            target = c;
                        }
                    }
                    xLayers++;
                    cells = GameInfo.GetXLayersExclusive(s.position, xLayers);
                } while(target == null && xLayers <= Math.Min(GameInfo.Map.width, GameInfo.RateLimitXLayers(xLayers)));

                if(target != null) {
                    //var newAssignment = new Assignment(s, target);
                    //Assignments.Add(new Assignment(s, target));
                    var otherTarget = AssignAndReturnPrevAssignIfAny(s, target);
                    if(otherTarget != null) {
                        //Assignments.Remove(otherTarget);
                        //Log.LogMessage($"Ship {otherTarget.Ship.Id} was requeued");
                        queue.Enqueue(otherTarget.Ship);
                    }
                }
            }
            var vals = ShipAssignments.Values.OrderBy(a => a.Distance);
            foreach(var a in vals) {
                var dirs = a.Target.position.GetAllDirectionsTo(a.Ship.position);
                dirs = dirs.OrderBy(d => GameInfo.CellAt(a.Ship, d).halite).ToList();
                if(dirs.Any(d => Safety.IsSafeMove(a.Ship, d))) {
                    var dir = dirs.First(d => Safety.IsSafeMove(a.Ship, d));
                    Fleet.AddMove(a.Ship.Move(dir, $"Moving to best target {a.Target.position.ToString()} End Game Collect Logic"));
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