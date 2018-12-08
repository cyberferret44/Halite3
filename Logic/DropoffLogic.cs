using System.Collections.Generic;
using Halite3.hlt;
using System.Linq;
using System;

namespace Halite3.Logic {
    public class DropoffLogic : Logic {
        // Super optomized dropoff logic
        Dictionary<Point, int[]> DropoffQueue = new Dictionary<Point, int[]>();

        // virtual drop off
        private class VirtualDropoff {
            public Position Position;
            public int InitialHalite;
            public VirtualDropoff(Position p, int halite) {
                Position = p;
                InitialHalite = halite;
            }
        }

        // local parameters
        private HashSet<int> MovingTowardsBase = new HashSet<int>();
        private List<VirtualDropoff> BestDropoffs = new List<VirtualDropoff>();
        VirtualDropoff NextDropoff = null;
        private int Xlayers;
        private int MinDropoffValue;
        private int Spacing;
        private double HarvestedPercentToDelete = .8;
        HashSet<int> shipsAssignedToNextDropoff = new HashSet<int>();

        // Abstractc Logic Implementation
        public override void Initialize() {
            // value initialization
            Xlayers = Map.width / 4;
            MinDropoffValue = (int)(((double)Xlayers + 1.0) / 2.0 * 4.0 * (double)Xlayers * 135.0);
            Spacing = (int)HParams[Parameters.DROPOFF_DISTANCE];
            Log.LogMessage($"Spacing is {Spacing}");

            // todo one magic number
            // todo set min to either MinDropoffValue, or like half of the max value so we don't get stuck in too-small of a local minima
            var availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
            while(availableCells.Count > 0) {
                int max = -1;
                Position pos = null;
                foreach(var cell in availableCells) {
                    int val = Map.GetXLayers(cell.position, Xlayers).Sum(x => x.halite);
                    if(val > max || (val == max && Map.CalculateDistance(cell.position, Me.shipyard.position) < Map.CalculateDistance(pos, Me.shipyard.position))) {
                        pos = cell.position;
                        max = val;
                    }
                }
                if(max < MinDropoffValue || (BestDropoffs.Count > 0 && max < BestDropoffs[0].InitialHalite / 1.75))
                    break;
                BestDropoffs.Add(new VirtualDropoff(pos, max));
                availableCells = Map.GetAllCells().Where(c => DistanceToClosestVirtualOrRealDropoff(c.position) >= Spacing).ToList();
            }
        }

        public override void ProcessTurn() {
            foreach(var ship in Me.ShipsSorted) {
                if(ShouldMoveShip(ship))
                    MovingTowardsBase.Add(ship.Id);
                if(ship.OnDropoff)
                    MovingTowardsBase.Remove(ship.Id);
            }

            foreach(var d in BestDropoffs.ToList()) {
                int halite = Map.GetXLayers(d.Position, Xlayers).Sum(x => x.halite);
                if(Map.At(d.Position).IsStructure || halite < d.InitialHalite * HarvestedPercentToDelete) {
                    if(NextDropoff == d) {
                        DeleteNextDropoff();
                    }
                    BestDropoffs.Remove(d);
                }
            }
        }

