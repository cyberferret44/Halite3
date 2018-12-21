using System.Linq;
using System.Collections.Generic;

namespace Halite3.hlt
{
    /// <summary>
    /// A ship is a type of Entity and is used to collect and transport halite.
    /// <para>
    /// Has a max halite capacity of 1000. Can move once per turn.
    /// </para>
    /// </summary>
    /// <see cref="https://halite.io/learn-programming-challenge/api-docs#ship"></see>
    public class Ship : Entity
    {
        public readonly int halite;
        public static List<Position> MyDropoffs => GameInfo.Me.GetDropoffs().ToList();
        public int Id => this.id.id;
        public bool CanMove => this.halite >= (int)(CellHalite / 10.0);
        public bool OnDropoff => CurrentMapCell.IsStructure && CurrentMapCell.structure.owner.Equals(this.owner);
        public int CellHalite => CurrentMapCell.halite;
        public List<MapCell> Neighbors => CurrentMapCell.Neighbors;
        public Position PreviousPosition;
        public Direction PreviousMove => PreviousPosition == null ? Direction.STILL : position.GetDirectionTo(PreviousPosition);

        public Ship(PlayerId owner, EntityId id, Position position, int halite) : base(owner, id, position)
        {
            this.halite = halite;
        }

        public MapCell CurrentMapCell => GameInfo.CellAt(this.position);
        public int DistanceToMyDropoff => GameInfo.Distance(this, ClosestDropoff);
        public int DistanceToOwnerDropoff => GameInfo.Distance(this, ClosestEnemyDropoff(owner.id));
        public Position ClosestDropoff => MyDropoffs.OrderBy(d => GameInfo.Distance(this, d)).ToList()[0];
        public Position ClosestEnemyDropoff(int playerId) => GameInfo.GetPlayer(playerId).GetDropoffs().OrderBy(d => GameInfo.Distance(this, d)).First();
        public Position ClosestOwnerDropoff => GameInfo.GetPlayer(owner.id).GetDropoffs().OrderBy(d => GameInfo.Distance(this, d)).First();


        // Visibility...
        public List<MapCell> Visibility2 => GameInfo.Map.GetXLayers(position, 2);

        /// <summary>
        /// Returns true if this ship is carrying the max amount of halite possible.
        /// </summary>
        public bool IsFull()
        {
            return halite >= Constants.MAX_HALITE;
        }

        /// <summary>
        /// Returns the command to turn this ship into a dropoff.
        /// </summary>
        public Command MakeDropoff()
        {
            return Command.TransformShipIntoDropoffSite(id);
        }

        /// <summary>
        /// Returns the command to move this ship in a direction.
        /// </summary>
        public Command Move(MapCell target, string comment) => Move(target.position, comment);
        public Command Move(Position target, string comment) => Move(target.GetDirectionTo(position), comment);
        public Command Move(Direction direction, string comment)
        {
            return Command.Move(id, direction, comment);
        }

        /// <summary>
        /// Returns the command to keep this ship still.
        /// </summary>
        public Command StayStill(string comment)
        {
            return Command.Move(id, Direction.STILL, comment);
        }

        /// <summary>
        /// Reads in the details of a new ship from the Halite engine.
        /// </summary>
        public static Ship _generate(PlayerId playerId, List<Ship> previousShips)
        {
            Input input = Input.ReadInput();

            EntityId shipId = new EntityId(input.GetInt());
            int x = input.GetInt();
            int y = input.GetInt();
            int halite = input.GetInt();

            var previous = previousShips.FirstOrDefault(s => s.Id == shipId.id);
            var newShip = new Ship(playerId, shipId, new Position(x, y), halite);
            if(previous != null) {
                newShip.PreviousPosition = previous.position;
            }
            return newShip;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null || this.GetType() != obj.GetType())
                return false;
            if (!base.Equals(obj)) return false;

            Ship ship = (Ship)obj;

            return halite == ship.halite;
        }

        public override int GetHashCode()
        {
            int result = base.GetHashCode();
            result = 31 * result + halite;
            return result;
        }
    }
}
