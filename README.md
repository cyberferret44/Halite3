<h2>Summary</h2>
Code starts in Bot.cs.  The game loop uses files in Logic/Logic.cs.  Each Logic.CommandShips call can optionally move any given ship, with the higher order Logic functions taking precedence over lower order ones.

From the 2016 competition, I recalled that the code will invariably undergo multiple revisions.  So I modularized the logic classes and attempted isolate certain types of ubiquitous Logic into common static classes; Navigation.cs, GameInfo.cs, Safety.cs, and Fleet.cs.

Also from the 2016 competition, I recalled wasting a lot of time manually tuning parameters. This time, I invested a few days upfront developing a dynamic HyperParameters.cs which could read parameters from local text files, and a genetic algorithm (GeneticTuner/Specimen.cs) which handled creating/destroying these files.  Seamlessness was paramount, so adding new parameters to tune was as simple as adding them to the enum in HyperParameters.cs with bounds and default values.


<h2>Lessons Learned</h2>

- Splitting out common functions earlier on (such as Navigation.cs) and writing unit tests around these functions would have saved a lot of time in the end.

- Using better tooling (such as a VS Code Code Runner) for quick code tests would have been extremely useful.


<h2>Turn Steps</h2>
As mentioned, high order Logic.CommandShips() calls take precedence. More detailed explanations below. Their order is...

- 1. Initiailze new turn Information

- 2. Logic/EndOfGameLogic.cs: go to nearest dropoff, ignoring collisions on dropoffs

- 3. Logic/DropoffLogic.cs: go to nearest dropoff (using Dijkstra's algorithm)

- 4. Logic/EarlyCollectLogic.cs: Predict the number of turns required to harvest, move to lowest order

- 5. Logic/LateCollectLogic.cs: Greedier collect that estimates best nearby target (great for picking up collision cargo)

- 6. Spawn Ship


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
This file performs better than EarlyCollectLogic.cs when the map has been mostly harvested, it simply greedily looks for it's best target within X cells.

<h2>/GeneticTuner Folder</h2>
The overall genetic algorithm is elegant but extremely simple.  It spawns children at the start of the game in memory.  If the bot finishes in the top half of players, (i.e. 1st in a 2p game and 1st or 2nd in a 4p game) it lives and it writes its children to disk (in txt files).  Otherwise, the file it derived its parameters from is deleted.

<b>EnemyFleet.cs</b>
An orphan class, only used to estimate if enemies are returning to dropoffs.

<b>Fleet.cs</b>
Handles commands for ships, such as moving, and information such as Ship Count and Available ships.

<b>GameInfo.cs</b>
A central static information class where I could ask ubquitious quesiotns like "what's the turn number?"

<b>HyperParameters.cs</b>
A dynamic HyperParameter class that facilitated the Genetic Algorithm

<b>Navigation.cs</b>
Handled basic navigation-based questions, such as Path of Least Resistance (using Djikstra's), Greedy Path of Least resistance if a close approximation is sufficient, or Is Cell Accessible

<b>Safety.cs</b>
This class was born from the question "Is a collision okay."  I found that a "SafetyRatio" drastically outperformed if/else logic.  For instance, normally it would be bad for a 300 halite ship to destroy my 900 halite ship.  But is the Safety Ratio is very high (i.e. very likely my other ships would recover the cargo), then I would allow my ship to take the otherwise risky move.  (This effectively beat "cheater" strategies such as someone parking a ship on my dropoff).

<b>SiteSelection.cs</b>
At the start of the game, this class selects what it believes to be the best dropoff spots on the board.  It dictates, each turn, if a certain dropoff should be "enabled" by setting it in GameInfo as the next dropff location.

<b>ValueMapping.cs</b>
Probably one of the most complicated classes.  This starts as a mirror of the board itself, where it's NxN size, and the values are the same as the MapCell.halite.  As ships are assigned to target cells in the CollectLogic classes, the values of the respective areas are reduced by an amount equivalent to the ship's missing halite.  EarlyGameCollectLogic.cs makes very heavy use of this class.

<b>Zone.cs</b>
Mainly used to calculate the Safety Ratio.  It just grabs a list of MapCells of some Radius around a Root Point.


<h2>Things I should have done different</h2>
- Not sure if it's feasible, but having a local MongoDB would have likely been far superior to text files.
- While Machine Learning models were highly optional, a simpler statistical model would have likely been extremely useful (for instance, the logic I use to determine how long a ship will take to harvest a square is deterministic.  I could have simply stored the information and in the end recorded how long it actually took, then switch to use the real statistical average rather than a napkin math approximation)
