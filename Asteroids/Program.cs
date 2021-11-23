using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Asteroids
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            // spout is 180 x 360
            var width = 100;
            var height = 100;
            var pixels = new bool[height, width];
            var newPixels = new bool[height, width];
            var velocities = new Vector2[height, width];
            var testCircleCenter = new PointInt(70, 50);
            var testAsteroidCenter = new PointInt(20, 10);//10, 70);

            // set up initial grid pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // test planet
                    if (IsPointInCircle(new PointInt(x, y), testCircleCenter, 20))
                    {
                        pixels[y, x] = true;
                    }

                    // test asteroid
                    if (IsPointInCircle(new PointInt(x, y), testAsteroidCenter, 7))
                    {
                        pixels[y, x] = true;
                    }
                }
            }
            
            // set up initial velocities
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    velocities[y, x] = default;
                    // test asteroid
                    var velocityForAsteroid = new Vector2(1f, 1f);
                    if (IsPointInCircle(new PointInt(x, y), testAsteroidCenter, 7))
                    {
                        // if (ShouldBePusher(x, y, pixels, velocityForPusher))
                        // {
                        velocities[y, x] = velocityForAsteroid;
                        // }
                    }
                }
            }

            var view = new StringBuilder(width * height);
            Render(height, width, pixels, velocities, view);
            var frameTimer = new Stopwatch();
            var tick = 1;

            // update
            while (true)
            {
                frameTimer.Reset();
                // Console.ReadLine(); // waits for enter to go to next frame
                frameTimer.Start();
                Clear(newPixels);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!pixels[y, x])
                        {
                            continue;
                        }

                        var velocity = velocities[y, x];
                        
                        var positionToMoveTo = GetPositionToMoveTo(x, y, velocity);
                        if (positionToMoveTo.X < 0 || positionToMoveTo.X >= width || positionToMoveTo.Y < 0 ||
                            positionToMoveTo.Y >= height)
                        {
                            // destroys pixels that left the grid
                            newPixels[y, x] = false;
                            continue;
                        }
                        
                        // write new position to a newPixels array, if already occupied, destroy on target position
                        if (newPixels[positionToMoveTo.Y, positionToMoveTo.X]) // probably wrong
                        {
                            // collision, destroy both
                            newPixels[positionToMoveTo.Y, positionToMoveTo.X] = false;
                        }
                        else
                        {
                            newPixels[positionToMoveTo.Y, positionToMoveTo.X] = true;
                            // velocities[y, x] = new Vector2(0f, 0f); // this breaks stuff
                            velocities[positionToMoveTo.Y, positionToMoveTo.X] = velocity;
                        }
                    }

                    tick++;
                }

                pixels = newPixels.Clone() as bool[,];

                // render
                Render(height, width, pixels, velocities, view);
                frameTimer.Stop();
                var timeToWait = TimeSpan.FromMilliseconds(33.33f) - frameTimer.Elapsed;
                if (timeToWait > TimeSpan.Zero)
                {
                    new ManualResetEvent(false).WaitOne(timeToWait); // THIS BLOCKS THE THREAD
                }
            }

            Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldBePusher(int x, int y, bool[,] pixels, Vector2 velocity)
        {
            return PixelExists(GetNeighborPosition(x, y, velocity), pixels) && ! PixelExists(GetNeighborPosition(x, y, -velocity), pixels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PointInt GetPositionToMoveTo(int x, int y, Vector2 velocity)
        {
            return new PointInt(x, y) + GetOffsetFromVelocity(velocity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PixelExists(PointInt position, bool[,] pixels) => pixels[position.Y, position.X];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PointInt GetNeighborPosition(int x, int y, Vector2 velocity)
        {
            var offset = GetOffsetFromVelocity(velocity);
            offset = new PointInt(
                offset.X == 0 ? 0 : offset.X / Math.Abs(offset.X),
                offset.Y == 0 ? 0 : offset.Y / Math.Abs(offset.Y));
            return new PointInt(x, y) + offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PointInt GetOffsetFromVelocity(Vector2 velocity)
        {
            return new PointInt((int)Math.Round(velocity.X), (int)Math.Round(velocity.Y));
        }

        private static void Render(int height, int width, bool[,] pixels, Vector2[,] velocities, StringBuilder view)
        {
            Console.Clear();
            view.Clear();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var velocity = velocities[y, x];
                    if (pixels[y, x])
                    {
                        var character = velocity switch
                        {
                            // _ => '█',
                            (>0f, >0f) => '↘',
                            (>0f, 0f) => '→',
                            (>0f, <0f) => '↗',
                            (0f, <0f) => '↑',
                            (0f, 0f) => '█',
                            (0f, >0f) => '↓',
                            (<0f, <0f) => '↖',
                            (<0f, 0f) => '←',
                            (<0f, >0f) => '↙',
                        };
                        view.Append(character);
                    }
                    else
                    {
                        view.Append(" ");
                    }
                }

                view.AppendLine();
            }

            Console.WriteLine(view);
            // Console.WriteLine("test");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPointInCircle(PointInt point, PointInt circleCenter, int radius)
        {
            var distance = (point - circleCenter).Magnitude();
            return distance <= radius + 0.5f;
        }

        private static void Clear(bool[,] array)
        {
            for (int y = 0; y < array.GetLength(0); y++)
            {
                for (int x = 0; x < array.GetLength(1); x++)
                {
                    array[y, x] = false;
                }
            }
        }
    }

    [DebuggerDisplay("({X},{Y})")]
    struct PointInt
    {
        public int X;
        public int Y;
        public bool NonZero => X != 0 || Y != 0;

        public PointInt(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static PointInt operator +(PointInt a, PointInt b)
        {
            return new PointInt(a.X + b.X, a.Y + b.Y);
        }

        public static PointInt operator -(PointInt a, PointInt b)
        {
            return new PointInt(a.X - b.X, a.Y - b.Y);
        }

        public static PointInt operator *(PointInt a, PointInt b)
        {
            return new PointInt(a.X * b.X, a.Y * b.Y);
        }

        public static PointInt operator /(PointInt a, PointInt b)
        {
            return new PointInt(a.X / b.X, a.Y / b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Magnitude()
        {
            return (float)Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y, 2));
        }
    }
}