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
                Ship.MyShipyards = new List<Shipyard> { me.shipyard };
                Position.MapWidth = GameMap.width;
                Position.MapHeight =  GameMap.height;

                CommandQueue = new List<Command>();
                CollisionCells = new HashSet<MapCell>();
                UsedShips = new HashSet<int>();

                // Specimen control logic for GeneticTuner
                if(game.TurnsRemaining <= 0) {
                    if(me.halite > game.players.Where(p => p != me).Max(p => p.halite)) {
                        specimen.SpawnChildren();
                    } else {
                        specimen.Kill();
                    }
                }

                //logical marking
                foreach(var ship in me.ShipsSorted) {
                    if(ship.CurrentMapCell == GameMap.At(me.shipyard) && movingtowardsbase.Contains(ship.Id))
                        movingtowardsbase.Remove(ship.Id);

                    if(ship.halite > HParams[Parameters.CARGO_TO_MOVE])
                        movingtowardsbase.Add(ship.Id);

                    if(ship.DistanceToShipyard * 1.5 > game.TurnsRemaining) {
                        FinalReturnToHome.Add(ship.Id);
                    }

                    if(!ship.CanMove) {
                        MakeMove(ship, Direction.STILL);
                    }
                }

                // End game, return all ships to nearest dropoff
                foreach (var ship in me.ShipsSorted.Where(s => FinalReturnToHome.Contains(s.Id) && !UsedShips.Contains(s.Id))) {
                    Direction direction = me.shipyard.position.GetDirectionTo(ship.position);
                    if(!IsSafeMove(ship, direction)) {
                        direction = Direction.STILL;
                    }
                    MakeMove(ship, direction);
                }

                // move to base
                // first move the ship on the base...
                var shipOnYard = GameMap.At(me.shipyard.position).ship;
                if(shipOnYard != null && shipOnYard.owner == me.id && ! UsedShips.Contains(shipOnYard.Id)) {
                    var n = GameMap.AnyEmptyNeighbor(shipOnYard.position);
                    if(n == null)
                        MakeBestSafeMove(shipOnYard, false);
                    else
                        MakeMove(shipOnYard, n); 
                }
                var shipsToMoveToBase = me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id) && movingtowardsbase.Contains(s.Id));
                foreach (var ship in shipsToMoveToBase) {
                    MakeBestReturnToDropoffMove(ship, me.shipyard);
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
            if(target.structure != null && target.structure is Shipyard && FinalReturnToHome.Contains(ship.Id))
                return true;
            return !CollisionCells.Contains(target);
        }

        public static void AddCollision(Ship ship, Direction move) {
            MapCell targetCell = GameMap.At(ship.position.DirectionalOffset(move));
            CollisionCells.Add(targetCell);
        }

        public static void MakeBestReturnToDropoffMove(Ship ship, Shipyard shipyard) {
            Direction direction = shipyard.position.GetDirectionTo(ship.position);
            if(IsSafeMove(ship, direction))
                MakeMove(ship, direction);
            else if(IsSafeMove(ship, Direction.STILL))
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
            //throw new Exception("this should never happen"); //exception....
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
