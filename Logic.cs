using Halite3.hlt;
using System.Collections.Generic;
public interface Logic {
    void DoPreProcessing();
    void ProcessTurn();
    List<MapCell> GetBestNeighbors(Position p);
}