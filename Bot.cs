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

            string BotName = "ScoreBot2.0_" + specimen.Name();
            game.Ready(BotName);
            
            if(IsDebug) {
                Stopwatch s = new Stopwatch();
                s.Start();
                while(!Debugger.IsAttached && s.ElapsedMilliseconds < 60000); // max 30 seconds to attach, prevents memory leaks;
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

                // Move ships to dropoffs
                DropoffLogic.CommandShips();

                // Combat Logic!!!
                CombatLogic.CommandShips();

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
            if(game.TurnsRemaining < 80 || 
                Me.halite < (ReserveForDropoff ? 5500 : Constants.SHIP_COST) ||
                Logic.Logic.CollisionCells.Contains(GameMap.At(Me.shipyard.position))) {
                return false;
            }

            int numShips = (int)((game.Opponents.Sum(x => x.ships.Count)/2 + Me.ships.Count*1.5) * .66);
            int numCells = GameMap.width * GameMap.height;
            int haliteRemaining = game.gameMap.HaliteRemaining;
            for(int i=0; i<game.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .1 * haliteRemaining / numCells);
                haliteRemaining -= haliteCollectable;
            }

            numShips += 1; // if I created another, how much could I get?
            int haliteRemaining2 = game.gameMap.HaliteRemaining;
            for(int i=0; i<game.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .1 * haliteRemaining2 / numCells);
                haliteRemaining2 -= haliteCollectable;
            }

            if(haliteRemaining - haliteRemaining2 > HParams[Parameters.TARGET_VALUE_TO_CREATE_SHIP]) {
                return true;
            }
            return false;
        }
    }
}
