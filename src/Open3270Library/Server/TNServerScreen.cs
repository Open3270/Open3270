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
using System.Collections;

namespace Open3270.TN3270Server
{
	/// <summary>
	/// Summary description for TNServerScreen.
	/// </summary>
	public class TNServerScreen
	{
		public byte[] mScreenBytes;
		public bool fFormatted = true;
		public bool inExtendedMode = false;
		public int currentCursorPosition = 0;
		private ArrayList mStrings;
		private bool mStringsLive = true;
		

		public TNServerScreen(int cx, int cy)
		{
			mScreenBytes = new byte[cx*cy];
			mStrings = new ArrayList();
		}
		public void Clear()
		{
			mStrings.Clear();
			mStringsLive = true;
		}
		public void Add(string text)
		{
			if (!mStringsLive)
				Clear();
			mStrings.Add(text);
		}
		public void SetCursor(int x, int y)
		{
			this.currentCursorPosition = x+y*80;
		}
		public void Format()
		{
			mScreenBytes = FormatScreen((string[])mStrings.ToArray(typeof(string)));

		}
		private byte[] FormatScreen(string[] Data)
		{

			//
			// Step 1 - map screen image into buffer
			//
			byte[] buffer = new Byte[10000];
			int to = 0;
			int i;
			int start,end;
			//
			
			//
			for (i=0; i<24; i++)
			{
				int count = 0;
				if (i<Data.Length && Data[i] !=null)
					count+=this.CopyMapData(Data[i],0,buffer,to,true);
				while (count < 80)
				{
					buffer[to+count] = 0x40;
					count++;
				}
				to+=80;//count;
			}
			//
			// Step 2 - clear trailing spaces on each field
			//
			i=0;
			while (i<to)
			{
				if (ISATTRIB(buffer[i]) && 
					ISENTRY(buffer[i]))
				{
					i++;
					start = i;
					while (i<to)
					{
						if (ISATTRIB(buffer[i]))
							break;
						i++;
					}
					if (i==to)
						break;
					end = i-1;
					while (end >= start)
					{
						if (buffer[end] != 0x40)
							break;
						buffer[end]=0;
						end--;
					}
				}
				else
					i++;
			}
			//
			// Step 3 - return the screen
			//
			byte[] response = new byte[to];
			for (i=0; i<to; i++)
			{
				response[i] = buffer[i];
			}
			return response;
		}

		//
		const int ATTR_PROTECT_BIT       =0x02;
		const int ATTR_3270_PROTECT_BIT  =0x20;
		const int ATTR_3270_NUMONLY      =0x10;

		const int ATTR_BOLD_BIT        =0x08;
		const int ATTR_SELECT_BIT      =0x04;
		const int ATTR_MORE_BIT        =0x10;
		const int ATTR_MDT_BIT         =0x01;

		const int ATTR_NORM            =ATTR_PROTECT_BIT;
		const int ATTR_BOLD            =ATTR_PROTECT_BIT | ATTR_BOLD_BIT;
		const int ATTR_INP             =ATTR_MORE_BIT;
		const int ATTR_INP_BOLD        =ATTR_BOLD_BIT;
		const int ATTR_HIDDEN          =ATTR_PROTECT_BIT | ATTR_BOLD_BIT | ATTR_SELECT_BIT | ATTR_MDT_BIT;
		const int ATTR_PASSWORD        =ATTR_SELECT_BIT | ATTR_BOLD_BIT;

		const int ATTR_3270_NORM       =0xC0 | ATTR_3270_PROTECT_BIT | ATTR_3270_NUMONLY; // make it autoskip
		const int ATTR_3270_BOLD       =0xC0 | ATTR_3270_PROTECT_BIT | ATTR_BOLD_BIT | ATTR_3270_NUMONLY ;
		const int ATTR_3270_INPUT      =0xC0;
		const int ATTR_3270_INPUT_BOLD =     0xC0 | ATTR_BOLD_BIT;
		const int ATTR_3270_HIDDEN     =ATTR_3270_PROTECT_BIT | ATTR_BOLD_BIT | ATTR_SELECT_BIT | ATTR_MDT_BIT;
		const int ATTR_3270_PASSWORD   =0xC0 | ATTR_PASSWORD;

