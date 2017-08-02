using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace  Open3270.TN3270
{
	public static class Constants
	{
		public static readonly TnKey[] FunctionKeys = new TnKey[] { TnKey.F1, TnKey.F2, TnKey.F3, TnKey.F4, TnKey.F5, TnKey.F6, TnKey.F7, TnKey.F8, TnKey.F9, TnKey.F10, TnKey.F11, TnKey.F12 };
		public static readonly TnKey[] AKeys = new TnKey[] { TnKey.PA1, TnKey.PA2, TnKey.PA3, TnKey.PA4, TnKey.PA5, TnKey.PA6, TnKey.PA7, TnKey.PA8, TnKey.PA9, TnKey.PA10, TnKey.PA11, TnKey.PA12 };

		/// <summary>
		/// A simple lookup table to return the numeric component of a function key.
		/// Likely more efficient than a bunch of string creating, comparison and parsing.
		/// </summary>
		public static readonly Dictionary<TnKey, int> FunctionKeyIntLUT = new Dictionary<TnKey, int>();

		static Constants()
		{
			FunctionKeyIntLUT.Add(TnKey.F1, 1);
			FunctionKeyIntLUT.Add(TnKey.F2, 2);
			FunctionKeyIntLUT.Add(TnKey.F3, 3);
			FunctionKeyIntLUT.Add(TnKey.F4, 4);
			FunctionKeyIntLUT.Add(TnKey.F5, 5);
			FunctionKeyIntLUT.Add(TnKey.F6, 6);
			FunctionKeyIntLUT.Add(TnKey.F7, 7);
			FunctionKeyIntLUT.Add(TnKey.F8, 8);
			FunctionKeyIntLUT.Add(TnKey.F9, 9);
			FunctionKeyIntLUT.Add(TnKey.F10, 10);
			FunctionKeyIntLUT.Add(TnKey.F11, 11);
			FunctionKeyIntLUT.Add(TnKey.F12, 12);
			FunctionKeyIntLUT.Add(TnKey.PA1, 1);
			FunctionKeyIntLUT.Add(TnKey.PA2, 2);
			FunctionKeyIntLUT.Add(TnKey.PA3, 3);
			FunctionKeyIntLUT.Add(TnKey.PA4, 4);
			FunctionKeyIntLUT.Add(TnKey.PA5, 5);
			FunctionKeyIntLUT.Add(TnKey.PA6, 6);
			FunctionKeyIntLUT.Add(TnKey.PA7, 7);
			FunctionKeyIntLUT.Add(TnKey.PA8, 8);
			FunctionKeyIntLUT.Add(TnKey.PA9, 9);
			FunctionKeyIntLUT.Add(TnKey.PA10, 10);
			FunctionKeyIntLUT.Add(TnKey.PA11, 11);
			FunctionKeyIntLUT.Add(TnKey.PA12, 12);


		}
	}
}
