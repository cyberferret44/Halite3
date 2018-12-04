using Halite3.hlt;
using Halite3.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using GeneticTuner;
using System.IO;

namespace Halite3
{
    public class MyBot
    {
        // Simple Variable to tell if bot is running locally vs on the server
        private static bool IsLocal = Directory.GetCurrentDirectory().StartsWith("/Users/cviolet") ||
                                      Directory.GetCurrentDirectory().StartsWith("C://Users");
        
        // Public Variables
        public static GameMap GameMap;
        public static HashSet<MapCell> CollisionCells = new HashSet<MapCell>();
        public static HyperParameters HParams;
        public static Player Me;
        public static Game game;

        // Private Variables
        private static List<Command> CommandQueue = new List<Command>();
        private static HashSet<int> UsedShips = new HashSet<int>();
        private static List<Ship> UnusedShips => Me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id)).ToList();

        public static void Main(string[] args)
        {
            Specimen specimen;
            if(IsLocal) {
                HParams = new HyperParameters("Halite3/HyperParameters.txt");
                specimen = new FakeSpecimen();
                //specimen = GeneticSpecimen.RandomSpecimen();
                //HParams = specimen.GetHyperParameters();
            } else  {
                HParams = new HyperParameters("HyperParameters.txt");
                specimen = new FakeSpecimen();
            }

            game = new Game();
            Logic.Logic CollectLogic = LogicFactory.GetCollectLogic();
            Logic.Logic DropoffLogic = LogicFactory.GetDropoffLogic();
            Logic.Logic EndOfGameLogic = LogicFactory.GetEndOfGameLogic();
            // At this point "game" variable is populated with initial map data.
            // This is a good place to do computationally expensive start-up pre-processing.
            // As soon as you call "ready" function below, the 2 second per turn timer will start.
            GameMap = game.gameMap;
            Me = game.me;
            CollectLogic.Initialize();
            DropoffLogic.Initialize();
            EndOfGameLogic.Initialize();

            //MyLogic.WriteToFile();
            game.Ready("NoWallsBot");
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

                // logic turn processing
                CollectLogic.ProcessTurn();
                DropoffLogic.ProcessTurn();
                EndOfGameLogic.ProcessTurn();

                // Specimen spawn logic for GeneticTuner
                if(game.TurnsRemaining == 0) {
                    if(Me.halite >= game.Opponents.Max(p => p.halite)) {
                        specimen.SpawnChildren();
                    } else {
                        specimen.Kill();
                    }
                }

                // Can't move, just hold still to define the CollisionCell before other ships move
                var shipsThatCantMove = UnusedShips.Where(s => !s.CanMove).ToList();
                CollectLogic.CommandShips(shipsThatCantMove);

                // End game, return all ships to nearest dropoff
                EndOfGameLogic.CommandShips(UnusedShips);

                // Move Ships off of Drops.  Try moving to empty spaces first, then move to habited spaces and push others off
                var shipsOnDropoffs = Me.ShipsOnDropoffs().Where(s => !UsedShips.Contains(s.Id)).ToList();
                CollectLogic.CommandShips(shipsOnDropoffs);

                // Move ships to dropoffs
                DropoffLogic.CommandShips(UnusedShips);

                // collect halite (move or stay) using Logic interface
                CollectLogic.CommandShips(UnusedShips);

                // spawn ships
                if (ShouldSpawnShip())
                {
                    CommandQueue.Add(Me.shipyard.Spawn());
                }

                game.EndTurn(CommandQueue);
            }
        }

        public static void MakeMove(Command command) {
            CommandQueue.Add(command);
            UsedShips.Add(command.Ship.Id);
            CollisionCells.Add(command.TargetCell);
        }

        // TODO add a more advanced solution here
        private static bool ShouldSpawnShip() {
            return GameMap.PercentHaliteCollected < .55 &&
                    (game.turnNumber <= HParams[Parameters.TURNS_TO_SAVE] || (Me.halite >= 6000 && (GameMap.PercentHaliteCollected < .3 && game.TurnsRemaining > 100))) &&
                    Me.halite >= Constants.SHIP_COST &&
                    !CollisionCells.Contains(GameMap.At(Me.shipyard.position));
        }
    }
}
