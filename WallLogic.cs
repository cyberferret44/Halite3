
using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
public class WallLogic : Logic {
    private static GameMap gameMap => MyBot.GameMap;
    public void DoPreProcessing() {

    }

    public void ProcessTurn() {

    }

    public List<MapCell> GetBestNeighbors(Position p) {
        return gameMap.GetXLayers(p, 1);
    } 
}