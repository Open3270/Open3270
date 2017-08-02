using System;

namespace SampleScreenscraping
{
	class MainClass
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
