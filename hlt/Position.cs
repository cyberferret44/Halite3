﻿using System;
using static Halite3.hlt.Direction;

namespace Halite3.hlt
{
    /// <summary>
    /// A position is an object with x and y values indicating the absolute position on the game map.
    /// </summary>
    /// <see cref="https://halite.io/learn-programming-challenge/api-docs#position"/>
    public class Position
    {
        public readonly int x;
        public readonly int y;

        public static int MapWidth;
        public static int MapHeight;

        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Returns a new position based on moving one unit in the given direction from the given position.
        /// Does not account for toroidal wraparound, that's done in GameMap.
        /// </summary>
        /// <seealso cref="GameMap.Normalize(Position)"/>
        public Position DirectionalOffset(Direction d)
        {
            int dx;
            int dy;

            switch (d)
            {
                case NORTH:
                    dx = 0;
                    dy = -1;
                    break;

                case SOUTH:
                    dx = 0;
                    dy = 1;
                    break;

                case EAST:
                    dx = 1;
                    dy = 0;
                    break;

                case WEST:
                    dx = -1;
                    dy = 0;
                    break;

                case STILL:
                    dx = 0;
                    dy = 0;
                    break;

                default:
                    throw new InvalidOperationException("Unknown direction " + d);
            }

            return new Position(x + dx, y + dy);
        }

        public Direction GetDirectionTo(Position otherPosition) {
            int DirectX = Math.Abs(this.x - otherPosition.x);
            int WrapX = MapWidth - DirectX;
            int DirectY = Math.Abs(this.y - otherPosition.y);
            int WrapY = MapHeight - DirectY;
            if(this.x < otherPosition.x)
                return DirectX < WrapX ? Direction.WEST : Direction.EAST;
            if(this.x > otherPosition.x)
                return DirectX < WrapX ? Direction.EAST : Direction.WEST;
            if(this.y > otherPosition.y)
                return DirectY < WrapY ? Direction.SOUTH : Direction.NORTH;
            if(this.y < otherPosition.y)
                return DirectY < WrapY ? Direction.NORTH : Direction.SOUTH;
            return Direction.STILL;
        }
    }
}
