#region License
/* 
 *
 * Open3270 - A C# implementation of the TN3270/TN3270E protocol
 *
 * Copyright (c) 2004-2020 Michael Warriner
 * Modifications (c) as per Git change history
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial 
 * portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
 
#endregion
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
