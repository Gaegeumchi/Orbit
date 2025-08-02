using System;
using gaegeumchi.Orbit.Server;
using gaegeumchi.Orbit.World;

namespace gaegeumchi.Orbit
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Orbit Server...");

            // MapLoader overworld = new MapLoader("world");

            gaegeumchi.Orbit.Server.Server server = new gaegeumchi.Orbit.Server.Server(25565);
            server.Start();
        }
    }
}