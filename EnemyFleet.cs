using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3 {
    public static class EnemyFleet {
        private static HashSet<int> ships = new HashSet<int>();
        private static Dictionary<int, int> LastDistanceDictionary = new Dictionary<int, int>();
        private static Dictionary<int, int> SuccessiveMovesBack = new Dictionary<int, int>();
        public static void UpdateFleet() {
            var enemyShips = GameInfo.OpponentShips.ToHashSet();
            var eIds = enemyShips.Select(x => x.Id).ToHashSet();

            // Remove any dead ships
            foreach(var shipId in ships.ToList()) {
                if(!eIds.Contains(shipId)) {
                    ships.Remove(shipId);
                    if(LastDistanceDictionary.ContainsKey(shipId)) {
                        LastDistanceDictionary.Remove(shipId);
                    }
                    if(SuccessiveMovesBack.ContainsKey(shipId)) {
                        LastDistanceDictionary.Remove(shipId);
                    }
                }
            }

            foreach(var ship in enemyShips) {
                if(!ships.Contains(ship.Id)) {
                    ships.Add(ship.Id);
                    LastDistanceDictionary[ship.Id] = 0;
                    SuccessiveMovesBack[ship.Id] = 0;
                } else {
                    var dist = ship.DistanceToOwnerDropoff;
                    var previousDist = LastDistanceDictionary[ship.Id];
                    LastDistanceDictionary[ship.Id] = dist;
                    if(dist != previousDist)
                        SuccessiveMovesBack[ship.Id] = dist < previousDist ? SuccessiveMovesBack[ship.Id] + 1 : 0;
                }
            }
        }

        public static bool IsReturningHome(Ship enemyShip) {
            if(!SuccessiveMovesBack.ContainsKey(enemyShip.Id)) {
                return false;
            }
            return SuccessiveMovesBack[enemyShip.Id] > 1;
        }
    }
}