#region License
/* 
 *
 * Open3270 - A C# implementation of the TN3270/TN3270E protocol
 *
 *   Copyright © 2004-2006 Michael Warriner. All rights reserved
 * 
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org.
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
