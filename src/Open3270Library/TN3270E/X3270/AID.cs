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

namespace Open3270.TN3270
{
	/// <summary>
	/// Attention ID
	/// </summary>
	internal class AID
	{
		//No AID generated
		public const byte  None		= 0x60;	
		public const byte  QReply	= 0x61;
		public const byte  Enter	= 0x7d;
		public const byte  F1		= 0xf1;
		public const byte  F2		= 0xf2;
		public const byte  F3		= 0xf3;
		public const byte  F4		= 0xf4;
		public const byte  F5		= 0xf5;
		public const byte  F6		= 0xf6;
		public const byte  F7		= 0xf7;
		public const byte  F8		= 0xf8;
		public const byte  F9		= 0xf9;
		public const byte  F10	= 0x7a;
		public const byte  F11	= 0x7b;
		public const byte  F12	= 0x7c;
		public const byte  F13	= 0xc1;
		public const byte  F14	= 0xc2;
		public const byte  F15	= 0xc3;
		public const byte  F16	= 0xc4;
		public const byte  F17	= 0xc5;
		public const byte  F18	= 0xc6;
		public const byte  F19	= 0xc7;
		public const byte  F20	= 0xc8;
		public const byte  F21	= 0xc9;
		public const byte  F22	= 0x4a;
		public const byte  F23	= 0x4b;
		public const byte  F24	= 0x4c;
		public const byte  Oicr	= 0xe6;
		public const byte  MsrMhs	= 0xe7;
		public const byte  SELECT	= 0x7e;
		public const byte  PA1		= 0x6c;
		public const byte  PA2		= 0x6e;
		public const byte  PA3		= 0x6b;
		public const byte  Clear	= 0x6d;
		public const byte  SysReq	= 0xf0;

		public const byte  SF		= 0x88;
		public const byte  SF_QReply	= 0x81;
	}
};
