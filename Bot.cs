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
        private static bool IsDebug = false;
        
        // Public Variables
        public static GameMap GameMap;
        public static HyperParameters HParams;
        public static Player Me;
        public static Game game;
        public static bool ReserveForDropoff = false;

        public static void Main(string[] args)
        {
            // Get initial game state
            game = new Game();
            GameMap = game.gameMap;
            Me = game.me;

            IsDebug = IsLocal && args.Count() > 0 && args[0] == "debug";
            Log.LogMessage("Is debug? "+ IsDebug);

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
            Logic.Logic CombatLogic = LogicFactory.GetCombatLogic(Me.id.id, IsLocal);
            Logic.Logic CollectLogic = LogicFactory.GetCollectLogic();
            Logic.Logic DropoffLogic = LogicFactory.GetDropoffLogic();
            Logic.Logic EndOfGameLogic = LogicFactory.GetEndOfGameLogic();
            CombatLogic.Initialize();
            CollectLogic.Initialize();
            DropoffLogic.Initialize();
            EndOfGameLogic.Initialize();

            string BotName = "MoveScoreBot"; //(Me.id.id == 0 ? "Aggro_" : "NEW_") + specimen.Name();
            game.Ready(BotName);
            
            if(IsDebug) {
                Stopwatch s = new Stopwatch();
                s.Start();
                while(!Debugger.IsAttached && s.ElapsedMilliseconds < 30000); // max 30 seconds to attach, prevents memory leaks;
                s.Stop();
            }

            Log.LogMessage("Successfully created bot! My Player ID is " + game.myId);
            for (; ; )
            {
                // Basic processing for the turn start
                game.UpdateFrame();
                Me = game.me;
                GameMap = game.gameMap;

                // logic turn processing
                CollectLogic.ProcessTurn();
                DropoffLogic.ProcessTurn();
                EndOfGameLogic.ProcessTurn();
                CombatLogic.ProcessTurn();

                // Score the ships first 
                Logic.Logic.InitializeNewTurn();

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

                // End game, return all ships to nearest dropoff
                EndOfGameLogic.CommandShips();

                // Combat Logic!!!
                CombatLogic.CommandShips();

                // Move ships to dropoffs
                DropoffLogic.CommandShips();

                // collect halite (move or stay) using Logic interface
                CollectLogic.CommandShips();

                // spawn ships
                if (ShouldSpawnShip())
                {
                    Logic.Logic.CommandQueue.Add(Me.shipyard.Spawn());
                }

                game.EndTurn(Logic.Logic.CommandQueue);
            }
        }

        // TODO add a more advanced solution here
        private static bool ShouldSpawnShip() {
            return GameMap.PercentHaliteCollected < .65 &&
                    (game.turnNumber <= game.TotalTurns * HParams[Parameters.TURNS_TO_SAVE] || (Me.halite >= 6000 && (GameMap.PercentHaliteCollected < .4 && game.TurnsRemaining > 100))) &&
                    Me.halite >= (ReserveForDropoff ? 6000 : Constants.SHIP_COST) &&
                    !Logic.Logic.CollisionCells.Contains(GameMap.At(Me.shipyard.position));
        }
    }
}
