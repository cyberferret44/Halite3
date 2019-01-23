namespace Halite3.hlt
{
    public class Command
    {
        public readonly string command;
        public MapCell TargetCell;
        public Ship Ship;
        private static GameMap gameMap => GameInfo.Map;
        private static Player me => GameInfo.Me;
        public string Comment;

        /// <summary>
        /// Create a new Spawn Ship command
        /// </summary>
        /// <returns>Command("g")</returns>
        public static Command SpawnShip()
        {
            return new Command("g", "") {
                Ship = null,
                TargetCell = gameMap.At(me.shipyard.position)
            };
        }

        /// <summary>
        /// Create a new Dropoff command
        /// </summary>
        /// <returns>Command("g")</returns>
        public static Command TransformShipIntoDropoffSite(EntityId id)
        {
            return new Command("c " + id, "Transforming ship " + id.id + " into a dropoff site.") {
                Ship = me.GetShipById(id.id),
                TargetCell = me.GetShipById(id.id).CurrentMapCell
            };
        }

        /// <summary>
        /// Create a new command for moving a ship in a given direction
        /// </summary>
        /// <param name="id">EntityId of the ship</param>
        /// <param name="direction">Direction to move in</param>
        /// <returns></returns>
        public static Command Move(EntityId id, Direction direction, string comment)
        {
            comment = "Ship " + id.id + " moved " + direction.ToString("g") + ".  " + comment;
            return new Command("m " + id + ' ' + (char)direction, comment) {
                Ship = me.GetShipById(id.id),
                TargetCell = gameMap.At(me.GetShipById(id.id).position.DirectionalOffset(direction))
            };
        }

        private Command(string command, string comment)
        {
            this.command = command;
            this.Comment = comment;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null || this.GetType() != obj.GetType())
                return false;

            Command command1 = (Command)obj;

            return command.Equals(command1.command);
        }

        public override int GetHashCode()
        {
            return command.GetHashCode();
        }
    }
}
