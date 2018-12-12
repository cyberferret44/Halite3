using Halite3.hlt;
using System.Collections.Generic;
using Halite3;
using System.Linq;
namespace Halite3.Logic {
    public abstract class Logic {
        // shortcut accessors
        protected static GameMap Map => MyBot.GameMap;
        protected static Player Me => MyBot.Me;
        protected HyperParameters HParams => MyBot.HParams;
        protected static List<Ship> AllShips => Me.ShipsSorted;

        // Command Queue
        public static List<Command> CommandQueue = new List<Command>();

        // Shared Information
        protected static MoveScores Scores;
        protected static HashSet<int> UsedShips = new HashSet<int>();
        protected static List<Ship> UnusedShips => Me.ShipsSorted.Where(s => !UsedShips.Contains(s.Id)).ToList();
        public static HashSet<MapCell> CollisionCells = new HashSet<MapCell>();
        public static HashSet<MapCell> TwoTurnAvoid = new HashSet<MapCell>();

        //abstract methods
        public abstract void Initialize();
        public abstract void ProcessTurn();
        public abstract void CommandShips();
        public abstract void ScoreMoves();

        // New Turn Method
        public static void InitializeNewTurn() {
            Scores = new MoveScores();
            UsedShips.Clear();
            CommandQueue.Clear();
            CollisionCells.Clear();
            TwoTurnAvoid.Clear();
            Scores.ScoreMoves(AllShips);
            MakeMandatoryMoves();
            foreach(var drop in MyBot.game.Opponents.SelectMany(x => x.GetDropoffs())) {
                if(Map.At(drop.position).Neighbors.Any(x => x.IsOccupiedByOpponent())) {
                    Scores.TapCell(Map.At(drop.position)); // prevents collisions on opponents drop points...
                }
            }
        }

        // static methods
        public static void MakeMove(Command command, string debugMessage) {
            Log.LogMessage($"Ship {command.Ship.id} moved {command.TargetCell.position.GetDirectionTo(command.Ship.position)}. {debugMessage}");
            CommandQueue.Add(command);
            UsedShips.Add(command.Ship.Id);
            CollisionCells.Add(command.TargetCell);
            Scores.AddCommand(command);
            MakeMandatoryMoves();
        }

        public static void MakeMandatoryMoves() {
            foreach(var v in Scores.Moves.Values) {
                if(v.BestMove().MoveValue >= 100000000.0) {
                    Log.LogMessage($"Ship {v.Ship.Id} has only one move, {v.BestMove().Direction.ToString("g")}");
                    MakeMove(v.Ship.Move(v.BestMove().Direction), "move scores incidental mandatory");
                    break;
                }
            }
        }

        // concrete methods
        public static bool IsSafeMove(Ship ship, Direction direction, bool IngoreEnemy = false) {
            MapCell target = Map.At(ship, direction);
            if(target.IsStructure && target.structure.IsMine && !CollisionCells.Contains(target)) {
                return true;
            }
            // todo not the right spot for this
            if(direction != Direction.STILL) {
                if(TwoTurnAvoid.Contains(target) && (int)(ship.CellHalite * .1) + (int)(target.halite * .1) > ship.halite) {
                    return false;
                }
            }
            bool result = !CollisionCells.Contains(target) && (IngoreEnemy || !target.IsOccupiedByOpponent());
            return result;
        }
    }

    public class EmptyLogic : Logic {
        public override void Initialize() { }
        public override void ProcessTurn() { }
        public override void CommandShips() { Log.LogMessage("Empty Logic!"); }
        public override void ScoreMoves() { Log.LogMessage("Empty Logic!"); }
    }
}