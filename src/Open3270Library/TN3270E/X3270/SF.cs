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
	internal class StructuredField
	{
		Telnet telnet;
		public StructuredField(Telnet telnet)
		{
			this.telnet = telnet;
			NSR = SupportedReplies.Length;
		}


		string[] Bit4x4 = new string[] 
	{
		"0000", "0001", "0010", "0011",
		"0100", "0101", "0110", "0111",
		"1000", "1001", "1010", "1011",
		"1100", "1101", "1110", "1111"
	};

		static class Codes
		{
			public const byte ReadPartition = 0x01;	/* read partition */
			public const byte Query = 0x02;	/*  query */
			public const byte QueryList = 0x03;	/*  query list */
			public const byte QCodeList = 0x00;	/*   QCODE list */
			public const byte EquivQCodeList = 0x40;	/*   equivalent+ QCODE list */
			public const byte All = 0x80;	/*   all */
			public const byte EraseReset = 0x03;	/* erase/reset */
			public const byte Default = 0x00;	/*  default */
			public const byte Alternate = 0x80;	/*  alternate */
			public const byte SetReplyMode = 0x09;	/* set reply mode */
			public const byte Field = 0x00;	/*  field */
			public const byte ExtendedField = 0x01;	/*  extended field */
			public const byte Character = 0x02;	/*  character */
			public const byte CreatePartition = 0x0c;	/* create partition */
			public const byte ProtectedFlag = 0x40;	/*  protected flag */
			public const byte CopyPresentationSpace = 0x20;	/*  local copy to presentation space */
			public const byte BaseCharacterSetIndex = 0x07;	/*  base character set index */
			public const byte OutboundDS = 0x40;	/* outbound 3270 DS */
			public const byte FileTransferRequest = 0xd0;   /* file transfer open request */
		}

		byte[] SupportedReplies = new byte[] 
		{
		  See.QR_SUMMARY,		
		  See.QR_USABLE_AREA,	
		  See.QR_ALPHA_PART,	
		  See.QR_CHARSETS,		
		  See.QR_COLOR,
		  See.QR_HIGHLIGHTING,	
		  See.QR_REPLY_MODES,	
		  See.QR_IMP_PART		
		};


		int NSR;
		bool QrInProgress = false;
		byte replyMode = 0;
		//#define DEFAULT_CGEN	0x02b90000
		//#define DEFAULT_CSET	0x00000025

		int cgcsgid = 0x02b90025; // the above 2 OR'd together


		/// <summary>
		/// Process a 3270 Write Structured Field command
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="start"></param>
		/// <param name="bufferLength"></param>
		/// <returns></returns>
		public PDS WriteStructuredField(byte[] buffer, int start, int bufferLength)
		{
			int fieldlen;
			int cp = start;
			bool first = true;
			PDS rv = PDS.OkayNoOutput;
			PDS rvThis = PDS.OkayNoOutput;
			bool badCommand = false;

			// Skip the WSF command itself.
			cp++;
			bufferLength--;

			// Interpret fields.
			while (bufferLength > 0)
			{

				if (first)
				{
					telnet.Trace.trace_ds(" ");
				}
				else
				{
					telnet.Trace.trace_ds("< WriteStructuredField ");
				}

				first = false;

				// Pick out the field length.
				if (bufferLength < 2)
				{
					telnet.Trace.trace_ds("error: single byte at end of message\n");
					return (rv != PDS.OkayNoOutput) ? rv : PDS.BadCommand;
				}

				fieldlen = (buffer[cp] << 8) + buffer[cp + 1];

				if (fieldlen == 0)
				{
					fieldlen = bufferLength;
				}

				if (fieldlen < 3)
				{
					telnet.Trace.trace_ds("error: field length %d too small\n",
						fieldlen);
					return (rv != PDS.OkayNoOutput) ? rv : PDS.BadCommand;
				}

				if ((int)fieldlen > bufferLength)
				{
					telnet.Trace.trace_ds("error: field length %d exceeds remaining message length %d\n",
						fieldlen, bufferLength);
					return (rv != PDS.OkayNoOutput) ? rv : PDS.BadCommand;
				}

				// Dispatch on the ID.
				switch (buffer[cp + 2])
				{
					case Codes.ReadPartition:
						telnet.Trace.trace_ds("ReadPartition");
						rvThis = ReadPart(CloneBytes(buffer, cp, fieldlen), (int)fieldlen);
						break;
					case Codes.EraseReset:
						telnet.Trace.trace_ds("EraseReset");
						rvThis = EraseReset(CloneBytes(buffer, cp, fieldlen), (int)fieldlen);
						break;
					case Codes.SetReplyMode:
						telnet.Trace.trace_ds("SetReplyMode");
						rvThis = SetReplyMode(CloneBytes(buffer, cp, fieldlen), (int)fieldlen);
						break;
					case Codes.CreatePartition:
						telnet.Trace.trace_ds("CreatePartition");
						rvThis = CreatePartition(CloneBytes(buffer, cp, fieldlen), (int)fieldlen);
						break;
					case Codes.OutboundDS:
						telnet.Trace.trace_ds("OutboundDS");
						rvThis = OutboundDS(CloneBytes(buffer, cp, fieldlen), (int)fieldlen);
						break;
					default:
						telnet.Trace.trace_ds("unsupported ID 0x%02x\n", buffer[cp + 2]);
						rvThis = PDS.BadCommand;
						break;
				}

				//Accumulate errors or output flags.
				//One real ugliness here is that if we have already generated some output, then we have already positively
				//acknowledged the request, so if we fail here, we have no way to return the error indication.
				if (rvThis < 0)
				{
					badCommand = true;
				}
				else
				{
					rv = (PDS)(rv | rvThis);
				}

				// Skip to the next field.
				cp += fieldlen;
				bufferLength -= fieldlen;
			}

			if (first)
			{
				telnet.Trace.trace_ds(" (null)\n");
			}

			if (badCommand && rv == PDS.OkayNoOutput)
			{
				return PDS.BadCommand;
			}
			else
			{
				return rv;
			}
		}

		PDS ReadPart(byte[] buffer, int bufferLength)
		{
			byte partition;

			int any = 0;
			string comma = "";
			NetBuffer obptr = null;

			if (bufferLength < 5)
			{
				telnet.Trace.trace_ds(" error: field length %d too small\n", bufferLength);
				return PDS.BadCommand;
			}

			partition = buffer[3];
			this.telnet.Trace.trace_ds("(0x%02x)", partition);

			switch (buffer[4])
			{
				case Codes.Query:
					{
						this.telnet.Trace.trace_ds(" Query");

						if (partition != 0xff)
						{
							this.telnet.Trace.trace_ds(" error: illegal partition\n");
							return PDS.BadCommand;
						}

						this.telnet.Trace.trace_ds("\n");
						obptr = this.QueryReplyStart();

						for (int i = 0; i < NSR; i++)
						{
							this.DoQueryReplay(obptr, SupportedReplies[i]);
						}

						this.QueryReplyEnd(obptr);

						break;
					}
				case Codes.QueryList:
					{
						telnet.Trace.trace_ds(" QueryList ");

						if (partition != 0xff)
						{
							telnet.Trace.trace_ds("error: illegal partition\n");
							return PDS.BadCommand;
						}

						if (bufferLength < 6)
						{
							telnet.Trace.trace_ds("error: missing request type\n");
							return PDS.BadCommand;
						}

						obptr = QueryReplyStart();

						switch (buffer[5])
						{
							case Codes.QCodeList:
								{
									telnet.Trace.trace_ds("List(");

									if (bufferLength < 7)
									{
										telnet.Trace.trace_ds(")\n");
										DoQueryReplay(obptr, See.QR_NULL);
									}
									else
									{
										for (int i = 6; i < bufferLength; i++)
										{
											telnet.Trace.trace_ds("%s%s", comma, See.GetQCodeode(buffer[i]));
											comma = ",";
										}

										this.telnet.Trace.trace_ds(")\n");

										for (int i = 0; i < NSR; i++)
										{
											bool found = false;

											for (int pos = 0; pos < bufferLength - 6; pos++)
											{
												if (buffer[pos + 6] == this.SupportedReplies[i])
												{
													found = true;
												}
											}

											if (found)
											{
												this.DoQueryReplay(obptr, this.SupportedReplies[i]);
												any++;
											}
										}

										if (any == 0)
										{
											this.DoQueryReplay(obptr, See.QR_NULL);
										}
									}
									break;
								}
							case Codes.EquivQCodeList:
								{
									this.telnet.Trace.trace_ds("Equivlent+List(");
									for (int i = 6; i < bufferLength; i++)
									{
										this.telnet.Trace.trace_ds("%s%s", comma, See.GetQCodeode(buffer[i]));
										comma = ",";
									}
									this.telnet.Trace.trace_ds(")\n");
									for (int i = 0; i < NSR; i++)
										this.DoQueryReplay(obptr, this.SupportedReplies[i]);
									break;
								}
							case Codes.All:
								{
									this.telnet.Trace.trace_ds("All\n");
									for (int i = 0; i < NSR; i++)
										this.DoQueryReplay(obptr, this.SupportedReplies[i]);
									break;
								}
							default:
								{
									this.telnet.Trace.trace_ds("unknown request type 0x%02x\n", buffer[5]);
									return PDS.BadCommand;
								}
						}
						this.QueryReplyEnd(obptr);
						break;
					}
				case ControllerConstant.SNA_CMD_RMA:
					{
						this.telnet.Trace.trace_ds(" ReadModifiedAll");
						if (partition != 0x00)
						{
							this.telnet.Trace.trace_ds(" error: illegal partition\n");
							return PDS.BadCommand;
						}
						this.telnet.Trace.trace_ds("\n");
						this.telnet.Controller.ProcessReadModifiedCommand(AID.QReply, true);
						break;
					}
				case ControllerConstant.SNA_CMD_RB:
					{
						this.telnet.Trace.trace_ds(" ReadBuffer");
						if (partition != 0x00)
						{
							this.telnet.Trace.trace_ds(" error: illegal partition\n");
							return PDS.BadCommand;
						}
						this.telnet.Trace.trace_ds("\n");
						this.telnet.Controller.ProcessReadBufferCommand(AID.QReply);
						break;
					}
				case ControllerConstant.SNA_CMD_RM:
					{
						this.telnet.Trace.trace_ds(" ReadModified");
						if (partition != 0x00)
						{
							this.telnet.Trace.trace_ds(" error: illegal partition\n");
							return PDS.BadCommand;
						}
						this.telnet.Trace.trace_ds("\n");
						this.telnet.Controller.ProcessReadModifiedCommand(AID.QReply, false);
						break;
					}
				default:
					{
						this.telnet.Trace.trace_ds(" unknown type 0x%02x\n", buffer[4]);
						return PDS.BadCommand;
					}
			}
			return PDS.OkayOutput;
		}

		PDS EraseReset(byte[] buffer, int bufferLength)
		{
			if (bufferLength != 4)
			{
				telnet.Trace.trace_ds(" error: wrong field length %d\n", bufferLength);
				return PDS.BadCommand;
			}

			switch (buffer[3])
			{
				case Codes.Default:
					telnet.Trace.trace_ds(" Default\n");
					telnet.Controller.Erase(false);
					break;
				case Codes.Alternate:
					telnet.Trace.trace_ds(" Alternate\n");
					telnet.Controller.Erase(true);
					break;
				default:
					telnet.Trace.trace_ds(" unknown type 0x%02x\n", buffer[3]);
					return PDS.BadCommand;
			}
			return PDS.OkayNoOutput;
		}


		PDS SetReplyMode(byte[] buffer, int bufferLength)
		{
			byte partition;
			int i;
			string comma = "(";

			if (bufferLength < 5)
			{
				telnet.Trace.trace_ds(" error: wrong field length %d\n", bufferLength);
				return PDS.BadCommand;
			}

			partition = buffer[3];
			telnet.Trace.trace_ds("(0x%02x)", partition);

			if (partition != 0x00)
			{
				telnet.Trace.trace_ds(" error: illegal partition\n");
				return PDS.BadCommand;
			}

			switch (buffer[4])
			{
				case Codes.Field:
					telnet.Trace.trace_ds(" Field\n");
					break;
				case Codes.ExtendedField:
					telnet.Trace.trace_ds(" ExtendedField\n");
					break;
				case Codes.Character:
					telnet.Trace.trace_ds(" Character");
					break;
				default:
					telnet.Trace.trace_ds(" unknown mode 0x%02x\n", buffer[4]);
					return PDS.BadCommand;
			}

			replyMode = buffer[4];
			if (buffer[4] == Codes.Character)
			{
				telnet.Controller.CrmnAttribute = bufferLength - 5;
				for (i = 5; i < bufferLength; i++)
				{
					telnet.Controller.CrmAttributes[i - 5] = buffer[i];
					telnet.Trace.trace_ds("%s%s", comma, See.GetEfaOnly(buffer[i]));
					comma = ",";
				}
				telnet.Trace.trace_ds("%s\n", (telnet.Controller.CrmnAttribute != 0) ? ")" : "");
			}
			return PDS.OkayNoOutput;
		}




		PDS CreatePartition(byte[] buffer, int bufferLength)
		{
			byte pid;
			byte unitOfMeasure;		/* unit of measure */
			byte addressingMode;		/* addressing mode */
			byte flags;		/* flags */
			int presentationHeight;		/* height of presentation space */
			int presentationWidth;		/* width of presentation space */
			int viewportOriginRow;		/* viewport origin row */
			int viewportOriginColumn;		/* viewport origin column */
			int viewportHeight;		/* viewport height */
			int viewportWidth;		/* viewport width */
			int windowOriginRow;		/* window origin row */
			int windowOriginColumn;		/* window origin column */
			int scrollRows;		/* scroll rows */

			int charCellPointWidth;		/* character cell point width */
			int charCellPointHeight;		/* character cell point height */


			if (bufferLength > 3)
			{
				this.telnet.Trace.trace_ds("(");

				// Partition
				pid = buffer[3];
				telnet.Trace.trace_ds("pid=0x%02x", pid);
				if (pid != 0x00)
				{
					telnet.Trace.trace_ds(") error: illegal partition\n");
					return PDS.BadCommand;
				}
			}
			else
			{
				pid = 0x00;
			}

			if (bufferLength > 4)
			{
				unitOfMeasure = (byte)((buffer[4] & 0xf0) >> 4);
				telnet.Trace.trace_ds(",uom=B'%s'", Bit4x4[unitOfMeasure]);

				if (unitOfMeasure != 0x0 && unitOfMeasure != 0x02)
				{
					telnet.Trace.trace_ds(") error: illegal units\n");
					return PDS.BadCommand;
				}

				addressingMode = (byte)(buffer[4] & 0x0f);
				telnet.Trace.trace_ds(",am=B'%s'", Bit4x4[addressingMode]);

				if (addressingMode > 0x2)
				{
					telnet.Trace.trace_ds(") error: illegal a-mode\n");
					return PDS.BadCommand;
				}
			}
			else
			{
				unitOfMeasure = 0;
				addressingMode = 0;
			}

			if (bufferLength > 5)
			{
				flags = buffer[5];
				telnet.Trace.trace_ds(",flags=0x%02x", flags);
			}
			else
			{
				flags = 0;
			}

			if (bufferLength > 7)
			{
				presentationHeight = Get16(buffer, 6);
				telnet.Trace.trace_ds(",h=%d", presentationHeight);
			}
			else
			{
				presentationHeight = telnet.Controller.MaxRows;
			}

			if (bufferLength > 9)
			{
				presentationWidth = Get16(buffer, 8);
				telnet.Trace.trace_ds(",w=%d", presentationWidth);
			}
			else
			{
				presentationWidth = telnet.Controller.MaxColumns;
			}

			if (bufferLength > 11)
			{
				viewportOriginRow = Get16(buffer, 10);
				telnet.Trace.trace_ds(",rv=%d", viewportOriginRow);
			}
			else
			{
				viewportOriginRow = 0;
			}

			if (bufferLength > 13)
			{
				viewportOriginColumn = Get16(buffer, 12);
				telnet.Trace.trace_ds(",cv=%d", viewportOriginColumn);
			}
			else
			{
				viewportOriginColumn = 0;
			}

			if (bufferLength > 15)
			{
				viewportHeight = Get16(buffer, 14);
				telnet.Trace.trace_ds(",hv=%d", viewportHeight);
			}
			else
			{
				viewportHeight = (presentationHeight > telnet.Controller.MaxRows) ? telnet.Controller.MaxRows : presentationHeight;
			}

			if (bufferLength > 17)
			{
				viewportWidth = Get16(buffer, 16);
				telnet.Trace.trace_ds(",wv=%d", viewportWidth);
			}
			else
			{
				viewportWidth = (presentationWidth > telnet.Controller.MaxColumns) ? telnet.Controller.MaxColumns : presentationWidth;
			}

			if (bufferLength > 19)
			{
				windowOriginRow = Get16(buffer, 18);
				telnet.Trace.trace_ds(",rw=%d", windowOriginRow);
			}
			else
			{
				windowOriginRow = 0;
			}

			if (bufferLength > 21)
			{
				windowOriginColumn = Get16(buffer, 20);
				telnet.Trace.trace_ds(",cw=%d", windowOriginColumn);
			}
			else
			{
				windowOriginColumn = 0;
			}

			if (bufferLength > 23)
			{
				scrollRows = Get16(buffer, 22);
				telnet.Trace.trace_ds(",rs=%d", scrollRows);
			}
			else
			{
				scrollRows = (presentationHeight > viewportHeight) ? 1 : 0;
			}

			if (bufferLength > 27)
			{
				charCellPointWidth = Get16(buffer, 26);
				telnet.Trace.trace_ds(",pw=%d", charCellPointWidth);
			}
			else
			{
				charCellPointWidth = 7;
			}

			if (bufferLength > 29)
			{
				charCellPointHeight = Get16(buffer, 28);
				telnet.Trace.trace_ds(",ph=%d", charCellPointHeight);
			}
			else
			{
				charCellPointHeight = 7;
			}


			telnet.Trace.trace_ds(")\n");

			telnet.Controller.SetCursorAddress(0);
			telnet.Controller.BufferAddress = 0;

			return PDS.OkayNoOutput;
		}

		PDS OutboundDS(byte[] buffer, int bufferLength)
		{
			if (bufferLength < 5)
			{
				this.telnet.Trace.trace_ds(" error: field length %d too short\n", bufferLength);
				return PDS.BadCommand;
			}

			this.telnet.Trace.trace_ds("(0x%02x)", buffer[3]);
			if (buffer[3] != 0x00)
			{
				this.telnet.Trace.trace_ds(" error: illegal partition 0x%0x\n", buffer[3]);
				return PDS.BadCommand;
			}

			switch (buffer[4])
			{
				case ControllerConstant.SNA_CMD_W:
					{
						this.telnet.Trace.trace_ds(" Write");

						if (bufferLength > 5)
						{
							this.telnet.Controller.ProcessWriteCommand(buffer, 4, bufferLength - 4, false);
						}
						else
						{
							this.telnet.Trace.trace_ds("\n");
						}
						break;
					}
				case ControllerConstant.SNA_CMD_EW:
					{
						this.telnet.Trace.trace_ds(" EraseWrite");
						this.telnet.Controller.Erase(telnet.Controller.ScreenAlt);

						if (bufferLength > 5)
						{
							this.telnet.Controller.ProcessWriteCommand(buffer, 4, bufferLength - 4, true);
						}
						else
						{
							this.telnet.Trace.trace_ds("\n");
						}
						break;
					}
				case ControllerConstant.SNA_CMD_EWA:
					{
						this.telnet.Trace.trace_ds(" EraseWriteAlternate");
						this.telnet.Controller.Erase(telnet.Controller.ScreenAlt);
						if (bufferLength > 5)
						{
							this.telnet.Controller.ProcessWriteCommand(buffer, 4, bufferLength - 4, true);
						}
						else
						{
							this.telnet.Trace.trace_ds("\n");
						}
						break;
					}
				case ControllerConstant.SNA_CMD_EAU:
					{
						this.telnet.Trace.trace_ds(" EraseAllUnprotected\n");
						this.telnet.Controller.ProcessEraseAllUnprotectedCommand();
						break;
					}
				default:
					{
						this.telnet.Trace.trace_ds(" unknown type 0x%02x\n", buffer[4]);
						return PDS.BadCommand;
					}
			}
			return PDS.OkayNoOutput;
		}

		NetBuffer QueryReplyStart()
		{
			NetBuffer obptr = new NetBuffer();
			obptr.Add(AID.SF);
			this.QrInProgress = true;
			return obptr;
		}

		void DoQueryReplay(NetBuffer obptr, byte code)
		{

			int i;
			string comma = "";
			int obptr0 = obptr.Index;
			int num, denom;

			if (this.QrInProgress)
			{
				this.telnet.Trace.trace_ds("> StructuredField\n");
				this.QrInProgress = false;
			}

			obptr.Add16(0);
			obptr.Add(See.SFID_QREPLY);
			obptr.Add(code);

			switch (code)
			{
				case See.QR_CHARSETS:
					{
						this.telnet.Trace.trace_ds("> QueryReply(CharacterSets)\n");

						obptr.Add(0x82);	/* flags: GE, CGCSGID present */
						obptr.Add(0x00);	/* more flags */
						obptr.Add(7);		/* SDW */
						obptr.Add(7);		/* SDH */
						obptr.Add(0x00);	/* Load PS format types */
						obptr.Add(0x00);
						obptr.Add(0x00);
						obptr.Add(0x00);
						obptr.Add(0x07);	/* DL */
						obptr.Add(0x00);	/* SET 0: */
						obptr.Add(0x10);	/*  FLAGS: non-loadable, single-plane, single-byte, no compare */
						obptr.Add(0x00);	/*  LCID */
						obptr.Add32(cgcsgid);	/*  CGCSGID */

						// TODO: Missing font stuff for extended font information
						break;
					}
				case See.QR_IMP_PART:
					{
						this.telnet.Trace.trace_ds("> QueryReply(ImplicitPartition)\n");
						obptr.Add(0x0);		/* reserved */
						obptr.Add(0x0);
						obptr.Add(0x0b);	/* length of display size */
						obptr.Add(0x01);	/* "implicit partition size" */
						obptr.Add(0x00);	/* reserved */
						obptr.Add16(80);	/* implicit partition width */
						obptr.Add16(24);	/* implicit partition height */
						obptr.Add16(this.telnet.Controller.MaxColumns);	/* alternate height */
						obptr.Add16(this.telnet.Controller.MaxRows);	/* alternate width */
						break;
					}
				case See.QR_NULL:
					{
						this.telnet.Trace.trace_ds("> QueryReply(Null)\n");
						break;
					}
				case See.QR_SUMMARY:
					{
						this.telnet.Trace.trace_ds("> QueryReply(Summary(");
						for (i = 0; i < NSR; i++)
						{
							this.telnet.Trace.trace_ds("%s%s", comma, See.GetQCodeode(this.SupportedReplies[i]));
							comma = ",";
							obptr.Add(this.SupportedReplies[i]);
						}
						this.telnet.Trace.trace_ds("))\n");
						break;
					}
				case See.QR_USABLE_AREA:
					{
						this.telnet.Trace.trace_ds("> QueryReply(UsableArea)\n");
						obptr.Add(0x01);	/* 12/14-bit addressing */
						obptr.Add(0x00);	/* no special character features */
						obptr.Add16(this.telnet.Controller.MaxColumns);	/* usable width */
						obptr.Add16(this.telnet.Controller.MaxRows);	/* usable height */
						obptr.Add(0x01);	/* units (mm) */
						num = 100;
						denom = 1;
						while (0 == (num % 2) && 0 == (denom % 2))
						{
							num /= 2;
							denom /= 2;
						}
						obptr.Add16((int)num);	/* Xr numerator */
						obptr.Add16((int)denom); /* Xr denominator */
						num = 100;
						denom = 1;
						while (0 == (num % 2) && 0 == (denom % 2))
						{
							num /= 2;
							denom /= 2;
						}
						obptr.Add16((int)num);	/* Yr numerator */
						obptr.Add16((int)denom); /* Yr denominator */
						obptr.Add(7);			/* AW */
						obptr.Add(7);			/* AH */
						obptr.Add16(this.telnet.Controller.MaxColumns * this.telnet.Controller.MaxRows);	/* buffer, questionable */
						break;
					}
				case See.QR_COLOR:
					{
						this.telnet.Trace.trace_ds("> QueryReply(Color)\n");
						obptr.Add(0x00);	/* no options */
						obptr.Add(this.telnet.Appres.color8 ? 8 : 16); /* report on 8 or 16 colors */
						obptr.Add(0x00);	/* default color: */
						obptr.Add(0xf0 + See.COLOR_GREEN);	/*  green */
						for (i = 0xf1; i <= (this.telnet.Appres.color8 ? 0xf8 : 0xff); i++)
						{
							obptr.Add(i);
							if (this.telnet.Appres.m3279)
							{
								obptr.Add(i);
							}
							else
							{
								obptr.Add(0x00);
							}
						}
						break;
					}
				case See.QR_HIGHLIGHTING:
					{
						this.telnet.Trace.trace_ds("> QueryReply(Highlighting)\n");
						obptr.Add(5);		/* report on 5 pairs */
						obptr.Add(See.XAH_DEFAULT);	/* default: */
						obptr.Add(See.XAH_NORMAL);	/*  normal */
						obptr.Add(See.XAH_BLINK);	/* blink: */
						obptr.Add(See.XAH_BLINK);	/*  blink */
						obptr.Add(See.XAH_REVERSE);	/* reverse: */
						obptr.Add(See.XAH_REVERSE);	/*  reverse */
						obptr.Add(See.XAH_UNDERSCORE); /* underscore: */
						obptr.Add(See.XAH_UNDERSCORE); /*  underscore */
						obptr.Add(See.XAH_INTENSIFY); /* intensify: */
						obptr.Add(See.XAH_INTENSIFY); /*  intensify */
						break;
					}
				case See.QR_REPLY_MODES:
					{
						this.telnet.Trace.trace_ds("> QueryReply(ReplyModes)\n");
						obptr.Add(Codes.Field);
						obptr.Add(Codes.ExtendedField);
						obptr.Add(Codes.Character);
						break;
					}
				case See.QR_ALPHA_PART:
					{
						this.telnet.Trace.trace_ds("> QueryReply(AlphanumericPartitions)\n");
						obptr.Add(0);		/* 1 partition */
						obptr.Add16(this.telnet.Controller.MaxRows * this.telnet.Controller.MaxColumns);	/* buffer space */
						obptr.Add(0);		/* no special features */
						break;
					}

				default:
					{
						// Internal error
						return;
					}
			}
			obptr.Add16At(obptr0, obptr.Index - obptr0);

		}

		void QueryReplyEnd(NetBuffer obptr)
		{
			this.telnet.Output(obptr);
			this.telnet.Keyboard.ToggleEnterInhibitMode(true);
		}

		private byte[] CloneBytes(byte[] data, int start, int length)
		{
			byte[] result = new byte[data.Length - start];
			Array.Copy(data, start, result, 0, length);
			return result;

		}
		private int Get16(byte[] buf, int offset)
		{
			int val = buf[offset + 1];
			val += buf[offset] << 8;
			return val;
		}

	}
}