		private bool ISATTRIB(byte c) 
		{
			if (c !=0 && c < 0x20)
				return true;
			else
				return false;
			//return ((c && (c < 0x20))!=0);
		}
		private bool ISENTRY(byte c)
		{
			return (0!=(c & ATTR_PROTECT_BIT));
		}


		//
		private int CopyMapData(string from, int fromIndex, byte[] to, int toIndex, bool fScreen)
			// PCH pchFrom, PCH pchTo, BOOL fScreen, int rows, int columns)
		{
			int toSave = toIndex;
			while (fromIndex < from.Length && from[fromIndex] != 0)
			{
				if (fScreen)
				{
					switch (from[fromIndex])
					{
						case ']': to[toIndex++] = ATTR_NORM; break;
						case '}': to[toIndex++] = ATTR_BOLD; break;
						case '[': to[toIndex++] = ATTR_INP; break;
						case '{': to[toIndex++] = ATTR_INP_BOLD; break;
						case '~': to[toIndex++] = ATTR_HIDDEN; break;
						case '^': to[toIndex++] = ATTR_PASSWORD; break;
						default:
							to[toIndex++] = Open3270.TN3270.Tables.A2E[(byte)from[fromIndex]];
							break;
					}
				}
				else
				{
					switch (from[fromIndex])
					{
						case ']': to[toIndex++] = ATTR_3270_NORM; break;
						case '}': to[toIndex++] = ATTR_3270_BOLD; break;
						case '[': to[toIndex++] = ATTR_3270_INPUT; break;
						case '{': to[toIndex++] = ATTR_3270_INPUT_BOLD; break;
						case '~': to[toIndex++] = ATTR_3270_HIDDEN; break;
						case '^': to[toIndex++] = ATTR_3270_PASSWORD; break;
						default:
							to[toIndex++] = Open3270.TN3270.Tables.A2E[(byte)from[fromIndex]];
							break;
					}
				}
				fromIndex++;
			}
			return toIndex - toSave;
		}

		const byte WCC_CLEARMDT         =0x01;
		const byte WCC_UNLOCK           =0x02;
		const byte WCC_BASE             =0xC0;

