using System;

namespace NESharp
{
    class Program
    {
        static void Main(string[] args)
        {
            DisplayEngine display = new DisplayEngine();
            display.Construct(680, 480, 2, 2);
            display.Start();

        }
    }
}
