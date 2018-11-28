using Halite3.hlt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using GeneticTuner;
using System.IO;

/* TODOS
Low hanging
- create separate hyperparameters for num players and board size
-- create a batch file folder with multiple other previous bots and run them against each other
-- add a hyper parameter to look at halite per cell remaining instead of looking at turns remaining in order to decide when to save

Difficult
- add a preprocessing and prioritization strategy
- add a javascript suite to visualize the genetic algorithm and run multiple runs
- ant scent implementation 

Things to consider
- utilize command line args to pass a hyperparameters.txt file (using HyperParameters.txt by default)
    and altering run.bat to pass in Halite3.HyperParameters.txt.  Then factor the specimen logic out
    to the genetic algorithm tuning tool.
- Consider creating a GameLogic class that way the MyBot class is just a flow controller
- pre-process quadrant values (maybe cetered on a point, radiating out by 5)

Ant algorithm....
- 
 */
namespace Halite3
{
    public class MyBot
    {
        public static GameMap GameMap;
        public static HashSet<MapCell> CollisionCells = new HashSet<MapCell>();
        public static HyperParameters HParams;
        public static Player Me;

        private static List<Command> CommandQueue = new List<Command>();
        private static Logic MyLogic = new WallLogic();
        //private static bool CreatedDropoff = false;
        private static HashSet<int> FinalReturnToHome = new HashSet<int>();
        private static HashSet<int> UsedShips = new HashSet<int>();
        private static HashSet<int> MovingTowardsBase = new HashSet<int>();

        public static void Main(string[] args)
        {
            Specimen specimen;
            try {
                HParams = new HyperParameters("Halite3/HyperParameters.txt"); //production
                specimen = new FakeSpecimen();
            } catch(System.IO.FileNotFoundException) {
                specimen = GeneticSpecimen.RandomSpecimen();
                HParams = specimen.GetHyperParameters(); //local
            }

            Game game = new Game();
            // At this point "game" variable is populated with initial map data.
            // This is a good place to do computationally expensive start-up pre-processing.
            // As soon as you call "ready" function below, the 2 second per turn timer will start.
            GameMap = game.gameMap;
            MyLogic.DoPreProcessing();
            //MyLogic.WriteToFile();
            game.Ready("WallBot");
            //while(!Debugger.IsAttached);

            Log.LogMessage("Successfully created bot! My Player ID is " + game.myId);

            for (; ; )
            {
                // Basic processing for the turn start
                game.UpdateFrame();
                Me = game.me;
                GameMap = game.gameMap;
                CommandQueue.Clear();
                CollisionCells.Clear();
                UsedShips.Clear();
                MyLogic.ProcessTurn();

                // Specimen spawn logic for GeneticTuner
                if(game.TurnsRemaining == 0) {
                    if(Me.halite >= game.Opponents.Max(p => p.halite)) {
                        specimen.SpawnChildren();
                    } else {
                        specimen.Kill();
                    }
                }

                // todo fix this later, testing purposes only
                /* if(Me.halite > 5000 && !CreatedDropoff) {
                    var dropoffship = Me.ShipsSorted.OrderBy(s => GameMap.NeighborsAt(s.position).Sum(n => n.halite) + GameMap.At(s.position).halite).Last();
                    CommandQueue.Add(dropoffship.MakeDropoff());
                    UsedShips.Add(dropoffship.Id);
                    CreatedDropoff = true;
                } */

                //logical marking
                foreach(var ship in Me.ShipsSorted) {
                    if(ship.CurrentMapCell.structure != null && MovingTowardsBase.Contains(ship.Id))
                        MovingTowardsBase.Remove(ship.Id);

                    if(ship.halite > HParams[Parameters.CARGO_TO_MOVE])
                        MovingTowardsBase.Add(ship.Id);

                    if(ship.DistanceToDropoff * 1.5 > game.TurnsRemaining) {
                        FinalReturnToHome.Add(ship.Id);
                    }

                    if(!ship.CanMove) {
                        MakeMove(ship, Direction.STILL);
                    }
                }

                // End game, return all ships to nearest dropoff
                foreach (var ship in Me.ShipsSorted.Where(s => FinalReturnToHome.Contains(s.Id) && !UsedShips.Contains(s.Id))) {
                    var directions = ship.ClosestDropoff.position.GetAllDirectionsTo(ship.position);
                    bool used = false;
                    foreach(var d in directions) {
                        if(IsSafeMove(ship, d) && !used) {
                            MakeMove(ship, d);
                            used = true;
                        }
                    }
                    if(!used)
                        MakeMove(ship, Direction.STILL);
                }

                // move to base...
                foreach(var ship in Me.ShipsOnDropoffs().Where(s => !UsedShips.Contains(s.Id))) {
                    var bestNeighbors = MyLogic.GetBestNeighbors(ship.position);
                    if(bestNeighbors.All(n => n.IsOccupied())) {
                        MakeMove(ship, bestNeighbors.First(n => !CollisionCells.Contains(n)));
                    } else {
                        var target = bestNeighbors.First(n => !n.IsOccupied());
                        MakeMove(ship, target);
                    }
                }
                var shipsToMoveToBase = Me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id) && MovingTowardsBase.Contains(s.Id));
                foreach (var ship in shipsToMoveToBase) {
                    MakeBestReturnToDropoffMove(ship);
                }

