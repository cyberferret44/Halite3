using Halite3.hlt;
using System.Collections.Generic;
using System.Linq;

namespace Halite3 {
    public class XLayersMap {
        // Initializer method and static stuff
        private static int width, height, spacing, xLayers;
        private static List<Position> Pivots;
        private static bool IsInitialized => Pivots == null;
        public static void Initialize(GameMap map) {
            width = map.width;
            height = map.height;
            spacing = width / 4;
            xLayers  = spacing * 3 / 4;
            Pivots = new List<Position>();
            // this should create 16 pivots, which will be sub-maps with specific information
            for(int y = 0; y < height; y += spacing /2) {
                for(int x = y%10; x < width; x += spacing) {
                    Pivots.Add(new Position(x, y));
                }
            }
        }
        // todo recommended assigned ship = spacing * 1.5 (less enemy ships, negating one another)
        // turn-by-turn information
        Dictionary<Point, XLayersInfo> LayersMap;
        public XLayersMap(GameMap map) {
            if(!IsInitialized)
                Initialize(map);

            LayersMap = new Dictionary<Point, XLayersInfo>();
            foreach(var pivot in Pivots) {
                LayersMap.Add(pivot.AsPoint, new XLayersInfo(xLayers, pivot));
            }
        }

        public XLayersInfo this[Position p]
        {
            get {
                if(LayersMap.ContainsKey(p.AsPoint)) {
                    return LayersMap[p.AsPoint];
                }
                // todo can optimize this to O(2) instead of O(16)
                int minDist = Pivots.Min(pivot => GameInfo.Distance(pivot, p));
                var info = new XLayersInfo(xLayers, Pivots.First(pivot => pivot == p));
                LayersMap[p.AsPoint] = info;
                return info;
            }
        }
    }
}