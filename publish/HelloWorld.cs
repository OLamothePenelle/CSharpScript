using System;
using System.Linq;

namespace CSharpScript
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CSharpScript's Hello World has received the following arguments:");
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }
        }
    }
}
