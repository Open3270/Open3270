using System;

namespace Sample
{
	internal class Program
	{
		private static void Main(string[] args)
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