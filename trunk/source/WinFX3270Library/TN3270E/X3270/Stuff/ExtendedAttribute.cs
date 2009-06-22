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
	/// Summary description for ExtendedAttribute.
	/// </summary>
	internal class ExtendedAttribute
	{
public const byte  GR_BLINK	=0x01;
public const byte  GR_REVERSE	=0x02;
public const byte  GR_UNDERLINE	=0x04;
public const byte  GR_INTENSIFY	=0x08;

public const byte  CS_MASK		=0x03;	/* mask for specific character sets */
public const byte CS_GE		=0x04;	/* cs flag for Graphic Escape */

		internal ExtendedAttribute()
		{
			cs = 0;
			fg = 0;
			gr = 0;
			bg = 0;
		}
		public byte cs;
		public byte fg;
		public byte bg;
		public byte gr;

		public bool IsZero
		{
			get { return (cs+fg+bg+gr)==0;}
		}
	}
}
