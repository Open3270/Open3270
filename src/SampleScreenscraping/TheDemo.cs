using Open3270;
using System;

//
namespace SampleScreenscraping
{
	/// <summary
	/// Summary description for TheDemo.
	/// </summary>
	public class TheDemo : IAudit
	{
		public TheDemo()
		{
		}

		public void Run()
		{
			using (TNEmulator emulator = new TNEmulator())
			{
				emulator.Audit = this;
				emulator.Debug = false;
				emulator.Config.TermType = "IBM-3278-2-E";
				emulator.Config.FastScreenMode = true;
				emulator.Config.AlwaysRefreshWhenWaiting = false;

				emulator.Connect("localhost", 3270, null);
				// wait for the host to startup
				//
				int index = emulator.WaitForTextOnScreen(1000, "Hercules", "Multi-User System for Interactive Computing / System Product");

				if (index == 0)
				{
					Console.WriteLine("Mainframe emulator isn't running MUSIC/SP properly - check readme.md file in TestMainframe project for help");
					return;
				}

				if (index == -1)
				{
					Console.WriteLine("Connection failed - didn't find 'Multi-User System for Interactive Computing / System Product' on screen");
					Console.WriteLine(emulator.CurrentScreenXML.Dump());
					return;
				}
				try
				{
					//
					//
					//
					//emulator.Refresh(true, 20000);
					emulator.SendKey(false, TnKey.Enter, 0);
					index = emulator.WaitForTextOnScreen(2000, "*MUSIC/SP ESA/390, sign on");
					Console.WriteLine("Response : " + index);
					if (index != 0)
					{
						Console.WriteLine(emulator.CurrentScreenXML.Dump());
						Console.WriteLine("ERROR : Failed to find login form failed");
						return;
					}
					//emulator.SendKey(true, TnKey.Enter, 10000);
					//emulator.WaitForHostSettle(100, 10000);
					emulator.SetText("$000");
					emulator.SendKey(false, TnKey.Tab, 0);
					emulator.SetText("music");
					emulator.SendKey(false, TnKey.Enter, 0);
					index = emulator.WaitForTextOnScreen(10000, "Press ENTER to continue...", "Userid is already signed on");
					Console.WriteLine("Response : " + index);
					if (index != 0)
					{
						Console.WriteLine(emulator.CurrentScreenXML.Dump());
						Console.WriteLine("ERROR : Login failed");
						return;
					}
					emulator.SendKey(false, TnKey.Enter, 0);
					index = emulator.WaitForTextOnScreen(10000, "Support Tasks: ADMIN Main Menu");
					Console.WriteLine("Response : " + index);
					if (index != 0)
					{
						Console.WriteLine(emulator.CurrentScreenXML.Dump());
						Console.WriteLine("ERROR : Failed to get to main menu");
						return;
					}
					Console.WriteLine(emulator.CurrentScreenXML.Dump());
					string timeText = emulator.GetText(65, 4, 15).Trim();
					Console.WriteLine("Success! Scraped current mainframe time as : >>>" + timeText + "<<<");

					return;
				}
				catch (Exception ex)
				{
					Console.WriteLine("EX: " + ex);
					Console.WriteLine(emulator.CurrentScreenXML.Dump());
				}

				//emulator.Refresh(true, 20000);

				bool done = false;
				do
				{
					string currentScreen = emulator.CurrentScreenXML.Dump();
					Console.WriteLine("DUMP SCREEN\n=============\n" + currentScreen + "\n=========\n");

					Console.WriteLine("Enter command : - refresh, text <text>, key <key>, quit");
					Console.WriteLine("key is one of : Attn, Backspace,BackTab,CircumNot,Clear,CursorSelect,Delete,DeleteField, DeleteWord,Down,Dup,Enter,Erase,EraseEOF,EraseInput,FieldEnd, FieldMark,FieldExit,Home,Insert,Interrupt,Key,Left,Left2,Newline,NextWord, PAnn, PFnn, PreviousWord,Reset,Right,Right2,SysReq,Tab,Toggle,ToggleInsert,ToggleReverse,Up");

					string command = Console.ReadLine();
					if (command == "quit")
						done = true;
					else if (command == "refresh")
					{
						emulator.Refresh(false, 0);
					}
					else if (command.Length > 4 && command.Substring(0, 4) == "key ")
					{
						emulator.SendKeyFromText(true, command.Substring(4));
					}
					else if (command.Length > 5 && command.Substring(0, 5) == "text ")
					{
						emulator.SendText(command.Substring(5));
					}
					else
						Console.WriteLine("unknown command '" + command + "'");
				}
				while (!done);
				emulator.Close();
			}
		}

		#region IAudit Members

		public void WriteLine(string text)
		{
			Console.WriteLine(text);
		}

		public void Write(string text)
		{
			Console.Write(text);
		}

		#endregion IAudit Members
	}
}