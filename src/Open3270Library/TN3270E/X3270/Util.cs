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
