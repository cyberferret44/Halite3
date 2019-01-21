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

        public static void Main(string[] args)
        {
            // Get initial game state
            GameInfo.ProcessTurn(new Game());
            GameInfo.IsDebug = GameInfo.IsLocal && args.Count() > 0 && args[0] == "debug";

            if(GameInfo.IsDebug) {
                Stopwatch s = new Stopwatch();
                s.Start();
                while(!Debugger.IsAttached && s.ElapsedMilliseconds < 60000); // max 30 seconds to attach, prevents memory leaks;
                s.Stop();
            }

            // Do Genetic Algorithm Specimen implementation
            Specimen specimen;
            if(GameInfo.IsDebug || (GameInfo.IsLocal && args.Count() > 0 && args[0] == "test")) {
                specimen = new FakeSpecimen();
                HParams = specimen.GetHyperParameters();
                Log.LogMessage("testing...");
            } else {
                specimen = GeneticSpecimen.RandomSpecimen();
                HParams = specimen.GetHyperParameters();
            }

            // Handle Logic
            Logic.Logic CombatLogic = LogicFactory.GetCombatLogic();
            Logic.Logic CollectLogic = LogicFactory.GetCollectLogic();
            Logic.Logic DropoffLogic = LogicFactory.GetDropoffLogic();
            Logic.Logic EndOfGameLogic = LogicFactory.GetEndOfGameLogic();
            Logic.Logic EndCollectLogic = new EndGameCollectLogic();
            SiteSelection.Initialize();

            string BotName = GameInfo.BOT_NAME + specimen.Name();
            GameInfo.Game.Ready(BotName);

            Log.LogMessage("Successfully created bot! My Player ID is " + GameInfo.Game.myId);
            Stopwatch combatWatch = new Stopwatch();
            Stopwatch dropoffWatch = new Stopwatch();
            Stopwatch proximityWatch = new Stopwatch();
            Stopwatch collectWatch = new Stopwatch();

            for (; ; )
            {
                // Basic processing for the turn start
                GameInfo.Game.UpdateFrame();
                GameInfo.ProcessTurn(GameInfo.Game);
                Fleet.UpdateFleet(GameInfo.MyShips);
                EnemyFleet.UpdateFleet();
                Log.LogMessage("value mapping...");
                ValueMapping3.ProcessTurn();
                SiteSelection.ProcessTurn();

                // logic turn processing
                CollectLogic.ProcessTurn();
                DropoffLogic.ProcessTurn();
                EndOfGameLogic.ProcessTurn();
                CombatLogic.ProcessTurn();

                // Score the ships first 
                Safety.InitializeNewTurn();

                // Specimen spawn logic for GeneticTuner
                if(GameInfo.TurnsRemaining == 0) {
                    var players = GameInfo.Opponents;
                    players.Add(GameInfo.Me);
                    players = players.OrderBy(x => x.halite).ToList();
                    int numChildren = players.Count - ((players.Count/2) + players.IndexOf(GameInfo.Me));
                    if(numChildren > 0) {
                        specimen.SpawnChildren(numChildren);
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
                    Log.LogMessage("total time in combat  logic = " + (combatWatch.ElapsedMilliseconds));
                    Log.LogMessage("total time in dropoff logic = " + (dropoffWatch.ElapsedMilliseconds));
                    Log.LogMessage("total time in proximity logic = " + (proximityWatch.ElapsedMilliseconds));
                    Log.LogMessage("total time in collect logic = " + (collectWatch.ElapsedMilliseconds));
                }

                // Combat Logic!!!
                Log.LogMessage($"*** Combat  Logic ***");
                combatWatch.Start();
                CombatLogic.CommandShips();
                combatWatch.Stop();

                // End game, return all ships to nearest dropoff
                Log.LogMessage($"*** EndGame Logic ***");
                EndOfGameLogic.CommandShips();

                // Move ships to dropoffs
                Log.LogMessage($"*** Dropoff Logic ***");
                dropoffWatch.Start();
                DropoffLogic.CommandShips();
                dropoffWatch.Stop();

                // collect halite (move or stay) using Logic interface
                Log.LogMessage($"*** Collect Logic ***");
                collectWatch.Reset();
                collectWatch.Start();
                if(GameInfo.Map.AverageHalitePerCell > HParams[Parameters.HALITE_TO_SWITCH_COLLECT] || GameInfo.Map.PercentHaliteCollected < .5) {
                    CollectLogic.CommandShips();
                }
                EndCollectLogic.CommandShips();
                collectWatch.Stop();
                Log.LogMessage("collect time was " + collectWatch.ElapsedMilliseconds);

                // spawn ships
                var cmdQueue = Fleet.GenerateCommandQueue();
                if (ShouldSpawnShip())
                {
                    cmdQueue.Add(GameInfo.Me.shipyard.Spawn());
                }

                GameInfo.Game.EndTurn(cmdQueue);
            }
        }

        // TODO move the .08 to hyperparameters
        public static bool ShouldSpawnShip(int haliteToAdd = 0) {
            //if(GameInfo.Me.id.id == 0) // debugging the collect logic
            //    return false;
            // todo 5500
            int halite = GameInfo.Me.halite + haliteToAdd;
            if(GameInfo.TurnsRemaining < 80 || 
                halite < (GameInfo.ReserveForDropoff ? (5000 - SiteSelection.NewDropoffDeduction()) : Constants.SHIP_COST) ||
                !Fleet.CellAvailable(GameInfo.MyShipyardCell)) {
                return false;
            }

            // this logic is special because of the specific treatment of enemy ships here
            int numShips = (int)(GameInfo.OpponentShipsCount * .5 + GameInfo.MyShipsCount * (1 + .5 * GameInfo.Opponents.Count));
            int numCells = GameInfo.TotalCellCount;
            int haliteRemaining = GameInfo.HaliteRemaining;
            for(int i=0; i<GameInfo.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .1 * haliteRemaining / numCells);
                haliteRemaining -= haliteCollectable;
            }

            numShips += 1; // if I created another, how much could I get?
            int haliteRemaining2 = GameInfo.HaliteRemaining;
            for(int i=0; i<GameInfo.TurnsRemaining; i++) {
                int haliteCollectable = (int)(numShips * .1 * haliteRemaining2 / numCells);
                haliteRemaining2 -= haliteCollectable;
            }
            if(haliteRemaining2 >= haliteRemaining) {
                Log.LogMessage("this shouldn't happen...");
            }

            if(haliteRemaining - haliteRemaining2 > MyBot.HParams[Parameters.TARGET_VALUE_TO_CREATE_SHIP]) {
                return true;
            }
            return false;
        }
    }
}
