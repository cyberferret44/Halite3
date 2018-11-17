using Halite3.hlt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Halite3
{
    public class MyBot
    {

        public static GameMap GameMap;
        public static Dictionary<MapCell, Direction> CollisionCells;
        public static void Main(string[] args)
        {
            int rngSeed;
            if (args.Length > 1)
            {
                rngSeed = int.Parse(args[1]);
            }
            else
            {
                rngSeed = System.DateTime.Now.Millisecond;
            }
            Random rng = new Random(rngSeed);


            Game game = new Game();
            // At this point "game" variable is populated with initial map data.
            // This is a good place to do computationally expensive start-up pre-processing.
            // As soon as you call "ready" function below, the 2 second per turn timer will start.
            game.Ready("BasicStarterBot");

            Log.LogMessage("Successfully created bot! My Player ID is " + game.myId + ". Bot rng seed is " + rngSeed + ".");
            HashSet<Ship> movingtowardsbase = new HashSet<Ship>();

            for (; ; )
            {
                game.UpdateFrame();
                Player me = game.me;
                GameMap = game.gameMap;
                Ship.Map = GameMap;
                Ship.MyShipyards = new List<Shipyard> { me.shipyard };

                List<Command> commandQueue = new List<Command>();
                CollisionCells = new Dictionary<MapCell, Direction>();
                var usedShips = new HashSet<Ship>();

                //logical marking
                foreach(var ship in me.ships.Values) {
                    AddMoveCollision(ship.CurrentMapCell, Direction.STILL);

                    if(ship.CurrentMapCell == GameMap.At(me.shipyard) && movingtowardsbase.Contains(ship))
                        movingtowardsbase.Remove(ship);

                    // TODO add hyperparameters and tuning
                    if(ship.halite > .7f * Constants.MAX_HALITE)
                        movingtowardsbase.Add(ship);
                }

                // TODO avoid thread lock where 4 ships surround base... easy to solve by forcing ships to approach from top or bottom
                // move to base
                foreach (var ship in me.ships.Values.OrderBy(x => x.DistanceToShipyard).ToList()) {
                    if(!movingtowardsbase.Contains(ship))
                        continue;
                    var direction = me.shipyard.position.GetDirectionTo(ship.position);
                    commandQueue.Add(ship.Move(GetMove(ship, direction)));
                    usedShips.Add(ship);
                }

                // collect halite
                foreach (Ship ship in me.ships.Values)
                {
                    if(usedShips.Contains(ship))
                        continue;
                    if(ship.CurrentMapCell != GameMap.At(me.shipyard) &&
                            GameMap.At(ship).halite > Constants.MAX_HALITE / 10) {
                        commandQueue.Add(ship.StayStill());
                        usedShips.Add(ship);
                    }
                }

                // move to collect halite
                foreach (Ship ship in me.ships.Values)
                {
                    if(usedShips.Contains(ship) || ship.halite < ship.CurrentMapCell.halite / 10)
                        continue;

                    var bestNeighbors = GameMap.NeighborsAt(ship.position).OrderByDescending(n => n.halite).ToList();

                    int i=0;
                    Direction d = Direction.STILL; 
                    while(i < bestNeighbors.Count && d == Direction.STILL) {
                        if(ship.CurrentMapCell.halite * 1.5 > bestNeighbors[i].halite) {
                            break;
                        }
                        var direction = bestNeighbors[i].position.GetDirectionTo(ship.position);
                        if(IsSafeMove(ship.CurrentMapCell, direction)) {
                            d = direction;
                        }
                        i++;
                    }
                    commandQueue.Add(ship.Move(d));
                    AddMoveCollision(ship.CurrentMapCell, d);
                }

                // spawn ships
                if (game.turnNumber <= 200 &&
                    me.halite >= Constants.SHIP_COST &&
                    IsSafeMove(GameMap.At(me.shipyard.position), Direction.STILL))
                {
                    commandQueue.Add(me.shipyard.Spawn());
                }

                game.EndTurn(commandQueue);
            }
        }

        public static bool IsSafeMove(MapCell current, Direction move) {
            MapCell target = GameMap.At(current.position.DirectionalOffset(move));
            if(!CollisionCells.ContainsKey(target))
                return true;
            if(CollisionCells[target] == Direction.STILL)
                return false;
           return CollisionCells[target] != move;
        }

        public static void AddMoveCollision(MapCell current, Direction move) {
            Direction unsafeMove = DirectionExtensions.InvertDirection(move);
            MapCell target = GameMap.At(current.position.DirectionalOffset(move));
            if(CollisionCells.ContainsKey(target))
                CollisionCells[target] = Direction.STILL;
            else
                CollisionCells.Add(target, Direction.STILL);
            CollisionCells[current] = unsafeMove;
        }

        public static Direction GetMove(Ship e, Direction d) {
            if(IsSafeMove(e.CurrentMapCell, d)) {
                AddMoveCollision(e.CurrentMapCell, d);
                return d;
            } else {
                return Direction.STILL; // todo set to null
            }
        }
    }
}
