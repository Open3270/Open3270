using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerminalDemo
{
	public class FieldIndices
	{

		public static class Login
		{
			public static int Username = 18;
			public static int GroupID = 21;
			public static int Password = 25;
			public static int Language = 29;
			public static int NewPassword = 34;
		}
		public static class Lord
		{
			public static int Command = 1;
			public static int Function = 5;
			public static int Item = 10;
			public static int Action = 12;
			public static int Quantity = 15;
			public static int Price = 17;
			public static int Market = 19;
			public static int Dealer = 21;
			public static int SpecTyping = 25;
			public static int NT = 27;
			public static int PotcSym = 29;
			public static int Account = 31;
			public static int SBDT = 33;
			public static int InvAC = 35;
			public static int MessageFlr = 39;
			public static int SecNo = 42;
			public static int BrSeq = 44;
			public static int BrSeq2 = 45;
			public static int Conc = 47;
		}
	}

	public class ScreenFieldLocation
	{
		public int Index { get; set; }
		public int Row { get; set; }
		public int Column { get; set; }
		public int Length { get; set; }

		public ScreenFieldLocation(int row, int column, int length)
		{
			this.Row = row;
			this.Column = column;
			this.Length = length;
		}
	}
}
