using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
using System.Diagnostics;
using System;
namespace Halite3.Logic {
    public class EarlyCollectLogic : Logic
    {
        static Dictionary<int, Point> PreviousTurn = new Dictionary<int, Point>();

        public override void ProcessTurn() { }

        public override void CommandShips()
        {
            var list = new List<Projection>();
            Fleet.AvailableShips.ForEach(s => list.Add(new Projection(s)));
            list = list.OrderBy(p => p.numTurns).ToList();
            while(list.Count > 0) {
                // switch next to a ship on a dropoff if it's surrounded
                var next = list[0];
                foreach(var proj in list.Where(l => l.ship.OnDropoff)) {
                    if(proj.ship.Neighbors.All(n => Fleet.CollisionCells.Contains(n) || n.IsOccupied())) {
                        next = proj;
                        break;
                    }
                }
                var s = next.ship;
                if(next.valuer.TurnsToFill(s, ValueMapping3.IsPreviousTarget(s.Id, next.valuer.Target.position)) != next.numTurns) {
                    list[list.IndexOf(next)] = new Projection(s);
                    list.OrderBy(p => p.numTurns);
                    continue;
                }
                Command move;
                if(!s.CanMove) {
                    move = s.StayStill("Ship cannot move, forcing it to stay still... Target " + next.valuer.Target.position.ToString() + "... Expected Turns: " + next.numTurns);
                }
                else if(!(s.CurrentMapCell.Neighbors.Any(n => n.halite > GameInfo.UpperThirdAverage && n.halite > s.CellHalite * MyBot.HParams[Parameters.STAY_MULTIPLIER]))
                && s.CellHalite > GameInfo.UpperThirdAverage && Safety.IsSafeMove(s, Direction.STILL)) {
                    move = s.StayStill("Forcing ship to sit still... Target " + next.valuer.Target.position.ToString() + "... Expected Turns: " + next.numTurns);
                } else {
                    move = next.GetMove();
                }
                DoMove(move, next.valuer.Target, next.ship.Id);
                list.Remove(next);
            }
        }

        private void DoMove(Command c, MapCell target, int shipId) {
            if(c != null) {
                ValueMapping3.AddNegativeShip(c.Ship, target);
                Fleet.AddMove(c);
            } else {
                Log.LogMessage($"Ship {shipId} tried to move to {target.position.ToString()} but could not.");
            }
        }

        private class Projection {
            public Projection(Ship s) {
                this.valuer = ValueMapping3.FindBestTarget(s);
                this.numTurns = this.valuer.TurnsToFill(s, ValueMapping3.IsPreviousTarget(s.Id, this.valuer.Target.position));
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
                    dirs.AddRange(extraDirs);
                }
                foreach(var d in dirs) {
                    var cell = GameInfo.CellAt(ship, d);
                    if(Safety.IsSafeAndAvoids2Cells(ship, d) && Navigation.IsAccessible(cell.position, valuer.Target.position)) {
                        return ship.Move(d, $"moving towards best projection {valuer.Target.position.ToString()}... Expected turns: {numTurns}");
                    }
                }
                return null;
            }
        }
    }
}