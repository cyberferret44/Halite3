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
            Logic.Logic ZoneAssignLogic = LogicFactory.GetAssignmentLogic();

            string BotName = "derp2" + specimen.Name();
            GameInfo.Game.Ready(BotName);
            
            if(GameInfo.IsDebug) {
                Stopwatch s = new Stopwatch();
                s.Start();
                while(!Debugger.IsAttached && s.ElapsedMilliseconds < 60000); // max 30 seconds to attach, prevents memory leaks;
                s.Stop();
            }

            Log.LogMessage("Successfully created bot! My Player ID is " + GameInfo.Game.myId);
            Stopwatch combatWatch = new Stopwatch();
            Stopwatch dropoffWatch = new Stopwatch();
            Stopwatch collectWatch = new Stopwatch();
            Stopwatch zoneAssignmentWatch = new Stopwatch();

            for (; ; )
            {
                // Basic processing for the turn start
                GameInfo.Game.UpdateFrame();
                Fleet.UpdateFleet(GameInfo.MyShips);
                ValueMapping.ProcessTurn();

                // logic turn processing
                CollectLogic.ProcessTurn();
                DropoffLogic.ProcessTurn();
                EndOfGameLogic.ProcessTurn();
                CombatLogic.ProcessTurn();
                //ZoneAssignLogic.ProcessTurn();

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
                    Log.LogMessage("total time in combat  logic = " + (combatWatch.ElapsedMilliseconds));
                    Log.LogMessage("total time in dropoff logic = " + (dropoffWatch.ElapsedMilliseconds));
                    Log.LogMessage("total time in zoneassign logic = " + (zoneAssignmentWatch.ElapsedMilliseconds));
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

                // Move ships to assigned Zones
                //Log.LogMessage($"*** ZoneAsn Logic ***");
                //zoneAssignmentWatch.Start();
                //ZoneAssignLogic.CommandShips();
                //zoneAssignmentWatch.Stop();

                // collect halite (move or stay) using Logic interface
                Log.LogMessage($"*** Collect Logic ***");
                collectWatch.Start();
                CollectLogic.CommandShips();
                collectWatch.Stop();

                // spawn ships
                var cmdQueue = Fleet.GenerateCommandQueue();
                if (GameInfo.ShouldSpawnShip())
                {
                    cmdQueue.Add(GameInfo.Me.shipyard.Spawn());
                }

                GameInfo.Game.EndTurn(cmdQueue);
            }
        }
    }
}
