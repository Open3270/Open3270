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

namespace Open3270.Library
{
	internal class ByteHandler
	{
		static public int ToBytes(byte[] buffer, int offset, int data)
		{
			buffer[offset++] = (byte)(data & 0xff);
			buffer[offset++] = (byte)((data & 0xff00) / 0x100);
			buffer[offset++] = (byte)((data & 0xff0000) / 0x10000);
			buffer[offset++] = (byte)((data & 0xff000000) / 0x1000000);
			return offset;
		}
		static public int FromBytes(byte[] buffer, int offset, out int data)
		{
			data = 0;
			data = (int)(buffer[offset++]);
			data += (int)(buffer[offset++] * 0x100);
			data += (int)(buffer[offset++] * 0x10000);
			data += (int)(buffer[offset++] * 0x1000000);
			return offset;
		}
	}
}
