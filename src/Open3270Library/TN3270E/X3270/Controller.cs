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
	internal class Controller : IDisposable
	{

		#region Fields

		byte[] screenBuffer;
		byte[] crmAttributes;
		byte[] aScreenBuffer = null;


		byte attentionID = AID.None;
		byte defaultCs = 0;
		byte defaultFg = 0;
		byte defaultGr = 0;
		byte replyMode = 0;
		byte fakeFA = 0;


		bool is3270 = false;
		bool isAltBuffer = false;
		bool screenAlt = true;
		bool isFormatted = false;
		bool tracePrimed = false;
		bool traceSkipping = false;
		bool screenChanged = false;
		bool debuggingFont = false;


		int cursorAddress = 0;
		int bufferAddress = 0;
		int currentFaIndex = 0;
		int maxColumns = 80;
		int maxRows = 25;
		int modelNumber;
		int crmnAttribute;
		int rowCount = 25;
		int columnCount = 80;
		int sscpStart = 0;
		int firstChanged = 0;
		int lastChanged = 0;
		int dataAvailableCount = 0;
		long startTime = 0;


		string modelName = null;


		ExtendedAttribute[] extendedAttributes;
		ExtendedAttribute[] aExtendedAttributeBuffer = null;
		ExtendedAttribute[] extendedAttributesZeroBuffer = null;


		Telnet telnet;
		TNTrace trace;
		Appres appres;
		StructuredField sf;
		
		PreviousEnum previous = PreviousEnum.None;

		//For synchonization only
		object dataAvailablePadlock = new object();

		#endregion Fields



		#region Properties


		public bool Formatted
		{
			get { return isFormatted; }
			set { isFormatted = value; }
		}


		public byte[] CrmAttributes
		{
			get { return crmAttributes; }
			set { crmAttributes = value; }
		}

		public int CrmnAttribute
		{
			get { return crmnAttribute; }
			set { crmnAttribute = value; }
		}

		public byte FakeFA
		{
			get { return fakeFA; }
			set { fakeFA = value; }
		}

		public int RowCount
		{
			get { return rowCount; }
			set { rowCount = value; }
		}

		public int ColumnCount
		{
			get { return columnCount; }
			set { Console.WriteLine("SET COLUMN COUNT="+value);columnCount = value; }
		}

		public bool Is3270
		{
			get { return is3270; }
			set { is3270 = value; }
		}
		public int MaxColumns
		{
			get { return maxColumns; }
			set { maxColumns = value; }
		}
		public int MaxRows
		{
			get { return maxRows; }
			set { maxRows = value; }
		}

		public byte AttentionID
		{
			get { return attentionID; }
			set { attentionID = value; }
		}

		public int BufferAddress
		{
			get { return bufferAddress; }
			set { bufferAddress = value; }
		}

		public int CursorAddress
		{
			get { return cursorAddress; }
			set { cursorAddress = value; }
		}
		public byte[] ScreenBuffer
		{
			get { return screenBuffer; }
			set { screenBuffer = value; }
		}

		public bool IsAltBuffer
		{
			get { return isAltBuffer; }
			set { isAltBuffer = value; }
		}

		public bool ScreenAlt
		{
			get { return screenAlt; }
			set { screenAlt = value; }
		}

		public bool ScreenChanged
		{
			get { return screenChanged; }
		}

		public int DataAvailableCount
		{
			get
			{
				lock (dataAvailablePadlock)
				{
					return dataAvailableCount;
				}
			}
		}

		#endregion Properties



		#region Calculated properties

		public bool IsBlank(byte c)
		{
			return ((c == CharacterGenerator.Null) || (c == CharacterGenerator.Space));
		}


		/// <summary>
		/// Tell me if there is any data on the screen.
		/// </summary>
		bool StreamHasData
		{
			get
			{
				int c = 0;
				int i;
				byte oc;

				for (i = 0; i < this.rowCount * this.columnCount; i++)
				{
					oc = this.screenBuffer[c++];
					if (!FieldAttribute.IsFA(oc) && !IsBlank(oc))
					{
						return true;
					}
				}
				return false;
			}
		}


		#endregion Calculated properties





		#region Ctors, dtors, and clean-up

		internal Controller(Telnet tn, Appres appres)
		{
			this.sf = new StructuredField(tn);
			crmAttributes = new byte[16];
			crmnAttribute = 0;
			this.telnet = tn;
			this.trace = tn.Trace;
			this.appres = appres;
			startTime = DateTime.Now.Ticks;
		}

		#region IDisposable Members

		~Controller()
		{
			Dispose(false);
		}

		bool isDisposed = false;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (isDisposed)
			{
				return;
			}
			isDisposed = true;

			if (disposing)
			{
				for (int i = 0; i < extendedAttributes.Length; i++)
					extendedAttributes[i] = null;
				for (int i = 0; i < aExtendedAttributeBuffer.Length; i++)
					aExtendedAttributeBuffer[i] = null;
				for (int i = 0; i < extendedAttributesZeroBuffer.Length; i++)
					extendedAttributesZeroBuffer[i] = null;

				this.telnet.ConnectionPending -= telnet_ConnectionPending;
				this.telnet.PrimaryConnectionChanged -= telnet_PrimaryConnectionChanged;
				this.telnet.Connected3270 -= telnet_Connected3270;
			}
		}



		#endregion

		#endregion Ctors, dtors, and clean-up





		#region Eventhandlers


		void telnet_PrimaryConnectionChanged(object sender, PrimaryConnectionChangedArgs e)
		{
			this.ReactToConnectionChange(e.Success);
		}


		void telnet_ConnectionPending(object sender, EventArgs e)
		{
			//Not doing anything here, yet.
		}


		void telnet_Connected3270(object sender, Connected3270EventArgs e)
		{
			this.ReactToConnectionChange(e.Is3270);
		}

		#endregion Eventhandlers








		private void OnAllChanged()
		{
			screenChanged = true;
			if (telnet.IsAnsi)
			{
				firstChanged = 0;
				lastChanged = rowCount * columnCount;
			}
		}


		private void OnRegionChanged(int f, int l)
		{
			screenChanged = true;
			if (telnet.IsAnsi)
			{
				if (firstChanged == -1 || f < firstChanged) firstChanged = f;
				if (lastChanged == -1 || l > lastChanged) lastChanged = l;
			}
		}


		private void OnOneChanged(int n)
		{
			OnRegionChanged(n, n + 1);
		}



		/// <summary>
		/// Initialize the emulated 3270 hardware.
		/// </summary>
		/// <param name="cmask"></param>
		public void Initialize(int cmask)
		{
			//Register callback routines.
			this.telnet.ConnectionPending += telnet_ConnectionPending;
			this.telnet.PrimaryConnectionChanged += telnet_PrimaryConnectionChanged;
			this.telnet.Connected3270 += telnet_Connected3270;
		}


		/// <summary>
		/// Handles connection state changes, e.g. initial connection, disconnection (or does it?), and connecting with 3270
		/// </summary>
		/// <param name="success">Parameter is currently ignored</param>
		private void ReactToConnectionChange(bool success)
		{
			if (is3270)
			{
				//Console.WriteLine("--ever_3270 is true, set fake_fa to 0xe0 - unprotected");
				fakeFA = 0xE0;
			}
			else
			{
				//Console.WriteLine("--ever_3270 is false, set fake_fa to 0xc4 - protected");
				fakeFA = 0xC4;
			}
			if (!telnet.Is3270 || (telnet.IsSscp && ((telnet.Keyboard.keyboardLock & KeyboardConstants.OiaTWait) != 0)))
			{
				telnet.Keyboard.KeyboardLockClear(KeyboardConstants.OiaTWait, "ctlr_connect");
				//status_reset();
			}

			defaultFg = 0x00;
			defaultGr = 0x00;
			defaultCs = 0x00;
			replyMode = ControllerConstant.SF_SRM_FIELD;
			crmnAttribute = 0;
		}



		/// <summary>
		/// Reinitialize the emulated 3270 hardware.
		/// </summary>
		/// <param name="cmask"></param>
		public void Reinitialize(int cmask)
		{
			if ((cmask & ControllerConstant.MODEL_CHANGE) != 0)
			{
				//Allocate buffers
				screenBuffer = new byte[maxRows * maxColumns];
				extendedAttributes = new ExtendedAttribute[maxRows * maxColumns];
				aScreenBuffer = new byte[maxRows * maxColumns];
				aExtendedAttributeBuffer = new ExtendedAttribute[maxRows * maxColumns];
				extendedAttributesZeroBuffer = new ExtendedAttribute[maxRows * maxColumns];
				int i;
				for (i = 0; i < maxRows * maxColumns; i++)
				{
					extendedAttributes[i] = new ExtendedAttribute();
					aExtendedAttributeBuffer[i] = new ExtendedAttribute();
					extendedAttributesZeroBuffer[i] = new ExtendedAttribute();
				}

				cursorAddress = 0;
				bufferAddress = 0;
			}
		}


		/// <summary>
		/// Deal with the relationships between model numbers and rows/cols.
		/// </summary>
		/// <param name="mn"></param>
		/// <param name="ovc"></param>
		/// <param name="ovr"></param>
		void SetRowsAndColumns(int mn, int ovc, int ovr)
		{
			int defmod;

			switch (mn)
			{
				case 2:
					{
						maxColumns = columnCount = 80;
						maxRows = rowCount = 24;
						modelNumber = 2;
						break;
					}
				case 3:
					{
						maxColumns = columnCount = 80;
						maxRows = rowCount = 32;
						modelNumber = 3;
						break;
					}
				case 4:
					{
						maxColumns = columnCount = 80;
						maxRows = rowCount = 43;
						modelNumber = 4;
						break;
					}
				case 5:
					{
						maxColumns = columnCount = 132;
						maxRows = rowCount = 27;
						modelNumber = 5;
						break;
					}
				default:
					{
						defmod = 4;
						telnet.Events.ShowError("Unknown model: %d\nDefaulting to %d", mn, defmod);
						this.SetRowsAndColumns(defmod, ovc, ovr);
						return;
					}
			}


			if (ovc != 0 || ovr != 0)
			{
				throw new ApplicationException("oops - oversize");
			}

			/* Update the model name. */
			modelName = "327" + (appres.m3279 ? "9" : "8") + "-" + modelNumber + (appres.extended ? "-E" : "");
			Reinitialize(255);// mfwHACK
		}



		//Set the formatted screen flag.  
		//A formatted screen is a screen thathas at least one field somewhere on it.
		void SetFormattedFlag()
		{
			int baddr;

			isFormatted = false;
			baddr = 0;
			do
			{
				if (FieldAttribute.IsFA(screenBuffer[baddr]))
				{
					isFormatted = true;
					break;
				}
				IncrementAddress(ref baddr);
			}
			while (baddr != 0);
		}


		/// <summary>
		/// Find the field attribute for the given buffer address.  Return its address rather than its value.
		/// </summary>
		/// <param name="baddr"></param>
		/// <returns></returns>
		public int GetFieldAttribute(int baddr)
		{
			int sbaddr;

			if (!isFormatted)
			{
				//Console.WriteLine("get_field_attribute on unformatted screen returns -1");
				return -1;// **BUG** //&fake_fa;
			}

			sbaddr = baddr;
			do
			{
				if (FieldAttribute.IsFA(screenBuffer[baddr]))
					return baddr;//&(screen_buf[baddr]);
				DecrementAddress(ref baddr);
			}
			while (baddr != sbaddr);
			return -1;// **BUG** &fake_fa;
		}



		/// <summary>
		/// Find the field attribute for the given buffer address, bounded by another buffer address.
		/// Return the attribute in a parameter.
		/// </summary>
		/// <param name="bAddr"></param>
		/// <param name="bound"></param>
		/// <param name="faOutIndex"></param>
		/// <returns>Returns true if an attribute is found, false if boundary hit.</returns>
		bool GetBoundedFieldAttribute(int bAddr, int bound, ref int faOutIndex)
		{
			int sbaddr;

			if (!isFormatted)
			{
				faOutIndex = -1;
				return true;
			}

			sbaddr = bAddr;
			do
			{
				if (FieldAttribute.IsFA(screenBuffer[bAddr]))
				{
					faOutIndex = bAddr;
					return true;
				}
				DecrementAddress(ref bAddr);
			}
			while (bAddr != sbaddr && bAddr != bound);

			//Screen is unformatted (and 'formatted' is inaccurate).
			if (bAddr == sbaddr)
			{
				faOutIndex = -1;
				return true;
			}

			// Wrapped to boundary
			return false;
		}


		/// <summary>
		/// Find the next unprotected field.  Returns the address following the unprotected attribute byte,
		/// or 0 if no nonzero-width unprotected field can be found.
		/// </summary>
		/// <param name="fromAddress"></param>
		/// <returns></returns>
		public int GetNextUnprotectedField(int fromAddress)
		{
			int baddr, nbaddr;

			nbaddr = fromAddress;
			do
			{
				baddr = nbaddr;
				IncrementAddress(ref nbaddr);
				if (FieldAttribute.IsFA(screenBuffer[baddr]) &&
					!FieldAttribute.IsProtected(screenBuffer[baddr]) &&
					!FieldAttribute.IsFA(screenBuffer[nbaddr]))
				{
					return nbaddr;
				}
			} while (nbaddr != fromAddress);
			return 0;
		}


		/// <summary>
		/// Perform an erase command, which may include changing the (virtual) screen size.
		/// </summary>
		/// <param name="alt"></param>
		public void Erase(bool alt)
		{
			this.telnet.Keyboard.ToggleEnterInhibitMode(false);

			Clear(true);

			if (alt == screenAlt)
			{
				return;
			}

			if (alt)
			{
				// Going from 24x80 to maximum. 
				// screen_disp(false);
				rowCount = maxRows;
				columnCount = maxColumns;
			}
			else
			{
				// Going from maximum to 24x80. 
				/*
				if (maxRows > 24 || maxColumns > 80)
				{
					if (debuggingFont)
					{
						BlankOutScreen();
						//	screen_disp(false);
					}
					rowCount = 24;
					columnCount = 80;
				}

*/
				rowCount = 24;
				columnCount = 80;
			}

			screenAlt = alt;
		}


		/// <summary>
		/// Interpret an incoming 3270 command.
		/// </summary>
		/// <param name="buf"></param>
		/// <param name="start"></param>
		/// <param name="bufferLength"></param>
		/// <returns></returns>
		public PDS ProcessDS(byte[] buf, int start, int bufferLength)
		{
			if (buf.Length == 0 || bufferLength == 0)
			{
				return PDS.OkayNoOutput;
			}

			trace.trace_ds("< ");


			// Handle 3270 command
			if (buf[start] == ControllerConstant.CMD_EAU || buf[start] == ControllerConstant.SNA_CMD_EAU)
			{
				//Erase all unprotected
				trace.trace_ds("EraseAllUnprotected\n");
				ProcessEraseAllUnprotectedCommand();
				return PDS.OkayNoOutput;
			}
			else if (buf[start] == ControllerConstant.CMD_EWA || buf[start] == ControllerConstant.SNA_CMD_EWA)
			{
				//Erase/write alternate
				trace.trace_ds("EraseWriteAlternate\n");
				Erase(true);
				ProcessWriteCommand(buf, start, bufferLength, true);
				return PDS.OkayNoOutput;

			}
			else if (buf[start] == ControllerConstant.CMD_EW || buf[start] == ControllerConstant.SNA_CMD_EW)
			{
				//Erase/write
				trace.trace_ds("EraseWrite\n");
				Erase(false);
				ProcessWriteCommand(buf, start, bufferLength, true);
				return PDS.OkayNoOutput;
			}
			else if (buf[start] == ControllerConstant.CMD_W || buf[start] == ControllerConstant.SNA_CMD_W)
			{
				//Write
				trace.trace_ds("Write\n");
				ProcessWriteCommand(buf, start, bufferLength, false);
				return PDS.OkayNoOutput;

			}
			else if (buf[start] == ControllerConstant.CMD_RB || buf[start] == ControllerConstant.SNA_CMD_RB)
			{
				//Read buffer
				trace.trace_ds("ReadBuffer\n");
				ProcessReadBufferCommand(attentionID);
				return PDS.OkayOutput;

			}
			else if (buf[start] == ControllerConstant.CMD_RM || buf[start] == ControllerConstant.SNA_CMD_RM)
			{
				//Read modifed
				trace.trace_ds("ReadModified\n");
				ProcessReadModifiedCommand(attentionID, false);
				return PDS.OkayOutput;

			}
			else if (buf[start] == ControllerConstant.CMD_RMA || buf[start] == ControllerConstant.SNA_CMD_RMA)
			{
				//Read modifed all
				trace.trace_ds("ReadModifiedAll\n");
				ProcessReadModifiedCommand(attentionID, true);
				return PDS.OkayOutput;
			}
			else if (buf[start] == ControllerConstant.CMD_WSF || buf[start] == ControllerConstant.SNA_CMD_WSF)
			{
				//Write structured field
				trace.trace_ds("WriteStructuredField");
				return sf.WriteStructuredField(buf, start, bufferLength /*buflen*/);
			}
			else if (buf[start] == ControllerConstant.CMD_EWA)
			{
				//No-op
				trace.trace_ds("NoOp\n");
				return PDS.OkayNoOutput;
			}
			else
			{
				//Unknown 3270 command
				telnet.Events.ShowError("Unknown 3270 Data Stream command: 0x%X\n", buf[start]);
				return PDS.BadCommand;
			}
		}



		/// <summary>
		/// Functions to insert SA attributes into the inbound data stream.
		/// </summary>
		/// <param name="obptr"></param>
		/// <param name="attr"></param>
		/// <param name="vValue"></param>
		/// <param name="currentp"></param>
		/// <param name="anyp"></param>
		void InsertSaAttribtutes(NetBuffer obptr, byte attr, byte vValue, ref byte currentp, ref bool anyp)
		{
			if (vValue != currentp)
			{
				currentp = vValue;
				obptr.Add(ControllerConstant.ORDER_SA);
				obptr.Add(attr);
				obptr.Add(vValue);
				if (anyp)
				{
					trace.trace_ds("'");
				}
				trace.trace_ds(" SetAttribute(%s)", See.GetEfa(attr, vValue));
				anyp = false;
			}
		}

		void InsertSaAttribtutes(NetBuffer obptr, int baddr, ref byte current_fgp, ref byte current_grp, ref byte current_csp, ref bool anyp)
		{
			if (replyMode == ControllerConstant.SF_SRM_CHAR)
			{
				int i;
				bool foundXAForeground = false;
				bool foundXAHighlighting = false;
				bool foundXACharset = false;
				for (i = 0; i < crmnAttribute; i++)
				{
					if (crmAttributes[i] == See.XA_FOREGROUND)
					{
						foundXAForeground = true;
					}
					if (crmAttributes[i] == See.XA_HIGHLIGHTING)
					{
						foundXAHighlighting = true;
					}
					if (crmAttributes[i] == See.XA_CHARSET)
					{
						foundXACharset = true;
					}
				}

				if (foundXAForeground)
				{
					this.InsertSaAttribtutes(obptr, See.XA_FOREGROUND, extendedAttributes[baddr].fg, ref current_fgp, ref anyp);
				}

				if (foundXAHighlighting)
				{
					byte gr;

					gr = extendedAttributes[baddr].gr;
					if (gr != 0)
					{
						gr |= 0xf0;
					}
					this.InsertSaAttribtutes(obptr, See.XA_HIGHLIGHTING, gr, ref current_grp, ref anyp);
				}

				if (foundXACharset)
				{
					byte cs;

					cs = (byte)(extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK);
					if (cs != 0)
					{
						cs |= 0xf0;
					}
					this.InsertSaAttribtutes(obptr, See.XA_CHARSET, cs, ref current_csp, ref anyp);
				}
			}
		}


		/// <summary>
		/// Process a 3270 Read-Modified command and transmit the data back to the host.
		/// </summary>
		/// <param name="attentionIDbyte"></param>
		/// <param name="all"></param>
		public void ProcessReadModifiedCommand(byte attentionIDbyte, bool all)
		{
			int baddr, sbaddr;
			bool sendData = true;
			bool shortRead = false;
			byte currentFG = 0x00;
			byte currentGR = 0x00;
			byte currentCS = 0x00;

			if (telnet.IsSscp && attentionIDbyte != AID.Enter)
			{
				return;
			}

			trace.trace_ds("> ");
			NetBuffer obptr = new NetBuffer();

			switch (attentionIDbyte)
			{
				//Test request 
				case AID.SysReq:
					{
						//Soh
						obptr.Add(0x01);
						//%
						obptr.Add(0x5b);
						// /
						obptr.Add(0x61);
						//stx
						obptr.Add(0x02);
						trace.trace_ds("SYSREQ");
						break;
					}
				//Short-read AIDs
				case AID.PA1:
				case AID.PA2:
				case AID.PA3:
				case AID.Clear:
					{
						if (!all)
						{
							shortRead = true;
							sendData = false;
						}

						if (!telnet.IsSscp)
						{
							obptr.Add(attentionIDbyte);
							trace.trace_ds(See.GetAidFromCode(attentionIDbyte));
							if (shortRead)
							{
								goto rm_done;
							}
							Util.EncodeBAddress(obptr, cursorAddress);
							trace.trace_ds(trace.rcba(cursorAddress));
						}
						break;
					}
				//No data on READ MODIFIED
				case AID.SELECT:
					{
						if (!all)
						{
							sendData = false;
						}
						
						if (!telnet.IsSscp)
						{
							obptr.Add(attentionIDbyte);
							trace.trace_ds(See.GetAidFromCode(attentionIDbyte));
							if (shortRead)
							{
								goto rm_done;
							}
							Util.EncodeBAddress(obptr, cursorAddress);
							trace.trace_ds(trace.rcba(cursorAddress));
						}
						break;
					}
				default:				/* ordinary AID */
					if (!telnet.IsSscp)
					{
						obptr.Add(attentionIDbyte);
						trace.trace_ds(See.GetAidFromCode(attentionIDbyte));
						if (shortRead)
							goto rm_done;
						Util.EncodeBAddress(obptr, cursorAddress);
						trace.trace_ds(trace.rcba(cursorAddress));
					}
					break;
			}

			baddr = 0;
			if (isFormatted)
			{
				//Find first field attribute
				do
				{
					if (FieldAttribute.IsFA(screenBuffer[baddr]))
					{
						break;
					}
					this.IncrementAddress(ref baddr);
				}
				while (baddr != 0);

				sbaddr = baddr;
				do
				{
					if (FieldAttribute.IsModified(screenBuffer[baddr]))
					{
						bool any = false;

						this.IncrementAddress(ref baddr);
						obptr.Add(ControllerConstant.ORDER_SBA);
						Util.EncodeBAddress(obptr, baddr);
						trace.trace_ds(" SetBufferAddress%s", trace.rcba(baddr));
						while (!FieldAttribute.IsFA(screenBuffer[baddr]))
						{
							if (sendData && screenBuffer[baddr] != 0)
							{
								InsertSaAttribtutes(obptr, baddr,
									ref currentFG,
									ref currentGR,
									ref currentCS,
									ref any);

								if ((extendedAttributes[baddr].cs & ControllerConstant.CS_GE) != 0)
								{
									obptr.Add(ControllerConstant.ORDER_GE);
									if (any)
									{
										trace.trace_ds("'");
									}
									trace.trace_ds(" GraphicEscape");
									any = false;
								}
								obptr.Add(Tables.Cg2Ebc[screenBuffer[baddr]]);
								if (!any)
								{
									trace.trace_ds(" '");
								}
								trace.trace_ds("%s", See.GetEbc(Tables.Cg2Ebc[screenBuffer[baddr]]));
								any = true;
							}
							this.IncrementAddress(ref baddr);
						}
						if (any)
						{
							trace.trace_ds("'");
						}
					}
					else
					{	
						//Not modified - skip 
						do
						{
							this.IncrementAddress(ref baddr);
						} while (!FieldAttribute.IsFA(screenBuffer[baddr]));
					}
				} while (baddr != sbaddr);
			}
			else
			{
				bool any = false;
				int nBytes = 0;

				//If we're in SSCP-LU mode, the starting point is where the host left the cursor.
				if (telnet.IsSscp)
				{
					baddr = sscpStart;
				}

				do
				{
					if (screenBuffer[baddr] != 0)
					{
						InsertSaAttribtutes(obptr, baddr,
							ref currentFG,
							ref currentGR,
							ref currentCS,
							ref any);
						if ((extendedAttributes[baddr].cs & ControllerConstant.CS_GE) != 0)
						{
							obptr.Add(ControllerConstant.ORDER_GE);
							if (any)
							{
								trace.trace_ds("' ");
							}
							trace.trace_ds(" GraphicEscape ");
							any = false;
						}
						obptr.Add(Tables.Cg2Ebc[screenBuffer[baddr]]);
						if (!any)
						{
							trace.trace_ds("'");
						}
						trace.trace_ds(See.GetEbc(Tables.Cg2Ebc[screenBuffer[baddr]]));
						any = true;
						nBytes++;
					}
					this.IncrementAddress(ref baddr);

					//If we're in SSCP-LU mode, end the return value at255 bytes, or where the screen wraps.
					if (telnet.IsSscp && (nBytes >= 255 || baddr == 0))
					{
						break;
					}
				} while (baddr != 0);

				if (any)
				{
					trace.trace_ds("'");
				}
			}

		rm_done:
			trace.trace_ds("\n");
			telnet.Output(obptr);
		}




		/// <summary>
		/// Calculate the proper 3270 DS value for an internal field attribute.
		/// </summary>
		/// <param name="fa"></param>
		/// <returns></returns>
		byte CalculateFA(byte fa)
		{
			byte r = 0x00;

			if (FieldAttribute.IsProtected(fa))
			{
				r |= 0x20;
			}
			if (FieldAttribute.IsNumeric(fa))
			{
				r |= 0x10;
			}
			if (FieldAttribute.IsModified(fa))
			{
				r |= 0x01;
			}

			r |= (byte)((fa & ControllerConstant.FA_INTENSITY) << 2);
			return r;
		}


		/// <summary>
		/// Process a 3270 Read-Buffer command and transmit the data back to the host.
		/// </summary>
		/// <param name="attentionIDbyte"></param>
		public void ProcessReadBufferCommand(byte attentionIDbyte)
		{
			int baddr;
			byte fa;
			bool any = false;
			int attr_count = 0;
			byte currentFG = 0x00;
			byte currentGR = 0x00;
			byte currentCS = 0x00;

			trace.trace_ds("> ");
			NetBuffer obptr = new NetBuffer();
			obptr.Add(attentionIDbyte);
			Util.EncodeBAddress(obptr, cursorAddress);
			trace.trace_ds("%s%s", See.GetAidFromCode(attentionIDbyte), trace.rcba(cursorAddress));

			baddr = 0;
			do
			{
				if (FieldAttribute.IsFA(screenBuffer[baddr]))
				{
					if (replyMode == ControllerConstant.SF_SRM_FIELD)
					{
						obptr.Add(ControllerConstant.ORDER_SF);
					}
					else
					{
						obptr.Add(ControllerConstant.ORDER_SFE);
						attr_count = obptr.Index;
						obptr.Add(1); /* for now */
						obptr.Add(See.XA_3270);
					}

					fa = CalculateFA(screenBuffer[baddr]);
					obptr.Add(ControllerConstant.CodeTable[fa]);

					if (any)
					{
						trace.trace_ds("'");
					}

					trace.trace_ds(" StartField%s%s%s",
						(replyMode == ControllerConstant.SF_SRM_FIELD) ? "" : "Extended",
						trace.rcba(baddr), See.GetSeeAttribute(fa));

					if (replyMode != ControllerConstant.SF_SRM_FIELD)
					{
						if (extendedAttributes[baddr].fg != 0)
						{
							obptr.Add(See.XA_FOREGROUND);
							obptr.Add(extendedAttributes[baddr].fg);
							trace.trace_ds("%s", See.GetEfa(See.XA_FOREGROUND, extendedAttributes[baddr].fg));
							obptr.IncrementAt(attr_count, 1);
						}
						if (extendedAttributes[baddr].gr != 0)
						{
							obptr.Add(See.XA_HIGHLIGHTING);
							obptr.Add(extendedAttributes[baddr].gr | 0xf0);
							trace.trace_ds("%s", See.GetEfa(See.XA_HIGHLIGHTING, (byte)(extendedAttributes[baddr].gr | 0xf0)));
							obptr.IncrementAt(attr_count, 1);
						}
						if ((extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK) != 0)
						{
							obptr.Add(See.XA_CHARSET);
							obptr.Add((extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK) | 0xf0);
							trace.trace_ds("%s", See.GetEfa(See.XA_CHARSET, (byte)((extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK) | 0xf0)));
							obptr.IncrementAt(attr_count, 1);
						}
					}
					any = false;
				}
				else
				{
					InsertSaAttribtutes(obptr, baddr,
						ref currentFG,
						ref currentGR,
						ref currentCS,
						ref any);
					if ((extendedAttributes[baddr].cs & ControllerConstant.CS_GE) != 0)
					{
						obptr.Add(ControllerConstant.ORDER_GE);
						if (any)
						{
							trace.trace_ds("'");
						}
						trace.trace_ds(" GraphicEscape");
						any = false;
					}
					obptr.Add(Tables.Cg2Ebc[screenBuffer[baddr]]);
					if (Tables.Cg2Ebc[screenBuffer[baddr]] <= 0x3f || Tables.Cg2Ebc[screenBuffer[baddr]] == 0xff)
					{
						if (any)
						{
							trace.trace_ds("'");
						}

						trace.trace_ds(" %s", See.GetEbc(Tables.Cg2Ebc[screenBuffer[baddr]]));
						any = false;
					}
					else
					{
						if (!any)
							trace.trace_ds(" '");
						trace.trace_ds("%s", See.GetEbc(Tables.Cg2Ebc[screenBuffer[baddr]]));
						any = true;
					}
				}
				IncrementAddress(ref baddr);
			}
			while (baddr != 0);
			if (any)
				trace.trace_ds("'");

			trace.trace_ds("\n");
			telnet.Output(obptr);
		}


		/// <summary>
		/// Construct a 3270 command to reproduce the current state of the display.
		/// </summary>
		/// <param name="obptr"></param>
		public void TakeBufferSnapshot(NetBuffer obptr)
		{
			int baddr = 0;
			int attr_count;
			byte current_fg = 0x00;
			byte current_gr = 0x00;
			byte current_cs = 0x00;
			byte av;

			obptr.Add(screenAlt ? ControllerConstant.CMD_EWA : ControllerConstant.CMD_EW);
			obptr.Add(ControllerConstant.CodeTable[0]);

			do
			{
				if (FieldAttribute.IsFA(screenBuffer[baddr]))
				{
					obptr.Add(ControllerConstant.ORDER_SFE);
					attr_count = obptr.Index;//obptr - obuf;
					obptr.Add(1); /* for now */
					obptr.Add(See.XA_3270);
					obptr.Add(ControllerConstant.CodeTable[CalculateFA(screenBuffer[baddr])]);
					if (extendedAttributes[baddr].fg != 0)
					{
						//space3270out(2);
						obptr.Add(See.XA_FOREGROUND);
						obptr.Add(extendedAttributes[baddr].fg);
						obptr.IncrementAt(attr_count, 1);
					}
					if (extendedAttributes[baddr].gr != 0)
					{
						obptr.Add(See.XA_HIGHLIGHTING);
						obptr.Add(extendedAttributes[baddr].gr | 0xf0);
						obptr.IncrementAt(attr_count, 1);
					}
					if ((extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK) != 0)
					{
						obptr.Add(See.XA_CHARSET);
						obptr.Add((extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK) | 0xf0);
						obptr.IncrementAt(attr_count, 1);
					}
				}
				else
				{
					av = extendedAttributes[baddr].fg;
					if (current_fg != av)
					{
						current_fg = av;
						obptr.Add(ControllerConstant.ORDER_SA);
						obptr.Add(See.XA_FOREGROUND);
						obptr.Add(av);
					}
					av = extendedAttributes[baddr].gr;
					if (av != 0)
						av |= 0xf0;
					if (current_gr != av)
					{
						current_gr = av;
						obptr.Add(ControllerConstant.ORDER_SA);
						obptr.Add(See.XA_HIGHLIGHTING);
						obptr.Add(av);
					}
					av = (byte)(extendedAttributes[baddr].cs & ExtendedAttribute.CS_MASK);
					if (av != 0)
						av |= 0xf0;
					if (current_cs != av)
					{
						current_cs = av;
						obptr.Add(ControllerConstant.ORDER_SA);
						obptr.Add(See.XA_CHARSET);
						obptr.Add(av);
					}
					if ((extendedAttributes[baddr].cs & ControllerConstant.CS_GE) != 0)
					{
						obptr.Add(ControllerConstant.ORDER_GE);
					}
					obptr.Add(Tables.Cg2Ebc[screenBuffer[baddr]]);
				}
				IncrementAddress(ref baddr);
			}
			while (baddr != 0);

			obptr.Add(ControllerConstant.ORDER_SBA);
			Util.EncodeBAddress(obptr, cursorAddress);
			obptr.Add(ControllerConstant.ORDER_IC);
		}


		/// <summary>
		///	Construct a 3270 command to reproduce the reply mode.
		/// Returns a bool indicating if one is necessary.
		/// </summary>
		/// <param name="obptr"></param>
		/// <returns></returns>
		bool TakeReplyModeSnapshot(NetBuffer obptr)
		{
			bool success = false;

			if (telnet.Is3270 && replyMode != ControllerConstant.SF_SRM_FIELD)
			{
				obptr.Add(ControllerConstant.CMD_WSF);
				obptr.Add(0x00);	//Implicit length
				obptr.Add(0x00);
				obptr.Add(ControllerConstant.SF_SET_REPLY_MODE);
				obptr.Add(0x00);	//Partition 0
				obptr.Add(replyMode);
				if (replyMode == ControllerConstant.SF_SRM_CHAR)
				{
					for (int i = 0; i < crmnAttribute; i++)
					{
						obptr.Add(crmAttributes[i]);
					}
				}
				success = true;
			}

			return success;
		}

		/// <summary>
		/// Process a 3270 Erase All Unprotected command.
		/// </summary>
		public void ProcessEraseAllUnprotectedCommand()
		{
			int baddr, sbaddr;
			byte fa;
			bool f;

			this.telnet.Keyboard.ToggleEnterInhibitMode(false);

			this.OnAllChanged();

			if (this.isFormatted)
			{
				//Find first field attribute
				baddr = 0;
				do
				{
					if (FieldAttribute.IsFA(screenBuffer[baddr]))
					{
						break;
					}
					this.IncrementAddress(ref baddr);
				}
				while (baddr != 0);
				sbaddr = baddr;
				f = false;
				do
				{
					fa = screenBuffer[baddr];
					if (!FieldAttribute.IsProtected(fa))
					{
						this.MDTClear(screenBuffer, baddr);
						do
						{
							IncrementAddress(ref baddr);
							if (!f)
							{
								SetCursorAddress(baddr);
								f = true;
							}
							if (!FieldAttribute.IsFA(screenBuffer[baddr]))
							{
								this.AddCharacter(baddr, CharacterGenerator.Null, 0);
							}
						} while (!FieldAttribute.IsFA(screenBuffer[baddr]));
					}
					else
					{
						do
						{
							this.IncrementAddress(ref baddr);
						} while (!FieldAttribute.IsFA(screenBuffer[baddr]));
					}
				} while (baddr != sbaddr);
				if (!f)
				{
					SetCursorAddress(0);
				}
			}
			else
			{
				Clear(true);
			}
			this.attentionID = AID.None;
			this.telnet.Keyboard.ResetKeyboardLock(false);
		}


		private void EndText()
		{
			if (previous == PreviousEnum.Text)
			{
				trace.trace_ds("'");
			}
		}


		private void EndText(string cmd)
		{
			this.EndText();
			trace.trace_ds(" " + cmd);
		}


		private byte AttributeToFA(byte attr)
		{
			return (byte)(ControllerConstant.FA_BASE |
				(((attr) & 0x20) != 0 ? ControllerConstant.FA_PROTECT : (byte)0) |
				(((attr) & 0x10) != 0 ? ControllerConstant.FA_NUMERIC : (byte)0) |
				(((attr) & 0x01) != 0 ? ControllerConstant.FA_MODIFY : (byte)0) |
				(((attr) >> 2) & ControllerConstant.FA_INTENSITY));
		}


		private void StartFieldWithFA(byte fa)
		{
			//current_fa = screen_buf[buffer_addr]; 
			currentFaIndex = bufferAddress;
			AddCharacter(bufferAddress, fa, 0);
			SetForegroundColor(bufferAddress, 0);
			ctlr_add_gr(bufferAddress, 0);
			trace.trace_ds(See.GetSeeAttribute(fa));
			isFormatted = true;
		}


		private void StartField()
		{
			this.StartFieldWithFA(ControllerConstant.FA_BASE);
		}


		private void StartFieldWithAttribute(byte attr)
		{
			byte new_attr = AttributeToFA(attr);
			this.StartFieldWithFA(new_attr);
		}

		/// <summary>
		/// Process a 3270 Write command.
		/// </summary>
		/// <param name="buf"></param>
		/// <param name="start"></param>
		/// <param name="length"></param>
		/// <param name="erase"></param>
		/// <returns></returns>
		public PDS ProcessWriteCommand(byte[] buf, int start, int length, bool erase)
		{
			bool packetwasjustresetrewrite = false;
			int baddr;
			byte newAttr;
			bool lastCommand;
			bool lastZpt;
			bool wccKeyboardRestore;
			bool wccSoundAlarm;
			bool raGe;
			byte na;
			int anyFA;
			byte efaFG;
			byte efaGR;
			byte efaCS;
			string paren = "(";
			defaultFg = 0;
			defaultGr = 0;
			defaultCs = 0;
			tracePrimed = true;


			trace.WriteLine("::ctlr_write::" + ((DateTime.Now.Ticks - startTime) / 10000) + " " + length + " bytes");

			// ResetRewrite is just : 00 00 00 00 00 f1 c2 ff ef
			if (length == 4 &&
				buf[start + 0] == 0xf1 &&
				buf[start + 1] == 0xc2 &&
				buf[start + 2] == 0xff &&
				buf[start + 3] == 0xef)
			{
				trace.WriteLine("****Identified packet as a reset/rewrite combination. patch 29/Mar/2005 assumes more data will follow so does not notify user yet");
				packetwasjustresetrewrite = true;
			}

			PDS rv = PDS.OkayNoOutput;

			this.telnet.Keyboard.ToggleEnterInhibitMode(false);

			if (buf.Length < 2)
			{
				return PDS.BadCommand;
			}

			bufferAddress = cursorAddress;
			if (See.WCC_RESET(buf[start + 1]))
			{
				if (erase)
				{
					replyMode = ControllerConstant.SF_SRM_FIELD;
				}
				trace.trace_ds("%sreset", paren);
				paren = ",";
			}
			wccSoundAlarm = See.WCC_SOUND_ALARM(buf[start + 1]);

			if (wccSoundAlarm)
			{
				trace.trace_ds("%salarm", paren);
				paren = ",";
			}
			wccKeyboardRestore = See.WCC_KEYBOARD_RESTORE(buf[start + 1]);

			if (wccKeyboardRestore)
			{
				//Console.WriteLine("2218::ticking_stop");
				//ticking_stop();
			}

			if (wccKeyboardRestore)
			{
				trace.trace_ds("%srestore", paren);
				paren = ",";
			}

			if (See.WCC_RESET_MDT(buf[start + 1]))
			{
				trace.trace_ds("%sresetMDT", paren);
				paren = ",";
				baddr = 0;
				if (appres.modified_sel)
				{
					OnAllChanged();
				}

				do
				{
					if (FieldAttribute.IsFA(screenBuffer[baddr]))
					{
						MDTClear(screenBuffer, baddr);
					}
					IncrementAddress(ref baddr);
				}
				while (baddr != 0);

			}
			if (paren != "(")
			{
				trace.trace_ds(")");
			}

			lastCommand = true;
			lastZpt = false;
			currentFaIndex = this.GetFieldAttribute(bufferAddress);

			for (int cp = 2; cp < (length); cp++)
			{
				switch (buf[cp + start])
				{
					//Start field
					case ControllerConstant.ORDER_SF:
						{

							EndText("StartField");
							if (previous != PreviousEnum.SBA)
							{
								trace.trace_ds(trace.rcba(bufferAddress));
							}
							previous = PreviousEnum.Order;

							//Skip field attribute
							cp++;
							this.StartFieldWithAttribute(buf[cp + start]);
							this.SetForegroundColor(bufferAddress, 0);
							this.IncrementAddress(ref bufferAddress);
							lastCommand = true;
							lastZpt = false;
							break;
						}

					//Set buffer address
					case ControllerConstant.ORDER_SBA:
						{
							//Skip buffer address
							cp += 2;
							bufferAddress = Util.DecodeBAddress(buf[cp + start - 1], buf[cp + start]);
							this.EndText("SetBufferAddress");
							previous = PreviousEnum.SBA;
							trace.trace_ds(trace.rcba(bufferAddress));
							if (bufferAddress >= columnCount * rowCount)
							{
								trace.trace_ds(" [invalid address, write command terminated]\n");
								// Let a script go.
								this.telnet.Events.RunScript("ctlr_write SBA_ERROR");
								return PDS.BadAddress;
							}
							currentFaIndex = GetFieldAttribute(bufferAddress);
							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Insert cursor
					case ControllerConstant.ORDER_IC:
						{
							this.EndText("InsertCursor");
							if (previous != PreviousEnum.SBA)
							{
								trace.trace_ds(trace.rcba(bufferAddress));
							}
							previous = PreviousEnum.Order;
							SetCursorAddress(bufferAddress);
							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Program tab
					case ControllerConstant.ORDER_PT:
						{
							this.EndText("ProgramTab");
							previous = PreviousEnum.Order;
							//If the buffer address is the field attribute of of an unprotected field, simply advance one position.
							if (FieldAttribute.IsFA(screenBuffer[bufferAddress]) && !FieldAttribute.IsProtected(screenBuffer[bufferAddress]))
							{
								this.IncrementAddress(ref bufferAddress);
								lastZpt = false;
								lastCommand = true;
								break;
							}

							//Otherwise, advance to the first position of the next unprotected field.
							baddr = GetNextUnprotectedField(bufferAddress);
							if (baddr < bufferAddress)
							{
								baddr = 0;
							}

							//Null out the remainder of the current field -- even if protected -- if the PT doesn't follow a command
							//or order, or (honestly) if the last order we saw was a null-filling PT that left the buffer address at 0.
							if (!lastCommand || lastZpt)
							{
								trace.trace_ds("(nulling)");
								while ((bufferAddress != baddr) && (!FieldAttribute.IsFA(screenBuffer[bufferAddress])))
								{
									this.AddCharacter(bufferAddress, CharacterGenerator.Null, 0);
									this.IncrementAddress(ref bufferAddress);
								}
								if (baddr == 0)
								{
									lastZpt = true;
								}
							}
							else
							{
								lastZpt = false;
							}
							bufferAddress = baddr;
							lastCommand = true;
							break;
						}
					//Repeat to address
					case ControllerConstant.ORDER_RA:
						{
							EndText("RepeatToAddress");
							//Skip buffer address
							cp += 2;
							baddr = Util.DecodeBAddress(buf[cp + start - 1], buf[cp + start]);
							trace.trace_ds(trace.rcba(baddr));
							//Skip char to repeat
							cp++;
							if (buf[cp + start] == ControllerConstant.ORDER_GE)
							{
								raGe = true;
								trace.trace_ds("GraphicEscape");
								cp++;
							}
							else
							{
								raGe = false;
							}
							previous = PreviousEnum.Order;
							if (buf[cp + start] != 0)
							{
								trace.trace_ds("'");
							}
							trace.trace_ds("%s", See.GetEbc(buf[cp + start]));
							if (buf[cp + start] != 0)
							{
								trace.trace_ds("'");
							}
							if (baddr >= columnCount * rowCount)
							{
								trace.trace_ds(" [invalid address, write command terminated]\n");
								// Let a script go.
								telnet.Events.RunScript("ctlr_write baddr>COLS*ROWS");
								return PDS.BadAddress;
							}
							do
							{
								if (raGe)
								{
									AddCharacter(bufferAddress, Tables.Ebc2Cg0[buf[cp + start]], ControllerConstant.CS_GE);
								}
								else if (defaultCs != 0)
								{
									AddCharacter(bufferAddress, Tables.Ebc2Cg0[buf[cp + start]], 1);
								}
								else
								{
									AddCharacter(bufferAddress, Tables.Ebc2Cg[buf[cp + start]], 0);
								}

								this.SetForegroundColor(bufferAddress, defaultFg);
								this.ctlr_add_gr(bufferAddress, defaultGr);
								this.IncrementAddress(ref bufferAddress);

							} while (bufferAddress != baddr);

							currentFaIndex = GetFieldAttribute(bufferAddress);
							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Erase unprotected to address
					case ControllerConstant.ORDER_EUA:
						{
							//Skip buffer address
							cp += 2;
							baddr = Util.DecodeBAddress(buf[cp + start - 1], buf[cp + start]);
							this.EndText("EraseUnprotectedAll");
							if (previous != PreviousEnum.SBA)
							{
								trace.trace_ds(trace.rcba(baddr));
							}
							previous = PreviousEnum.Order;
							if (baddr >= columnCount * rowCount)
							{
								trace.trace_ds(" [invalid address, write command terminated]\n");
								//Let a script go.
								this.telnet.Events.RunScript("ctlr_write baddr>COLS*ROWS#2");
								return PDS.BadAddress;
							}
							do
							{
								if (FieldAttribute.IsFA(screenBuffer[bufferAddress]))
								{
									currentFaIndex = bufferAddress;
								}
								else if (!FieldAttribute.IsProtected(screenBuffer[currentFaIndex]))
								{
									this.AddCharacter(bufferAddress, CharacterGenerator.Null, 0);
								}

								this.IncrementAddress(ref bufferAddress);
							} while (bufferAddress != baddr);

							currentFaIndex = GetFieldAttribute(bufferAddress);

							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Graphic escape 
					case ControllerConstant.ORDER_GE:
						{
							EndText("GraphicEscape ");
							cp++;
							previous = PreviousEnum.Order;
							if (buf[cp + start] != 0)
							{
								trace.trace_ds("'");
							}
							trace.trace_ds("%s", See.GetEbc(buf[cp + start]));
							if (buf[cp + start] != 0)
							{
								trace.trace_ds("'");
							}

							this.AddCharacter(bufferAddress, Tables.Ebc2Cg0[buf[cp + start]], ControllerConstant.CS_GE);
							this.SetForegroundColor(bufferAddress, defaultFg);
							this.ctlr_add_gr(bufferAddress, defaultGr);
							this.IncrementAddress(ref bufferAddress);

							currentFaIndex = GetFieldAttribute(bufferAddress);
							lastCommand = false;
							lastZpt = false;
							break;
						}
					//Modify field
					case ControllerConstant.ORDER_MF:
						{
							this.EndText("ModifyField");
							if (previous != PreviousEnum.SBA)
							{
								trace.trace_ds(trace.rcba(bufferAddress));
							}
							previous = PreviousEnum.Order;
							cp++;
							na = buf[cp + start];
							if (FieldAttribute.IsFA(screenBuffer[bufferAddress]))
							{
								for (int i = 0; i < (int)na; i++)
								{
									cp++;
									if (buf[cp + start] == See.XA_3270)
									{
										trace.trace_ds(" 3270");
										cp++;
										newAttr = AttributeToFA(buf[cp + start]);
										this.AddCharacter(bufferAddress, newAttr, 0);
										trace.trace_ds(See.GetSeeAttribute(newAttr));
									}
									else if (buf[cp + start] == See.XA_FOREGROUND)
									{
										trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
										cp++;
										if (appres.m3279)
										{
											this.SetForegroundColor(bufferAddress, buf[cp + start]);
										}
									}
									else if (buf[cp + start] == See.XA_HIGHLIGHTING)
									{
										trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
										cp++;
										this.ctlr_add_gr(bufferAddress, (byte)(buf[cp + start] & 0x07));
									}
									else if (buf[cp + start] == See.XA_CHARSET)
									{
										int cs = 0;

										trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
										cp++;
										if (buf[cp + start] == 0xf1)
										{
											cs = 1;
										}
										this.AddCharacter(bufferAddress, screenBuffer[bufferAddress], (byte)cs);
									}
									else if (buf[cp + start] == See.XA_ALL)
									{
										trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
										cp++;
									}
									else
									{
										trace.trace_ds("%s[unsupported]", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
										cp++;
									}
								}
								this.IncrementAddress(ref bufferAddress);
							}
							else
								cp += na * 2;
							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Start field extended
					case ControllerConstant.ORDER_SFE:
						{
							EndText("StartFieldExtended");
							if (previous != PreviousEnum.SBA)
							{
								trace.trace_ds(trace.rcba(bufferAddress));
							}
							previous = PreviousEnum.Order;
							//Skip order
							cp++;
							na = buf[cp + start];
							anyFA = 0;
							efaFG = 0;
							efaGR = 0;
							efaCS = 0;
							for (int i = 0; i < (int)na; i++)
							{
								cp++;
								if (buf[cp + start] == See.XA_3270)
								{
									trace.trace_ds(" 3270");
									cp++;
									this.StartFieldWithAttribute(buf[cp + start]);
									anyFA++;
								}
								else if (buf[cp + start] == See.XA_FOREGROUND)
								{
									trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
									cp++;
									if (appres.m3279)
									{
										efaFG = buf[cp + start];
									}
								}
								else if (buf[cp + start] == See.XA_HIGHLIGHTING)
								{
									trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
									cp++;
									efaGR = (byte)(buf[cp + start] & 0x07);
								}
								else if (buf[cp + start] == See.XA_CHARSET)
								{
									trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
									cp++;
									if (buf[cp + start] == 0xf1)
									{
										efaCS = 1;
									}
								}
								else if (buf[cp + start] == See.XA_ALL)
								{
									trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
									cp++;
								}
								else
								{
									trace.trace_ds("%s[unsupported]", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
									cp++;
								}
							}
							if (anyFA == 0)
							{
								this.StartFieldWithFA(ControllerConstant.FA_BASE);
							}
							this.AddCharacter(bufferAddress, screenBuffer[bufferAddress], efaCS);
							this.SetForegroundColor(bufferAddress, efaFG);
							this.ctlr_add_gr(bufferAddress, efaGR);
							this.IncrementAddress(ref bufferAddress);
							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Set attribute
					case ControllerConstant.ORDER_SA:
						{
							this.EndText("SetAttribtue");
							previous = PreviousEnum.Order;
							cp++;
							if (buf[cp + start] == See.XA_FOREGROUND)
							{
								trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
								if (appres.m3279)
								{
									defaultFg = buf[cp + start + 1];
								}
							}
							else if (buf[cp + start] == See.XA_HIGHLIGHTING)
							{
								trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
								defaultGr = (byte)(buf[cp + start + 1] & 0x07);
							}
							else if (buf[cp + start] == See.XA_ALL)
							{
								trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
								defaultFg = 0;
								defaultGr = 0;
								defaultCs = 0;
							}
							else if (buf[cp + start] == See.XA_CHARSET)
							{
								trace.trace_ds("%s", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
								defaultCs = (buf[cp + start + 1] == 0xf1) ? (byte)1 : (byte)0;
							}
							else
								trace.trace_ds("%s[unsupported]", See.GetEfa(buf[cp + start], buf[cp + start + 1]));
							cp++;
							lastCommand = true;
							lastZpt = false;
							break;
						}
					//Format control orders
					case ControllerConstant.FCORDER_SUB:	
					case ControllerConstant.FCORDER_DUP:
					case ControllerConstant.FCORDER_FM:
					case ControllerConstant.FCORDER_FF:
					case ControllerConstant.FCORDER_CR:
					case ControllerConstant.FCORDER_NL:
					case ControllerConstant.FCORDER_EM:
					case ControllerConstant.FCORDER_EO:
						{
							this.EndText(See.GetEbc(buf[cp + start]));
							previous = PreviousEnum.Order;
							this.AddCharacter(bufferAddress, Tables.Ebc2Cg[buf[cp + start]], defaultCs);
							this.SetForegroundColor(bufferAddress, defaultFg);
							this.ctlr_add_gr(bufferAddress, defaultGr);
							this.IncrementAddress(ref bufferAddress);
							lastCommand = true;
							lastZpt = false;
							break;
						}
					case ControllerConstant.FCORDER_NULL:
						{
							this.EndText("NULL");
							previous = PreviousEnum.NullCharacter;
							this.AddCharacter(bufferAddress, Tables.Ebc2Cg[buf[cp + start]], defaultCs);
							this.SetForegroundColor(bufferAddress, defaultFg);
							this.ctlr_add_gr(bufferAddress, defaultGr);
							this.IncrementAddress(ref bufferAddress);
							lastCommand = false;
							lastZpt = false;
							break;
						}
					//Enter character
					default:
						{
							if (buf[cp + start] <= 0x3F)
							{
								this.EndText("ILLEGAL_ORDER");
								trace.trace_ds("%s", See.GetEbc(buf[cp + start]));
								lastCommand = true;
								lastZpt = false;
								break;
							}
							if (previous != PreviousEnum.Text)
								trace.trace_ds(" '");
							previous = PreviousEnum.Text;
							trace.trace_ds("%s", See.GetEbc(buf[cp + start]));
							this.AddCharacter(bufferAddress, Tables.Ebc2Cg[buf[cp + start]], defaultCs);
							this.SetForegroundColor(bufferAddress, defaultFg);
							this.ctlr_add_gr(bufferAddress, defaultGr);
							this.IncrementAddress(ref bufferAddress);
							lastCommand = false;
							lastZpt = false;
							break;
						}
				}
			}

			this.SetFormattedFlag();

			if (previous == PreviousEnum.Text)
			{
				trace.trace_ds("'");
			}

			trace.trace_ds("\n");
			if (wccKeyboardRestore)
			{
				this.attentionID = AID.None;
				this.telnet.Keyboard.ResetKeyboardLock(false);
			}
			else if ((telnet.Keyboard.keyboardLock & KeyboardConstants.OiaTWait) != 0)
			{
				this.telnet.Keyboard.KeyboardLockClear(KeyboardConstants.OiaTWait, "ctlr_write");
				//status_syswait();
			}
			if (wccSoundAlarm)
			{
				//	ring_bell();
			}

			tracePrimed = false;

			this.ProcessPendingInput();

			//Let a script go.
			if (!packetwasjustresetrewrite)
			{
				this.telnet.Events.RunScript("ctlr_write - end");
				try
				{
					this.NotifyDataAvailable();
				}
				catch
				{
				}
			}

			return rv;
		}




		private void NotifyDataAvailable()
		{
			lock (dataAvailablePadlock)
			{
				if (telnet != null)
				{
					this.dataAvailableCount = this.telnet.StartedReceivingCount;
				}
				else
				{
					this.dataAvailableCount++;
				}
			}
			int rcvCnt = 0;
			if (telnet != null)
			{
				rcvCnt = this.telnet.StartedReceivingCount;
			}
			trace.trace_dsn("NotifyDataAvailable : dataReceivedCount = " + rcvCnt + "  dataAvailableCount = " + DataAvailableCount.ToString() + Environment.NewLine);
		}


		/// <summary>
		/// Write SSCP-LU data, which is quite a bit dumber than regular 3270 output.
		/// </summary>
		/// <param name="buf"></param>
		/// <param name="start"></param>
		/// <param name="buflen"></param>
		public void WriteSspcLuData(byte[] buf, int start, int buflen)
		{
			int i;
			int cp = start;
			int sRow;
			byte c;
			int baddr;
			byte fa;


			//The 3174 Functionl Description says that anything but NL, NULL, FM, or DUP is to be displayed as a graphic.  However, to deal with
			//badly-behaved hosts, we filter out SF, IC and SBA sequences, andwe display other control codes as spaces.
			trace.trace_ds("SSCP-LU data\n");
			cp = start;
			for (i = 0; i < buflen; cp++, i++)
			{
				switch (buf[cp])
				{
					case ControllerConstant.FCORDER_NL:

						//Insert NULLs to the end of the line and advance to the beginning of the next line.
						sRow = bufferAddress / columnCount;
						while ((bufferAddress / columnCount) == sRow)
						{
							this.AddCharacter(bufferAddress, Tables.Ebc2Cg[0], defaultCs);
							this.SetForegroundColor(bufferAddress, defaultFg);
							this.ctlr_add_gr(bufferAddress, defaultGr);
							this.IncrementAddress(ref bufferAddress);
						}
						break;

					case ControllerConstant.ORDER_SF:	/* some hosts forget their talking SSCP-LU */
						cp++;
						i++;
						fa = AttributeToFA(buf[cp]);
						trace.trace_ds(" StartField" + trace.rcba(bufferAddress) + " " + See.GetSeeAttribute(fa) + " [translated to space]\n");
						this.AddCharacter(bufferAddress, CharacterGenerator.Space, defaultCs);
						this.SetForegroundColor(bufferAddress, defaultFg);
						this.ctlr_add_gr(bufferAddress, defaultGr);
						this.IncrementAddress(ref bufferAddress);
						break;
					case ControllerConstant.ORDER_IC:
						trace.trace_ds(" InsertCursor%s [ignored]\n", trace.rcba(bufferAddress));
						break;
					case ControllerConstant.ORDER_SBA:
						baddr = Util.DecodeBAddress(buf[cp + 1], buf[cp + 2]);
						trace.trace_ds(" SetBufferAddress%s [ignored]\n", trace.rcba(baddr));
						cp += 2;
						i += 2;
						break;

					case ControllerConstant.ORDER_GE:
						cp++;
						if (++i >= buflen)
							break;
						if (buf[cp] <= 0x40)
							c = CharacterGenerator.Space;
						else
							c = Tables.Ebc2Cg0[buf[cp]];
						AddCharacter(bufferAddress, c, ControllerConstant.CS_GE);
						SetForegroundColor(bufferAddress, defaultFg);
						ctlr_add_gr(bufferAddress, defaultGr);
						IncrementAddress(ref bufferAddress);
						break;

					default:
						if (buf[cp] == ControllerConstant.FCORDER_NULL)
							c = CharacterGenerator.Space;
						else if (buf[cp] == ControllerConstant.FCORDER_FM)
							c = CharacterGenerator.Asterisk;
						else if (buf[cp] == ControllerConstant.FCORDER_DUP)
							c = CharacterGenerator.Semicolon;
						else if (buf[cp] < 0x40)
						{
							trace.trace_ds(" X'" + buf[cp] + "') [translated to space]\n");
							c = CharacterGenerator.Space; /* technically not necessary */
						}
						else
							c = Tables.Ebc2Cg[buf[cp]];
						AddCharacter(bufferAddress, c, defaultCs);
						SetForegroundColor(bufferAddress, defaultFg);
						ctlr_add_gr(bufferAddress, defaultGr);
						IncrementAddress(ref bufferAddress);
						break;
				}
			}
			SetCursorAddress(bufferAddress);
			sscpStart = bufferAddress;

			/* Unlock the keyboard. */
			attentionID = AID.None;
			telnet.Keyboard.ResetKeyboardLock(false);

			/* Let a script go. */
			telnet.Events.RunScript("ctlr_write_sscp_lu done");
			//sms_host_output();
		}



		public void ProcessPendingInput()
		{
			//Process type ahead queue
			while (telnet.Keyboard.RunTypeAhead());
			//Notify script we're ok
			//Console.WriteLine("--sms_continue");

			this.Continue();
		}


		public void Continue()
		{
			lock (telnet)
			{
				switch (telnet.WaitState)
				{
					case SmsState.Idle:
						break;
					case SmsState.KBWait:
						if (telnet.IsKeyboardInWait)
						{
							telnet.WaitEvent1.Set();
						}
						break;
					case SmsState.WaitAnsi:
						if (telnet.IsAnsi)
						{
							telnet.WaitEvent1.Set();
						}
						break;
					case SmsState.Wait3270:
						if (telnet.Is3270 | telnet.IsSscp)
						{
							telnet.WaitEvent1.Set();
						}
						break;
					case SmsState.Wait:
						if (!telnet.CanProceed)
							break;
						if (telnet.IsPending ||
							(telnet.IsConnected && (telnet.Keyboard.keyboardLock & KeyboardConstants.AwaitingFirst) != 0))
							break;
						// do stuff
						telnet.WaitEvent1.Set();

						break;
					case SmsState.ConnectWait:
						if (telnet.IsPending ||
							(telnet.IsConnected && (telnet.Keyboard.keyboardLock & KeyboardConstants.AwaitingFirst) != 0))
							break;
						// do stuff
						telnet.WaitEvent1.Set();
						break;
					default:
						Console.WriteLine("**BUGBUG**IGNORED STATE " + telnet.WaitState);
						break;
				}
			}
		}





		 /// <summary>
		 /// Clear the text (non-status) portion of the display.  Also resets the cursor and buffer addresses and extended attributes.
		 /// </summary>
		 /// <param name="can_snap"></param>
		public void Clear(bool can_snap)
		{
			/* Snap any data that is about to be lost into the trace file. */
			if (this.StreamHasData)
			{
				if (can_snap && !traceSkipping && appres.Toggled(Appres.ScreenTrace))
				{
					trace.trace_screen();
				}
				//		scroll_save(maxROWS, ever_3270 ? false : true);
			}
			traceSkipping = false;

			/* Clear the screen. */
			int i;
			for (i = 0; i < rowCount * columnCount; i++)
			{
				screenBuffer[i] = 0;
				// CFC,Jr. 8/23/2008
				// Clear the ExtendedAttributes instead of creating new ones
				//ea_buf[i] = new ExtendedAttribute();
				extendedAttributes[i].Clear();
			}
			//memset((char *)screen_buf, 0, ROWS*COLS);
			//memset((char *)ea_buf, 0, ROWS*COLS*sizeof(struct ea));
			OnAllChanged();
			SetCursorAddress(0);
			bufferAddress = 0;
			//	unselect(0, ROWS*COLS);
			isFormatted = false;
			defaultFg = 0;
			defaultGr = 0;
			sscpStart = 0;
		}



		/// <summary>
		/// Fill the screen buffer with blanks.
		/// </summary>
		void BlankOutScreen()
		{
			int i;
			for (i = 0; i < rowCount * columnCount; i++)
			{
				screenBuffer[i] = CharacterGenerator.Space;
			}
			OnAllChanged();
			SetCursorAddress(0);
			bufferAddress = 0;
			//	unselect(0, ROWS*COLS);
			isFormatted = false;
		}


		
		/// <summary>
		/// Change a character in the 3270 buffer.
		/// </summary>
		/// <param name="baddr"></param>
		/// <param name="c"></param>
		/// <param name="cs"></param>
		public void AddCharacter(int baddr, byte c, byte cs)
		{
			byte oc;
			char ch = System.Convert.ToChar(Tables.Cg2Ascii[c]);

			if ((oc = screenBuffer[baddr]) != c || extendedAttributes[baddr].cs != cs)
			{
				if (tracePrimed && !IsBlank(oc))
				{
					if (appres.Toggled(Appres.ScreenTrace))
					{
						trace.trace_screen();
					}
					//			scroll_save(maxROWS, false);
					tracePrimed = false;
				}
				//		if (SELECTED(baddr))
				//			unselect(baddr, 1);
				OnOneChanged(baddr);
				screenBuffer[baddr] = c;
				extendedAttributes[baddr].cs = cs;
			}
			//			Dump();
		}

		/*
		 * Change the graphic rendition of a character in the 3270 buffer.
		 */
		public void ctlr_add_gr(int baddr, byte gr)
		{
			if (extendedAttributes[baddr].gr != gr)
			{
				//		if (SELECTED(baddr))
				//			unselect(baddr, 1);
				OnOneChanged(baddr);
				extendedAttributes[baddr].gr = gr;
				//		if (gr & GR_BLINK)
				//			blink_start();
			}
		}


		/// <summary>
		/// Change the foreground color for a character in the 3270 buffer.
		/// </summary>
		/// <param name="baddr"></param>
		/// <param name="color"></param>
		public void SetForegroundColor(int baddr, byte color)
		{
			if (appres.m3279)
			{
				if ((color & 0xf0) != 0xf0)
				{
					color = 0;
				}
				if (this.extendedAttributes[baddr].fg != color)
				{
					//		if (SELECTED(baddr))
					//			unselect(baddr, 1);
					this.OnOneChanged(baddr);
					this.extendedAttributes[baddr].fg = color;
				}
			}
		}


		/// <summary>
		/// Change the background color for a character in the 3270 buffer.
		/// </summary>
		/// <param name="baddr"></param>
		/// <param name="color"></param>
		public void SetBackgroundColor(int baddr, byte color)
		{
			if (appres.m3279)
			{
				if ((color & 0xf0) != 0xf0)
				{
					color = 0;
				}
				if (this.extendedAttributes[baddr].bg != color)
				{
					//		if (SELECTED(baddr))
					//			unselect(baddr, 1);
					this.OnOneChanged(baddr);
					this.extendedAttributes[baddr].bg = color;
				}
			}
		}

	
		/// <summary>
		/// Copy a block of characters in the 3270 buffer, optionally including all of the extended attributes.  
		///(The character set, which is actually kept in the extended attributes, is considered part of the characters here.)
		/// </summary>
		/// <param name="fromAddress"></param>
		/// <param name="toAddress"></param>
		/// <param name="count"></param>
		/// <param name="moveExtendedAttributes"></param>
		public void CopyBlock(int fromAddress, int toAddress, int count, bool moveExtendedAttributes)
		{
			bool changed = false;

			int any = 0;
			int start, end, inc;

			if (toAddress < fromAddress || fromAddress + count < toAddress)
			{
				// Scan forward
				start = 0;
				end = count + 1;
				inc = 1;
			}
			else
			{
				// Scan backward
				start = count - 1;
				end = -1;
				inc = -1;
			}

			for (int i = start; i != end; i += inc)
			{
				if (screenBuffer[fromAddress + i] != screenBuffer[toAddress + i])
				{
					screenBuffer[toAddress + i] = screenBuffer[fromAddress + i];
					changed = true;
				}
			}

			if (changed)
			{
				this.OnRegionChanged(toAddress, toAddress + count);
				/*
				 * For the time being, if any selected text shifts around on
				 * the screen, unhighlight it.  Eventually there should be
				 * logic for preserving the highlight if the *all* of the
				 * selected text moves.
				 */
				//if (area_is_selected(baddr_to, count))
				//	unselect(baddr_to, count);
			}

			
			 //If we aren't supposed to move all the extended attributes, move the character sets separately.
			
			if (!moveExtendedAttributes)
			{
				for (int i = start; i != end; i += inc)
				{
					if (extendedAttributes[toAddress + i].cs != extendedAttributes[fromAddress + i].cs)
					{
						extendedAttributes[toAddress + i].cs = extendedAttributes[fromAddress + i].cs;
						OnRegionChanged(toAddress + i, toAddress + i + 1);
						any++;
					}
				}
				//if (any && area_is_selected(baddr_to, count))
				//	unselect(baddr_to, count);
			}

			//Move extended attributes.
			if (moveExtendedAttributes)
			{
				changed = false;
				for (int i = 0; i < count; i++)
				{
					if (extendedAttributes[fromAddress + i] != extendedAttributes[toAddress + i])
					{
						extendedAttributes[fromAddress + i] = extendedAttributes[toAddress + i];
						changed = true;
					}
				}
				if (changed)
				{
					OnRegionChanged(toAddress, toAddress + count);
				}
			}
		}


		/// <summary>
		/// Erase a region of the 3270 buffer, optionally clearing extended attributes as well.
		/// </summary>
		/// <param name="baddr"></param>
		/// <param name="count"></param>
		/// <param name="clear_ea"></param>
		public void EraseRegion(int baddr, int count, bool clear_ea)
		{
			//Console.WriteLine("ctlr_aclear - bugbug - compare to c code");
			int i;
			bool changed = false;
			for (i = 0; i < count; i++)
			{
				if (this.screenBuffer[baddr] != 0)
				{
					this.screenBuffer[baddr] = 0;
					changed = true;
				}
			}
			if (changed)
			{
				this.OnRegionChanged(baddr, baddr + count);
				//		if (area_is_selected(baddr, count))
				//			unselect(baddr, count);
			}
			if (clear_ea)
			{
				changed = false;
				for (i = 0; i < count; i++)
				{
					if (!this.extendedAttributes[baddr + i].IsZero)
					{
						// CFC,Jr. 8/23/2008
						// Clear the ExtendedAttributes instead of creating new ones
						//ea_buf[baddr + i] = new ExtendedAttribute();
						this.extendedAttributes[baddr + i].Clear();
						changed = true;
					}
				}
				if (changed)
				{
					this.OnRegionChanged(baddr, baddr + count);
				}
			}
		}

		/*
		 * Scroll the screen 1 row.
		 *
		 * This could be accomplished with ctlr_bcopy() and ctlr_aclear(), but this
		 * operation is common enough to warrant a separate path.
		 */
		public void ScrollOne()
		{
			throw new ApplicationException("ctlr_scroll not implemented");
		}


		/// <summary>
		/// Note that a particular region of the screen has changed.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		void ScreenRegionChanged(int start, int end)
		{
			OnRegionChanged(start, end);
		}


		/// <summary>
		/// Swap the regular and alternate screen buffers
		/// </summary>
		/// <param name="alt"></param>
		public void SwapAltBuffers(bool alt)
		{

			byte[] stmp;
			ExtendedAttribute[] etmp;


			if (alt != isAltBuffer)
			{

				stmp = screenBuffer;
				screenBuffer = aScreenBuffer;
				aScreenBuffer = stmp;

				etmp = extendedAttributes;
				extendedAttributes = aExtendedAttributeBuffer;
				aExtendedAttributeBuffer = etmp;

				isAltBuffer = alt;
				OnAllChanged();
				//		unselect(0, ROWS*COLS);

				/*
				 * There may be blinkers on the alternate screen; schedule one
				 * iteration just in case.
				 */
				//		blink_start();
			}
		}


		/// <summary>
		/// Set or clear the MDT on an attribute
		/// </summary>
		/// <param name="data"></param>
		/// <param name="offset"></param>
		public void SetMDT(byte[] data, int offset)
		{
			// mfw
			if (offset != -1)
			{
				if ((data[offset] & ControllerConstant.FA_MODIFY) != 0)
				{
					return;
				}

				data[offset] |= ControllerConstant.FA_MODIFY;
				if (appres.modified_sel)
				{
					this.OnAllChanged();
				}
			}
		}

		public void MDTClear(byte[] data, int offset)
		{
			if ((data[offset] & ControllerConstant.FA_MODIFY) == 0)
				return;
			data[offset] &= ControllerConstant.FA_MODIFY_MASK;//(byte)~FA_MODIFY;
			if (appres.modified_sel)
				OnAllChanged();
		}



		/// <summary>
		/// Support for screen-size swapping for scrolling
		/// </summary>
		void Shrink()
		{
			int i;
			for (i = 0; i < rowCount * columnCount; i++)
			{
				screenBuffer[i] = debuggingFont ? CharacterGenerator.Space : CharacterGenerator.Null;
			}
			OnAllChanged();
			//	screen_disp(false);
		}

		public int CursorX
		{
			get { return AddressToColumn(cursorAddress); }
		}
		public int CursorY
		{
			get { return AddresstoRow(cursorAddress); }
		}

		public event EventHandler CursorLocationChanged;
		protected virtual void OnCursorLocationChanged()
		{
			if (this.CursorLocationChanged != null)
			{
				this.CursorLocationChanged(this, EventArgs.Empty);
			}
		}
		

		public void SetCursorAddress(int address)
		{
			if (address != cursorAddress)
			{
				cursorAddress = address;
				this.OnCursorLocationChanged();
			}
			
		}
		public int AddresstoRow(int address)
		{
			return ((address) / columnCount);
		}
		public int AddressToColumn(int address)
		{
			return address % columnCount;
		}
		public int RowColumnToByteAddress(int row, int column)
		{
			return (((row) * columnCount) + column);
		}
		public void IncrementAddress(ref int address)
		{
			(address) = ((address) + 1) % (columnCount * rowCount);
		}
		public void DecrementAddress(ref int address)
		{
			(address) = (address != 0) ? (address - 1) : ((columnCount * rowCount) - 1);
		}

		public void RemoveTimeOut(System.Threading.Timer timer)
		{
			//Console.WriteLine("remove timeout");
			if (timer != null)
			{
				timer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
			}

		}
		public System.Threading.Timer AddTimeout(int milliseconds, System.Threading.TimerCallback callback)
		{
			//Console.WriteLine("add timeout");
			System.Threading.Timer timer = new System.Threading.Timer(callback, this, milliseconds, 0);
			return timer;

		}


		public bool MoveCursor(CursorOp op, int x, int y)
		{
			int bAddress;
			int sbAddress;
			int nbAddress;
			bool success = false;

			switch (op)
			{
				case CursorOp.Exact:
				case CursorOp.NearestUnprotectedField:
					{
						if (!telnet.Is3270)
						{
							x--;
							y--;
						}
						if (x < 0)
							x = 0;
						if (y < 0)
							y = 0;
						
						bAddress = ((y * columnCount) + x) % (rowCount * columnCount);
					
						if (op == CursorOp.Exact)
						{
							this.SetCursorAddress(bAddress);
						}
						else
						{
							this.SetCursorAddress(GetNextUnprotectedField(cursorAddress));
						}

						success = true;
						break;
					}
				case CursorOp.Tab:
					{
						if (telnet.IsAnsi)
						{
							this.telnet.SendChar('\t');
							return true;
						}
						else
						{
							this.SetCursorAddress(GetNextUnprotectedField(cursorAddress));
						}
						success= true;
						break;
					}
				case CursorOp.BackTab:
					{
						if (telnet.Is3270)
						{
							bAddress = cursorAddress;
							this.DecrementAddress(ref bAddress);
							if (FieldAttribute.IsFA(screenBuffer[bAddress]))
							{
								//At beginning of field
								this.DecrementAddress(ref bAddress);
							}
							sbAddress = bAddress;
							while (true)
							{
								nbAddress = bAddress;
								this.IncrementAddress(ref nbAddress);
								if (FieldAttribute.IsFA(screenBuffer[bAddress])
									&& !FieldAttribute.IsProtected(screenBuffer[bAddress])
									&& !FieldAttribute.IsFA(screenBuffer[nbAddress]))
								{
									break;
								}

								this.DecrementAddress(ref bAddress);

								if (bAddress == sbAddress)
								{
									this.SetCursorAddress(0);
									success = true;
								}
							}

							this.IncrementAddress(ref bAddress);
							this.SetCursorAddress(bAddress);
							success = true;
							
						}
						break;
					}
					
				default:
					throw new ApplicationException("Sorry, cursor op '" + op + "' not implemented");
			}

			return success;
		}




		public void DumpRange(int first, int len, bool in_ascii, byte[] buf, int rel_rows, int rel_cols)
		{

			bool any = false;
			byte[] lineBuffer = new byte[this.maxColumns * 3 + 1];
			int s = 0;
			string debug = "";

			/*
			 * If the client has looked at the live screen, then if they later
			 * execute 'Wait(output)', they will need to wait for output from the
			 * host.  output_wait_needed is cleared by sms_host_output,
			 * which is called from the write logic in ctlr.c.
			 */
			//	if (sms != SN && buf == screen_buf)
			//		sms->output_wait_needed = True;

			for (int i = 0; i < len; i++)
			{
				byte c;

				if (i != 0 && 0 == ((first + i) % rel_cols))
				{
					lineBuffer[s] = 0;
					this.telnet.Action.action_output(lineBuffer, s);
					s = 0;
					debug = "";
					any = false;
				}
				if (!any)
				{
					any = true;
				}
				if (in_ascii)
				{
					c = Tables.Cg2Ascii[buf[first + i]];
					lineBuffer[s++] = (c == 0) ? (byte)' ' : c;
					if (c == 0)
					{
						debug += " ";
					}
					else
					{
						debug += System.Convert.ToChar(c);
					}
				}
				else
				{
					string temp = String.Format("{0}{1:x2}", i != 0 ? " " : "", Tables.Cg2Ebc[buf[first + i]]);
					int tt;
					for (tt = 0; tt < temp.Length; tt++)
					{
						lineBuffer[s++] = (byte)temp[tt];
					}
				}
			}
			if (any)
			{
				lineBuffer[s] = 0;
				this.telnet.Action.action_output(lineBuffer, s);
			}
		}

		void DumpRangeXML(int first, int length, bool inAscii, byte[] buffer, int relRows, int relCols)
		{

			bool any = false;
			byte[] linebuf = new byte[maxColumns * 3 * 5 + 1];
			int s = 0;
			if (!inAscii)
			{
				throw new ApplicationException("sorry, dump_rangeXML only valid for ascii buffer");
			}

			/*
			 * If the client has looked at the live screen, then if they later
			 * execute 'Wait(output)', they will need to wait for output from the
			 * host.  output_wait_needed is cleared by sms_host_output,
			 * which is called from the write logic in ctlr.c.
			 */
			//if (sms != SN && buf == screen_buf)
			//	sms->output_wait_needed = True;

			for (int i = 0; i < length; i++)
			{
				byte c;

				if (i != 0 && 0 == ((first + i) % relCols))
				{
					linebuf[s] = 0;
					telnet.Action.action_output(linebuf, s);
					s = 0;
					any = false;
				}
				if (!any)
				{
					any = true;
				}

				c = Tables.Cg2Ascii[buffer[first + i]];
				if (c == 0) c = (byte)' ';
				string temp = "";

				temp = "" + System.Convert.ToChar(c);
				int tt;
				for (tt = 0; tt < temp.Length; tt++)
				{
					linebuf[s++] = (byte)temp[tt];
				}
			}
			if (any)
			{
				linebuf[s] = 0;
				this.telnet.Action.action_output(linebuf, s, true);
			}
		}



		bool DumpFixed(object[] args, string name, bool inAscii, byte[] buffer, int relRows, int relColumns, int cAddress)
		{
			int row, col, len, rows = 0, cols = 0;

			switch (args.Length)
			{
				//Everything
				case 0:
					{
						row = 0;
						col = 0;
						len = relRows * relColumns;
						break;
					}
				//From cursor, for n
				case 1:
					{
						row = cAddress / relColumns;
						col = cAddress % relColumns;
						len = (int)args[0];
						break;
					}
				//From (row,col), for n
				case 3:
					{
						row = (int)args[0];
						col = (int)args[1];
						len = (int)args[2];
						break;
					}
				//From (row,col), for rows x cols
				case 4:
					{
						row = (int)args[0];
						col = (int)args[1];
						rows = (int)args[2];
						cols = (int)args[3];
						len = 0;
						break;
					}
				default:
					{
						this.telnet.Events.ShowError(name + " requires 0, 1, 3 or 4 arguments");
						return false;
					}
			}

			if (
				(row < 0 || row > relRows || col < 0 || col > relColumns || len < 0) ||
				((args.Length < 4) && ((row * relColumns) + col + len > relRows * relColumns)) ||
				((args.Length == 4) && (cols < 0 || rows < 0 ||
				col + cols > relColumns || row + rows > relRows))
				)
			{
				this.telnet.Events.ShowError(name + ": Invalid argument", name);
				return false;
			}


			if (args.Length < 4)
			{
				this.DumpRange((row * relColumns) + col, len, inAscii, buffer, relRows, relColumns);
			}
			else
			{
				int i;

				for (i = 0; i < rows; i++)
				{
					this.DumpRange(((row + i) * relColumns) + col, cols, inAscii, buffer, relRows, relColumns);
				}
			}

			return true;
		}

		bool DumpField(string name, bool in_ascii)
		{
			int faIndex;
			byte fa = this.fakeFA;
			int start, baddr;
			int length = 0;

			if (!isFormatted)
			{
				telnet.Events.ShowError(name + ": Screen is not formatted");
				return false;
			}
			faIndex = GetFieldAttribute(cursorAddress);
			start = faIndex;
			IncrementAddress(ref start);
			baddr = start;
			do
			{
				if (FieldAttribute.IsFA(screenBuffer[baddr]))
				{
					break;
				}
				length++;
				this.IncrementAddress(ref baddr);
			} while (baddr != start);

			this.DumpRange(start, length, in_ascii, screenBuffer, rowCount, columnCount);
			return true;
		}

		int DumpFieldAsXML(int address, ExtendedAttribute ea)
		{
			byte fa = this.fakeFA;
			int faIndex;
			int start, baddr;
			int length = 0;


			faIndex = GetFieldAttribute(address);
			if (faIndex != -1)
			{
				fa = screenBuffer[faIndex];
			}
			start = faIndex;
			this.IncrementAddress(ref start);
			baddr = start;

			do
			{
				if (FieldAttribute.IsFA(screenBuffer[baddr]))
				{

					if (extendedAttributes[baddr].fg != 0) 
						ea.fg = extendedAttributes[baddr].fg;
					if (extendedAttributes[baddr].bg != 0) 
						ea.bg = extendedAttributes[baddr].bg;
					if (extendedAttributes[baddr].cs != 0) 
						ea.cs = extendedAttributes[baddr].cs;
					if (extendedAttributes[baddr].gr != 0) 
						ea.gr = extendedAttributes[baddr].gr;

					break;
				}
				length++;
				this.IncrementAddress(ref baddr);
			}
			while (baddr != start);

			int columnStart = AddressToColumn(start);
			int rowStart = AddresstoRow(start);
			int rowEnd = AddresstoRow(baddr) + 1;
			int remainingLength = length;

			int rowCount;

			for (rowCount = rowStart; rowCount < rowEnd; rowCount++)
			{
				if (rowCount == rowStart)
				{
					if (length > (columnCount - columnStart))
					{
						length = columnCount - columnStart;
					}
					remainingLength -= length;
				}
				else
				{
					start = RowColumnToByteAddress(rowCount, 0);
					length = Math.Min(columnCount, remainingLength);
					remainingLength -= length;
				}


				this.telnet.Action.action_output("<Field>");
				this.telnet.Action.action_output("<Location position=\"" + start + "\" left=\"" + AddressToColumn(start) + "\" top=\"" + AddresstoRow(start) + "\" length=\"" + length + "\"/>");
				
				string temp = "";
				temp += "<Attributes Base=\"" + fa + "\"";

				if (FieldAttribute.IsProtected(fa))
				{
					temp += " Protected=\"true\"";
				}
				else
					temp += " Protected=\"false\"";
				if (FieldAttribute.IsZero(fa))
				{
					temp += " FieldType=\"Hidden\"";
				}
				else if (FieldAttribute.IsHigh(fa))
				{
					temp += " FieldType=\"High\"";
				}
				else if (FieldAttribute.IsIntense(fa))
				{
					temp += " FieldType=\"Intense\"";
				}
				else
				{
					if (ea.fg != 0)
					{
						temp += " Foreground=\"" + See.GetEfaUnformatted(See.XA_FOREGROUND, ea.fg) + "\"";
					}
					if (ea.bg != 0)
					{
						temp += " Background=\"" + See.GetEfaUnformatted(See.XA_BACKGROUND, ea.bg) + "\"";
					}
					if (ea.gr != 0)
					{
						temp += " Highlighting=\"" + See.GetEfaUnformatted(See.XA_HIGHLIGHTING, (byte)(ea.bg | 0xf0)) + "\"";
					}
					if ((ea.cs & ExtendedAttribute.CS_MASK) != 0)
					{
						temp += " Mask=\"" + See.GetEfaUnformatted(See.XA_CHARSET, (byte)((ea.cs & ExtendedAttribute.CS_MASK) | 0xf0)) + "\"";
					}
				}

				temp += "/>";
				this.telnet.Action.action_output(temp);
				this.DumpRangeXML(start, length, true, screenBuffer, rowCount, columnCount);
				this.telnet.Action.action_output("</Field>");
			}

			if (baddr <= address)
			{
				return -1;
			}
			return baddr;
		}


		//endif

		public void Dump()
		{
			int x, y;
			Console.WriteLine("dump starting.... Cursor@" + cursorAddress);
			for (y = 0; y < 24; y++)
			{
				string temp = "";
				for (x = 0; x < 80; x++)
				{
					byte ch = Tables.Cg2Ascii[screenBuffer[x + y * 80]];
					if (ch == 0)
					{
						temp += " ";
					}
					else
					{
						temp += "" + System.Convert.ToChar(ch);
					}
				}
				Console.WriteLine("{0:d2} {1}", y, temp);
			}
		}
		public bool AsciiAction(params object[] args)
		{
			return this.DumpFixed(args, "Ascii_action", true, screenBuffer, rowCount, columnCount, cursorAddress);
		}

		public bool AsciiFieldAction(params object[] args)
		{
			return DumpField("AsciiField_action", true);
		}

		public bool DumpXMLAction(params object[] args)
		{
			int pos = 0;
			//string name = "DumpXML_action";
			telnet.Action.action_output("<?xml version=\"1.0\"?>");// encoding=\"utf-16\"?>");
			telnet.Action.action_output("<XMLScreen xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
			telnet.Action.action_output("<CX>" + columnCount + "</CX>");
			telnet.Action.action_output("<CY>" + rowCount + "</CY>");
			if (this.isFormatted)
			{
				this.telnet.Action.action_output("<Formatted>true</Formatted>");
				ExtendedAttribute ea = new ExtendedAttribute();
				// CFCJR Mar 4,2008 : user tmcquire in post on www.open3270.net
				// says this do loop can hang up (pos never changes) in certain cases.
				// Added lastPos check to prevent this.
				int lastPos = -1;
				int cnt = 0;
				do
				{
					lastPos = pos;
					pos = DumpFieldAsXML(pos, ea);
					if (lastPos == pos)
					{
						cnt++;
					}
					else
					{
						cnt = 0;
					}
				}
				while (pos != -1 && cnt < 999);
			}
			else
			{
				this.telnet.Action.action_output("<Formatted>false</Formatted>");
			}

			//Output unformatted image anyway
			int i;
			this.telnet.Action.action_output("<Unformatted>");
			for (i = 0; i < rowCount; i++)
			{
				int start = RowColumnToByteAddress(i, 0);

				int length = columnCount;
				this.telnet.Action.action_output("<Text>");
				this.DumpRangeXML(start, length, true, screenBuffer, rowCount, columnCount);
				this.telnet.Action.action_output("</Text>");
			}
			this.telnet.Action.action_output("</Unformatted>");
			this.telnet.Action.action_output("</XMLScreen>");
			return true;
		}





	}
}
