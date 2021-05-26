using System;

//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

namespace SPECjbb2005
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine();
            if(System.Runtime.GCSettings.IsServerGC)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Current GC mode: Server GC");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Current GC mode: Workstation GC");
            }
            Console.WriteLine();
            Console.ResetColor();
            Specjbb2005.src.spec.jbb.JBBmain main = new Specjbb2005.src.spec.jbb.JBBmain();
            main.JBBmainMain(args);
        }
    }
}