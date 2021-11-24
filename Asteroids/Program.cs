using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGameEngine;
using Point = ConsoleGameEngine.Point;

namespace Asteroids
{
    class Program
    {
        static void Main(string[] args)
        {
            const float targetFramerate = 60f;
            Console.BackgroundColor = ConsoleColor.Black;
            // spout is 180 x 360
            var width = 320;
            var height = 200;
            // using bunch of separate arrays instead of structs for easier porting to JS and/or GPGPU
            var pixels = new bool[height, width];
            var newPixels = new bool[height, width];
            var velocities = new Vector2[height, width];
            var newVelocities = new Vector2[height, width];
            var realPositions = new Vector2[height, width];
            var newRealPositions = new Vector2[height, width];
            var testCircleCenter = new PointInt(70*2, 50*2);

            var asteroids = new List<Asteroid>()
            {
                new Asteroid(new PointInt(20 * 2, 12 * 2), 7 * 2),
                new Asteroid(new PointInt(15 * 2, 80 * 2), 5 * 2),
                new Asteroid(new PointInt(20 * 2, 46 * 2), 10 * 2)
            };
            
            // Console.SetWindowSize(width, height);
            // Console.SetBufferSize(width, height);

            var view = new StringBuilder(width * height);

            var engine = new ConsoleEngine(width, height, 5, 5); // 1 or 8?

            // set up initial grid pixels
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    realPositions[y, x] = new Vector2(x, y);
                    // test planet
                    if (IsPointInCircle(new PointInt(x, y), testCircleCenter, 20*2))
                    {
                        pixels[y, x] = true;
                    }
                    
                    foreach (var asteroid in asteroids)
                    {
                        if (IsPointInCircle(new PointInt(x, y), asteroid.Center, asteroid.Radius))
                        {
                            pixels[y, x] = true;
                        }
                    }
                }
            }

            // Console.BackgroundColor = ConsoleColor.Black;
            // Console.Clear(); // to fill with black
            Render(height, width, pixels, velocities, view, newPixels, engine);
            engine.DisplayBuffer();
            
            // set up initial velocities
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    velocities[y, x] = default;

