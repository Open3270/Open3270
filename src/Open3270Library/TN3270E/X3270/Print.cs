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
using System.IO;

namespace Open3270.TN3270
{
	/// <summary>
	/// Summary description for print.
	/// </summary>
	internal class Print : IDisposable
	{
		Telnet telnet;
		internal Print(Telnet telnet)
		{
			this.telnet = telnet;
		}

		/// <summary>
		/// Print the ASCIIfied contents of the screen onto a stream.
		/// </summary>
		/// <param name="f"></param>
		/// <param name="even_if_empty"></param>
		/// <returns>Returns True if anything printed, False otherwise.</returns>
		bool PrintFormattedScreen(StreamWriter f, bool even_if_empty)
		{
			int i;
			byte e;
			byte c;
			int ns = 0;
			int nr = 0;
			bool any = false;
			byte fa = telnet.Controller.FakeFA;
			int fa_index = telnet.Controller.GetFieldAttribute(0);
			if (fa_index != -1)
				fa = telnet.Controller.ScreenBuffer[fa_index];

			for (i = 0; i < telnet.Controller.RowCount * telnet.Controller.ColumnCount; i++)
			{
				if (i != 0 && (i % telnet.Controller.ColumnCount) == 0)
				{
					nr++;
					ns = 0;
				}
				e = telnet.Controller.ScreenBuffer[i];
				if (FieldAttribute.IsFA(e))
				{
					c = (byte)' ';
					fa = telnet.Controller.ScreenBuffer[i];
				}
				if (FieldAttribute.IsZero(fa))
					c = (byte)' ';
				else
					c = Tables.Cg2Ascii[e];
				if (c == (byte)' ')
					ns++;
				else
				{
					any = true;
					while (nr != 0)
					{
						f.WriteLine();
						nr--;
					}
					while (ns != 0)
					{
						f.WriteLine(" ");
						ns--;
					}
					f.WriteLine(System.Convert.ToChar(c));
				}
			}
			nr++;
			if (!any && !even_if_empty)
				return false;
			while (nr != 0)
			{
				f.WriteLine();
				nr--;
			}
			return true;
		}


		/// <summary>
		///  Print the contents of the screen as text.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool PrintTextAction(params object[] args)
		{
			bool secure = telnet.Appres.secure;

			if (args.Length != 1)
			{
				telnet.Events.ShowError("PrintText_action: requires streamwriter parameter");
				return false;
			}
			StreamWriter f = (StreamWriter)args[0];
			//	secure = True;
			PrintFormattedScreen(f, true);
			return true;
		}

		public void Dispose()
		{

		}
	}
}
