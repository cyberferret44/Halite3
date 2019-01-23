﻿using System;
using System.Collections.Generic;

namespace Halite3.hlt
{
    /// <summary>
    /// A Direction is one of the 4 cardinal directions or STILL.
    /// </summary>
    /// <see cref="https://halite.io/learn-programming-challenge/api-docs#direction"/>
    public enum Direction
    {
        NORTH = 'n',
        EAST = 'e',
        SOUTH = 's',
        WEST = 'w',
        STILL = 'o'
    }

    public static class DirectionExtensions
    {
        /// <summary>
        /// Returns the opposite of this direction. The opposite of STILL is STILL.
        /// </summary>
        public static Direction InvertDirection(this Direction direction)
        {
            switch (direction)
            {
                case Direction.NORTH: return Direction.SOUTH;
                case Direction.EAST: return Direction.WEST;
                case Direction.SOUTH: return Direction.NORTH;
                case Direction.WEST: return Direction.EAST;
                case Direction.STILL: return Direction.STILL;
                default: throw new InvalidOperationException("Unknown direction " + direction);
            }
        }

        public static List<Direction> GetLeftRightDirections(Direction d) {
            if(d == Direction.NORTH || d == Direction.SOUTH)
                return new List<Direction> { Direction.EAST, Direction.WEST };
            else if(d == Direction.EAST || d == Direction.WEST)
                return new List<Direction> { Direction.NORTH, Direction.SOUTH };
            return new List<Direction>();
        }

        public static Direction[] ALL_CARDINALS = { Direction.NORTH, Direction.SOUTH, Direction.EAST, Direction.WEST };
        public static Direction[] ALL_DIRECTIONS = { Direction.NORTH, Direction.SOUTH, Direction.EAST, Direction.WEST, Direction.STILL};
    }
}
