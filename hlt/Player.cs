using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite3.hlt
{
    /// <summary>
    /// Players have an id, a shipyard, halite and dictionaries of ships and dropoffs as member variables.
    /// </summary>
    /// <see cref="https://halite.io/learn-programming-challenge/api-docs#player"/>
    public class Player
    {
        public readonly PlayerId id;
        public readonly Shipyard shipyard;
        public int halite;
        public readonly Dictionary<int, Ship> ships = new Dictionary<int, Ship>();
        public List<Ship> AllShips => ships.Values.ToList();
        public readonly Dictionary<int, Dropoff> dropoffDictionary = new Dictionary<int, Dropoff>();
        public List<Ship> ShipsSorted = new List<Ship>();

        public int NetValue => ships.Values.Sum(x => x.halite) + halite;

        public List<Entity> GetDropoffs()  {
            var dropoffs = dropoffDictionary.Values.Select(v => (Entity)v).ToList();
            dropoffs.Add((Entity)shipyard);
            return dropoffs.Where(d => d.owner.id == id.id).ToList();
        }

        public Ship GetShipById(int id) => ships.ContainsKey(id) ? ships[id] : null;
        
        public List<Ship> ShipsOnDropoffs() {
            var myShips = new List<Ship>();
            foreach(var drop in GetDropoffs()) {
                var shipOnDrop = GameInfo.CellAt(drop.position).ship;
                if(shipOnDrop != null && shipOnDrop.owner.Equals(id)) {
                    myShips.Add(shipOnDrop);
                }
            }
            return myShips;
        }

        private Player(PlayerId playerId, Shipyard shipyard, int halite = 0)
        {
            this.id = playerId;
            this.shipyard = shipyard;
            this.halite = halite;
        }

        /// <summary>
        /// Update each ship and dropoff for the player.
        /// </summary>
        public void _update(int numShips, int numDropoffs, int halite)
        {
            this.halite = halite;

            ships.Clear();
            for (int i = 0; i < numShips; ++i)
            {
                Ship ship = Ship._generate(id);
                ships[ship.Id] = ship;
            }
            
            dropoffDictionary.Clear();
            for (int i = 0; i < numDropoffs; ++i)
            {
                Dropoff dropoff = Dropoff._generate(id);
                dropoffDictionary[dropoff.id.id] = dropoff;
            }

            ShipsSorted = ships.Values.OrderBy(s => SpecialDistance(s, s.ClosestDropoff.position)).ToList();
        }

        private double SpecialDistance(Ship ship, Position target) {
            double dist = (double)ship.DistanceToDropoff;
            double Xcomponent = Math.Pow(ship.position.DeltaX(target)+.01, .5);
            double Ycomponent = Math.Pow(ship.position.DeltaY(target)+.01, .5);
            return dist + (Xcomponent + Ycomponent)/10000.0;
        }

        /// <summary>
        /// Create a new Player by reading from the Halite engine.
        /// </summary>
        /// <returns></returns>
        public static Player _generate()
        {
            Input input = Input.ReadInput();

            PlayerId playerId = new PlayerId(input.GetInt());
            int shipyard_x = input.GetInt();
            int shipyard_y = input.GetInt();

            return new Player(playerId, new Shipyard(playerId, new Position(shipyard_x, shipyard_y)));
        }
    }
}
