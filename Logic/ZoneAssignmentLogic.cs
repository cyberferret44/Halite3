using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;
namespace Halite3.Logic {
    public class ZoneAssignmentLogic : Logic
    {
        Dictionary<int, Zone> ZoneAssignments = new Dictionary<int, Zone>();
        public ZoneAssignmentLogic() { }
        public override void ProcessTurn() {
            // remove dead ships from our dictionary
            foreach(var id in ZoneAssignments.Keys.ToList()) {
                if(Fleet.IsDead(id))
                    ZoneAssignments.Remove(id);
            }

            // make sure everything is assigned
            foreach(var id in Fleet.AllShipIds) {
                if(!ZoneAssignments.ContainsKey(id))
                    ZoneAssignments[id] = null;
            }

            // unassign any cells on dropoff so they can be reassigned
            // also unassign any ships which are in their target zone (leave the logic to Collect)
            foreach(var ship in Fleet.AllShips.Where(s => ZoneAssignments.ContainsKey(s.Id))) {
                if(ship.OnDropoff) {
                    ZoneAssignments[ship.Id] = null;
                } else if(ZoneAssignments[ship.Id] != null && ZoneAssignments[ship.Id].InZone(ship)) {
                    ZoneAssignments[ship.Id] = null;
                }
            }

            // assign any cells that
            // 1. Do not exist in the dictionary.
            // 2. Are on dropoff
            // 3. Visibility 2 shows no good cells
            foreach(var ship in Fleet.AllShips) {
                if(!ZoneAssignments.ContainsKey(ship.Id) || ship.OnDropoff || (ZoneAssignments[ship.Id] == null && ship.Visibility2.All(c => c.halite <= GameInfo.NumToIgnore))) {
                    var zone = GetZoneAssignment(ship);
                    ZoneAssignments[ship.Id] = zone;
                    Log.LogMessage($"Ship {ship.Id} was assigned to zone {zone.Position.ToString()}");
                }
            }
        }

        public override void CommandShips()
        {
            foreach(var ship in Fleet.AvailableShips.Where(s => ZoneAssignments[s.Id] != null)) {
                // If not in zone, track to zone
                var zone = ZoneAssignments[ship.Id];
                if(ZoneMap.Zones[ship] != ZoneAssignments[ship.Id]) {
                    // navigate to zone...
                    // new logic, path of least resistance
                    var polr = GameInfo.CalculatePathOfLeastResistance(ship.position, zone.Position);
                    List<Direction> directions = zone.Position.GetAllDirectionsTo(ship.position);
                    if(IsSafeAndAvoids2Cells(ship, polr[0].position.GetDirectionTo(ship.position))) {
                        var best = polr[0].position.GetDirectionTo(ship.position);
                        MakeMove(ship.Move(best, "Moving PoLR to zone " + zone.Position.ToString()));
                    } else if(directions.Any(d => IsSafeMove(ship, d))) {
                        MakeMove(ship.Move(directions.First(d => IsSafeMove(ship, d)), "Moving to zone " + zone.Position.ToString()));
                    } else if (IsSafeMove(ship, Direction.STILL)) {
                        MakeMove(ship.StayStill("Trying to move to zone " + zone.Position.ToString() + " but had to stay still."));
                    }
                }
            }
        }

        public Zone GetZoneAssignment(Ship ship) {
            Zone bestZone = null;
            int fewestTurns = int.MaxValue;
            foreach(var zone in ZoneMap.Zones.List) {
                // calculate effective ships in zone.....
                int haliteConsumable = 0;
                var shipsAssignedButNotInZone = ZoneAssignments.Keys.Count(sid => ZoneAssignments[sid] == zone && !ZoneAssignments[sid].InZone(GameInfo.GetMyShip(sid)));
                haliteConsumable += shipsAssignedButNotInZone * 900;
                haliteConsumable += zone.EnemyShips.Sum(s => Math.Max(900 - s.halite, 0));
                haliteConsumable += zone.MyShips.Where(s => zone.InZone(s) && ZoneAssignments[s.Id] == null).Sum(s => Math.Max(900 - s.halite, 0));
                
                // now estimate halite consumption based on ships in zone
                int halite = zone.AllCells.Sum(c => Math.Max(0, c.halite - GameInfo.NumToIgnore));
                halite -= haliteConsumable; // todo add ships assigned to zone but not in zone...
                int turnsToGather = 0;
                int halGathered = 0;
                while(halGathered < 900) {
                    if(halite <= 900) {
                        turnsToGather = 100000; // cant use int max because of addition below...
                        break;
                    }
                    int avg = halite / zone.AllCells.Count(c => c.halite > GameInfo.NumToIgnore);
                    int thisHal = (int)(.15 * avg);
                    halite -= thisHal;
                    halGathered += thisHal;
                    turnsToGather++;
                }
                var polr = GameInfo.CalculatePathOfLeastResistance(ship.position, zone.Position);
                if(polr != null) {
                    turnsToGather += (int)(polr.Count * 1.5);
                }

                turnsToGather += GameInfo.Distance(zone.Position, GameInfo.MyClosestDrop(zone.Position));
                Log.LogMessage($"Zone {zone.Position.ToString()} had a predicted turns of {turnsToGather}");
                if(fewestTurns > turnsToGather) {
                    fewestTurns = turnsToGather;
                    bestZone = zone;
                }
            }
            Log.LogMessage("The best zone was " + bestZone.Position.ToString());
            return bestZone;
        }
    }
}