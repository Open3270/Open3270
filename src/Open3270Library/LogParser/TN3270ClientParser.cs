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
using Open3270;

namespace Open3270.TN3270
{
	internal enum CS
	{
		Waiting,
		R_IAC,
		R_SB,
		R_DATA,
		R_IAC_END,
		R_WILL,
		R_WONT,
		R_HEADER,
		R_HEADERDATA

		
	}
	/// <summary>
	/// Summary description for TN3270ClientParser.
	/// </summary>
	public class TN3270ClientParser
	{
		int COLS = 80;
		public int BA_TO_ROW(int ba)	
		{
			return ((ba) / COLS);
		}
		public int BA_TO_COL(int ba)
		{
			return ba%COLS;
		}

		//
		const byte IAC				= 255;//Convert.ToChar(255);  // 0xff
		const byte DO					= 253;//Convert.ToChar(253); // 
		const byte DONT				= 254;//Convert.ToChar(254);
		const byte WILL				= 251;//Convert.ToChar(251);
		const byte WONT				= 252;//Convert.ToChar(252);
		//const byte SB					= 250;//Convert.ToChar(250);
		//const byte SE					= 240;//Convert.ToChar(240);
		//const byte EOR					= 239;//Convert.ToChar(240);
		const byte SB	=250;		/* interpret as subnegotiation */
		const byte GA	=249;		/* you may reverse the line */
		const byte EL	=248;		/* erase the current line */
		const byte EC	=247;		/* erase the current character */
		const byte AYT	=246;		/* are you there */
		const byte AO	=245;		/* abort output--but let prog finish */
		const byte IP	=244;		/* interrupt process--permanently */
		const byte BREAK	=243;		/* break */
		const byte DM	=242;		/* data mark--for connect. cleaning */
		const byte NOP	=241;		/* nop */
		const byte SE	=240;		/* end sub negotiation */
		const byte EOR  =239 ;            /* end of record (transparent mode) */ //0xef
		const byte SUSP	=237;		/* suspend process */
		const byte xEOF	=236;		/* end of file */
		

		const byte SYNCH	=242;		/* for telfunc calls */

		const	Char IS			= '0';
		const	Char SEND		= '1';
		const	Char INFO		= '2';
		const	Char VAR		= '0';
		const	Char VALUE		= '1';
		const	Char ESC		= '2';
		const	Char USERVAR	= '3';

		//
		CS cs;
		byte[] data;
		int datapos;
		byte datatype;
		TnHeader header = null;
		/// <summary>
		/// Constructor for the client data parser class
		/// </summary>
		public TN3270ClientParser()
		{
			cs = CS.Waiting;
			data = new byte[10240];
			datapos = 0;
		}
		
		/// <summary>
		/// Parse the next byte in the client data stream
		/// </summary>
		/// <param name="v"></param>
		public void Parse(byte v)
		{
			Console.WriteLine(""+v);
			switch (cs)
			{
				case CS.Waiting:
					if (v==IAC)
					{
						N("IAC");
						cs = CS.R_IAC;
					}
					else
					{
						// assume we're reading a header block
						header = null;
						datatype = 0;
						datapos = 1;
						data[0] = v;
						cs = CS.R_HEADER;
					}
					break;
				case CS.R_HEADER:
					data[datapos] = v;
					datapos++;
					if (datapos == TnHeader.EhSize)
					{
						header = new TnHeader(data);
						datapos = 0;
						cs = CS.R_HEADERDATA;
					}
					break;
				case CS.R_HEADERDATA:
					data[datapos] = v;

					if (datapos==0)
						Console.WriteLine(See.GetAidFromCode(v));
					if (datapos==2)
						Console.WriteLine(Util.DecodeBAddress(data[1], data[2]));

					if (datapos == 3 && data[3] != ControllerConstant.ORDER_SBA)
						throw new ApplicationException("ni");
					else
						Console.WriteLine("SBA");

					if (datapos==5)
					{
						int baddr = Util.DecodeBAddress(data[4],data[5]);
						Console.WriteLine(BA_TO_COL(baddr)+", "+BA_TO_ROW(baddr));
					}

					if (datapos>5)
						Console.WriteLine(See.GetEbc(Tables.Cg2Ebc[data[datapos]]));


					datapos++;
					break;

				case CS.R_IAC:
					if (v==SB)
					{
						N("SB");
						cs = CS.R_DATA;
						datatype = v;
						datapos = 0;
					}
					else if (v==WILL)
					{
						N("WILL");
						cs = CS.R_WILL;
					}
					else
						NError(v);
					break;
				case CS.R_WILL:
					Console.WriteLine("will "+v);
					cs = CS.Waiting;
					break;
				case CS.R_DATA:
					if (v==IAC)
					{
						cs = CS.R_IAC_END;
					}
					else
					{
						data[datapos] = v;
						datapos++;
					}
					break;
				case CS.R_IAC_END:
					if (v==IAC)
					{
						data[datapos] = v;
						datapos++;
					}
					else
					{
						N("IAC");
						if (v==SE)
						{
							N("SE");
							N(data,datapos);
							cs = CS.Waiting;
						}
						else
							NError(v);
					}
					break;
				default:
					NError(v);
					break;
			}
		}
		private void N(string text)
		{
			Console.WriteLine(text);
		}
		private void NError(byte b)
		{
			throw new ApplicationException(String.Format("parse error. State is {0} and byte is {1} ({1:x2})", cs, b));
		}
		private void N(byte[] data, int count)
		{
			
			for (int i=0; i<count; i++)
			{
				Console.Write("{0:x2} ", data[i]);
			}
			Console.WriteLine();
		}
	}
}
