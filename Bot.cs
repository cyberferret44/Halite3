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
        public static bool ReserveForDropoff = false;

        // Private Variables
        private static List<Command> CommandQueue = new List<Command>();
        private static HashSet<int> UsedShips = new HashSet<int>();
        private static List<Ship> UnusedShips => Me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id)).ToList();

        public static void Main(string[] args)
        {
            // Get initial game state
            game = new Game();
            GameMap = game.gameMap;
            Me = game.me;

            // Do Genetic Algorithm Specimen implementation
            Specimen specimen;
            if(IsLocal) {
                specimen = GeneticSpecimen.RandomSpecimen("Halite3/", game);
                HParams = specimen.GetHyperParameters();
            } else  {
                specimen = GeneticSpecimen.RandomSpecimen("", game);
                HParams = specimen.GetHyperParameters();
            }

            // Handle Logic
            Logic.Logic CollectLogic = LogicFactory.GetCollectLogic();
            Logic.Logic DropoffLogic = LogicFactory.GetDropoffLogic();
            Logic.Logic EndOfGameLogic = LogicFactory.GetEndOfGameLogic();
            CollectLogic.Initialize();
            DropoffLogic.Initialize();
            EndOfGameLogic.Initialize();

            string BotName = "NEW_" + specimen.Name();
            game.Ready(BotName);
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
                    if((game.Opponents.Count == 1 && Me.halite >= game.Opponents[0].halite) ||
                        game.Opponents.Count == 3 && Me.halite >= game.Opponents.OrderBy(x => x.halite).ElementAt(1).halite) {
                        specimen.SpawnChildren();
                    } else {
                        specimen.Kill();
                    }
                    if(game.myId.id == 1 && IsLocal) {
                        string content = $"\n{BotName},{Me.halite}";
                        foreach(var o in game.Opponents) {
                            content += $",{o.id.id},{o.halite}";
                        }
                        using(StreamWriter sw = File.AppendText("ResultsHistory.txt")) {
                            sw.Write(content);
                        }
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
            if(command.Ship.Neighbors.Any(n => n.IsStructure) && command.Ship.halite > 0 && command.Ship.CurrentMapCell.halite == 0 && command.TargetCell == command.Ship.CurrentMapCell) {
                Log.LogMessage((new System.Diagnostics.StackTrace()).ToString());
            }
        }

        // TODO add a more advanced solution here
        private static bool ShouldSpawnShip() {
            return GameMap.PercentHaliteCollected < .6 &&
                    (game.turnNumber <= game.TotalTurns * HParams[Parameters.TURNS_TO_SAVE] || (Me.halite >= 6000 && (GameMap.PercentHaliteCollected < .4 && game.TurnsRemaining > 100))) &&
                    Me.halite >= (ReserveForDropoff ? 6000 : Constants.SHIP_COST) &&
                    !CollisionCells.Contains(GameMap.At(Me.shipyard.position));
        }
    }
}
