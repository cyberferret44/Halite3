<p1><b>Summary</b></p1>

The Main function and turn loop is in Bot.cs.  The loop makes use of files that implement Abstract Logic/Logic.cs.  The various Logic implementations are then called in order of precedence, finally concluding with a create ship command.

From the 2016 competition, I recalled that the code will invariably undergo multiple revisions.  So I modularized the logic classes and I attempted isolate certain types of ubiquitous Logic into common static classes; Navigation.cs, GameInfo.cs, Safety.cs, and Fleet.cs.

Also from the 2016 competition, I recalled wasting a lot of time manually tuning parameters.  Rather than do that, I invested time upfront developing HyperParameters.cs which could read parameters from local text files, and a genetic algorithm (GeneticTuner/Specimen.cs) which handled creating/destroying these files.  Seamlessness was paramount, so adding new parameters to tune was as simple as adding them to the enum in HyperParameters.cs with bounds and default values.

<b>Turn Steps</b>
In each Logic class, the CommandShips has the priority over all subsequent classes to command the ship.
1. Initiailze new turn Information
2. End of Game Dropoff Logic (Logic/EndOfGameLogic.cs)
3. Dropoff Logic (Logic/DropoffLogic.cs)
3. Early Collect Logic (EarlyCollectLogic.cs)
4. Late Collect Logic (LateCollectLogic.cs, also a catch for remaining ships)
5. Spawn Ships


<p style="font-size:20; weight:bold">/Logic</p>
<b>End Of Game Dropoff Logic</b>
This file is straightforward, simply returns the ships to nearest


<b>Lessons Learned</b>
