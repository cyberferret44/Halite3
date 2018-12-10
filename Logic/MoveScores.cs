using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    // A simplified interface for informational purposes used by Logic classes
    public class ScoredMove {
        public Ship Ship;
        public Direction Direction;
        public double MoveValue;
        public ScoredMove(KeyValuePair<Direction, double> kvp, Ship ship) {
            this.Ship = ship;
            this.Direction = kvp.Key;
            this.MoveValue = kvp.Value;
        }
        public ScoredMove(Direction d, double val, Ship ship) {
            this.Ship = ship;
            this.Direction = d;
            this.MoveValue = val;
        }
    }

    // Aggregate class that contains and calaulcates all scores for all ships passed to it
    public class MoveScores {
        // This class offloads the nitty gritty logic from the calculate method
        public class ScoredMoves {
            // Variables
            public readonly Ship Ship;
            public readonly Dictionary<Direction, double> Scores;
            private readonly List<KeyValuePair<Direction, double>> RemovedMoves;

            // Constructor
            public ScoredMoves(Ship ship) { 
                Ship = ship;
                Scores = new Dictionary<Direction, double>();
                RemovedMoves = new List<KeyValuePair<Direction, double>>();
            }

            // Accessor Variables
            public ScoredMove BestMove => Scores.Count == 0 ? null : Scores.Count == 1 ? new ScoredMove(Scores.Keys.First(), double.MaxValue, Ship) : new ScoredMove(Scores.OrderBy(kvp => kvp.Value).First(), Ship);
            private Point Target(Direction d) => MyBot.GameMap.At(Ship.position.DirectionalOffset(d)).position.AsPoint;

            // Logical Methods
            public void AddMove(Direction direction, double Value) => Scores[direction] = Value;
            public void MultiplyValue(Direction direction, double value) => Scores[direction] = Scores[direction] * value;

            // Heavier Methods
            public void TapCell(MapCell target) {
                foreach(var kvp in Scores) {
                    var targetPos = Ship.position.DirectionalOffset(kvp.Key).AsPoint;
                    if(targetPos.Equals(target.position.AsPoint)) {
                        RemovedMoves.Add(kvp);
                        Scores.Remove(kvp.Key);
                        break;
                    }
                }

                // if there's only one remaining move for this agent, we need to set it to max value
                if(Scores.Count == 1) {
                    Scores[Scores.Keys.First()] = double.MaxValue;
                }
            }
        }

        public List<ScoredMoves> Moves;

        public void ScoreMoves(List<Ship> ships) {
            Moves = new List<ScoredMoves>();
            foreach(var ship in ships) {
                var scoredMoves = new ScoredMoves(ship);

                // Case 1, a ship that can't move should get ultimate priority to sit still
                if(!ship.CanMove) {
                    scoredMoves.AddMove(Direction.STILL, double.MaxValue);
                } else {
                    // Add all directions...
                    foreach(var d in DirectionExtensions.ALL_DIRECTIONS) {
                        scoredMoves.AddMove(d, 1.0);
                    }
                }
                Moves.Add(scoredMoves);
            }

            foreach(var eShip in MyBot.game.Opponents.SelectMany(x => x.ships.Values)) {
                var cell = MyBot.GameMap.At(eShip.position);
                Moves.ForEach(m => m.TapCell(cell));
            }
        }

        // todo test when you get back this should fix the collision issue
        public void AddCommand(Command command) {
            var move = Moves.Single(m => m.Ship.Id.Equals(command.Ship.Id));
            Moves.Remove(move);
            Moves.ForEach(m => m.TapCell(command.TargetCell));
        }
    }
}