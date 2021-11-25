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
            const float targetFramerate = 30f;
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
            var rotationGroupAssignements = new int[height, width];
            var newRotationGroupAssignements = new int[height, width];
            var testPlanetCenter = new PointInt(70*2, 50*2);

            var asteroids = new List<Asteroid>()
            {
                /*new (new PointInt(20 * 2, 12 * 2), 7 * 2),
                new (new PointInt(15 * 2, 80 * 2), 5 * 2),
                new (new PointInt(20 * 2, 46 * 2), 10 * 2)*/
            };

            var rotationGroups = new List<RotationGroup>()
            {
                default, // index 0 is default which shouldn't rotate
                new(new Vector2(testPlanetCenter.X, testPlanetCenter.Y), 0.05f) // for planet
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
                    var radius = 20 * 2;
                    if (IsPointInCircle(new PointInt(x, y), testPlanetCenter, radius) ||
                        (x < testPlanetCenter.X + 3 && x > testPlanetCenter.X - 3 && y > testPlanetCenter.Y - radius - 6 && y < testPlanetCenter.Y))
                    {
                        pixels[y, x] = true;
                        rotationGroupAssignements[y, x] = 1;
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

                    var velocitiesPerAsteroid = new Vector2[] { new (1f, 0.4f), new (1f, -0.2f), new (1f, 0f) };
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

            var averageTimer = Stopwatch.StartNew();
            // update
            while (true)
            {
                frameTimer.Reset();
                // Console.ReadLine(); // waits for enter to go to next frame
                frameTimer.Start();
                Clear(newPixels, newVelocities, newRealPositions, newRotationGroupAssignements);
                var pixelsThatNoPixelRotatesTo = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!pixels[y, x])
                        {
                            continue;
                        }
                        
                        // rotation tips: https://stackoverflow.com/questions/4764516/help-with-rotating-xy-pixel-coordinates-unsure-about-my-function
                        // find the pixel that rotates to this pixel, by iterating over all rotationGroups, and rotating in minus angle
                        // unfortunately this loop needs to be moved before this velocity loop, because  
                        var foundPixelThatRotatesToThisPixel = false;
                        for (int rotationGroupIdx = 1; rotationGroupIdx < rotationGroups.Count; rotationGroupIdx++)
                        {
                            var rotationGroup = rotationGroups[rotationGroupIdx];
                            if (realPositions[y, x] != rotationGroup.Pivot)
                            {
                                var posRelativeToPivot =  realPositions[y, x] - rotationGroup.Pivot;
                                var r = posRelativeToPivot.Length();
                                var angle = Math.Atan2(posRelativeToPivot.Y, posRelativeToPivot.X);
                                angle += -rotationGroup.AngularVelocity; // rotate in minus angle because I'm looking for pixel that rotates to current pixel
                                // Transform back from polar coordinates to cartesian 
                                var rotatedPosRelativeToPivot = new Vector2(r * (float)Math.Cos(angle), r * (float)Math.Sin(angle));
                                var rotatedRealPosition = rotatedPosRelativeToPivot + rotationGroup.Pivot;
                                var pixelThatRotatesToCurrent = new PointInt((int)Math.Round(rotatedRealPosition.X), (int)Math.Round(rotatedRealPosition.Y));
                                
                                if (pixelThatRotatesToCurrent.Y >= newRealPositions.GetLength(0) || 
                                    pixelThatRotatesToCurrent.X >= newRealPositions.GetLength(1) ||
                                    pixelThatRotatesToCurrent.Y < 0 || pixelThatRotatesToCurrent.X < 0) // can't be it because out of bounds
                                {
                                    continue;
                                }

                                if (rotationGroups[rotationGroupAssignements[pixelThatRotatesToCurrent.Y, pixelThatRotatesToCurrent.X]] == rotationGroup)
                                {
                                    // pixel that rotates to current pixel is in this rotation group
                                    realPositions[y, x] = realPositions[pixelThatRotatesToCurrent.Y, pixelThatRotatesToCurrent.X];
                                    velocities[y, x] = velocities[pixelThatRotatesToCurrent.Y, pixelThatRotatesToCurrent.X];
                                    // throw new Exception();
                                    foundPixelThatRotatesToThisPixel = true;
                                }
                            }
                        }

                        if (!foundPixelThatRotatesToThisPixel)
                            pixelsThatNoPixelRotatesTo++;
                        
                        
                        var velocity = new Vector2(velocities[y, x].X, velocities[y, x].Y);
                        velocities[y, x] = new Vector2(0f, 0f);

                        var rotationGroupIndex = rotationGroupAssignements[y, x];
                        var positionToMoveTo = ApplyVelocity(realPositions[y, x], velocity, newRealPositions, rotationGroups[rotationGroupIndex]);

                        // if (OutsideOfGrid(positionToMoveTo, width, height))
                        // {
                        //     // destroys pixels that left the grid
                        //     newPixels[y, x] = false;
                        //     continue;
                        // }
                        
                        if (newPixels[positionToMoveTo.Y, positionToMoveTo.X] &&
                            rotationGroupAssignements[positionToMoveTo.Y, positionToMoveTo.X] == rotationGroupIndex)
                        {
                            // target pixel already occupied by same rotation group pixel
                            positionToMoveTo = new PointInt(x, y);
                        }

                        /*if (newPixels[positionToMoveTo.Y, positionToMoveTo.X]) 
                        {
                            // collision, destroy both
                            newPixels[positionToMoveTo.Y, positionToMoveTo.X] = false;
                            newVelocities[positionToMoveTo.Y, positionToMoveTo.X] = new Vector2(0f, 0f);
                        }
                        else
                        {*/
                            newPixels[positionToMoveTo.Y, positionToMoveTo.X] = true;
                            newVelocities[positionToMoveTo.Y, positionToMoveTo.X] = velocity;
                        // }
                    }

                    tick++;
                }
                
                // render
                Render(height, width, newPixels, newVelocities, view, pixels, engine);
                pixels = newPixels.Clone() as bool[,];
                velocities = newVelocities.Clone() as Vector2[,];
                realPositions = newRealPositions.Clone() as Vector2[,];

                var timeToWait = TimeSpan.FromMilliseconds(1000f/targetFramerate) - logicTimer.Elapsed;
                // new ManualResetEvent(false).WaitOne(timeToWait); // THIS BLOCKS THE THREAD
                AccurateWait(timeToWait); // THIS BUSY-BLOCKS THE THREAD

                if (tick % targetFramerate == 0)
                {
                    var elapsed1s = ((float)averageTimer.ElapsedMilliseconds / targetFramerate ).ToString();
                    engine.WriteText(new Point(0, 0), $"{elapsed1s}{new string(' ', Console.BufferWidth - elapsed1s.Length)}", 15); // to clear 2nd digit of prev frame
                }
                frameTimer.Stop();
                var elapsedFrame = frameTimer.ElapsedMilliseconds.ToString();
                engine.WriteText(new Point(0, 1), $"{elapsedFrame}{new string(' ',  Console.BufferWidth - elapsedFrame.Length)}", 15); // to clear 2nd digit of prev frame
                engine.DisplayBuffer(); // THIS LINE ALONE TAKES 30-50 MS (at least in debug)

                if (tick % targetFramerate == 0) // reset timer every second
                    averageTimer.Restart();
                
                tick++;

                // Console.ReadLine();
            }

            Console.ReadLine();
        }

        private static bool OutsideOfGrid(PointInt position, int width, int height)
        {
            return position.X < 0 || position.X >= width || position.Y < 0 ||
                   position.Y >= height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PointInt ApplyVelocity(Vector2 realPosition, Vector2 velocity, Vector2[,] newRealPositions, RotationGroup rotationGroup)
        {
            var currentPositionInt = new PointInt((int)Math.Round(realPosition.X), (int)Math.Round(realPosition.Y));
            var newRealPosition = realPosition;

            var direction = velocity.Normalize(); // for now treating velocity as if it has magnitude 1
            newRealPosition += direction;
            var newPosition = new PointInt((int)Math.Round(newRealPosition.X), (int)Math.Round(newRealPosition.Y));
            
            if (newPosition.Y < newRealPositions.GetLength(0) && 
                newPosition.X < newRealPositions.GetLength(1) &&
                newPosition.Y >= 0 && newPosition.X >= 0) // if out of bounds it will be detected and destroyed later
            {
                // if (newPosition.X == 125 && newPosition.Y == 115 )
                //     throw new Exception();
                
                newRealPositions[newPosition.Y, newPosition.X] = newRealPosition;
            }

            if (newPosition.Y == -1)
                throw new Exception();

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

        private static void Clear(bool[,] array, Vector2[,] array2, Vector2[,] array3, int[,] array4)
        {
            for (int y = 0; y < array.GetLength(0); y++)
            {
                for (int x = 0; x < array.GetLength(1); x++)
                {
                    array[y, x] = false;
                    array2[y, x] = Vector2.Zero;
                    array3[y, x] = Vector2.Zero;
                    array4[y, x] = 0;
                }
            }
        }
        
        private static void AccurateWait(TimeSpan timeToWait)
        {
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeToWait)
            {
                if (timeToWait - sw.Elapsed > TimeSpan.FromMilliseconds(6))
                {
                    Thread.Sleep(0);
                }
            }
        }
        

    }


    [DebuggerDisplay("({X},{Y})")]
    record struct PointInt
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

    record struct RotationGroup
    {
        public Vector2 Pivot;
        public float AngularVelocity;

        public RotationGroup(Vector2 pivot, float angularVelocity)
        {
            Pivot = pivot;
            AngularVelocity = angularVelocity;
        }
        
        // #region Equality
        // public bool Equals(RotationGroup other)
        // {
        //     return Pivot.Equals(other.Pivot) && AngularVelocity.Equals(other.AngularVelocity);
        // }
        //
        // public override bool Equals(object obj)
        // {
        //     return obj is RotationGroup other && Equals(other);
        // }
        //
        // public override int GetHashCode()
        // {
        //     return HashCode.Combine(Pivot, AngularVelocity);
        // }
        //
        // public static bool operator ==(RotationGroup left, RotationGroup right)
        // {
        //     return left.Equals(right);
        // }
        //
        // public static bool operator !=(RotationGroup left, RotationGroup right)
        // {
        //     return !left.Equals(right);
        // }
        // #endregion
    }
    }