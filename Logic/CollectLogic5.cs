using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class CollectLogic5 : Logic
    {
        static Dictionary<int, Point> PreviousTurn = new Dictionary<int, Point>();

        public CollectLogic5() { }

        public override void ProcessTurn() { }

        public override void CommandShips()
        {
            var list = new List<Projection>();
            Fleet.AvailableShips.ForEach(s => list.Add(new Projection(s)));
            list = list.OrderBy(p => p.numTurns).ToList();
            // todo this should use reinforcement or a scoring model...
            foreach(var s in Fleet.AvailableShips) {
                var projection = list.Single(p => p.ship == s);
                if(!(s.CurrentMapCell.Neighbors.Any(n => n.halite > GameInfo.UpperThirdAverage && n.halite > s.CellHalite * 4))
                && s.CellHalite > GameInfo.UpperThirdAverage && !Fleet.CollisionCells.Contains(s.CurrentMapCell)) {
                    DoMove(s.StayStill("Forcing ship to sit still..."), projection.valuer.Target);
                }
            }
            foreach(var s in Fleet.AvailableShips) {
                var projection = list.Single(p => p.ship == s);
                if(s.CurrentMapCell.Neighbors.Any(n => n.halite > GameInfo.UpperThirdAverage && n.halite > s.CellHalite * 4)) {
                    var neighbors = s.CurrentMapCell.Neighbors.Where(n => n.halite > GameInfo.UpperThirdAverage && n.halite > s.CellHalite * 4);
                    neighbors = neighbors.OrderByDescending(n => n.halite);
                    foreach(var n in neighbors) {
                        var d = n.position.GetDirectionTo(s.position);
                        if(IsSafeAndAvoids2Cells(s, d)) {
                            DoMove(s.Move(d, $"greedy moving towards {n.position.ToString()}"), projection.valuer.Target);
                            break;
                        }
                    }
                }
            }

            list = list.Where(p => Fleet.ShipAvailable(p.ship)).ToList();
            while(list.Count > 0) {
                var next = list[0];
                if(next.valuer.TurnsToFill(next.ship) != next.numTurns) {
                    list[0] = new Projection(next.ship);
                    list.OrderBy(p => p.numTurns);
                    continue;
                }
                DoMove(next.GetMove(), next.valuer.Target);
                list.Remove(next);
            }
        }

        private void DoMove(Command c, MapCell target) {
            if(c != null) {
                ValueMapping3.AddNegativeShip(c.Ship, target);
                MakeMove(c);
            }
        }

        private class Projection {
            public Projection(Ship s) {
                this.valuer = ValueMapping3.FindBestTarget(s);
                this.numTurns = this.valuer.TurnsToFill(s);
                this.ship = s;
            }
            public CellValuer valuer;
            public Ship ship;
            public int numTurns;
            public Command GetMove() {
                var dirs = valuer.Target.position.GetAllDirectionsTo(ship.position);
                dirs = dirs.OrderBy(d => GameInfo.CellAt(ship.position.DirectionalOffset(d)).halite).ToList();
                foreach(var d in dirs) {
                    if(IsSafeAndAvoids2Cells(ship, d)) {
                        return ship.Move(d, $"moving towards best projection {valuer.Target.position.ToString()}. Expected turns: {numTurns}");
                    }
                }
                return null;
            }
        }
    }
}