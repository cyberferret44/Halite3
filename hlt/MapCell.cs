using System;
using System.Collections.Generic;
using System.Linq;

namespace Halite3.hlt
{
    /// <summary>
    /// A map cell is an object representation of a cell on the game map.
    /// Map cell has position, halite, ship, and structure as member variables.
    /// </summary>
    /// <see cref="https://halite.io/learn-programming-challenge/api-docs#map-cell"/>
    public class MapCell
    {
        public readonly Position position;
        public int halite;
        public Ship ship;
        public Entity structure;
        public bool IsStructure => structure != null;
        public bool IsMyStructure => structure != null && structure.owner.id == GameInfo.MyId;
        public bool IsOpponentsStructure => structure != null && structure.owner.id != GameInfo.MyId;
        public MapCell North => GameInfo.CellAt(position, Direction.NORTH);
        public MapCell South => GameInfo.CellAt(position, Direction.SOUTH);
        public MapCell East => GameInfo.CellAt(position, Direction.EAST);
        public MapCell West => GameInfo.CellAt(position, Direction.WEST);
        public MapCell GetNeighbor(Direction d) => d == Direction.NORTH ? North : d == Direction.SOUTH ? South :
                                                   d == Direction.EAST ? East : d == Direction.WEST ? West : this;
        public List<MapCell> Neighbors => new List<MapCell> { North, South, East, West };
        public List<MapCell> NeighborsAndSelf => new List<MapCell> { this, North, South, East, West };
        public List<MapCell> Corners => new List<MapCell> { North.West, North.East, South.East, South.West };
        public int SmallestEnemyValue => Neighbors.Min(x => x.IsOccupiedByOpponent() ? x.ship.halite : int.MaxValue);

        public List<Ship> ClosestShips(List<Ship> ships) {
            int minDist = int.MaxValue;
            var results = new List<Ship>();
            foreach(var ship in ships) {
                int thisDist = GameInfo.Distance(position, ship);
                if(thisDist == minDist) {
                    results.Add(ship);
                } else if( thisDist < minDist) {
                    results.Clear();
                    results.Add(ship);
                    minDist = thisDist;
                }
            }
            return results;
        }

        // Other things
        public bool IsInspired => GameInfo.Map.GetXLayers(position, 4).Sum(x => x.IsOccupiedByOpponent() ? 1 : 0) >= 2;
        public bool IsThreatened => Neighbors.Any(n => n.IsOccupiedByOpponent());
        public List<Ship> ThreatenedBy => Neighbors.Where(n => n.IsOccupiedByOpponent()).Select(n => n.ship).ToList();

        public MapCell(Position position, int halite)
        {
            this.position = position;
            this.halite = halite;
        }

        /// <summary>
        /// Returns true if there is neither a ship nor a structure on this MapCell.
        /// </summary>
        public bool IsEmpty()
        {
            return ship == null && structure == null;
        }

        /// <summary>
        /// Returns true if there is a ship on this MapCell.
        /// </summary>
        public bool IsOccupied()
        {
            return ship != null;
        }

        public bool IsOccupiedByOpponent() {
            return ship != null && ship.owner.id != GameInfo.MyId;
        }

        public bool IsOccupiedByMe() {
            return ship != null && ship.owner.id == GameInfo.MyId;
        }

        /// <summary>
        /// Returns true if there is a structure on this MapCell.
        /// </summary>
        public bool HasStructure()
        {
            return structure != null;
        }

        /// <summary>
        /// Is used to mark the cell under this ship as unsafe (occupied) for collision avoidance.
        /// <para>
        /// This marking resets every turn and is used by NaiveNavigate to avoid collisions.
        /// </para>
        /// </summary>
        /// <seealso cref="GameMap.NaiveNavigate(Ship, Position)"/>
        public void MarkUnsafe(Ship ship)
        {
            this.ship = ship;
        }
    }
}
