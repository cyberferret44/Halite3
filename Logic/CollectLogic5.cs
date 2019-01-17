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
            //list = list.Where(p => Fleet.ShipAvailable(p.ship)).ToList();
            list = list.OrderBy(p => p.numTurns).ToList();
            //Log.LogMessage("time to project " + w.ElapsedMilliseconds);
            while(list.Count > 0) {
                var next = list.Any(l => l.ship.OnDropoff) ? list.First(l => l.ship.OnDropoff) : list[0];
                var s = next.ship;
                if(next.valuer.TurnsToFill(s) != next.numTurns) {
                    list[list.IndexOf(next)] = new Projection(s);
                    list.OrderBy(p => p.numTurns);
                    continue;
                }
                var move = next.GetMove();
                if(!s.CanMove) {
                    move = s.StayStill("Ship cannot move, forcing it to stay still... Target " + next.valuer.Target.position.ToString() + "... Expected Turns: " + next.numTurns);
                }
                // todo can test this if staement for performance to remove
                else if(!(s.CurrentMapCell.Neighbors.Any(n => n.halite > GameInfo.UpperThirdAverage && n.halite > s.CellHalite * MyBot.HParams[Parameters.STAY_MULTIPLIER]))
                && s.CellHalite > GameInfo.UpperThirdAverage && !Fleet.CollisionCells.Contains(s.CurrentMapCell)) {
                    move = s.StayStill("Forcing ship to sit still... Target " + next.valuer.Target.position.ToString() + "... Expected Turns: " + next.numTurns);
                }
                
                DoMove(move, next.valuer.Target, next.ship.Id);
                list.Remove(next);
            }
        }

        private void DoMove(Command c, MapCell target, int shipId) {
            if(c != null) {
                ValueMapping3.AddNegativeShip(c.Ship, target);
                MakeMove(c);
            } else {
                Log.LogMessage($"Ship {shipId} tried to move to {target.position.ToString()} but could not.");
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
            public double numTurns;
            public Command GetMove() {
                var dirs = valuer.Target.position.GetAllDirectionsTo(ship.position);
                dirs = dirs.OrderBy(d => GameInfo.CellAt(ship.position.DirectionalOffset(d)).halite).ToList();
                if(dirs.Count == 1 && GameInfo.Distance(ship, valuer.Target.position) > 1) {
                    var extraDirs = DirectionExtensions.GetLeftRightDirections(dirs[0]);
                    dirs.AddRange(extraDirs); // todo maybe could optimize this one...
                }
                foreach(var d in dirs) {
                    var cell = GameInfo.CellAt(ship, d);
                    if(IsSafeAndAvoids2Cells(ship, d) && Navigation.IsAccessible(cell.position, valuer.Target.position)) {
                        return ship.Move(d, $"moving towards best projection {valuer.Target.position.ToString()}... Expected turns: {numTurns}");
                    }
                }
                return null;
            }
        }
    }
}