                    var velocitiesPerAsteroid = new Vector2[] { new Vector2(1f, 0.4f), new Vector2(1f, -0.2f), new Vector2(1f, 0f) };
                    // test asteroid
                    for (int i = 0; i < asteroids.Count; i++)
                    {
                        if (IsPointInCircle(new PointInt(x, y), asteroids[i].Center, asteroids[i].Radius))
                        {
                            velocities[y, x] = velocitiesPerAsteroid[i];
                        }
                    }
                }
            }

            var logicTimer = new Stopwatch();
            var frameTimer = new Stopwatch();
            var tick = 1;
            Console.ReadLine();
            
            // update
            while (true)
            {
                logicTimer.Reset();
                frameTimer.Reset();
                // Console.ReadLine(); // waits for enter to go to next frame
                logicTimer.Start();
                frameTimer.Start();
                Clear(newPixels, newVelocities, newRealPositions);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!pixels[y, x])
                        {
                            continue;
                        }

                        var velocity = new Vector2(velocities[y, x].X, velocities[y, x].Y);
                        velocities[y, x] = new Vector2(0f, 0f);
                        
                        // to support any direction of velocity, I need to store each pixel's position
                        var positionToMoveTo = GetPositionToMoveTo(x, y, velocity, realPositions, newRealPositions);
                        
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
                            newVelocities[positionToMoveTo.Y, positionToMoveTo.X] = new Vector2(0f, 0f);
                        }
                        else
                        {
                            newPixels[positionToMoveTo.Y, positionToMoveTo.X] = true;
                            newVelocities[positionToMoveTo.Y, positionToMoveTo.X] = velocity;
                        }
                    }

                    tick++;
                }
                
                // render
                Render(height, width, newPixels, newVelocities, view, pixels, engine);
                pixels = newPixels.Clone() as bool[,];
                velocities = newVelocities.Clone() as Vector2[,];
                realPositions = newRealPositions.Clone() as Vector2[,];
                logicTimer.Stop();
                
                var timeToWait = TimeSpan.FromMilliseconds(1000f/targetFramerate) - logicTimer.Elapsed;
                if (timeToWait > TimeSpan.Zero)
                {
                    // new ManualResetEvent(false).WaitOne(timeToWait); // THIS BLOCKS THE THREAD
                    AccurateWait((int)timeToWait.TotalMilliseconds); // THIS BUSY-BLOCKS THE THREAD
                }
                
                var elapsed = logicTimer.ElapsedMilliseconds.ToString();
                engine.WriteText(new Point(0, 0), $"{elapsed}{new string(' ', Console.BufferWidth - elapsed.Length)}", 15); // to clear 2nd digit of prev frame
                frameTimer.Stop();
                elapsed = frameTimer.ElapsedMilliseconds.ToString();
                engine.WriteText(new Point(0, 1), $"{elapsed}{new string(' ',  Console.BufferWidth - elapsed.Length)}", 15); // to clear 2nd digit of prev frame
                engine.DisplayBuffer();

                tick++;

                // Console.ReadLine();
            }

            Console.ReadLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PointInt GetPositionToMoveTo(int x, int y, Vector2 velocity, Vector2[,] realPositions, Vector2[,] newRealPositions)
        {
            var direction = velocity.Normalize(); // for now treating velocity as if it has magnitude 1
            var newRealPosition = realPositions[y, x] + direction;
            var newPosition = new PointInt((int)Math.Round(newRealPosition.X), (int)Math.Round(newRealPosition.Y));
            
            if (newPosition.Y < newRealPositions.GetLength(0) && 
                newPosition.X < newRealPositions.GetLength(1) &&
                newPosition.Y >= 0 && newPosition.X >= 0) // if out of bounds it will be detected and destroyed later
            {
                newRealPositions[newPosition.Y, newPosition.X] = newRealPosition;
            }

            return newPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PixelExists(PointInt position, bool[,] pixels) => pixels[position.Y, position.X];

        private static void Render(int height, int width, bool[,] pixels, Vector2[,] velocities, StringBuilder view, bool[,] prevPixels, ConsoleEngine engine)
        {
            // Console.Clear();
            // view.Clear();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y, x] == prevPixels[y, x])
                    {
                        continue;
                    }

                    // var character = pixels[y, x] ? ConsoleCharacter.Full : ConsoleCharacter.Null; //'█' : ' ';
                    var color = pixels[y, x] ? 15 : 0;
                    // engine.SetPixel(new Point(x, y), 15, character); // 0 is black, 15 is white
                    engine.SetPixel(new Point(x, y), 0, color, ' ');
                    // var velocity = velocities[y, x];
                    // if (pixels[y, x])
                    // {
                    //     var character = velocity switch // console font doesn't support some of those characters unfortunately
                    //     {
                    //         // _ => '█',
                    //         (>0f, >0f) => '↘',
                    //         (>0f, 0f) => '→',
                    //         (>0f, <0f) => '↗',
                    //         (0f, <0f) => '↑',
                    //         (0f, 0f) => '█',
                    //         (0f, >0f) => '↓',
                    //         (<0f, <0f) => '↖',
                    //         (<0f, 0f) => '←',
                    //         (<0f, >0f) => '↙',
                    //     };
                    //     // if (!pixels[y, x] && character == '█') // hack to visualize velocity for empty pixels
                    //     // {
                    //     //     character = ' ';
                    //     // }
                    //
                    //     character = '█';
                    //     view.Append(character);
                    // }
                    // else
                    // {
                    //     view.Append(' ');
                    // }

                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPointInCircle(PointInt point, PointInt circleCenter, int radius)
        {
            var distance = (point - circleCenter).Magnitude();
            return distance <= radius + 0.5f;
        }

        private static void Clear(bool[,] array, Vector2[,] array2, Vector2[,] array3)
        {
            for (int y = 0; y < array.GetLength(0); y++)
            {
                for (int x = 0; x < array.GetLength(1); x++)
                {
                    array[y, x] = false;
                    array2[y, x] = Vector2.Zero;
                    array3[y, x] = Vector2.Zero;
                }
            }
        }
        
        private static void AccurateWait(int ms)
        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < ms)
                ;
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

    struct Asteroid
    { 
        public PointInt Center;
        public int Radius;

        public Asteroid(PointInt center, int radius)
        {
            Center = center;
            Radius = radius;
        }
    }
}