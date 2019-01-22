using Halite3.hlt;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Halite3 {
    public static class DropoffHandler {
        //  public values
        public static Shipyard MyShipyard => GameInfo.Me.shipyard;
        public static List<Position> MyEffectiveDropoffs => GetDropoffs();
        public static Position MyClosestDrop(Position p) => MyEffectiveDropoffs.OrderBy(x => GameInfo.Distance(p, x)).First();
        public static int MyClosestDropDistance(Position p) => GameInfo.Distance(p, MyClosestDrop(p));
        public static bool IsMyShipyard(Dropoff d) => IsMyShipyard(d.position);
        public static bool IsMyShipyard(Position p) => p.Equals(MyShipyard.position);

        // private values
        private static List<VirtualDropoff> BestDropoffs = new List<VirtualDropoff>();

        private static List<Position> GetDropoffs()  {
            HashSet<Point> dropoffs = GameInfo.Me.Dropoffs.Select(d => d.AsPoint).ToHashSet();
            if(NextDropoff != null) {
                dropoffs.Add(NextDropoff.Position.AsPoint);
            }
            return dropoffs.Select(x => x.AsPosition).ToList();
        }

        private static int Spacing;
        private static int HaliteCutoff;
        public static void Initialize() {
            // value initialization
            Spacing = (int)MyBot.HParams[Parameters.DROPOFF_DISTANCE];
            Log.LogMessage($"Actual Dropoff Distance is {Spacing}");

            var availableCells = GameInfo.Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
            while(availableCells.Count > 0) {
                int max = -1;
                Position pos = null;
                foreach(var cell in availableCells) {
                    var virtDrop = new VirtualDropoff(cell.position);
                    if(virtDrop.VirtualDropValue > max || (virtDrop.VirtualDropValue == max && GameInfo.Map.CalculateDistance(cell.position, GameInfo.Me.shipyard.position) < GameInfo.Map.CalculateDistance(pos, GameInfo.Me.shipyard.position))) {
                        pos = cell.position;
                        max = (int)virtDrop.VirtualDropValue;
                    }
                }
                HaliteCutoff = Math.Max(max/3, HaliteCutoff);
                if(max * .7 < HaliteCutoff)
                    break;

                BestDropoffs.Add(new VirtualDropoff(pos));
                availableCells = GameInfo.Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
                Log.LogMessage($"Best drop-off at ({pos.x},{pos.y}) with a value {max}");
            }
            Log.LogMessage("Halite Cutoff: " + HaliteCutoff);
        }

        public static void ProcessTurn() {
            // first make dropoffs...
            foreach(var ship in Fleet.AllShips) {
                if(NextDropoff != null && ship.position.Equals(NextDropoff.Position) && CanCreateDropoff(ship.position)) {
                    Fleet.AddMove(ship.MakeDropoff());
                }
            }

            // iterate bestdropoffs to potentially select next dropoff...
            if(GameInfo.ReserveForDropoff && !ShouldCreateDropoff()) {
                GameInfo.ReserveForDropoff = false;
                NextDropoff = null;
            }

            if(BestDropoffs.Any()) {
                BestDropoffs = BestDropoffs.OrderByDescending(d => d.VirtualDropValue * Math.Pow(.95, MyClosestDropDistance(d.Position))).ToList();
                var bestDrop = BestDropoffs[0];
                
                if(!GameInfo.ReserveForDropoff && ShouldCreateDropoff() && bestDrop.Cell.MyClosestShips().Any(s => GameInfo.Distance(s.position, bestDrop.Position) <= s.DistanceToMyDropoff)) {
                    Log.LogMessage("drop-off: save halite for newdropoff has been flagged as true");
                    GameInfo.ReserveForDropoff = true;
                }

                if(GameInfo.ReserveForDropoff && CanCreateDropoff(bestDrop.Position)) {
                    NextDropoff = BestDropoffs[0];
                    Log.LogMessage($"Next dropoff flagged as {NextDropoff.Position.ToString()}");
                }
            }

            // Delete any dropoffs that have been mostly havested
            foreach(var d in BestDropoffs.ToList()) {
                int halite = (int)d.VirtualDropValue;
                if(GameInfo.Map.At(d.Position).IsStructure || halite < HaliteCutoff) {
                    if(NextDropoff == d) {
                        DeleteNextDropoff();
                    }
                    BestDropoffs.Remove(d);
                    Log.LogMessage($"drop-off at {d.Position.x},{d.Position.y} has been deleted... halite {halite}, cutoff {HaliteCutoff}");
                }
            }
        }

        private static int DistanceToClosestVirtualOrRealDropoff(Position position, MapCell exclude = null) {
            int closestReal = MyEffectiveDropoffs.Min(x => GameInfo.Map.CalculateDistance(position, x));
            var best = BestDropoffs;
            if(exclude != null)
                best = best.Where(d => !d.Cell.position.AsPoint.Equals(exclude.position.AsPoint)).ToList();
            int closestVirtual = best.Any() ? best.Min(x => GameInfo.Map.CalculateDistance(x.Position, position)) : int.MaxValue;
            return Math.Min(closestReal, closestVirtual);
        }

        private static VirtualDropoff GetClosestVirtualDropoff(Position position) {
            if(BestDropoffs.Count == 0)
                return null;
            int closest = BestDropoffs.Min(x => GameInfo.Map.CalculateDistance(position, x.Position));
            return BestDropoffs.First(d => GameInfo.Map.CalculateDistance(position, d.Position) == closest);
        }

        private static bool ShouldCreateDropoff() => Fleet.ShipCount / MyEffectiveDropoffs.Count >= 14 ; // need a minimum of ships per drop
        private static bool CanCreateDropoff(Position pos) => GameInfo.Me.halite + GameInfo.Map.At(pos).halite + 500 >= 5000;

        private static void DeleteNextDropoff() {
            Log.LogMessage($"Drop-off {NextDropoff.Position.x},{NextDropoff.Position.y} was deleted.");
            GameInfo.ReserveForDropoff = false;
            NextDropoff = null;
        }

        public static int NewDropoffDeduction() {
            if(NextDropoff == null)
                return 0;
            var newDropHalite = NextDropoff.Cell.halite;
            var s = Fleet.MyClosestShips(NextDropoff.Position).OrderByDescending(x => x.halite).FirstOrDefault();
            if(s == null)
                return newDropHalite;
            var resistance = Navigation.PathCost(s.position, NextDropoff.Position);
            return (int)(s.halite + newDropHalite - resistance);
        }
    }
    
    // Virtual Dropoffs
    public class VirtualDropoff {
        private static int DropoffXlayers = GameInfo.Map.width / 4;
        public Position Position;
        public VirtualDropoff(Position p) {
            Position = p;
        }
        bool IsActivated = false;
        bool ShouldCreate = false;
        public MapCell Cell => GameInfo.CellAt(Position);
        public double VirtualDropValue => GameInfo.Map.GetXLayers(Cell.position, DropoffXlayers).Sum(x => x.halite / (1 + GameInfo.Distance(Cell, x)));
    }
}