using System.Linq;
using System.Collections.Generic;
using Halite3.hlt;
using System;
namespace Halite3 {
    public class Zone {
        List<MapCell> ZoneCells;
        Position RootCell;
        int Layers;
        public Zone(Position position, int numLayers) {
            RootCell = position;
            ZoneCells = GameInfo.Map.GetXLayers(position, numLayers);
            Layers = numLayers;
        }

        public int NumEnemyShips => ZoneCells.Count(x => x.IsOccupiedByOpponent);
        public List<Ship> MyShips => ZoneCells.Where(x => x.IsOccupiedByMe).Select(x => x.ship).ToList();
        public int NumMyShips => ZoneCells.Count(x => x.IsOccupiedByMe);
        public List<Ship> EnemyShips => ZoneCells.Where(x => x.IsOccupiedByOpponent).Select(x => x.ship).ToList();

        // a safety ratio
        public double SafetyRatio => CalculateSafetyRatio();
        private double CalculateSafetyRatio() {
            if(NumMyShips + NumEnemyShips == 0)
                return 1.0;
            
            double myVal = MyShips.Sum(s => 1 / Math.Sqrt(1+GameInfo.Distance(s, RootCell)));
            double enemyVal = EnemyShips.Sum(s => 1 / Math.Sqrt(1+GameInfo.Distance(s, RootCell)));
            return (.5+myVal) / (myVal + enemyVal);
        }

        // likelihood my ships can recover
        public double CargoRecoveryLikelihood(Ship myCrashedShip, Ship enemyCrashedShip) {
            // edge cases, cell is a structure
            if(GameInfo.CellAt(RootCell).IsOpponentsStructure)
                return 0;
            if(GameInfo.CellAt(RootCell).IsMyStructure)
                return 1.0;

            // do score calculation
            double myPoints = 0;
            double enemyPoints = 0;

            for(int i=1; i<Layers; i++) {
                var cells = GameInfo.Map.GetXLayers(RootCell, i);
                int closestEnemyDrop = cells.Any(c => c.IsOpponentsStructure) ? cells.Where(c => c.IsOpponentsStructure).Min(c => GameInfo.Distance(RootCell, c.position)) : -1;
                int closestMyDrop = cells.Any(c => c.IsMyStructure) ? cells.Where(c => c.IsMyStructure).Min(c => GameInfo.Distance(RootCell, c.position)) : -1;
                foreach(var c in cells.Where(c => c.IsOccupied())) {
                    double points = 1.0/Math.Sqrt(i);
                    var deduction = c.ship.halite;
                    if(c.IsOccupiedByMe) {
                        deduction = closestMyDrop >= 0 ? deduction * i/Layers : deduction;
                        points *= (1000.0 - deduction)/1000.0;
                        myPoints += points;
                    } else {
                        deduction = closestEnemyDrop >= 0 ? deduction * i/Layers : deduction;
                        points *= (1000.0 - deduction)/1000.0;
                        enemyPoints += points;
                    }
                }
            }
            if(myPoints + enemyPoints == 0)
                return 0;
            double destructionRatio = 1.0;
            destructionRatio = (enemyCrashedShip.halite + 1000.0) / (myCrashedShip.halite + 1000.0);
            return myPoints / (myPoints + enemyPoints) * destructionRatio;
        }
    }
}