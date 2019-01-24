<h2>Summary</h2>
The Main function and turn loop is in Bot.cs.  The loop makes use of files that implement Abstract Logic/Logic.cs.  The various Logic implementations are then called in order of precedence, finally concluding with a create ship command.

From the 2016 competition, I recalled that the code will invariably undergo multiple revisions.  So I modularized the logic classes and I attempted isolate certain types of ubiquitous Logic into common static classes; Navigation.cs, GameInfo.cs, Safety.cs, and Fleet.cs.

Also from the 2016 competition, I recalled wasting a lot of time manually tuning parameters.  Rather than do that, I invested time upfront developing HyperParameters.cs which could read parameters from local text files, and a genetic algorithm (GeneticTuner/Specimen.cs) which handled creating/destroying these files.  Seamlessness was paramount, so adding new parameters to tune was as simple as adding them to the enum in HyperParameters.cs with bounds and default values.

<h2>Turn Steps</h2>
In each Logic class, the CommandShips has the priority over all subsequent classes to command the ship.
1. Initiailze new turn Information
2. End of Game Dropoff Logic (Logic/EndOfGameLogic.cs)
3. Dropoff Logic (Logic/DropoffLogic.cs)
3. Early Collect Logic (EarlyCollectLogic.cs)
4. Late Collect Logic (LateCollectLogic.cs, also a catch for remaining ships)
5. Spawn Ships


<h2>/Logic Folder</h2>
<b>CombatLogic.cs</b>
This class just looks for opportunities to disrupt a neighboring opponent (if they're worth hitting, and a good chance another of my ships will recover the cargo).

<b>DropoffLogic.cs</b>
Simply Returns ship to nearesy *safe* dropoff via the lowest cost path.

<b>EarlyCollectLogic.cs</b>
Most complicated class, but still pretty simple, foreach ships, asks the question
<i>For each cell in X radius, how quickly could you collect and return to base if you went there?</i>
Ships are then ordered by fastest results and moved to targets.  To prevent all ships from moving to the same spot, I reduce the value of a ship's target location (via ValueMapping3.cs) and recalculate all affected ships.

<b>EndOfGameLogic.cs</b>
This file is straightforward, simply returns the ships to nearest dropoff, ignoring collisions on drops.

<b>LateCollectLogic.cs</b>
This file performs better than EarlyCollectLogic.cs when the map has been mostly harvested.


<h2>Genetic Tuner</h2>
The overall genetic algorithm is elegant but extremely simple.  It spawns children at the start of the game in memory.  If the bot finishes in the top half of players, (i.e. 1st in a 2p game and 1st or 2nd in a 4p game) it lives and it writes its children to disk (in txt files).  Otherwise, the file it derived its parameters from is deleted.

<b>EnemyFleet.cs</b>
<b>Fleet.cs</b>
<b>GameInfo.cs</b>
<b>HyperParameters.cs</b>
<b>Navigation.cs</b>
<b>Safety.cs</b>
<b>SiteSelection.cs</b>
<b>ValueMapping.cs</b>
<b>Zone.cs</b>


<h2>Lessons Learned</h2>