		public byte[] AsTN3270Buffer(bool fClear, bool fUnlock, bool TN3270E)
		{
			if (mStringsLive)
			{
				this.Format();
				mStringsLive = false;
			}
			byte[] buffer = new byte[10000];
			int to = 0;
			int from = 0;
			bool fProtected = true;
			byte wcc = WCC_BASE;
			int blankCount = 0;
			int currentOffset = 0;
			//
			//
			if (TN3270E)
			{
				buffer[to++] = 0x07;
				buffer[to++] = 0x00;
				buffer[to++] = 0x00;
				buffer[to++] = 0x00;
				buffer[to++] = 0x00;
			}
			//
			if (fFormatted || fClear)
			{
				if (fClear)
				{
					buffer[to++] = 0x05;
				}
				else
					buffer[to++] = 0x01;

				if (fUnlock)
					wcc |= WCC_UNLOCK;
				//
				buffer[to++] = wcc;
			}
			//
			while (from < mScreenBytes.Length)
			{
				if (ISATTRIB(mScreenBytes[from]))
				{
					if (blankCount > 0)
					{
						to+=FlushBlanks(buffer,to,blankCount,currentOffset);
						blankCount = 0;
					}
					buffer[to++] = Open3270.TN3270.See.ORDER_SF;
					switch (mScreenBytes[from])
					{
						case ATTR_NORM:
							buffer[to++]=(byte) ATTR_3270_NORM;
							fProtected=true;
							break;
						case ATTR_BOLD:
							buffer[to++]=(byte) ATTR_3270_BOLD;
							fProtected=true;
							break;
						case ATTR_HIDDEN:
							buffer[to++]=(byte) ATTR_3270_HIDDEN;
							fProtected=true;
							break;
						case ATTR_INP:
							buffer[to++]=(byte) ATTR_3270_INPUT;
							fProtected=false;
							break;
						case ATTR_INP_BOLD:
							buffer[to++]=(byte) ATTR_3270_INPUT_BOLD;
							fProtected=false;
							break;
						case ATTR_PASSWORD:
							buffer[to++]=(byte) ATTR_3270_PASSWORD;
							fProtected=false;
							break;
					}
				}
				else if (fProtected && (mScreenBytes[from] ==0 || mScreenBytes[from]==0x40))
				{
					blankCount++;
				}
				else
				{
					if (blankCount > 0)
					{
						to+=FlushBlanks(buffer,to,blankCount, currentOffset);
						blankCount=0;
					}
					buffer[to++] = mScreenBytes[from];
				}
				from++;
				currentOffset++;
			}
			//
			// Now send formatted formatted
			//
			if (fFormatted)
			{
				buffer[to++]=Open3270.TN3270.See.ORDER_SBA;
				to+=Create12BitAddress(buffer, to, currentCursorPosition);
				buffer[to++]=Open3270.TN3270.See.ORDER_IC;
			}
			//
			// End of buffer
			//
			buffer[to++]=(byte) 0xFF;
			buffer[to++]=(byte) 0xEF;
			//
			// ok - length of buffer is "to", send the buffer
			//
			byte[] ret = new byte[to];
			for (int i=0; i<to; i++)
			{
				ret[i]=buffer[i];
			}
			return ret;
		}
		private int FlushBlanks(byte[] data, int to, int count, int currentOffset)
		{
			int offset = 0;
			if (count<5)
			{
				while (count-- > 0)
				{
					data[to+offset] = 0x40;
					offset++;
				}
			}
			else
			{
				data[to+offset] = Open3270.TN3270.See.ORDER_RA;
				offset++;
				offset += this.Create12BitAddress(data, to+offset, currentOffset);
				data[to+offset] = 0x00;
				offset++;
			}
			return offset;
		}

		static byte[] inboundAddrChars = new byte[] 
		{
			0x40, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
			0x50, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F,
			0x60, 0x61, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
			0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F
		};

		private int Create12BitAddress(byte[] data, int to, int Address)
		{
			data[to++] = inboundAddrChars[Address >> 6]; 
			data[to++] = inboundAddrChars[Address & 0x003F];    // xxxx xxxx xx11 1111
			return 2;
		}
		private enum TNS
		{
			DO_AID, DO_CURADDR1, DO_CURADDR2, DO_FIRST, DO_DATA, DO_SBA1, DO_SBA2, DO_IAC
		}

		private int BUFADDR(byte[] data, int offset) 
		{
			return ((data[offset] & 0x3f) << 6) + (data[offset+1] & 0x3f);
		}


		public byte lastAid = 0;
		
