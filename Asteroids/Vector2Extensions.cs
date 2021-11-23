using System.Numerics;

namespace Asteroids
{
    public static class Vector2Extensions
    {
        public static void Deconstruct(this Vector2 vector, out float x, out float y)
        {
            x = vector.X;
            y = vector.Y;
        }
    }
}