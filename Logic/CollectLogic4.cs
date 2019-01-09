using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class CollectLogic4 : Logic
    {
        static Dictionary<int, Point> PreviousTurn = new Dictionary<int, Point>();

        public CollectLogic4() { }

        public override void ProcessTurn() { }

        public override void CommandShips()
        {
            var projections = new List<Projection>();
            Fleet.AvailableShips.ForEach(s => projections.Add(new Projection(s)));
            projections = projections.OrderBy(p => p.Distance).ToList();
            /* foreach(var p in projections.Where(p => PreviousTurn.ContainsKey(p.Ship.Id)).ToList()) {
                if(p.Target.position.Equals(PreviousTurn[p.Ship.Id].AsPosition)) {
                    Command move = p.GetMove();
                    if(move != null) {
                        MakeMove(move);
                        ValueMapping2.AddNegativeShip(p.Ship, p.Target);
                        PreviousTurn[p.Ship.Id] = p.Target.position.AsPoint;
                    }
                    projections.Remove(p);
                }
            }*/
            while(projections.Count > 0) {
                var next = projections[0];
                if(next.Target == null)
                    break;
                if(next.Value != ValueMapping2.GetValue(next.Target)) {
                    // recalculate value and resubmit
                    projections[0] = new Projection(next.Ship);
                    projections = projections.OrderBy(p => p.Distance).ToList();
                } else {
                    Command move = next.GetMove();
                    if(move != null) {
                        MakeMove(move);
                        ValueMapping2.AddNegativeShip(next.Ship, next.Target);
                        PreviousTurn[next.Ship.Id] = next.Target.position.AsPoint;
                    }
                    projections.Remove(next);
                }
            }
        }

        private class Projection {
            public Projection(Ship s) {
                Ship = s;
                var prevTarget = CollectLogic4.PreviousTurn.ContainsKey(Ship.Id) ? GameInfo.CellAt(CollectLogic4.PreviousTurn[Ship.Id]) : null;
                Target = ValueMapping2.FindBestTarget(Ship, prevTarget?.position);
                Value = Target == null ? -100000 : ValueMapping2.GetValue(Target);
            }
            public Command GetMove() {
                var dirs = Target.position.GetAllDirectionsTo(Ship.position);
                dirs = dirs.OrderBy(d => GameInfo.CellAt(Ship.position, d).halite).ToList();
                foreach(var d in dirs) {
                    if(IsSafeAndAvoids2Cells(Ship, d)) {
                        return Ship.Move(d, "moving towards best projection " + Target.position.ToString());
                    }
                }
                return null;
            }
            public int Distance => Target == null ? int.MaxValue : GameInfo.Distance(Ship.position, Target.position);
            public double Value;
            public MapCell Target;
            public Ship Ship;
        }
    }
}