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
	internal class SF
	{
		Telnet telnet;
		public SF(Telnet telnet)
		{
			this.telnet = telnet;
			NSR = supported_replies.Length;
		}

		/* Structured fields */
		public const byte SF_READ_PART	=0x01;	/* read partition */
		public const byte SF_RP_QUERY	=0x02;	/*  query */
		public const byte SF_RP_QLIST	=0x03;	/*  query list */
		public const byte  SF_RPQ_LIST	=0x00;	/*   QCODE list */
		public const byte  SF_RPQ_EQUIV	=0x40;	/*   equivalent+ QCODE list */
		public const byte  SF_RPQ_ALL	=0x80;	/*   all */
		public const byte SF_ERASE_RESET	=0x03;	/* erase/reset */
		public const byte SF_ER_DEFAULT	=0x00;	/*  default */
		public const byte SF_ER_ALT		=0x80;	/*  alternate */
		public const byte SF_SET_REPLY_MODE =0x09;	/* set reply mode */
		public const byte SF_SRM_FIELD	=0x00;	/*  field */
		public const byte SF_SRM_XFIELD	=0x01;	/*  extended field */
		public const byte SF_SRM_CHAR	=0x02;	/*  character */
		public const byte SF_CREATE_PART	=0x0c;	/* create partition */
		public const byte CPFLAG_PROT		=0x40;	/*  protected flag */
		public const byte CPFLAG_COPY_PS	=0x20;	/*  local copy to presentation space */
		public const byte CPFLAG_BASE		=0x07;	/*  base character set index */
		public const byte SF_OUTBOUND_DS	=0x40;	/* outbound 3270 DS */
		public const byte SF_TRANSFER_DATA	=0xd0;   /* file transfer open request */


		byte[] supported_replies = new byte[] {
												  See.QR_SUMMARY,		/* 0x80 */
												  See.QR_USABLE_AREA,		/* 0x81 */
												  See.QR_ALPHA_PART,		/* 0x84 */
												  See.QR_CHARSETS,		/* 0x85 */
												  See.QR_COLOR,		/* 0x86 */
												  See.QR_HIGHLIGHTING,	/* 0x87 */
												  See.QR_REPLY_MODES,		/* 0x88 */
												  See.QR_IMP_PART		/* 0xa6 */
											  };
		//private int byteNSR;//	(sizeof(supported_replies)/sizeof(byte))
		int NSR;
		bool qr_in_progress = false;
		byte reply_mode = 0;
//#define DEFAULT_CGEN	0x02b90000
//#define DEFAULT_CSET	0x00000025
		int cgcsgid = 0x02b90025; // the above 2 OR'd together

		

		/*
		 * Process a 3270 Write Structured Field command
		 */
		public PDS write_structured_field(byte[] buf, int start, int buflen)
		{
			int fieldlen;
			//byte *cp = buf;
			int cp = start;
			bool first = true;
			PDS rv = PDS.OkayNoOutput;
			PDS rv_this = PDS.OkayNoOutput;
			bool bad_cmd = false;

			/* Skip the WSF command itself. */
			cp++;
			buflen--;

			/* Interpret fields. */
			while (buflen > 0) 
			{

				if (first)
					telnet.Trace.trace_ds(" ");
				else
					telnet.Trace.trace_ds("< WriteStructuredField ");
				first = false;

				/* Pick out the field length. */
				if (buflen < 2) 
				{
					telnet.Trace.trace_ds("error: single byte at end of message\n");
					return (rv != PDS.OkayNoOutput) ? rv : PDS.BadCommand;
				}
				fieldlen = (buf[cp] << 8) + buf[cp+1];
				if (fieldlen == 0)
					fieldlen = buflen;
				if (fieldlen < 3) 
				{
					telnet.Trace.trace_ds("error: field length %d too small\n",
						fieldlen);
					return (rv != PDS.OkayNoOutput)  ? rv : PDS.BadCommand;
				}
				if ((int)fieldlen > buflen) 
				{
					telnet.Trace.trace_ds("error: field length %d exceeds remaining message length %d\n",
						fieldlen, buflen);
					return (rv != PDS.OkayNoOutput)  ? rv : PDS.BadCommand;
				}

				/* Dispatch on the ID. */
				switch (buf[cp+2]) 
				{
					case SF_READ_PART:
						telnet.Trace.trace_ds("ReadPartition");
						rv_this = sf_read_part(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_ERASE_RESET:
						telnet.Trace.trace_ds("EraseReset");
						rv_this = sf_erase_reset(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_SET_REPLY_MODE:
						telnet.Trace.trace_ds("SetReplyMode");
						rv_this = sf_set_reply_mode(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_CREATE_PART:
						telnet.Trace.trace_ds("CreatePartition");
						rv_this = sf_create_partition(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_OUTBOUND_DS:
						telnet.Trace.trace_ds("OutboundDS");
						rv_this = sf_outbound_ds(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					default:
						telnet.Trace.trace_ds("unsupported ID 0x%02x\n", buf[cp+2]);
						rv_this = PDS.BadCommand;
						break;
				}

				/*
				 * Accumulate errors or output flags.
				 * One real ugliness here is that if we have already
				 * generated some output, then we have already positively
				 * acknowledged the request, so if we fail here, we have no
				 * way to return the error indication.
				 */
				if (rv_this < 0)
					bad_cmd = true;
				else
				{
					rv = (PDS)(rv | rv_this);
				}

				/* Skip to the next field. */
				cp += fieldlen;
				buflen -= fieldlen;
			}
			if (first)
				telnet.Trace.trace_ds(" (null)\n");

			if (bad_cmd && rv==PDS.OkayNoOutput)
				return PDS.BadCommand;
			else
				return rv;
		}

		PDS sf_read_part(byte[] buf, int buflen)
		{
			byte partition;
			int i;
			int any = 0;
			string comma = "";
			NetBuffer obptr = null;

			if (buflen < 5) 
			{
				telnet.Trace.trace_ds(" error: field length %d too small\n", buflen);
				return PDS.BadCommand;
			}

			partition = buf[3];
			telnet.Trace.trace_ds("(0x%02x)", partition);

			switch (buf[4]) 
			{
				case SF_RP_QUERY:
					telnet.Trace.trace_ds(" Query");
					if (partition != 0xff) 
					{
						telnet.Trace.trace_ds(" error: illegal partition\n");
						return PDS.BadCommand;
					}
					telnet.Trace.trace_ds("\n");
					obptr = query_reply_start();
					for (i = 0; i < NSR; i++)
						do_query_reply(obptr, supported_replies[i]);
					query_reply_end(obptr);
					break;
				case SF_RP_QLIST:
					telnet.Trace.trace_ds(" QueryList ");
					if (partition != 0xff) 
					{
						telnet.Trace.trace_ds("error: illegal partition\n");
						return PDS.BadCommand;
					}
					if (buflen < 6) 
					{
						telnet.Trace.trace_ds("error: missing request type\n");
						return PDS.BadCommand;
					}
					obptr = query_reply_start();
				switch (buf[5]) 
				{
					case SF_RPQ_LIST:
						telnet.Trace.trace_ds("List(");
						if (buflen < 7) 
						{
							telnet.Trace.trace_ds(")\n");
							do_query_reply(obptr, See.QR_NULL);
						} 
						else 
						{
							for (i = 6; i < buflen; i++) 
							{
								telnet.Trace.trace_ds("%s%s", comma,
									See.GetQCodeode(buf[i]));
								comma = ",";
							}
							telnet.Trace.trace_ds(")\n");
							for (i = 0; i < NSR; i++) 
							{
								int pos;
								bool found = false;
								for (pos = 0; pos<buflen-6; pos++)
								{
									if (buf[pos+6]==supported_replies[i])
										found = true;
								}
								if (found)
								{
									do_query_reply(obptr, supported_replies[i]);
									any++;
								}
							}
							if (any==0) 
							{
								do_query_reply(obptr, See.QR_NULL);
							}
						}
						break;
					case SF_RPQ_EQUIV:
						telnet.Trace.trace_ds("Equivlent+List(");
						for (i = 6; i < buflen; i++) 
						{
							telnet.Trace.trace_ds("%s%s", comma, See.GetQCodeode(buf[i]));
							comma = ",";
						}
						telnet.Trace.trace_ds(")\n");
						for (i = 0; i < NSR; i++)
							do_query_reply(obptr, supported_replies[i]);
						break;
					case SF_RPQ_ALL:
						telnet.Trace.trace_ds("All\n");
						for (i = 0; i < NSR; i++)
							do_query_reply(obptr, supported_replies[i]);
						break;
					default:
						telnet.Trace.trace_ds("unknown request type 0x%02x\n", buf[5]);
						return PDS.BadCommand;
				}
					query_reply_end(obptr);
					break;
				case ControllerConstant.SNA_CMD_RMA:
					telnet.Trace.trace_ds(" ReadModifiedAll");
					if (partition != 0x00) 
					{
						telnet.Trace.trace_ds(" error: illegal partition\n");
						return PDS.BadCommand;
					}
					telnet.Trace.trace_ds("\n");
					telnet.Controller.ProcessReadModifiedCommand(AID.QReply, true);
					break;
				case ControllerConstant.SNA_CMD_RB:
					telnet.Trace.trace_ds(" ReadBuffer");
					if (partition != 0x00) 
					{
						telnet.Trace.trace_ds(" error: illegal partition\n");
						return PDS.BadCommand;
					}
					telnet.Trace.trace_ds("\n");
					telnet.Controller.ProcessReadBufferCommand(AID.QReply);
					break;
				case ControllerConstant.SNA_CMD_RM:
					telnet.Trace.trace_ds(" ReadModified");
					if (partition != 0x00) 
					{
						telnet.Trace.trace_ds(" error: illegal partition\n");
						return PDS.BadCommand;
					}
					telnet.Trace.trace_ds("\n");
					telnet.Controller.ProcessReadModifiedCommand(AID.QReply, false);
					break;
				default:
					telnet.Trace.trace_ds(" unknown type 0x%02x\n", buf[4]);
					return PDS.BadCommand;
			}
			return PDS.OkayOutput;
		}

		PDS sf_erase_reset(byte[] buf, int buflen)
		{
			if (buflen != 4) 
			{
				telnet.Trace.trace_ds(" error: wrong field length %d\n", buflen);
				return PDS.BadCommand;
			}

			switch (buf[3]) 
			{
				case SF_ER_DEFAULT:
					telnet.Trace.trace_ds(" Default\n");
					telnet.Controller.Erase(false);
					break;
				case SF_ER_ALT:
					telnet.Trace.trace_ds(" Alternate\n");
					telnet.Controller.Erase(true);
					break;
				default:
					telnet.Trace.trace_ds(" unknown type 0x%02x\n", buf[3]);
					return PDS.BadCommand;
			}
			return PDS.OkayNoOutput;
		}

		PDS sf_set_reply_mode(byte[] buf, int buflen)
		{
			byte partition;
			int i;
			string comma = "(";

			if (buflen < 5) 
			{
				telnet.Trace.trace_ds(" error: wrong field length %d\n", buflen);
				return PDS.BadCommand;
			}

			partition = buf[3];
			telnet.Trace.trace_ds("(0x%02x)", partition);
			if (partition != 0x00) 
			{
				telnet.Trace.trace_ds(" error: illegal partition\n");
				return PDS.BadCommand;
			}

			switch (buf[4]) 
			{
				case SF_SRM_FIELD:
					telnet.Trace.trace_ds(" Field\n");
					break;
				case SF_SRM_XFIELD:
					telnet.Trace.trace_ds(" ExtendedField\n");
					break;
				case SF_SRM_CHAR:
					telnet.Trace.trace_ds(" Character");
					break;
				default:
					telnet.Trace.trace_ds(" unknown mode 0x%02x\n", buf[4]);
					return PDS.BadCommand;
			}
			reply_mode = buf[4];
			if (buf[4] == SF_SRM_CHAR) 
			{
				telnet.Controller.CrmnAttribute = buflen - 5;
				for (i = 5; i < buflen; i++) 
				{
					telnet.Controller.CrmAttributes[i - 5] = buf[i];
					telnet.Trace.trace_ds("%s%s", comma, See.GetEfaOnly(buf[i]));
					comma = ",";
				}
				telnet.Trace.trace_ds("%s\n", (telnet.Controller.CrmnAttribute!=0) ? ")" : "");
			}
			return PDS.OkayNoOutput;
		}
		string[] bit4 = new string[] 
	{
		"0000", "0001", "0010", "0011",
		"0100", "0101", "0110", "0111",
		"1000", "1001", "1010", "1011",
		"1100", "1101", "1110", "1111"
	};

		PDS sf_create_partition(byte[] buf, int buflen)
		{
			byte pid;
			byte uom;		/* unit of measure */
			byte am;		/* addressing mode */
			byte flags;		/* flags */
			int h;		/* height of presentation space */
			int w;		/* width of presentation space */
			int rv;		/* viewport origin row */
			int cv;		/* viewport origin column */
			int hv;		/* viewport height */
			int wv;		/* viewport width */
			int rw;		/* window origin row */
			int cw;		/* window origin column */
			int rs;		/* scroll rows */
			/* hole */
			int pw;		/* character cell point width */
			int ph;		/* character cell point height */


			if (buflen > 3) 
			{
				telnet.Trace.trace_ds("(");

				/* Partition. */
				pid = buf[3];
				telnet.Trace.trace_ds("pid=0x%02x", pid);
				if (pid != 0x00) 
				{
					telnet.Trace.trace_ds(") error: illegal partition\n");
					return PDS.BadCommand;
				}
			} 
			else
				pid = 0x00;

			if (buflen > 4) 
			{
				uom = (byte)((buf[4] & 0xf0) >> 4);
				telnet.Trace.trace_ds(",uom=B'%s'", bit4[uom]);
				if (uom != 0x0 && uom != 0x02) 
				{
					telnet.Trace.trace_ds(") error: illegal units\n");
					return PDS.BadCommand;
				}
				am = (byte)(buf[4] & 0x0f);
				telnet.Trace.trace_ds(",am=B'%s'", bit4[am]);
				if (am > 0x2) 
				{
					telnet.Trace.trace_ds(") error: illegal a-mode\n");
					return PDS.BadCommand;
				}
			} 
			else 
			{
				uom = 0;
				am = 0;
			}

			if (buflen > 5) 
			{
				flags = buf[5];
				telnet.Trace.trace_ds(",flags=0x%02x", flags);
			} 
			else
				flags = 0;

			if (buflen > 7) 
			{
				h = GET16(buf, 6);//GET16(h, &buf[6]);
				telnet.Trace.trace_ds(",h=%d", h);
			} 
			else
				h = telnet.Controller.MaxRows;

			if (buflen > 9) 
			{
				w = GET16(buf, 8);//GET16(w, &buf[8]);
				telnet.Trace.trace_ds(",w=%d", w);
			} 
			else
				w = telnet.Controller.MaxColumns;

			if (buflen > 11) 
			{
				rv = GET16(buf,10);//GET16(rv, &buf[10]);
				telnet.Trace.trace_ds(",rv=%d", rv);
			} 
			else
				rv = 0;

			if (buflen > 13) 
			{
				cv = GET16(buf,12);//GET16(cv, &buf[12]);
				telnet.Trace.trace_ds(",cv=%d", cv);
			} 
			else
				cv = 0;

			if (buflen > 15) 
			{
				hv = GET16(buf, 14);//GET16(hv, &buf[14]);
				telnet.Trace.trace_ds(",hv=%d", hv);
			} 
			else
				hv = (h > telnet.Controller.MaxRows)? telnet.Controller.MaxRows: h;

			if (buflen > 17) 
			{
				wv = GET16(buf, 16);//GET16(wv, &buf[16]);
				telnet.Trace.trace_ds(",wv=%d", wv);
			} 
			else
				wv = (w > telnet.Controller.MaxColumns)? telnet.Controller.MaxColumns: w;

			if (buflen > 19) 
			{
				rw = GET16(buf,18);//GET16(rw, &buf[18]);
				telnet.Trace.trace_ds(",rw=%d", rw);
			} 
			else
				rw = 0;

			if (buflen > 21) 
			{
				cw = GET16(buf,20);//GET16(cw, &buf[20]);
				telnet.Trace.trace_ds(",cw=%d", cw);
			} 
			else
				cw = 0;

			if (buflen > 23) 
			{
				rs = GET16(buf,22);//GET16(rs, &buf[22]);
				telnet.Trace.trace_ds(",rs=%d", rs);
			} 
			else
				rs = (h > hv)? 1: 0;

			if (buflen > 27) 
			{
				pw = GET16(buf,26);//GET16(pw, &buf[26]);
				telnet.Trace.trace_ds(",pw=%d", pw);
			} 
			else
				pw = 7;//*char_width;

			if (buflen > 29) 
			{
				ph = GET16(buf, 28);//GET16(ph, &buf[28]);
				telnet.Trace.trace_ds(",ph=%d", ph);
			} 
			else
				ph = 7;//*char_height;
			telnet.Trace.trace_ds(")\n");

			telnet.Controller.SetCursorAddress(0);
			telnet.Controller.BufferAddress = 0;

			return PDS.OkayNoOutput;
		}

		PDS sf_outbound_ds(byte[] buf, int buflen)
		{
			if (buflen < 5) 
			{
				telnet.Trace.trace_ds(" error: field length %d too short\n", buflen);
				return PDS.BadCommand;
			}

			telnet.Trace.trace_ds("(0x%02x)", buf[3]);
			if (buf[3] != 0x00) 
			{
				telnet.Trace.trace_ds(" error: illegal partition 0x%0x\n", buf[3]);
				return PDS.BadCommand;
			}

			switch (buf[4]) 
			{
				case ControllerConstant.SNA_CMD_W:
					telnet.Trace.trace_ds(" Write");
					if (buflen > 5)
						telnet.Controller.ProcessWriteCommand(buf, 4, buflen-4, false);
					else
						telnet.Trace.trace_ds("\n");
					break;
				case ControllerConstant.SNA_CMD_EW:
					telnet.Trace.trace_ds(" EraseWrite");
					telnet.Controller.Erase(telnet.Controller.ScreenAlt);
					if (buflen > 5)
						telnet.Controller.ProcessWriteCommand(buf, 4, buflen-4, true);
					else
						telnet.Trace.trace_ds("\n");
					break;
				case ControllerConstant.SNA_CMD_EWA:
					telnet.Trace.trace_ds(" EraseWriteAlternate");
					telnet.Controller.Erase(telnet.Controller.ScreenAlt);
					if (buflen > 5)
						telnet.Controller.ProcessWriteCommand(buf, 4, buflen-4, true);
					else
						telnet.Trace.trace_ds("\n");
					break;
				case ControllerConstant.SNA_CMD_EAU:
					telnet.Trace.trace_ds(" EraseAllUnprotected\n");
					telnet.Controller.ProcessEraseAllUnprotectedCommand();
					break;
				default:
					telnet.Trace.trace_ds(" unknown type 0x%02x\n", buf[4]);
					return PDS.BadCommand;
			}
			return PDS.OkayNoOutput;
		}

		NetBuffer query_reply_start()
		{
			NetBuffer obptr = new NetBuffer();
			obptr.Add(AID.SF);
			qr_in_progress = true;
			return obptr;
		}

		void do_query_reply(NetBuffer obptr, byte code)
		{
			//int len;
			int i;
			string comma = "";
			int obptr0 = obptr.Index;//obptr - obuf;
			//byte *obptr_len;
			int num, denom;

			if (qr_in_progress) 
			{
				telnet.Trace.trace_ds("> StructuredField\n");
				qr_in_progress = false;
			}

			//space3270out(4);
			obptr.Add16(0); // Length - set later
			obptr.Add(See.SFID_QREPLY);
			obptr.Add(code);
			switch (code) 
			{
				case See.QR_CHARSETS:
					telnet.Trace.trace_ds("> QueryReply(CharacterSets)\n");
					//space3270out(23);
					obptr.Add(0x82);	/* flags: GE, CGCSGID present */
					obptr.Add(0x00);	/* more flags */
					obptr.Add(7);//*char_width);	/* SDW */
					obptr.Add(7);//*char_height);/* SDH */
					obptr.Add(0x00);	/* Load PS format types */
					obptr.Add(0x00);
					obptr.Add(0x00);
					obptr.Add(0x00);
					obptr.Add(0x07);	/* DL */
					obptr.Add(0x00);	/* SET 0: */
					obptr.Add(0x10);	/*  FLAGS: non-loadable, single-plane,
					     single-byte, no compare */
					obptr.Add(0x00);	/*  LCID */
					obptr.Add32(cgcsgid);	/*  CGCSGID */

                    // TODO: Missing font stuff for extended font information
					break;

				case See.QR_IMP_PART:
					telnet.Trace.trace_ds("> QueryReply(ImplicitPartition)\n");
//					space3270out(13);
					obptr.Add(0x0);		/* reserved */
					obptr.Add(0x0);
					obptr.Add(0x0b);	/* length of display size */
					obptr.Add(0x01);	/* "implicit partition size" */
					obptr.Add(0x00);	/* reserved */
					obptr.Add16(80);	/* implicit partition width */
					obptr.Add16(24);	/* implicit partition height */
					obptr.Add16(telnet.Controller.MaxColumns);	/* alternate height */
					obptr.Add16(telnet.Controller.MaxRows);	/* alternate width */
					break;

				case See.QR_NULL:
					telnet.Trace.trace_ds("> QueryReply(Null)\n");
					break;

				case See.QR_SUMMARY:
					telnet.Trace.trace_ds("> QueryReply(Summary(");
//					space3270out(NSR);
					for (i = 0; i < NSR; i++) 
					{
						telnet.Trace.trace_ds("%s%s", comma,
							See.GetQCodeode(supported_replies[i]));
						comma = ",";
						obptr.Add(supported_replies[i]);
					}
					telnet.Trace.trace_ds("))\n");
					break;

				case See.QR_USABLE_AREA:
					telnet.Trace.trace_ds("> QueryReply(UsableArea)\n");
//					space3270out(19);
					obptr.Add(0x01);	/* 12/14-bit addressing */
					obptr.Add(0x00);	/* no special character features */
					obptr.Add16(telnet.Controller.MaxColumns);	/* usable width */
					obptr.Add16(telnet.Controller.MaxRows);	/* usable height */
					obptr.Add(0x01);	/* units (mm) */
					num = 100;
					denom = 1;
					while (0==(num %2) && 0==(denom % 2)) 
					{
						num /= 2;
						denom /= 2;
					}
					obptr.Add16((int)num);	/* Xr numerator */
					obptr.Add16((int)denom); /* Xr denominator */
					num = 100; 
					denom = 1;
					while (0==(num %2) && 0==(denom % 2)) 
					{
						num /= 2;
						denom /= 2;
					}
					obptr.Add16((int)num);	/* Yr numerator */
					obptr.Add16((int)denom); /* Yr denominator */
					obptr.Add(7);//*char_width);	/* AW */
					obptr.Add(7);//*char_height);/* AH */
					obptr.Add16(telnet.Controller.MaxColumns*telnet.Controller.MaxRows);	/* buffer, questionable */
					break;

				case See.QR_COLOR:
					telnet.Trace.trace_ds("> QueryReply(Color)\n");
//					space3270out(4 + 2*15);
					obptr.Add(0x00);	/* no options */
					obptr.Add(telnet.Appres.color8? 8: 16); /* report on 8 or 16 colors */
					obptr.Add(0x00);	/* default color: */
					obptr.Add(0xf0 + See.COLOR_GREEN);	/*  green */
					for (i = 0xf1; i <= (telnet.Appres.color8? 0xf8: 0xff); i++) 
					{
						obptr.Add(i);
						if (telnet.Appres.m3279)
							obptr.Add(i);
						else
							obptr.Add(0x00);
					}
					break;

				case See.QR_HIGHLIGHTING:
					telnet.Trace.trace_ds("> QueryReply(Highlighting)\n");
//					space3270out(11);
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

				case See.QR_REPLY_MODES:
					telnet.Trace.trace_ds("> QueryReply(ReplyModes)\n");
//					space3270out(3);
					obptr.Add(SF_SRM_FIELD);
					obptr.Add(SF_SRM_XFIELD);
					obptr.Add(SF_SRM_CHAR);
					break;

				case See.QR_ALPHA_PART:
					telnet.Trace.trace_ds("> QueryReply(AlphanumericPartitions)\n");
//					space3270out(4);
					obptr.Add(0);		/* 1 partition */
					obptr.Add16(telnet.Controller.MaxRows*telnet.Controller.MaxColumns);	/* buffer space */
					obptr.Add(0);		/* no special features */
					break;


				default:
					return;	/* internal error */
			}
			obptr.Add16At(obptr0, obptr.Index-obptr0);
			//obptr_len = obuf + obptr0;
			//len = (obptr - obuf) - obptr0;
			//SET16(obptr_len, len);
		}

		void query_reply_end(NetBuffer obptr)
		{
			telnet.Output(obptr);
			telnet.Keyboard.ToggleEnterInhibitMode(true);
		}

		private byte[] CloneBytes(byte[] data, int start, int length)
		{
			byte[] result = new byte[data.Length-start];
			Array.Copy(data, start, result, 0,length);
			return result;

		}
		private int GET16(byte[] buf, int offset)
		{
			int val = buf[offset+1];
			val+=buf[offset]<<8;
			return val;
		}

	}
}
