using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;

namespace Halite3.Logic {
    // A simplified interface for informational purposes used by Logic classes
    public class ScoredMove {
        public Ship Ship;
        public Direction Direction;
        public double MoveValue;
        public MapCell TargetCell => MyBot.GameMap.At(this.Ship, this.Direction);
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
        public ScoredMove BestMove => Scores.Count == 0 ? null : Scores.Count == 1 ? new ScoredMove(Scores.Keys.First(), double.MaxValue, Ship) : new ScoredMove(Scores.OrderBy(kvp => kvp.Value).Last(), Ship);
        private Point Target(Direction d) => MyBot.GameMap.At(Ship, d).position.AsPoint;

        // Logical Methods
        // Logical Methods
        public void AddMove(Direction direction, double Value) {
            if(RemovedMoves.All(m => m.Key != direction))
                Scores[direction] = Value;
        }
        public void RemoveValue(Direction direction) => Scores.Remove(direction);
        public void MultiplyValue(Direction direction, double value) => Scores[direction] = Scores[direction] * value;

        // Heavier Methods
        public void TapCell(MapCell target) {
            foreach(var kvp in Scores) {
                var targetPos = MyBot.GameMap.At(Ship, kvp.Key);
                if(targetPos.Equals(target)) {
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

    // Aggregate class that contains and calaulcates all scores for all ships passed to it
    public class MoveScores {
        // Local Variables
        public Dictionary<int, ScoredMoves> Moves;

        // Logical Methods
        public void AddMove(Ship ship, Direction direction, double value) => Moves[ship.Id].AddMove(direction, value);
        public void RemoveValue(Ship ship, Direction direction) => Moves[ship.Id].RemoveValue(direction);
        public void MultiplyValue(Ship ship, Direction direction, double value) => Moves[ship.Id].MultiplyValue(direction, value);

        // Methods
        public void ScoreMoves(List<Ship> ships) {
            Moves = new Dictionary<int, ScoredMoves>();
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
                Moves.Add(ship.Id, scoredMoves);
            }
        }

        // Get next best command.....
        public ScoredMoves GetBestAvailableMove() {
            //todo consider how it would affect the other agent's command
            ScoredMoves best = null;
            double maxValue = double.MinValue;
            foreach(var move in Moves.Values) {
                if(move.BestMove.MoveValue >= maxValue) {
                    maxValue = move.BestMove.MoveValue;
                    best = move;
                }
            }
            return best;
        }

        public void AddCommand(Command command) {
            var move = Moves.Single(m => m.Key == command.Ship.Id);
            Moves.Remove(move.Key);
            TapCell(command.TargetCell);
        }

        public void TapCell(MapCell cell) {
            Moves.Values.ToList().ForEach(m => m.TapCell(cell));
        }
    }
}