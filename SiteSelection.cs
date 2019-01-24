using Halite3.hlt;
using System;
using System.Linq;

namespace Halite3 {
    public static class SiteSelection {
        private static int Spacing;
        private static int HaliteCutoff;
        public static void Initialize() {
            // value initialization
            Spacing = (int)MyBot.HParams[Parameters.DROPOFF_DISTANCE];
            Log.LogMessage($"Xlayers for dropoff is {GameInfo.DropoffXlayers}");
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

                GameInfo.BestDropoffs.Add(new VirtualDropoff(pos));
                availableCells = GameInfo.Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
                Log.LogMessage($"Best drop-off at ({pos.x},{pos.y}) with a value {max}");
            }
            Log.LogMessage("Halite Cutoff: " + HaliteCutoff);
        }

        public static void ProcessTurn() {
            // first make dropoffs...
            foreach(var ship in Fleet.AllShips) {
                if(GameInfo.NextDropoff != null && ship.position.Equals(GameInfo.NextDropoff.Position) && CanCreateDropoff(ship.position)) {
                    Fleet.AddMove(ship.MakeDropoff());
                }
            }

            // iterate bestdropoffs to potentially select next dropoff...
            if(GameInfo.ReserveForDropoff && !ShouldCreateDropoff()) {
                Log.LogMessage("drop-off: save for new dropoff disabled");
                GameInfo.ReserveForDropoff = false;
                GameInfo.NextDropoff = null;
            }

            if(GameInfo.BestDropoffs.Any()) {
                GameInfo.BestDropoffs = GameInfo.BestDropoffs.OrderByDescending(d => d.VirtualDropValue * Math.Pow(.95, GameInfo.MyClosestDropDistance(d.Position))).ToList();
                var bestDrop = GameInfo.BestDropoffs[0];
                
                if(!GameInfo.ReserveForDropoff && ShouldCreateDropoff() && bestDrop.Cell.MyClosestShips().Any(s => GameInfo.Distance(s.position, bestDrop.Position) <= s.DistanceToMyDropoff)) {
                    Log.LogMessage("drop-off: save halite for newdropoff has been flagged as true");
                    GameInfo.ReserveForDropoff = true;
                }

                if(GameInfo.ReserveForDropoff && CanCreateDropoff(bestDrop.Position)) {
                    GameInfo.NextDropoff = GameInfo.BestDropoffs[0];
                    Log.LogMessage($"Next dropoff flagged as {GameInfo.NextDropoff.Position.ToString()}");
                }
            }

            // Delete any dropoffs that have been mostly havested
            foreach(var d in GameInfo.BestDropoffs.ToList()) {
                int halite = (int)d.VirtualDropValue;
                if(GameInfo.Map.At(d.Position).IsStructure || halite < HaliteCutoff) {
                    if(GameInfo.NextDropoff == d) {
                        DeleteNextDropoff();
                    }
                    GameInfo.BestDropoffs.Remove(d);
                    Log.LogMessage($"drop-off at {d.Position.x},{d.Position.y} has been deleted... halite {halite}, cutoff {HaliteCutoff}");
                }
            }
        }
        

        private static int DistanceToClosestVirtualOrRealDropoff(Position position, MapCell exclude = null) {
            int closestReal = GameInfo.Me.GetDropoffs().Min(x => GameInfo.Map.CalculateDistance(position, x));
            var best = GameInfo.BestDropoffs;
            if(exclude != null)
                best = best.Where(d => !d.Cell.position.AsPoint.Equals(exclude.position.AsPoint)).ToList();
            int closestVirtual = best.Any() ? best.Min(x => GameInfo.Map.CalculateDistance(x.Position, position)) : int.MaxValue;
            return Math.Min(closestReal, closestVirtual);
        }

        private static VirtualDropoff GetClosestVirtualDropoff(Position position) {
            if(GameInfo.BestDropoffs.Count == 0)
                return null;
            int closest = GameInfo.BestDropoffs.Min(x => GameInfo.Map.CalculateDistance(position, x.Position));
            return GameInfo.BestDropoffs.First(d => GameInfo.Map.CalculateDistance(position, d.Position) == closest);
        }

        private static bool ShouldCreateDropoff() => Fleet.ShipCount / GameInfo.Me.GetDropoffs().Count >= MyBot.HParams[Parameters.SHIPS_PER_DROPOFF]; // need a minimum of ships per drop
        private static bool CanCreateDropoff(Position pos) {
            //int target = 4000; // 4000 + 1000 for ship cost
            int halite = GameInfo.Me.halite;
            var closestShips = GameInfo.CellAt(pos).MyClosestShips();
            var ship = closestShips.OrderBy(s => s.halite).First();
            halite += ship.halite - Navigation.PathCost(ship.position, pos);
            halite += (int)(.75 * GameInfo.CellAt(pos).halite);
            return halite > 4000;
        }

        private static void DeleteNextDropoff() {
            Log.LogMessage($"Drop-off {GameInfo.NextDropoff.Position.x},{GameInfo.NextDropoff.Position.y} was deleted.");
            GameInfo.ReserveForDropoff = false;
            GameInfo.NextDropoff = null;
        }
    }
}