		public string HandleTN3270Data(byte[] data, int length)
		{
			TNS state = TNS.DO_AID;
			byte[] stateData = new byte[8];
			bool fInField = false;
			
			int offset = 0;
			int currentScreenOffset = 0;
			if (this.inExtendedMode)
			{
				offset+=5;
			}
			if (!fFormatted)
			{
				switch (data[offset]) 
				{
					case 0x6d:
					case 0x7d:
						state=TNS.DO_AID;
						break;
					default:
						lastAid=0x7d;
						state=TNS.DO_DATA;
						break;
				}
			}
			while (offset<length)
			{
				switch (state) 
				{
					case TNS.DO_AID:
						lastAid=data[offset];
						state=TNS.DO_CURADDR1;
						break;
					case TNS.DO_CURADDR1:
						stateData[0]=data[offset];
						state=TNS.DO_CURADDR2;
						break;
					case TNS.DO_CURADDR2:
						stateData[1]=data[offset];
						this.currentCursorPosition = BUFADDR(stateData, 0);
						if (fFormatted)
							state=TNS.DO_FIRST;
						else
							state=TNS.DO_DATA;
						break;
					case TNS.DO_FIRST:
						if (data[offset]==0x11)
							state=TNS.DO_SBA1;
						else if (data[offset]==0xFF)
							state=TNS.DO_IAC;
						else
						{
							Console.WriteLine("TNS.DO_FIRST = {0:X2}", data[offset]);
							throw new ApplicationException("Bad formatted screen response!");
							//return null;
						}
						break;
         
					case TNS.DO_DATA:
						if (data[offset]==0x11)
						{
							while (!ISATTRIB(mScreenBytes[currentScreenOffset]) &&
								(currentScreenOffset < mScreenBytes.Length))
								mScreenBytes[currentScreenOffset++]=0;
                   
							state=TNS.DO_SBA1;
						}
						else if (data[offset]==0xFF)
						{
							state=TNS.DO_IAC;
						}
						else
							mScreenBytes[currentScreenOffset++]=data[offset];
						break;
					case TNS.DO_SBA1:
						stateData[0]=data[offset];
						state=TNS.DO_SBA2;
						break;
					case TNS.DO_SBA2:
						stateData[1]=data[offset];
						currentScreenOffset=BUFADDR(stateData, 0);
						state=TNS.DO_DATA;
						fInField=true;
						break;
					case TNS.DO_IAC:
						if (data[offset]==0xEF)
						{
							if (fInField)
							{
								while ((currentScreenOffset < mScreenBytes.Length) && 
									!ISATTRIB(mScreenBytes[currentScreenOffset]))
								{
									mScreenBytes[currentScreenOffset++]=0;
								}
							}                   
							return AidToText(lastAid);
						}
						else
						{
							state=TNS.DO_DATA;
							mScreenBytes[currentScreenOffset++]=data[offset];
						}
						break;
				

				}
				offset++;
			}
			return AidToText(lastAid);
		}
		//
		/* Key mnemonic translations */
		byte GetAid(string AidData)
		{
			byte AidKey=0;
			int offset = 1;
  
			switch (AidData[offset])
			{
				case 'A':
					if (AidData[offset+1] != '@')
						return AidKey;
				switch (AidData[offset+2])
				{
					case 'H':
						AidKey=0x30;
						break;
					case 'Q':
						AidKey=0x2D;    // ATTN
						break;
					case 'J':
						AidKey=0x3D;
						break;
					case 'C':
						AidKey=0x2A;
						break;
					case '<':
						AidKey=0x3D;   // record backspace
						break;
					default:
						return AidKey;
				}
					AidData+=2;
					break;

				case 'E':  // Enter
					AidKey=0x27;
					break;
				case 'C':  // Clear
					AidKey=0x5F;
					break;
				case 'H':  // Help
					AidKey=0x2b;
					break;
				case 'P':  // Print
					AidKey=0x2f;
					break;
				case '1':  // PF1
					AidKey=0x31;
					break;
				case '2':  // PF2
					AidKey=0x32;
					break;
				case '3':  // PF3
					AidKey=0x33;
					break;
				case '4':  // PF4
					AidKey=0x34;
					break;
				case '5':  // PF5
					AidKey=0x35;
					break;
				case '6':  // PF6
					AidKey=0x36;
					break;
				case '7':  // PF7
					AidKey=0x37;
					break;
				case '8':  // PF8
					AidKey=0x38;
					break;
				case '9':  // PF9
					AidKey=0x39;
					break;
				case 'a':  // PF10
					AidKey=0x3A;
					break;
				case 'b':  // PF11
					AidKey=0x23;
					break;
				case 'c':  // PF12
					AidKey=0x40;
					break;
				case 'd':  // PF13
					AidKey=0x41;
					break;
				case 'e':  // PF14
					AidKey=0x42;
					break;
				case 'f':  // PF15
					AidKey=0x43;
					break;
				case 'g':  // PF16
					AidKey=0x44;
					break;
				case 'h':  // PF17
					AidKey=0x45;
					break;
				case 'i':  // PF18
					AidKey=0x46;
					break;
				case 'j':  // PF19
					AidKey=0x47;
					break;
				case 'k':  // PF20
					AidKey=0x48;
					break;
				case 'l':  // PF21
					AidKey=0x49;
					break;
				case 'm':  // PF22
					AidKey=0x5B;
					break;
				case 'n':  // PF23
					AidKey=0x2E;
					break;
				case 'o':  // PF24
					AidKey=0x3C;
					break;
				case 'u':  // PgUp
					AidKey=0x25;
					break;
				case 'v':  // PgDown
					AidKey=0x3e;
					break;
				case 'x':  // PA1
					AidKey=0x25;
					break;
				case 'y':  // PA2
					AidKey=0x3E;
					break;
				case 'z':  // PA3
					AidKey=0x2C;
					break;
				default:
					break;
      
			}  // end of switch
			return (Open3270.TN3270.Tables.A2E[AidKey]);
		}

