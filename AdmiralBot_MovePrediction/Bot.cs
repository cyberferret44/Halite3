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
        // Public Variables
        public static HyperParameters HParams;
        public static bool ReserveForDropoff = false;

        public static void Main(string[] args)
        {
            // Get initial game state
            GameInfo.SetInfo(new Game());
            GameInfo.IsDebug = GameInfo.IsLocal && args.Count() > 0 && args[0] == "debug";

            // Do Genetic Algorithm Specimen implementation
            Specimen specimen;
            if(GameInfo.IsLocal) {
                specimen = GeneticSpecimen.RandomSpecimen("Halite3/");
                HParams = specimen.GetHyperParameters();
            } else  {
                specimen = GeneticSpecimen.RandomSpecimen("");
                HParams = specimen.GetHyperParameters();
            }

            // Handle Logic
            Logic.Logic CombatLogic = LogicFactory.GetCombatLogic();
            Logic.Logic CollectLogic = LogicFactory.GetCollectLogic();
            Logic.Logic DropoffLogic = LogicFactory.GetDropoffLogic();
            Logic.Logic EndOfGameLogic = LogicFactory.GetEndOfGameLogic();
            CombatLogic.Initialize();
            CollectLogic.Initialize();
            DropoffLogic.Initialize();
            EndOfGameLogic.Initialize();

            string BotName = "ScoreBot2.0_" + specimen.Name();
            GameInfo.Game.Ready(BotName);
            
            if(GameInfo.IsDebug) {
                Stopwatch s = new Stopwatch();
                s.Start();
                while(!Debugger.IsAttached && s.ElapsedMilliseconds < 60000); // max 30 seconds to attach, prevents memory leaks;
                s.Stop();
            }

            Log.LogMessage("Successfully created bot! My Player ID is " + GameInfo.Game.myId);
            for (; ; )
            {
                // Basic processing for the turn start
                GameInfo.Game.UpdateFrame();

                // logic turn processing
                CollectLogic.ProcessTurn();
                DropoffLogic.ProcessTurn();
                EndOfGameLogic.ProcessTurn();
                CombatLogic.ProcessTurn();

                // Score the ships first 
                Logic.Logic.InitializeNewTurn();

                // Specimen spawn logic for GeneticTuner
                if(GameInfo.TurnsRemaining == 0) {
                    if((GameInfo.Opponents.Count == 1 && GameInfo.Me.halite >= GameInfo.Opponents[0].halite) ||
                        GameInfo.Opponents.Count == 3 && GameInfo.Me.halite >= GameInfo.Opponents.OrderBy(x => x.halite).ElementAt(1).halite) {
                        specimen.SpawnChildren();
                    } else {
                        specimen.Kill();
                    }
                    if(GameInfo.MyId == 1 && GameInfo.IsLocal) {
                        string content = $"\n{BotName},{GameInfo.Me.halite}";
                        foreach(var o in GameInfo.Opponents) {
                            content += $",{o.id.id},{o.halite}";
                        }
                        using(StreamWriter sw = File.AppendText("ResultsHistory.txt")) {
                            sw.Write(content);
                        }
                    }
                }

                // Combat Logic!!!
                CombatLogic.CommandShips();
                
                // End game, return all ships to nearest dropoff
                EndOfGameLogic.CommandShips();

                // Move ships to dropoffs
                DropoffLogic.CommandShips();

                // collect halite (move or stay) using Logic interface
                CollectLogic.CommandShips();

                // spawn ships
                if (ShouldSpawnShip())
                {
                    Logic.Logic.CommandQueue.Add(GameInfo.Me.shipyard.Spawn());
                }

                GameInfo.Game.EndTurn(Logic.Logic.CommandQueue);
            }
        }

        // TODO move the .08 to hyperparameters
        private static bool ShouldSpawnShip() {
            if(GameInfo.TurnsRemaining < 80 || 
                GameInfo.Me.halite < (ReserveForDropoff ? 5500 : Constants.SHIP_COST) ||
                Logic.Logic.CollisionCells.Contains(GameInfo.MyShipyardCell)) {
                return false;
            }

            // todo what if 4p?
            int numShips = (int)(GameInfo.OpponentShipsCount/2 + GameInfo.MyShipsCount*1.5);
            int numCells = GameInfo.TotalCellCount;
            int haliteRemaining = GameInfo.HaliteRemaining;
            for(int i=0; i<GameInfo.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .08 * haliteRemaining / numCells);
                haliteRemaining -= haliteCollectable;
            }

            numShips += 1; // if I created another, how much could I get?
            int haliteRemaining2 = GameInfo.HaliteRemaining;
            for(int i=0; i<GameInfo.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .08 * haliteRemaining2 / numCells);
                haliteRemaining2 -= haliteCollectable;
            }

            if(haliteRemaining - haliteRemaining2 > HParams[Parameters.TARGET_VALUE_TO_CREATE_SHIP]) {
                return true;
            }
            return false;
        }
    }
}
