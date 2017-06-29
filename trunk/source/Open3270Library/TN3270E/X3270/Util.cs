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
	/// Summary description for Util.
	/// </summary>
	internal class Util
	{

		/// <summary>
		/// Expands a character in the manner of "cat -v".
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		static public string ControlSee(byte c)
		{
			string p = "";

			c &= 0xff;
			if ((c & 0x80)!=0 && (c <= 0xa0)) 
			{
				p+= "M-";
				c &= 0x7f;
			}
			if (c >= ' ' && c != 0x7f) 
			{
				p+=System.Convert.ToChar((char)c);
			} 
			else 
			{
				p+= "^";
				if (c == 0x7f) 
				{
					p+= "?";
				} 
				else 
				{
					p+= ""+System.Convert.ToChar((char)c) + "@";
				}
			}
			return p;
		}


		public static int DecodeBAddress(byte c1, byte c2)
		{
			if ((c1 & 0xC0) == 0x00)
			{
				return (int)(((c1 & 0x3F) << 8) | c2);
			}
			else
			{
				return (int)(((c1 & 0x3F) << 6) | (c2 & 0x3F));
			}
		}


		public static void EncodeBAddress(NetBuffer ptr, int addr)
		{
			if ((addr) > 0xfff)
			{
				ptr.Add(((addr) >> 8) & 0x3F);
				ptr.Add((addr) & 0xFF);
			}
			else
			{
				ptr.Add(ControllerConstant.CodeTable[((addr) >> 6) & 0x3F]);
				ptr.Add(ControllerConstant.CodeTable[(addr) & 0x3F]);
			}
		}

	}
}
