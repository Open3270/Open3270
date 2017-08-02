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
	internal class sf
	{
		Telnet telnet;
		public sf(Telnet telnet)
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
												  see.QR_SUMMARY,		/* 0x80 */
												  see.QR_USABLE_AREA,		/* 0x81 */
												  see.QR_ALPHA_PART,		/* 0x84 */
												  see.QR_CHARSETS,		/* 0x85 */
												  see.QR_COLOR,		/* 0x86 */
												  see.QR_HIGHLIGHTING,	/* 0x87 */
												  see.QR_REPLY_MODES,		/* 0x88 */
												  see.QR_IMP_PART		/* 0xa6 */
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
		public pds write_structured_field(byte[] buf, int start, int buflen)
		{
			int fieldlen;
			//byte *cp = buf;
			int cp = start;
			bool first = true;
			pds rv = pds.PDS_OKAY_NO_OUTPUT;
			pds rv_this = pds.PDS_OKAY_NO_OUTPUT;
			bool bad_cmd = false;

			/* Skip the WSF command itself. */
			cp++;
			buflen--;

			/* Interpret fields. */
			while (buflen > 0) 
			{

				if (first)
					telnet.trace.trace_ds(" ");
				else
					telnet.trace.trace_ds("< WriteStructuredField ");
				first = false;

				/* Pick out the field length. */
				if (buflen < 2) 
				{
					telnet.trace.trace_ds("error: single byte at end of message\n");
					return (rv != pds.PDS_OKAY_NO_OUTPUT) ? rv : pds.PDS_BAD_CMD;
				}
				fieldlen = (buf[cp] << 8) + buf[cp+1];
				if (fieldlen == 0)
					fieldlen = buflen;
				if (fieldlen < 3) 
				{
					telnet.trace.trace_ds("error: field length %d too small\n",
						fieldlen);
					return (rv != pds.PDS_OKAY_NO_OUTPUT)  ? rv : pds.PDS_BAD_CMD;
				}
				if ((int)fieldlen > buflen) 
				{
					telnet.trace.trace_ds("error: field length %d exceeds remaining message length %d\n",
						fieldlen, buflen);
					return (rv != pds.PDS_OKAY_NO_OUTPUT)  ? rv : pds.PDS_BAD_CMD;
				}

				/* Dispatch on the ID. */
				switch (buf[cp+2]) 
				{
					case SF_READ_PART:
						telnet.trace.trace_ds("ReadPartition");
						rv_this = sf_read_part(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_ERASE_RESET:
						telnet.trace.trace_ds("EraseReset");
						rv_this = sf_erase_reset(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_SET_REPLY_MODE:
						telnet.trace.trace_ds("SetReplyMode");
						rv_this = sf_set_reply_mode(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_CREATE_PART:
						telnet.trace.trace_ds("CreatePartition");
						rv_this = sf_create_partition(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					case SF_OUTBOUND_DS:
						telnet.trace.trace_ds("OutboundDS");
						rv_this = sf_outbound_ds(CloneBytes(buf, cp, fieldlen), (int)fieldlen);
						break;
					default:
						telnet.trace.trace_ds("unsupported ID 0x%02x\n", buf[cp+2]);
						rv_this = pds.PDS_BAD_CMD;
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
					rv = (pds)(rv | rv_this);
				}

				/* Skip to the next field. */
				cp += fieldlen;
				buflen -= fieldlen;
			}
			if (first)
				telnet.trace.trace_ds(" (null)\n");

			if (bad_cmd && rv==pds.PDS_OKAY_NO_OUTPUT)
				return pds.PDS_BAD_CMD;
			else
				return rv;
		}

		pds sf_read_part(byte[] buf, int buflen)
		{
			byte partition;
			int i;
			int any = 0;
			string comma = "";
			NetBuffer obptr = null;

			if (buflen < 5) 
			{
				telnet.trace.trace_ds(" error: field length %d too small\n", buflen);
				return pds.PDS_BAD_CMD;
			}

			partition = buf[3];
			telnet.trace.trace_ds("(0x%02x)", partition);

			switch (buf[4]) 
			{
				case SF_RP_QUERY:
					telnet.trace.trace_ds(" Query");
					if (partition != 0xff) 
					{
						telnet.trace.trace_ds(" error: illegal partition\n");
						return pds.PDS_BAD_CMD;
					}
					telnet.trace.trace_ds("\n");
					obptr = query_reply_start();
					for (i = 0; i < NSR; i++)
						do_query_reply(obptr, supported_replies[i]);
					query_reply_end(obptr);
					break;
				case SF_RP_QLIST:
					telnet.trace.trace_ds(" QueryList ");
					if (partition != 0xff) 
					{
						telnet.trace.trace_ds("error: illegal partition\n");
						return pds.PDS_BAD_CMD;
					}
					if (buflen < 6) 
					{
						telnet.trace.trace_ds("error: missing request type\n");
						return pds.PDS_BAD_CMD;
					}
					obptr = query_reply_start();
				switch (buf[5]) 
				{
					case SF_RPQ_LIST:
						telnet.trace.trace_ds("List(");
						if (buflen < 7) 
						{
							telnet.trace.trace_ds(")\n");
							do_query_reply(obptr, see.QR_NULL);
						} 
						else 
						{
							for (i = 6; i < buflen; i++) 
							{
								telnet.trace.trace_ds("%s%s", comma,
									see.see_qcode(buf[i]));
								comma = ",";
							}
							telnet.trace.trace_ds(")\n");
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
								do_query_reply(obptr, see.QR_NULL);
							}
						}
						break;
					case SF_RPQ_EQUIV:
						telnet.trace.trace_ds("Equivlent+List(");
						for (i = 6; i < buflen; i++) 
						{
							telnet.trace.trace_ds("%s%s", comma, see.see_qcode(buf[i]));
							comma = ",";
						}
						telnet.trace.trace_ds(")\n");
						for (i = 0; i < NSR; i++)
							do_query_reply(obptr, supported_replies[i]);
						break;
					case SF_RPQ_ALL:
						telnet.trace.trace_ds("All\n");
						for (i = 0; i < NSR; i++)
							do_query_reply(obptr, supported_replies[i]);
						break;
					default:
						telnet.trace.trace_ds("unknown request type 0x%02x\n", buf[5]);
						return pds.PDS_BAD_CMD;
				}
					query_reply_end(obptr);
					break;
				case Ctlr.SNA_CMD_RMA:
					telnet.trace.trace_ds(" ReadModifiedAll");
					if (partition != 0x00) 
					{
						telnet.trace.trace_ds(" error: illegal partition\n");
						return pds.PDS_BAD_CMD;
					}
					telnet.trace.trace_ds("\n");
					telnet.tnctlr.ctlr_read_modified(AID.AID_QREPLY, true);
					break;
				case Ctlr.SNA_CMD_RB:
					telnet.trace.trace_ds(" ReadBuffer");
					if (partition != 0x00) 
					{
						telnet.trace.trace_ds(" error: illegal partition\n");
						return pds.PDS_BAD_CMD;
					}
					telnet.trace.trace_ds("\n");
					telnet.tnctlr.ctlr_read_buffer(AID.AID_QREPLY);
					break;
				case Ctlr.SNA_CMD_RM:
					telnet.trace.trace_ds(" ReadModified");
					if (partition != 0x00) 
					{
						telnet.trace.trace_ds(" error: illegal partition\n");
						return pds.PDS_BAD_CMD;
					}
					telnet.trace.trace_ds("\n");
					telnet.tnctlr.ctlr_read_modified(AID.AID_QREPLY, false);
					break;
				default:
					telnet.trace.trace_ds(" unknown type 0x%02x\n", buf[4]);
					return pds.PDS_BAD_CMD;
			}
			return pds.PDS_OKAY_OUTPUT;
		}

		pds sf_erase_reset(byte[] buf, int buflen)
		{
			if (buflen != 4) 
			{
				telnet.trace.trace_ds(" error: wrong field length %d\n", buflen);
				return pds.PDS_BAD_CMD;
			}

			switch (buf[3]) 
			{
				case SF_ER_DEFAULT:
					telnet.trace.trace_ds(" Default\n");
					telnet.tnctlr.ctlr_erase(false);
					break;
				case SF_ER_ALT:
					telnet.trace.trace_ds(" Alternate\n");
					telnet.tnctlr.ctlr_erase(true);
					break;
				default:
					telnet.trace.trace_ds(" unknown type 0x%02x\n", buf[3]);
					return pds.PDS_BAD_CMD;
			}
			return pds.PDS_OKAY_NO_OUTPUT;
		}

		pds sf_set_reply_mode(byte[] buf, int buflen)
		{
			byte partition;
			int i;
			string comma = "(";

			if (buflen < 5) 
			{
				telnet.trace.trace_ds(" error: wrong field length %d\n", buflen);
				return pds.PDS_BAD_CMD;
			}

			partition = buf[3];
			telnet.trace.trace_ds("(0x%02x)", partition);
			if (partition != 0x00) 
			{
				telnet.trace.trace_ds(" error: illegal partition\n");
				return pds.PDS_BAD_CMD;
			}

			switch (buf[4]) 
			{
				case SF_SRM_FIELD:
					telnet.trace.trace_ds(" Field\n");
					break;
				case SF_SRM_XFIELD:
					telnet.trace.trace_ds(" ExtendedField\n");
					break;
				case SF_SRM_CHAR:
					telnet.trace.trace_ds(" Character");
					break;
				default:
					telnet.trace.trace_ds(" unknown mode 0x%02x\n", buf[4]);
					return pds.PDS_BAD_CMD;
			}
			reply_mode = buf[4];
			if (buf[4] == SF_SRM_CHAR) 
			{
				telnet.tnctlr.crm_nattr = buflen - 5;
				for (i = 5; i < buflen; i++) 
				{
					telnet.tnctlr.crm_attr[i - 5] = buf[i];
					telnet.trace.trace_ds("%s%s", comma, see.see_efa_only(buf[i]));
					comma = ",";
				}
				telnet.trace.trace_ds("%s\n", (telnet.tnctlr.crm_nattr!=0) ? ")" : "");
			}
			return pds.PDS_OKAY_NO_OUTPUT;
		}
		string[] bit4 = new string[] 
	{
		"0000", "0001", "0010", "0011",
		"0100", "0101", "0110", "0111",
		"1000", "1001", "1010", "1011",
		"1100", "1101", "1110", "1111"
	};

		pds sf_create_partition(byte[] buf, int buflen)
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
				telnet.trace.trace_ds("(");

				/* Partition. */
				pid = buf[3];
				telnet.trace.trace_ds("pid=0x%02x", pid);
				if (pid != 0x00) 
				{
					telnet.trace.trace_ds(") error: illegal partition\n");
					return pds.PDS_BAD_CMD;
				}
			} 
			else
				pid = 0x00;

			if (buflen > 4) 
			{
				uom = (byte)((buf[4] & 0xf0) >> 4);
				telnet.trace.trace_ds(",uom=B'%s'", bit4[uom]);
				if (uom != 0x0 && uom != 0x02) 
				{
					telnet.trace.trace_ds(") error: illegal units\n");
					return pds.PDS_BAD_CMD;
				}
				am = (byte)(buf[4] & 0x0f);
				telnet.trace.trace_ds(",am=B'%s'", bit4[am]);
				if (am > 0x2) 
				{
					telnet.trace.trace_ds(") error: illegal a-mode\n");
					return pds.PDS_BAD_CMD;
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
				telnet.trace.trace_ds(",flags=0x%02x", flags);
			} 
			else
				flags = 0;

			if (buflen > 7) 
			{
				h = GET16(buf, 6);//GET16(h, &buf[6]);
				telnet.trace.trace_ds(",h=%d", h);
			} 
			else
				h = telnet.tnctlr.maxROWS;

			if (buflen > 9) 
			{
				w = GET16(buf, 8);//GET16(w, &buf[8]);
				telnet.trace.trace_ds(",w=%d", w);
			} 
			else
				w = telnet.tnctlr.maxCOLS;

			if (buflen > 11) 
			{
				rv = GET16(buf,10);//GET16(rv, &buf[10]);
				telnet.trace.trace_ds(",rv=%d", rv);
			} 
			else
				rv = 0;

			if (buflen > 13) 
			{
				cv = GET16(buf,12);//GET16(cv, &buf[12]);
				telnet.trace.trace_ds(",cv=%d", cv);
			} 
			else
				cv = 0;

			if (buflen > 15) 
			{
				hv = GET16(buf, 14);//GET16(hv, &buf[14]);
				telnet.trace.trace_ds(",hv=%d", hv);
			} 
			else
				hv = (h > telnet.tnctlr.maxROWS)? telnet.tnctlr.maxROWS: h;

			if (buflen > 17) 
			{
				wv = GET16(buf, 16);//GET16(wv, &buf[16]);
				telnet.trace.trace_ds(",wv=%d", wv);
			} 
			else
				wv = (w > telnet.tnctlr.maxCOLS)? telnet.tnctlr.maxCOLS: w;

			if (buflen > 19) 
			{
				rw = GET16(buf,18);//GET16(rw, &buf[18]);
				telnet.trace.trace_ds(",rw=%d", rw);
			} 
			else
				rw = 0;

			if (buflen > 21) 
			{
				cw = GET16(buf,20);//GET16(cw, &buf[20]);
				telnet.trace.trace_ds(",cw=%d", cw);
			} 
			else
				cw = 0;

			if (buflen > 23) 
			{
				rs = GET16(buf,22);//GET16(rs, &buf[22]);
				telnet.trace.trace_ds(",rs=%d", rs);
			} 
			else
				rs = (h > hv)? 1: 0;

			if (buflen > 27) 
			{
				pw = GET16(buf,26);//GET16(pw, &buf[26]);
				telnet.trace.trace_ds(",pw=%d", pw);
			} 
			else
				pw = 7;//*char_width;

			if (buflen > 29) 
			{
				ph = GET16(buf, 28);//GET16(ph, &buf[28]);
				telnet.trace.trace_ds(",ph=%d", ph);
			} 
			else
				ph = 7;//*char_height;
			telnet.trace.trace_ds(")\n");

			telnet.tnctlr.cursor_move(0);
			telnet.tnctlr.buffer_addr = 0;

			return pds.PDS_OKAY_NO_OUTPUT;
		}

		pds sf_outbound_ds(byte[] buf, int buflen)
		{
			if (buflen < 5) 
			{
				telnet.trace.trace_ds(" error: field length %d too short\n", buflen);
				return pds.PDS_BAD_CMD;
			}

			telnet.trace.trace_ds("(0x%02x)", buf[3]);
			if (buf[3] != 0x00) 
			{
				telnet.trace.trace_ds(" error: illegal partition 0x%0x\n", buf[3]);
				return pds.PDS_BAD_CMD;
			}

			switch (buf[4]) 
			{
				case Ctlr.SNA_CMD_W:
					telnet.trace.trace_ds(" Write");
					if (buflen > 5)
						telnet.tnctlr.ctlr_write(buf, 4, buflen-4, false);
					else
						telnet.trace.trace_ds("\n");
					break;
				case Ctlr.SNA_CMD_EW:
					telnet.trace.trace_ds(" EraseWrite");
					telnet.tnctlr.ctlr_erase(telnet.tnctlr.screen_alt);
					if (buflen > 5)
						telnet.tnctlr.ctlr_write(buf, 4, buflen-4, true);
					else
						telnet.trace.trace_ds("\n");
					break;
				case Ctlr.SNA_CMD_EWA:
					telnet.trace.trace_ds(" EraseWriteAlternate");
					telnet.tnctlr.ctlr_erase(telnet.tnctlr.screen_alt);
					if (buflen > 5)
						telnet.tnctlr.ctlr_write(buf, 4, buflen-4, true);
					else
						telnet.trace.trace_ds("\n");
					break;
				case Ctlr.SNA_CMD_EAU:
					telnet.trace.trace_ds(" EraseAllUnprotected\n");
					telnet.tnctlr.ctlr_erase_all_unprotected();
					break;
				default:
					telnet.trace.trace_ds(" unknown type 0x%02x\n", buf[4]);
					return pds.PDS_BAD_CMD;
			}
			return pds.PDS_OKAY_NO_OUTPUT;
		}

		NetBuffer query_reply_start()
		{
			NetBuffer obptr = new NetBuffer();
			obptr.Add(AID.AID_SF);
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
				telnet.trace.trace_ds("> StructuredField\n");
				qr_in_progress = false;
			}

			//space3270out(4);
			obptr.Add16(0); // Length - set later
			obptr.Add(see.SFID_QREPLY);
			obptr.Add(code);
			switch (code) 
			{
				case see.QR_CHARSETS:
					telnet.trace.trace_ds("> QueryReply(CharacterSets)\n");
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

				case see.QR_IMP_PART:
					telnet.trace.trace_ds("> QueryReply(ImplicitPartition)\n");
//					space3270out(13);
					obptr.Add(0x0);		/* reserved */
					obptr.Add(0x0);
					obptr.Add(0x0b);	/* length of display size */
					obptr.Add(0x01);	/* "implicit partition size" */
					obptr.Add(0x00);	/* reserved */
					obptr.Add16(80);	/* implicit partition width */
					obptr.Add16(24);	/* implicit partition height */
					obptr.Add16(telnet.tnctlr.maxCOLS);	/* alternate height */
					obptr.Add16(telnet.tnctlr.maxROWS);	/* alternate width */
					break;

				case see.QR_NULL:
					telnet.trace.trace_ds("> QueryReply(Null)\n");
					break;

				case see.QR_SUMMARY:
					telnet.trace.trace_ds("> QueryReply(Summary(");
//					space3270out(NSR);
					for (i = 0; i < NSR; i++) 
					{
						telnet.trace.trace_ds("%s%s", comma,
							see.see_qcode(supported_replies[i]));
						comma = ",";
						obptr.Add(supported_replies[i]);
					}
					telnet.trace.trace_ds("))\n");
					break;

				case see.QR_USABLE_AREA:
					telnet.trace.trace_ds("> QueryReply(UsableArea)\n");
//					space3270out(19);
					obptr.Add(0x01);	/* 12/14-bit addressing */
					obptr.Add(0x00);	/* no special character features */
					obptr.Add16(telnet.tnctlr.maxCOLS);	/* usable width */
					obptr.Add16(telnet.tnctlr.maxROWS);	/* usable height */
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
					obptr.Add16(telnet.tnctlr.maxCOLS*telnet.tnctlr.maxROWS);	/* buffer, questionable */
					break;

				case see.QR_COLOR:
					telnet.trace.trace_ds("> QueryReply(Color)\n");
//					space3270out(4 + 2*15);
					obptr.Add(0x00);	/* no options */
					obptr.Add(telnet.appres.color8? 8: 16); /* report on 8 or 16 colors */
					obptr.Add(0x00);	/* default color: */
					obptr.Add(0xf0 + see.COLOR_GREEN);	/*  green */
					for (i = 0xf1; i <= (telnet.appres.color8? 0xf8: 0xff); i++) 
					{
						obptr.Add(i);
						if (telnet.appres.m3279)
							obptr.Add(i);
						else
							obptr.Add(0x00);
					}
					break;

				case see.QR_HIGHLIGHTING:
					telnet.trace.trace_ds("> QueryReply(Highlighting)\n");
//					space3270out(11);
					obptr.Add(5);		/* report on 5 pairs */
					obptr.Add(see.XAH_DEFAULT);	/* default: */
					obptr.Add(see.XAH_NORMAL);	/*  normal */
					obptr.Add(see.XAH_BLINK);	/* blink: */
					obptr.Add(see.XAH_BLINK);	/*  blink */
					obptr.Add(see.XAH_REVERSE);	/* reverse: */
					obptr.Add(see.XAH_REVERSE);	/*  reverse */
					obptr.Add(see.XAH_UNDERSCORE); /* underscore: */
					obptr.Add(see.XAH_UNDERSCORE); /*  underscore */
					obptr.Add(see.XAH_INTENSIFY); /* intensify: */
					obptr.Add(see.XAH_INTENSIFY); /*  intensify */
					break;

				case see.QR_REPLY_MODES:
					telnet.trace.trace_ds("> QueryReply(ReplyModes)\n");
//					space3270out(3);
					obptr.Add(SF_SRM_FIELD);
					obptr.Add(SF_SRM_XFIELD);
					obptr.Add(SF_SRM_CHAR);
					break;

				case see.QR_ALPHA_PART:
					telnet.trace.trace_ds("> QueryReply(AlphanumericPartitions)\n");
//					space3270out(4);
					obptr.Add(0);		/* 1 partition */
					obptr.Add16(telnet.tnctlr.maxROWS*telnet.tnctlr.maxCOLS);	/* buffer space */
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
			telnet.net_output(obptr);
			telnet.keyboard.kybd_inhibit(true);
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
