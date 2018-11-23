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
- refactor for multiple shipyard, test by creating shipyard at most valuable location at turn 50
-- todo recursive cascade to push ships out
-- move back to base, priortize path of least resistance
-- create a batch file folder with multiple other previous bots and run them against each other

Difficult
- add a preprocessing and prioritization strategy
- add a javascript suite to visualize the genetic algorithm and run multiple runs

Things to consider
- utilize command line args to pass a hyperparameters.txt file (using HyperParameters.txt by default)
    and altering run.bat to pass in Halite3.HyperParameters.txt.  Then factor the specimen logic out
    to the genetic algorithm tuning tool.
- Consider creating a GameLogic class that way the MyBot class is just a flow controller
- pre-process quadrant values (maybe cetered on a point, radiating out by 5)
 */
namespace Halite3
{
    public class MyBot
    {
        public static GameMap GameMap;
        public static HashSet<MapCell> CollisionCells;
        public static List<Command> CommandQueue = new List<Command>();
        public static HashSet<int> UsedShips = new HashSet<int>();
        public static HashSet<int> FinalReturnToHome = new HashSet<int>();
        public static HyperParameters HParams;
        public static bool CreatedDropoff = false;
        public static void Main(string[] args)
        {
            //SpecimenExaminer.GenerateCSVFromSpecimenFolder(); // uncomment to enable csv generation
            Specimen specimen;
            try {
                HParams = new HyperParameters("HyperParameters.txt"); //production
                specimen = new FakeSpecimen();
            } catch(System.IO.FileNotFoundException) {
                specimen = GeneticSpecimen.RandomSpecimen();
                HParams = specimen.GetHyperParameters(); //local
            }

            int rngSeed;
            if (args.Length > 1)
            {
                rngSeed = int.Parse(args[1]);
            }
            else
            {
                rngSeed = System.DateTime.Now.Millisecond;
            }
            Random rng = new Random(rngSeed);
            Game game = new Game();
            // At this point "game" variable is populated with initial map data.
            // This is a good place to do computationally expensive start-up pre-processing.
            // As soon as you call "ready" function below, the 2 second per turn timer will start.
            game.Ready("GeneticBot3.0");
            //while(!Debugger.IsAttached);
            //game.Ready("GeneticBot_debug");

            Log.LogMessage("Successfully created bot! My Player ID is " + game.myId + ". Bot rng seed is " + rngSeed + ".");
            HashSet<int> movingtowardsbase = new HashSet<int>();

            for (; ; )
            {
                game.UpdateFrame();
                Player me = game.me;
                GameMap = game.gameMap;
                Ship.Map = GameMap;
                Ship.MyDropoffs = me.GetDropoffs();
                Position.MapWidth = GameMap.width;
                Position.MapHeight =  GameMap.height;

                CommandQueue = new List<Command>();
                CollisionCells = new HashSet<MapCell>();
                UsedShips = new HashSet<int>();

                // Specimen control logic for GeneticTuner
                if(game.TurnsRemaining == 0) {
                    if(me.halite >= game.Opponents.Max(p => p.halite)) {
                        specimen.SpawnChildren();
                    } else {
                        specimen.Kill();
                    }
                }

                // todo fix this later, testing purposes only
                if(me.halite > 5000 && !CreatedDropoff) {
                    var dropoffship = me.ShipsSorted.OrderBy(s => GameMap.NeighborsAt(s.position).Sum(n => n.halite) + GameMap.At(s.position).halite).Last();
                    CommandQueue.Add(dropoffship.MakeDropoff());
                    UsedShips.Add(dropoffship.Id);
                    CreatedDropoff = true;
                }

                //logical marking
                foreach(var ship in me.ShipsSorted) {
                    if(ship.CurrentMapCell.structure != null && movingtowardsbase.Contains(ship.Id))
                        movingtowardsbase.Remove(ship.Id);

                    if(ship.halite > HParams[Parameters.CARGO_TO_MOVE])
                        movingtowardsbase.Add(ship.Id);

                    if(ship.DistanceToDropoff * 1.5 > game.TurnsRemaining) {
                        FinalReturnToHome.Add(ship.Id);
                    }

                    if(!ship.CanMove) {
                        MakeMove(ship, Direction.STILL);
                    }
                }

                // End game, return all ships to nearest dropoff
                // todo refactor this to be prettier....
                foreach (var ship in me.ShipsSorted.Where(s => FinalReturnToHome.Contains(s.Id) && !UsedShips.Contains(s.Id))) {
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

                // move to base
                // first move the ship on the base...
                // todo is this necessary?
                foreach(var drop in me.GetDropoffs()) {
                    var shipOnDrop = GameMap.At(drop.position).ship;
                    if(shipOnDrop != null && shipOnDrop.owner == me.id && ! UsedShips.Contains(shipOnDrop.Id)) {
                        var n = GameMap.AnyEmptyNeighbor(shipOnDrop.position);
                        if(n == null)
                            MakeBestSafeMove(shipOnDrop, false);
                        else
                            MakeMove(shipOnDrop, n); 
                        }
                }
                var shipsToMoveToBase = me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id) && movingtowardsbase.Contains(s.Id));
                foreach (var ship in shipsToMoveToBase) {
                    MakeBestReturnToDropoffMove(ship);
                }

                // collect halite (move or stay)
                foreach (Ship ship in me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id)))
                {
                    MakeBestSafeMove(ship);
                }

                // spawn ships
                if (game.turnNumber <= HParams[Parameters.TURNS_TO_SAVE] &&
                    me.halite >= Constants.SHIP_COST &&
                    !CollisionCells.Contains(GameMap.At(me.shipyard.position)))
                {
                    CommandQueue.Add(me.shipyard.Spawn());
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