        public override void CommandShips(List<Ship> ships) {
            // need this for the virtual dropoff to be included
            ships = ships.Where(s => MovingTowardsBase.Contains(s.Id)).ToList();
            ships = ships.OrderBy(s => Map.CalculateDistance(s.position, GetClosestDropoff(s))).ToList();
            // todo, queue up ships
            foreach(var ship in ships) {
                if(NextDropoff != null && ship.position.Equals(NextDropoff.Position) && CanCreateDropoff(ship)) {
                    MyBot.MakeMove(ship.MakeDropoff());
                } else {
                    Position closestDrop = GetClosestDropoff(ship);
                    List<Direction> directions = closestDrop.GetAllDirectionsTo(ship.position);
                    directions = directions.OrderBy(d => Map.At(ship.position.DirectionalOffset(d)).halite).ToList();
                    if(directions.Count == 1 && Map.At(ship.position.DirectionalOffset(directions[0])).IsOccupiedByOpponent()) {
                        if(directions[0] == Direction.NORTH)
                            directions.AddRange(new List<Direction>{ Direction.EAST, Direction.WEST});
                        if(directions[0] == Direction.SOUTH)
                            directions.AddRange(new List<Direction>{ Direction.EAST, Direction.WEST});
                        if(directions[0] == Direction.EAST)
                            directions.AddRange(new List<Direction>{ Direction.NORTH, Direction.SOUTH});
                        if(directions[0] == Direction.WEST)
                            directions.AddRange(new List<Direction>{ Direction.NORTH, Direction.SOUTH});
                    }
                    directions.Add(Direction.STILL);
                    foreach(Direction d in directions) {
                        if(IsSafeMove(ship, d)) {
                            MyBot.MakeMove(ship.Move(d));
                            break;
                        }
                    }
                }
            }
        }

        private int DistanceToClosestVirtualOrRealDropoff(Position position) {
            if(BestDropoffs.Count == 0)
                return int.MaxValue;
            int closestReal = Me.GetDropoffs().Min(x => Map.CalculateDistance(position, x.position));
            int closestVirtual = BestDropoffs.Min(x => Map.CalculateDistance(x.Position, position));
            return Math.Min(closestReal, closestVirtual);
        }

        private VirtualDropoff GetClosestVirtualDropoff(Position position) {
            if(BestDropoffs.Count == 0)
                return null;
            return BestDropoffs.OrderBy(d => Map.CalculateDistance(position, d.Position)).First();
        }

        // Forecasting!!!
        private Position GetClosestDropoff(Ship ship) {
            // if the next drop is closer than the closest REAL dropoff, reassign it
            if(shipsAssignedToNextDropoff.Contains(ship.Id)) {
                return NextDropoff.Position;
            }
            else if(NextDropoff != null && Map.CalculateDistance(NextDropoff.Position, ship.position) < ship.DistanceToDropoff && shipsAssignedToNextDropoff.Count < Me.ShipsSorted.Count / 4) {
                shipsAssignedToNextDropoff.Add(ship.Id);
                Log.LogMessage($"Ship {ship.Id} assigned to ({NextDropoff.Position.x},{NextDropoff.Position.y}).");
                return NextDropoff.Position;
            }
            else if(NextDropoff == null && ShouldCreateDropoff() && DistanceToClosestVirtualOrRealDropoff(ship.position) < ship.DistanceToDropoff) {
                var next = GetClosestVirtualDropoff(ship.position);
                CreateNextDropoff(next);
                shipsAssignedToNextDropoff.Add(ship.Id);
                Log.LogMessage($"Next dropoff at position ({next.Position.x},{next.Position.y}) has been assigned to ship {ship.Id}");
                return NextDropoff.Position;
            }
            return ship.ClosestDropoff.position;
        }

        private bool ShouldCreateDropoff() {
            return  MyBot.game.turnNumber > 100 && (Me.ShipsSorted.Count / Me.GetDropoffs().Count) > 15;
        }

        private bool CanCreateDropoff(Ship ship) {
            return Me.halite + ship.halite + ship.CurrentMapCell.halite >= 5000;
        }

        // todo consider opponents
        private bool ShouldMoveShip(Ship ship) {
            return ship.IsFull() ||
                ship.halite > HParams[Parameters.CARGO_TO_MOVE] * Constants.MAX_HALITE + .25 * ship.CurrentMapCell.halite;
        }

        private void DeleteNextDropoff() {
            Log.LogMessage($"Dropoff {NextDropoff.Position.x},{NextDropoff.Position.y} was deleted.");
            MyBot.ReserveForDropoff = false;
            NextDropoff = null;
            shipsAssignedToNextDropoff.Clear();
        }

        private void CreateNextDropoff(VirtualDropoff v) {
            MyBot.ReserveForDropoff = true;
            NextDropoff = v;
        }
    }
}