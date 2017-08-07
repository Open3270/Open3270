using System;

namespace SampleScreenscraping
{
	internal class MainClass
	{
		public static void Main(string[] args)
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