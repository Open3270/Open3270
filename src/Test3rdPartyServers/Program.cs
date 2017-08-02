using System;

namespace Test3rdPartyServers
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			try
			{
				new TheTests().Run();
			}
			catch (Exception e)
			{
				Console.WriteLine("Exception\n" + e);
			}
		}
	}
}
