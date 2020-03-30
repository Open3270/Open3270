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
using System.Text;

namespace Open3270.TN3270
{
	public static class KeyboardConstants
	{
		public const int ErrorMask = 0x000f;
		public const int ErrorProtected = 1;
		public const int ErrorNumeric = 2;
		public const int ErrorOverflow = 3;
		public const int NotConnected = 0x0010;
		public const int AwaitingFirst = 0x0020;
		public const int OiaTWait = 0x0040;
		public const int OiaLocked = 0x0080;
		public const int DeferredUnlock = 0x0100;
		public const int EnterInhibit = 0x0200;
		public const int Scrolled = 0x0400;
		public const int OiaMinus = 0x0800;

		public const int NoSymbol = 0;
		public const int WFlag = 0x100;
		public const int PasteWFlag = 0x200;
		public const int UnlockMS = 350;


		public static byte[] PfTranslation = new byte[] { 
												 AID.F1,  AID.F2,  AID.F3,  AID.F4,  AID.F5,  AID.F6,
												 AID.F7,  AID.F8,  AID.F9,  AID.F10, AID.F11, AID.F12,
												 AID.F13, AID.F14, AID.F15, AID.F16, AID.F17, AID.F18,
												 AID.F19, AID.F20, AID.F21, AID.F22, AID.F23, AID.F24
											 };

		public static byte[] PaTranslation = new byte[]  { 
												  AID.PA1, AID.PA2, AID.PA3
											  };
	}
}
