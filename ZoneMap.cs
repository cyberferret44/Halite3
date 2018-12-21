using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;

namespace Halite3 {
    // USAGE
    // ZoneMap.Zones[Position] will give you the zone assigned to that position, that's it
    public class ZoneMap {
        // Initializer method and static stuff
        private static int width, height, spacing, xLayers;
        private static List<Zone> zoneMap;
        private static int turnFlag;
        public static ZoneMap Zones => Instance == null ? Initialize() : turnFlag != GameInfo.TurnNumber ? Reinitialize() : Instance;
        private static ZoneMap Instance;
        public List<Zone> List => zoneMap;
        public static int Range => xLayers;

        public static ZoneMap Initialize() {
            Log.LogMessage("initialized");
            width = GameInfo.Map.width;
            height = GameInfo.Map.height;
            spacing = width / 4;
            Log.LogMessage("spacing = " + spacing);
            xLayers  = spacing * 3 / 4;
            Log.LogMessage("xLayers = " + xLayers);
            zoneMap = new List<Zone>();
            Instance = new ZoneMap();
            // this should create 32 zones, which will be sub-maps with specific information.  They start ordering from my ship yard.
            var sp = GameInfo.MyShipyard.position;
            for(int y = 0; y < height; y += spacing /2) {
                for(int x = y%spacing; x < width; x += spacing) {
                    zoneMap.Add(new Zone(new Position(sp.x + x, sp.y + y), xLayers));
                }
            }

            zoneMap.ForEach(z => z.Update(GameInfo.Map));
            turnFlag = GameInfo.TurnNumber;
            return Instance;
        }

        public static ZoneMap Reinitialize() {
            Log.LogMessage("Re-initialized");
            zoneMap.ForEach(z => z.Update(GameInfo.Map));
            turnFlag = GameInfo.TurnNumber;
            return Instance;
        }

        public Zone this[Ship s]
        {
            get {
                // TODO can optimize this to O(2) instead of O(64)
                int minDist = zoneMap.Min(zone => GameInfo.Distance(zone.Position, s.position));
                return zoneMap.First(zone => GameInfo.Distance(zone.Position, s.position) == minDist);
            }
        }
    }
}