		//
		Hashtable hashTextToAid = null;
		Hashtable hashAidToText = null;
		void initAidTable()
		{
			hashTextToAid = new Hashtable();
			hashTextToAid["tab"] = "@T";
			hashTextToAid["enter"] = "@E";
			hashTextToAid["clear"] = "@C";
			hashTextToAid["home"] = "@0";
			hashTextToAid["erase eof"] = "@F";
			hashTextToAid["eraseeof"] = "@F";
			hashTextToAid["pf1"] = "@1";
			hashTextToAid["pf2"] = "@2";
			hashTextToAid["pf3"] = "@3";
			hashTextToAid["pf4"] = "@4";
			hashTextToAid["pf5"] = "@5";
			hashTextToAid["pf6"] = "@6";
			hashTextToAid["pf7"] = "@7";
			hashTextToAid["pf8"] = "@8";
			hashTextToAid["pf9"] = "@9";
			hashTextToAid["pf10"] = "@a";
			hashTextToAid["pf11"] = "@b";
			hashTextToAid["pf12"] = "@c";
			hashTextToAid["pf13"] = "@d";
			hashTextToAid["pf14"] = "@e";
			hashTextToAid["pf15"] = "@f";
			hashTextToAid["pf16"] = "@g";
			hashTextToAid["pf17"] = "@h";
			hashTextToAid["pf18"] = "@i";
			hashTextToAid["pf19"] = "@j";
			hashTextToAid["pf20"] = "@k";
			hashTextToAid["pf21"] = "@l";
			hashTextToAid["pf22"] = "@m";
			hashTextToAid["pf23"] = "@n";
			hashTextToAid["pf24"] = "@o";
			hashTextToAid["left tab"] = "@B";
			hashTextToAid["back tab"] = "@B";
			hashTextToAid["lefttab"] = "@B";
			hashTextToAid["backtab"] = "@B";
			hashTextToAid["put"] =  "@S@p";//       // [put](F_row_col, "text";
			hashTextToAid["c_pos"] =  "@S@c";//     // [c_pos](offset;
			hashTextToAid["buffers"] =  "@S@d";//   // [buffers](hostwritesexpected;
			hashTextToAid["sleep"] =  "@S@e";//     // [sleep](milliseconds;
			hashTextToAid["settle"] =  "@S@f";//    // [settle](settletime;
			hashTextToAid["delete"] = "@D";
			hashTextToAid["help"] = "@H";
			hashTextToAid["insert"] = "@I";
			hashTextToAid["left"] = "@L";
			hashTextToAid["new line"] = "@N";
			hashTextToAid["newline"] = "@N";
			hashTextToAid["space"] = "@O";
			hashTextToAid["print"] = "@P";
			hashTextToAid["reset"] = "@R";
			hashTextToAid["up"] = "@U";
			hashTextToAid["down"] = "@V";
			hashTextToAid["right"] = "@Z";
			hashTextToAid["plus"] = "@p";
			hashTextToAid["end"] = "@q";
			hashTextToAid["page up"] = "@u";
			hashTextToAid["page down"] = "@v";
			hashTextToAid["pageup"] = "@u";
			hashTextToAid["pagedown"] = "@v";
			hashTextToAid["recordback"] =  "@A@<";
			hashTextToAid["recbksp"] =  "@A@<";
			hashTextToAid["pa1"] = "@x";
			hashTextToAid["pa2"] = "@y";
			hashTextToAid["pa3"] = "@z";
			hashTextToAid["word delete"] = "@A@D";
			hashTextToAid["field exit"] = "@A@E";
			hashTextToAid["erase input"] = "@A@F";
			hashTextToAid["worddelete"] = "@A@D";
			hashTextToAid["fieldexit"] = "@A@E";
			hashTextToAid["eraseinput"] = "@A@F";
			hashTextToAid["sysreq"] = "@A@H";
			hashTextToAid["insert"] = "@A@I";
			hashTextToAid["cur select"] = "@A@J";
			hashTextToAid["curselect"] = "@A@J";
			hashTextToAid["attn"] = "@A@Q";
			hashTextToAid["printps"] = "@A@T";
			hashTextToAid["erase eol"] = "@S@A";
			hashTextToAid["eraseeol"] = "@S@A";
			hashTextToAid["test"] =  "@A@C";

			hashAidToText = new Hashtable();
			foreach (DictionaryEntry de in hashTextToAid)
			{
				string v = (string)de.Value;
				byte vCode = GetAid(v);
				hashAidToText[vCode] = (string)de.Key;
			}
		}
		public string AidToText(byte aid)
		{
			if (hashTextToAid == null)
			{
				initAidTable();
			}
			return (string)hashAidToText[aid];
		}
		public byte TextToAid(string pszText)
		{
			if (hashTextToAid == null)
			{
				initAidTable();
			}
			

			string aidKey = hashTextToAid[pszText.ToLower()] as string;
			if (aidKey==null)
				return 0;

			//
			return GetAid(aidKey);
		}
		public void ClearField(int x, int y, bool multiline)
		{
			int position;
			int end;
			if (multiline)
				FindField(ToCursorPosition(x,y), 80*24, out position, out end);
			else
				FindField(ToCursorPosition(x,y), ToCursorPosition(79,y), out position, out end);
			
			WriteScreen(position, end, true, null);
		}
		public void WriteField(int x, int y, bool direct, string text)
		{
			WriteField(ToCursorPosition(x,y),direct,text);
		}
		public void WriteField(int position, bool direct,string text)
		{
			int end;
			FindField(position, 80*24, out position, out end);
			WriteScreen(position, end, direct, text);
		}
		public string ReadField(int x, int y)
		{
			return ReadField(ToCursorPosition(x,y));
		}
		public string ReadField(int position)
		{
			//Console.WriteLine("position is "+position);
			int end;
			FindField(position, 80*24, out position, out end);
			//
			//Console.WriteLine("start is "+position+", end is "+end);
			return ReadScreen(position, end);
		}
		public string ReadScreen(int position, int end)
		{
			int i;
			string text = "";
			for (i=position; i<end; i++)
			{
				text += System.Convert.ToChar(Open3270.TN3270.Tables.E2A[mScreenBytes[i]]);

			}
			return text.TrimEnd();
		}
		public void WriteFormattedData(int x, int y, string text)
		{
			int position = this.ToCursorPosition(x,y);
			CopyMapData(text,0,mScreenBytes,position,true);

		}
		public void WriteScreen(int position, int end, bool direct, string text)
		{
			//Console.WriteLine("position = "+position+", end ="+end);
			if (direct)
			{
				int i;
				for (i=position; i<end; i++)
				{
					char ch;
					if (text != null && i-position<text.Length)
						ch = text[i-position];
					else
						ch = ' ';
					byte b = Open3270.TN3270.Tables.A2E[ch];
					mScreenBytes[i] = b;
				}
			}
			else
			{
				if (text==null)
					text="";
				while (text.Length < (end-position))
					text = text+" ";

				CopyMapData(text,0,mScreenBytes,position,true);
			}
		}
		public void FindField(int startposition, int max, out int position, out int end)
		{
			position = startposition;
			while (position < max && ISATTRIB(mScreenBytes[position]))
				position++;
			end = position;
			while (end < max && !ISATTRIB(mScreenBytes[end]))
				end++;
		}
		public int ToCursorPosition(int x, int y)
		{
			return x+y*80;
		}
	}
}
