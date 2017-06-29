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
	/// Summary description for AID.
	/// </summary>
	internal class AID
	{
		/* AIDs */
		public const byte  AID_NO		= 0x60;	/* no AID generated */
		public const byte  AID_QREPLY	= 0x61;
		public const byte  AID_ENTER	= 0x7d;
		public const byte  AID_PF1		= 0xf1;
		public const byte  AID_PF2		= 0xf2;
		public const byte  AID_PF3		= 0xf3;
		public const byte  AID_PF4		= 0xf4;
		public const byte  AID_PF5		= 0xf5;
		public const byte  AID_PF6		= 0xf6;
		public const byte  AID_PF7		= 0xf7;
		public const byte  AID_PF8		= 0xf8;
		public const byte  AID_PF9		= 0xf9;
		public const byte  AID_PF10	= 0x7a;
		public const byte  AID_PF11	= 0x7b;
		public const byte  AID_PF12	= 0x7c;
		public const byte  AID_PF13	= 0xc1;
		public const byte  AID_PF14	= 0xc2;
		public const byte  AID_PF15	= 0xc3;
		public const byte  AID_PF16	= 0xc4;
		public const byte  AID_PF17	= 0xc5;
		public const byte  AID_PF18	= 0xc6;
		public const byte  AID_PF19	= 0xc7;
		public const byte  AID_PF20	= 0xc8;
		public const byte  AID_PF21	= 0xc9;
		public const byte  AID_PF22	= 0x4a;
		public const byte  AID_PF23	= 0x4b;
		public const byte  AID_PF24	= 0x4c;
		public const byte  AID_OICR	= 0xe6;
		public const byte  AID_MSR_MHS	= 0xe7;
		public const byte  AID_SELECT	= 0x7e;
		public const byte  AID_PA1		= 0x6c;
		public const byte  AID_PA2		= 0x6e;
		public const byte  AID_PA3		= 0x6b;
		public const byte  AID_CLEAR	= 0x6d;
		public const byte  AID_SYSREQ	= 0xf0;

		public const byte  AID_SF		= 0x88;
		public const byte  SFID_QREPLY	= 0x81;
	}
};