                // collect halite (move or stay)
                foreach (Ship ship in Me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id)))
                {
                    if(GameMap.At(ship.position).halite >= 70) {
                        MakeMove(ship, Direction.STILL);
                        continue;
                    }

                    var bestNeighbors = MyLogic.GetBestNeighbors(ship.position).Where(n => !CollisionCells.Contains(n)).ToList();
                    if(bestNeighbors.All(n => n.IsOccupied() && n.halite >= 70)) {
                        MakeMove(ship, bestNeighbors[0]);
                    } else {
                        var target = bestNeighbors.First(n => !n.IsOccupied() || n.halite < 70);
                        MakeMove(ship, target);
                    }
                    UsedShips.Add(ship.Id);
                }

                // spawn ships
                if (game.turnNumber <= HParams[Parameters.TURNS_TO_SAVE] &&
                    Me.halite >= Constants.SHIP_COST &&
                    !CollisionCells.Contains(GameMap.At(Me.shipyard.position)))
                {
                    CommandQueue.Add(Me.shipyard.Spawn());
                }

                game.EndTurn(CommandQueue);
            }
        }

        public static bool IsSafeMove(Ship ship, Direction move) {
            MapCell target = GameMap.At(ship.position.DirectionalOffset(move));
            if(target.structure != null && FinalReturnToHome.Contains(ship.Id))
                return true;
            return !CollisionCells.Contains(target);
        }

        public static void AddCollision(Ship ship, Direction move) {
            MapCell targetCell = GameMap.At(ship.position.DirectionalOffset(move));
            CollisionCells.Add(targetCell);
        }

        /// Move back to base, priortize path of least resistance, but take either move if other isn't available
        public static void MakeBestReturnToDropoffMove(Ship ship) {
            Entity closestDrop = ship.ClosestDropoff;
            List<Direction> directions = closestDrop.position.GetAllDirectionsTo(ship.position);
            directions = directions.OrderBy(d => GameMap.At(ship.position.DirectionalOffset(d)).halite).ToList();
            foreach(Direction d in directions) {
                if(IsSafeMove(ship, d)) {
                    MakeMove(ship, d);
                    return;
                }
            }
            if(IsSafeMove(ship, Direction.STILL))
                MakeMove(ship, Direction.STILL);
            else
                MakeBestSafeMove(ship);
        }

        // TODO rename to be someting about collecting halite
        public static void MakeBestSafeMove(Ship ship, bool includeStill = true) {
            var availableCells = GameMap.NeighborsAt(ship.position).ToList();
            if(includeStill) {
                availableCells.Add(ship.CurrentMapCell);
            }
            availableCells = availableCells.OrderByDescending(c => c == ship.CurrentMapCell ? c.halite * (ship.CurrentMapCell.halite > 100 ? 3.0 : 1.4) : c.halite).ToList();
            foreach(MapCell cell in availableCells) {
                if(!CollisionCells.Contains(cell) && 
                    (CollisionCells.Contains(ship.CurrentMapCell) || cell.ship == null || cell.ship.BestNeighbor != ship.CurrentMapCell)) {
                    var d = cell.position.GetDirectionTo(ship.position);
                    MakeMove(ship, d);
                    return;
                }
            }
            throw new Exception("this should never happen"); //exception....
        }

        public static void MakeMove(Ship ship, MapCell target) {
            MakeMove(ship, target.position.GetDirectionTo(ship.position));
        }

        public static void MakeMove(Ship ship, Direction move) {
            CommandQueue.Add(ship.Move(move));
            UsedShips.Add(ship.Id);
            AddCollision(ship, move);
        }
    }
}
