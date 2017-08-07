using System;
using System.Collections.Generic;
using System.Text;

namespace Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                new TheDemo().Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception\n" + e);
            }
        }
    }
}
