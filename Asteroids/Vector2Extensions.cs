using System.Numerics;
using System.Runtime.CompilerServices;

namespace Asteroids
{
    public static class Vector2Extensions
    {
        public static void Deconstruct(this Vector2 vector, out float x, out float y)
        {
            x = vector.X;
            y = vector.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 Normalize(this Vector2 vector)
        {
            if (vector is { X: 0f, Y: 0f })
                return vector;
            
            return vector / vector.Length();
        }
    }
}