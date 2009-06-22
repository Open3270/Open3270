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
//
namespace Open3270.TN3270
{
	public enum CursorOp
	{
		Tab,
		BackTab,
		Exact,
		NearestUnprotectedField

	}
}
namespace Open3270.TN3270
{
	internal enum pds 
	{
		PDS_OKAY_NO_OUTPUT = 0,	/* command accepted, produced no output */
		PDS_OKAY_OUTPUT = 1,	/* command accepted, produced output */
		PDS_BAD_CMD = -1,	/* command rejected */
		PDS_BAD_ADDR = -2	/* command contained a bad address */
	}

	/// <summary>
	/// Summary description for Ctlr.
	/// </summary>
	internal class Ctlr : IDisposable
	{
		enum state 
		{
			DATA = 0, ESC = 1, CSDES = 2,
			N1 = 3, DECP = 4, TEXT = 5, TEXT2 = 6
		}

		/* Configuration change masks. */
		public const int  NO_CHANGE		=0x0000;	/* no change */
		public const int  MODEL_CHANGE	=0x0001;	/* screen dimensions changed */
		public const int  FONT_CHANGE	=0x0002;	/* emulator font changed */
		public const int  COLOR_CHANGE	=0x0004;	/* color scheme or 3278/9 mode changed */
		public const int  SCROLL_CHANGE	=0x0008;	/* scrollbar snapped on or off */
		public const int  CHARSET_CHANGE	=0x0010;	/* character set changed */
		public const int  ALL_CHANGE	=0xffff;	/* everything changed */

		/* field attribute definitions
		 * 	The 3270 fonts are based on the 3270 character generator font found on
		 *	page 12-2 in the IBM 3270 Information Display System Character Set
		 *	Reference.  Characters 0xC0 through 0xCF and 0xE0 through 0xEF
		 *	(inclusive) are purposely left blank and are used to represent field
		 *	attributes as follows:
		 *
		 *		11x0xxxx
		 *		  | ||||
		 *		  | ||++--- 00 normal intensity/non-selectable
		 *		  | ||      01 normal intensity/selectable
		 *		  | ||      10 high intensity/selectable
		 *		  | ||	    11 zero intensity/non-selectable
		 *		  | |+----- unprotected(0)/protected(1)
		 *		  | +------ alphanumeric(0)/numeric(1)
		 *		  +-------- unmodified(0)/modified(1)
		 */
		public const byte FA_BASE			=0xc0;
		public const byte FA_MASK			=0xd0;
		public const byte FA_MODIFY		=0x20;
		public const byte FA_MODIFY_MASK = 0xdf;
		public const byte FA_NUMERIC		=0x08;
		public const byte FA_PROTECT		=0x04;
		public const byte FA_INTENSITY		=0x03;
		public const byte FA_INT_NORM_NSEL	=0x00;
		public const byte FA_INT_NORM_SEL		=0x01;
		public const byte FA_INT_HIGH_SEL		=0x02;
		public const byte FA_INT_ZERO_NSEL	=0x03;
		public bool IS_FA(byte c)
		{
			return (((c) & FA_MASK) == FA_BASE);
		}
		public bool FA_IS_MODIFIED(byte c) { return (c & FA_MODIFY)!=0; }
		public bool FA_IS_NUMERIC(byte c)	{ return (c & FA_NUMERIC)!=0; }
		public bool FA_IS_PROTECTED(byte c) { return (c & FA_PROTECT)!=0;}
		public bool FA_IS_PROTECTED_AT(int index) { return (screen_buf[index] & FA_PROTECT)!=0;}
		public bool FA_IS_SKIP(byte c) { return (FA_IS_NUMERIC(c) && FA_IS_PROTECTED(c));}
		public bool FA_IS_ZERO(byte c) { return ((c & FA_INTENSITY) == FA_INT_ZERO_NSEL);}
		public bool FA_IS_HIGH(byte c) { return ((c & FA_INTENSITY) == FA_INT_HIGH_SEL);}
		public bool FA_IS_NORMAL(byte c) 
		{ 
			return 	((c & FA_INTENSITY) == FA_INT_NORM_NSEL 	||
				(c & FA_INTENSITY) == FA_INT_NORM_SEL);
		}
		public bool FA_IS_SELECTABLE(byte c)
		{
			return 	((c & FA_INTENSITY) == FA_INT_NORM_SEL	||
				(c & FA_INTENSITY) == FA_INT_HIGH_SEL);
		}
		public bool FA_IS_INTENSE(byte c) { return ((c & FA_INT_HIGH_SEL) == FA_INT_HIGH_SEL);}


		// const
		/* 3270 commands */
		public const int CMD_W		= 0x01;	/* write */
		public const int CMD_RB		= 0x02;	/* read buffer */
		public const int CMD_NOP		= 0x03;	/* no-op */
		public const int CMD_EW		= 0x05;	/* erase/write */
		public const int CMD_RM		= 0x06;	/* read modified */
		public const int CMD_EWA		= 0x0d;	/* erase/write alternate */
		public const int CMD_RMA		= 0x0e;	/* read modified all */
		public const int CMD_EAU		= 0x0f;	/* erase all unprotected */
		public const int CMD_WSF		= 0x11;	/* write structured field */

		/* SNA 3270 commands */
		public const int SNA_CMD_RMA	= 0x6e;	/* read modified all */
		public const int SNA_CMD_EAU	= 0x6f;	/* erase all unprotected */
		public const int SNA_CMD_EWA	= 0x7e;	/* erase/write alternate */
		public const int SNA_CMD_W	= 0xf1;	/* write */
		public const int SNA_CMD_RB	= 0xf2;	/* read buffer */
		public const int SNA_CMD_WSF	= 0xf3;	/* write structured field */
		public const int SNA_CMD_EW	= 0xf5;	/* erase/write */
		public const int SNA_CMD_RM	= 0xf6;	/* read modified */

		/* 3270 orders */
		public const int ORDER_PT	= 0x05;	/* program tab */
		public const int ORDER_GE	= 0x08;	/* graphic escape */
		public const int ORDER_SBA	= 0x11;	/* set buffer address */
		public const int ORDER_EUA	= 0x12;	/* erase unprotected to address */
		public const int ORDER_IC	= 0x13;	/* insert cursor */
		public const int ORDER_SF	= 0x1d;	/* start field */
		public const int ORDER_SA	= 0x28;	/* set attribute */
		public const int ORDER_SFE	= 0x29;	/* start field extended */
		public const int ORDER_YALE	= 0x2b;	/* Yale sub command */
		public const int ORDER_MF	= 0x2c;	/* modify field */
		public const int ORDER_RA	= 0x3c;	/* repeat to address */

		public const int FCORDER_NULL	= 0x00;	/* format control: null */
		public const int FCORDER_FF	= 0x0c;	/*		   form feed */
		public const int FCORDER_CR	= 0x0d;	/*		   carriage return */
		public const int FCORDER_NL	= 0x15;	/*		   new line */
		public const int FCORDER_EM	= 0x19;	/*		   end of medium */
		public const int FCORDER_DUP	= 0x1c;	/*		   duplicate */
		public const int FCORDER_FM	= 0x1e;	/*		   field mark */
		public const int FCORDER_SUB	= 0x3f;	/*		   substitute */
		public const int FCORDER_EO	= 0xff;	/*		   eight ones */

		/* SCS control code, some overlap orders */
		public const int SCS_BS      	= 0x16;	/* Back Space  */
		public const int SCS_BEL		= 0x2f;	/* Bell Function */
		public const int SCS_CR      	= 0x0d;	/* Carriage Return */
		public const int SCS_ENP		= 0x14;	/* Enable Presentation */
		public const int SCS_FF		= 0x0c;	/* Forms Feed */
		public const int SCS_GE		= 0x08;	/* Graphic Escape */
		public const int SCS_HT		= 0x05;	/* Horizontal Tab */
		public const int SCS_INP		= 0x24;	/* Inhibit Presentation */
		public const int SCS_IRS		= 0x1e;	/* Interchange-Record Separator */
		public const int SCS_LF		= 0x25;	/* Line Feed */
		public const int SCS_NL		= 0x15;	/* New Line */
		public const int SCS_SA		= 0x28;	/* Set Attribute */
		public const int SCS_SET		= 0x2b;	/* Set: */
		public const int SCS_SHF	= 0xc1;	/*  Horizontal format */
		public const int SCS_SLD	= 0xc6;	/*  Line Density */
		public const int SCS_SVF	= 0xc2;	/*  Vertical Format */
		public const int SCS_TRN		= 0x35;	/* Transparent */
		public const int SCS_VCS		= 0x04;	/* Vertical Channel Select */
		public const int SCS_VT		= 0x0b;	/* Vertical Tab */

		/* Structured fields */
		public const int SF_READ_PART	= 0x01;	/* read partition */
		public const int SF_RP_QUERY	= 0x02;	/*  query */
		public const int SF_RP_QLIST	= 0x03;	/*  query list */
		public const int SF_RPQ_LIST	= 0x00;	/*   QCODE list */
		public const int SF_RPQ_EQUIV	= 0x40;	/*   equivalent+ QCODE list */
		public const int SF_RPQ_ALL	= 0x80;	/*   all */
		public const int SF_ERASE_RESET	= 0x03;	/* erase/reset */
		public const int SF_ER_DEFAULT	= 0x00;	/*  default */
		public const int SF_ER_ALT	= 0x80;	/*  alternate */
		public const int SF_SET_REPLY_MODE = 0x09;	/* set reply mode */
		public const int SF_SRM_FIELD	= 0x00;	/*  field */
		public const int SF_SRM_XFIELD	= 0x01;	/*  extended field */
		public const int SF_SRM_CHAR	= 0x02;	/*  character */
		public const int SF_CREATE_PART	= 0x0c;	/* create partition */
		public const int CPFLAG_PROT	= 0x40;	/*  protected flag */
		public const int CPFLAG_COPY_PS	= 0x20;	/*  local copy to presentation space */
		public const int CPFLAG_BASE	= 0x07;	/*  base character set index */
		public const int SF_OUTBOUND_DS	= 0x40;	/* outbound 3270 DS */
		public const int SF_TRANSFER_DATA = 0xd0  ; /* file transfer open request */

		/* Query replies */
		public const int QR_SUMMARY	= 0x80;	/* summary */
		public const int QR_USABLE_AREA	= 0x81;	/* usable area */
		public const int QR_ALPHA_PART	= 0x84;	/* alphanumeric partitions */
		public const int QR_CHARSETS	= 0x85;	/* character sets */
		public const int QR_COLOR	= 0x86;	/* color */
		public const int QR_HIGHLIGHTING	= 0x87;	/* highlighting */
		public const int QR_REPLY_MODES	= 0x88;	/* reply modes */
		public const int QR_PC3270	= 0x93   ; /* PC3270 */
		public const int QR_DDM    	= 0x95   ; /* distributed data management */
		public const int QR_IMP_PART	= 0xa6;	/* implicit partition */
		public const int QR_NULL		= 0xff;	/* null */

		public const int CS_GE = 0x04;/* cs flag for Graphic Escape */
		public byte[] screen_buf;
		public ExtendedAttribute[] ea_buf;
		public bool is_altbuffer = false;
		public byte[] ascreen_buf = null;
		public bool screen_alt = true;
		public ExtendedAttribute[] aea_buf = null;
		ExtendedAttribute[] zero_buf = null;
		public int cursor_addr = 0;
		bool debugging_font = false;
		public int buffer_addr = 0;
		public int current_fa_index = 0;
		
		public byte aid = AID.AID_NO;		/* current attention ID */
		//byte current_fa;
		//int current_fa_address =0;
		public byte default_cs = 0;
		public byte default_fg = 0;
		public byte default_bg = 0;
		public byte default_gr = 0;
		public bool ever_3270 = false;
		public int maxCOLS = 132;
		public int maxROWS = 43;
		public int model_num;
		public byte reply_mode = 0;

		private Telnet telnet;
		public bool formatted = false;
		private TNTrace trace;
		private bool trace_primed = false;
		private bool trace_skipping = false;
		private Appres appres;
		//int ov_cols = 0;
		//int ov_rows = 0;
		string model_name = null;

		public byte[] crm_attr;
		public int crm_nattr;

		public byte fake_fa = 0;
		int sscp_start = 0;
		sf sf;

		internal Ctlr(Telnet tn, Appres appres)
		{
			this.sf = new sf(tn);
			crm_attr = new byte[16];
			crm_nattr = 0;
			this.telnet = tn;
			this.trace  = tn.trace;
			this.appres = appres;
			startTime = DateTime.Now.Ticks;
		}

		long startTime = 0;
		/*
		 * code_table is used to translate buffer addresses and attributes to the 3270
		 * datastream representation
		 */
		static public byte[]	code_table = new byte[] 
{
	0x40, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7,
	0xC8, 0xC9, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
	0x50, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7,
	0xD8, 0xD9, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F,
	0x60, 0x61, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7,
	0xE8, 0xE9, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
	0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7,
	0xF8, 0xF9, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F,
		};

		public bool IsBlank(byte c)	
		{
			if ((c == CG.CG_null) || (c == CG.CG_space))
				return true;
			else
				return false;
		}

		bool screen_changed = false;
		int first_changed = 0;
		int last_changed  = 0;
		public int ROWS = 25;
		public int COLS = 80;

		public bool ScreenChanged
		{
			get { return screen_changed; }
		}
		private void ALL_CHANGED()
		{
			screen_changed = true;
			if (telnet.IN_ANSI)
			{
				first_changed = 0;
				last_changed = ROWS*COLS;
			}
		}
		private void REGION_CHANGED(int f, int l)
		{
			screen_changed = true;
			if (telnet.IN_ANSI)
			{
				if (first_changed==-1 || f<first_changed) first_changed = f;
				if (last_changed==-1  || l>last_changed)  last_changed = l;
			}
		}
		private void ONE_CHANGED(int n)
		{
			REGION_CHANGED(n,n+1);
		}
		static internal int DECODE_BADDR(byte c1, byte c2)
		{
			if 	((c1 & 0xC0) == 0x00)
				return 	(int)(((c1 & 0x3F) << 8) | c2);
			else
				return (int)(((c1 & 0x3F) << 6) | (c2 & 0x3F));
		}
		static internal void ENCODE_BADDR(NetBuffer ptr, int addr)
		{
			if ((addr) > 0xfff) 
			{ 
				ptr.Add(((addr) >> 8) & 0x3F); 
				ptr.Add((addr) & 0xFF);
			} 
			else 
			{ 
				ptr.Add(code_table[((addr) >> 6) & 0x3F]); 
				ptr.Add(code_table[(addr) & 0x3F]);
			} 
		}

		/*
		 * Initialize the emulated 3270 hardware.
		 */
		public void ctlr_init(int cmask)
		{
			/* Register callback routines. */
			telnet.register_schange(STCALLBACK.ST_HALF_CONNECT, new SChangeDelegate(ctlr_half_connect));
			telnet.register_schange(STCALLBACK.ST_CONNECT, new SChangeDelegate(ctlr_connect));
			telnet.register_schange(STCALLBACK.ST_3270_MODE, new SChangeDelegate(ctlr_connect));
		}
		/*
		 * Reinitialize the emulated 3270 hardware.
		 */
		public void ctlr_reinit(int cmask)
		{
			if ((cmask & MODEL_CHANGE) !=0)
			{
				/* Allocate buffers */
				screen_buf = new byte[maxROWS*maxCOLS];
				ea_buf = new ExtendedAttribute[maxROWS*maxCOLS];
				ascreen_buf = new byte[maxROWS*maxCOLS];
				aea_buf = new ExtendedAttribute[maxROWS*maxCOLS];
				zero_buf = new ExtendedAttribute[maxROWS*maxCOLS];
				int i;
				for (i=0; i<maxROWS*maxCOLS; i++)
				{
					ea_buf[i] = new ExtendedAttribute();
					aea_buf[i] = new ExtendedAttribute();
					zero_buf[i] = new ExtendedAttribute();
				}

				cursor_addr = 0;
				buffer_addr = 0;
			}
		}

		/*
		 * Deal with the relationships between model numbers and rows/cols.
		 */
        void set_rows_cols(int mn, int ovc, int ovr)
        {
            int defmod;

            switch (mn)
            {
                case 2:
                    maxCOLS = COLS = 80;
                    maxROWS = ROWS = 24;
                    model_num = 2;
                    break;
                case 3:
                    maxCOLS = COLS = 80;
                    maxROWS = ROWS = 32;
                    model_num = 3;
                    break;
                case 4:
                    maxCOLS = COLS = 80;
                    maxROWS = ROWS = 43;
                    model_num = 4;
                    break;
                case 5:
                    maxCOLS = COLS = 132;
                    maxROWS = ROWS = 27;
                    model_num = 5;
                    break;
                default:
                    defmod = 4;
                    telnet.events.popup_an_error("Unknown model: %d\nDefaulting to %d", mn,
                        defmod);
                    set_rows_cols(defmod, ovc, ovr);
                    return;
            }

            /* Apply oversize. */
            //ov_cols = 0;
            //ov_rows = 0;
            if (ovc != 0 || ovr != 0)
            {
                throw new ApplicationException("oops - oversize");
            }

            /* Update the model name. */
            model_name = "327" + (appres.m3279 ? "9" : "8") + "-" + model_num + (appres.extended ? "-E" : "");

        }


		/*
		 * Set the formatted screen flag.  A formatted screen is a screen that
		 * has at least one field somewhere on it.
		 */
		void set_formatted()
		{
			int	baddr;

			formatted = false;
			baddr = 0;
			do 
			{
				if (IS_FA(screen_buf[baddr])) 
				{
					formatted = true;
					break;
				}
				INC_BA(ref baddr);
			} 
			while (baddr != 0);
		}

		/*
		 * Called when a host is half connected.
		 */
		void ctlr_half_connect(bool ignored)
		{
			//Console.WriteLine("--ticking_start//");
	
			//	ticking_start(true);
		}


		/*
		 * Called when a host connects, disconnects, or changes ANSI/3270 modes.
		 */
		void ctlr_connect(bool ignored)
		{
			//Console.WriteLine("--ctlr_connect//ticking_stop//");
			//	ticking_stop();
			//	status_untiming();

			if (ever_3270)
			{
				//Console.WriteLine("--ever_3270 is true, set fake_fa to 0xe0 - unprotected");
				fake_fa = 0xE0;
			}
			else
			{
				//Console.WriteLine("--ever_3270 is false, set fake_fa to 0xc4 - protected");
				fake_fa = 0xC4;
			}
			if (!telnet.IN_3270 || (telnet.IN_SSCP && ((telnet.keyboard.kybdlock & Keyboard.KL_OIA_TWAIT)!=0))) 
			{
				telnet.keyboard.kybdlock_clr(Keyboard.KL_OIA_TWAIT, "ctlr_connect");
				//status_reset();
			}

			default_fg = 0x00;
			default_gr = 0x00;
			default_cs = 0x00;
			reply_mode = SF_SRM_FIELD;
			crm_nattr = 0;
		}


		/*
		 * Find the field attribute for the given buffer address.  Return its address
		 * rather than its value.
		 */
		public int get_field_attribute(int baddr)
		{
			int	sbaddr;

			if (!formatted)
			{
				//Console.WriteLine("get_field_attribute on unformatted screen returns -1");
				return -1;// **BUG** //&fake_fa;
			}

			sbaddr = baddr;
			do 
			{
				if (IS_FA(screen_buf[baddr]))
					return baddr;//&(screen_buf[baddr]);
				DEC_BA(ref baddr);
			} 
			while (baddr != sbaddr);
			return -1;// **BUG** &fake_fa;
		}

		/*
		 * Find the field attribute for the given buffer address, bounded by another
		 * buffer address.  Return the attribute in a parameter.
		 *
		 * Returns true if an attribute is found, false if boundary hit.
		 */
		bool get_bounded_field_attribute(int baddr, int bound, ref int fa_out_index)//byte[] char *fa_out)
		{
			int	sbaddr;

			if (!formatted) 
			{
				fa_out_index = -1;
				//*fa_out = fake_fa;
				return true;
			}

			sbaddr = baddr;
			do 
			{
				if (IS_FA(screen_buf[baddr])) 
				{
					fa_out_index = baddr;//*fa_out = screen_buf[baddr];
					return true;
				}
				DEC_BA(ref baddr);
			} 
			while (baddr != sbaddr && baddr != bound);

			/* Screen is unformatted (and 'formatted' is inaccurate). */
			if (baddr == sbaddr) 
			{
				fa_out_index = -1;
				//*fa_out = fake_fa;
				return true;
			}

			/* Wrapped to boundary. */
			return false;
		}

		/*
		 * Given the address of a field attribute, return the address of the
		 * extended attribute structure.
		 */
		/*
struct ea * fa2ea(byte *fa)
{
	if (fa == &fake_fa)
		return &fake_ea;
	else
		return &ea_buf[fa - screen_buf];
}
							  */

		/*
		 * Find the next unprotected field.  Returns the address following the
		 * unprotected attribute byte, or 0 if no nonzero-width unprotected field
		 * can be found.
		 */
		public int next_unprotected(int baddr0)
		{
			int baddr, nbaddr;

			nbaddr = baddr0;
			do 
			{
				baddr = nbaddr;
				INC_BA(ref nbaddr);
				if (IS_FA(screen_buf[baddr]) &&
					!FA_IS_PROTECTED(screen_buf[baddr]) &&
					!IS_FA(screen_buf[nbaddr]))
					return nbaddr;
			} while (nbaddr != baddr0);
			return 0;
		}

		/*
		 * Perform an erase command, which may include changing the (virtual) screen
		 * size.
		 */
		public void ctlr_erase(bool alt)
		{
			telnet.keyboard.kybd_inhibit(false);

			ctlr_clear(true);

			///* Let a script go. */
			//telnet.events.RunScript("ctlr_erase");
			//sms_host_output();

			if (alt == screen_alt)
				return;

			//	screen_disp(true);

			if (alt) 
			{
				/* Going from 24x80 to maximum. */
				//		screen_disp(false);
				ROWS = maxROWS;
				COLS = maxCOLS;
			} 
			else 
			{
				/* Going from maximum to 24x80. */
				if (maxROWS > 24 || maxCOLS > 80) 
				{
					if (debugging_font) 
					{
						ctlr_blanks();
						//				screen_disp(false);
					}
					ROWS = 24;
					COLS = 80;
				}
			}

			screen_alt = alt;
		}

		/*
		 * Interpret an incoming 3270 command.
		 */
		public pds process_ds(byte[] buf, int start, int length)
		{
			//
			if (buf.Length==0 || length==0)
				return pds.PDS_OKAY_NO_OUTPUT;

			trace.trace_ds("< ");


			// handle 3270 command
			if (buf[start]==CMD_EAU || buf[start]==SNA_CMD_EAU)
			{
				/* erase all unprotected */
				trace.trace_ds("EraseAllUnprotected\n");
				ctlr_erase_all_unprotected();
				return pds.PDS_OKAY_NO_OUTPUT;
			}
			else if (buf[start]==CMD_EWA || buf[start]==SNA_CMD_EWA)
			{
				/* erase/write alternate */
				trace.trace_ds("EraseWriteAlternate");
				ctlr_erase(true);
				ctlr_write(buf,start,length, true);
				return pds.PDS_OKAY_NO_OUTPUT;

			}
			else if (buf[start]==CMD_EW || buf[start]==SNA_CMD_EW)
			{
				/* erase/write */
				trace.trace_ds("EraseWrite");
				ctlr_erase(false);
				//Console.WriteLine("**BUGBUG**");
				ctlr_write(buf, start, length /*buflen*/, true);
				return pds.PDS_OKAY_NO_OUTPUT;
			}
			else if (buf[start]==CMD_W || buf[start]==SNA_CMD_W)
			{
				/* write */
				trace.trace_ds("Write");
				ctlr_write(buf, start, length /*buflen*/, false);
				return pds.PDS_OKAY_NO_OUTPUT;

			}
			else if (buf[start]==CMD_RB || buf[start]==SNA_CMD_RB)
			{
				/* read buffer */
				trace.trace_ds("ReadBuffer\n");
				ctlr_read_buffer(aid);
				return pds.PDS_OKAY_OUTPUT;

			}
			else if (buf[start]==CMD_RM || buf[start]==SNA_CMD_RM)
			{
				/* read modifed */
				trace.trace_ds("ReadModified\n");
				ctlr_read_modified(aid, false);
				return pds.PDS_OKAY_OUTPUT;

			}
			else if (buf[start]==CMD_RMA || buf[start]==SNA_CMD_RMA)
			{
				/* read modifed all */
				trace.trace_ds("ReadModifiedAll\n");
				ctlr_read_modified(aid, true);
				return pds.PDS_OKAY_OUTPUT;
			}
			else if (buf[start]==CMD_WSF || buf[start]==SNA_CMD_WSF)
			{
				/* write structured field */
				trace.trace_ds("WriteStructuredField");
				return sf.write_structured_field(buf, start, length /*buflen*/);
			}
			else if (buf[start]==CMD_EWA)
			{
				/* no-op */
				trace.trace_ds("NoOp\n");
				return pds.PDS_OKAY_NO_OUTPUT;
			}
			else
			{
				/* unknown 3270 command */
				telnet.events.popup_an_error("Unknown 3270 Data Stream command: 0x%X\n", buf[start]);
				return pds.PDS_BAD_CMD;
			}
		}
		/*
		 * Functions to insert SA attributes into the inbound data stream.
		 */
		void insert_sa1(NetBuffer obptr, byte attr, byte vvalue, ref byte currentp, ref bool anyp)
		{
			if (vvalue == currentp)
				return;
			currentp = vvalue;
			//space3270out(3);
			obptr.Add(ORDER_SA);
			obptr.Add(attr);
			obptr.Add(vvalue);
			if (anyp)
				trace.trace_ds("'");
			trace.trace_ds(" SetAttribute(%s)",see. see_efa(attr, vvalue));
			anyp = false;
		}

		void insert_sa(NetBuffer obptr, int baddr, ref byte current_fgp, ref byte current_grp, ref byte current_csp, ref bool anyp)
		{
			if (reply_mode != SF_SRM_CHAR)
				return;

			int i;
			bool foundXA_FOREGROUND = false;
			bool foundXA_HIGHLIGHTING = false;
			bool foundXA_CHARSET = false;
			for (i=0; i<crm_nattr; i++)
			{
				if (crm_attr[i]==see.XA_FOREGROUND)
					foundXA_FOREGROUND = true;
				if (crm_attr[i]==see.XA_HIGHLIGHTING)
					foundXA_HIGHLIGHTING = true;
				if (crm_attr[i]==see.XA_CHARSET)
					foundXA_CHARSET = true;
			}
			if (foundXA_FOREGROUND)
				insert_sa1(obptr, see.XA_FOREGROUND, ea_buf[baddr].fg, ref current_fgp, ref anyp);
			if (foundXA_HIGHLIGHTING)
			{
				byte gr;

				gr = ea_buf[baddr].gr;
				if (gr!=0)
					gr |= 0xf0;
				insert_sa1(obptr, see.XA_HIGHLIGHTING, gr, ref current_grp, ref anyp);
			}
			if (foundXA_CHARSET) 
			{
				byte cs;

				cs = (byte)(ea_buf[baddr].cs & ExtendedAttribute.CS_MASK);
				if (cs!=0)
					cs |= 0xf0;
				insert_sa1(obptr, see.XA_CHARSET, cs, ref current_csp, ref anyp);
			}
		}

		/*
		 * Process a 3270 Read-Modified command and transmit the data back to the
		 * host.
		 */
		public void ctlr_read_modified(byte aid_byte, bool all)
		{
			int	baddr, sbaddr;
			bool		send_data = true;
			bool		short_read = false;
			byte	current_fg = 0x00;
			byte	current_gr = 0x00;
			byte	current_cs = 0x00;

			if (telnet.IN_SSCP && aid_byte != AID.AID_ENTER)
				return;

			trace.trace_ds("> ");
			NetBuffer obptr = new NetBuffer();

			switch (aid_byte) 
			{

				case AID.AID_SYSREQ:			/* test request */
					obptr.Add(0x01);	/* soh */
					obptr.Add(0x5b);	/*  %  */
					obptr.Add(0x61);	/*  /  */
					obptr.Add(0x02);	/* stx */
					trace.trace_ds("SYSREQ");
					break;

				case AID.AID_PA1:			/* short-read AIDs */
				case AID.AID_PA2:
				case AID.AID_PA3:
				case AID.AID_CLEAR:
					if (!all)
						short_read = true;
					/* fall through... */
					if (!all)
						send_data = false;
					/* fall through... */
					if (!telnet.IN_SSCP) 
					{
						obptr.Add(aid_byte);
						trace.trace_ds(see.see_aid(aid_byte));
						if (short_read)
							goto rm_done;
						ENCODE_BADDR(obptr, cursor_addr);
						trace.trace_ds(trace.rcba(cursor_addr));
					} 
					break;

				case AID.AID_SELECT:			/* No data on READ MODIFIED */
					if (!all)
						send_data = false;
					/* fall through... */
					if (!telnet.IN_SSCP) 
					{
						obptr.Add(aid_byte);
						trace.trace_ds(see.see_aid(aid_byte));
						if (short_read)
							goto rm_done;
						ENCODE_BADDR(obptr, cursor_addr);
						trace.trace_ds(trace.rcba(cursor_addr));
					} 
					break;

				default:				/* ordinary AID */
					if (!telnet.IN_SSCP) 
					{
						obptr.Add(aid_byte);
						trace.trace_ds(see.see_aid(aid_byte));
						if (short_read)
							goto rm_done;
						ENCODE_BADDR(obptr, cursor_addr);
						trace.trace_ds(trace.rcba(cursor_addr));
					} 
					break;
			}

			baddr = 0;
			if (formatted) 
			{
				/* find first field attribute */
				do 
				{
					if (IS_FA(screen_buf[baddr]))
						break;
					INC_BA(ref baddr);
				} 
				while (baddr != 0);
				sbaddr = baddr;
				do 
				{
					if (FA_IS_MODIFIED(screen_buf[baddr])) 
					{
						bool	any = false;

						INC_BA(ref baddr);
						obptr.Add(ORDER_SBA);
						ENCODE_BADDR(obptr, baddr);
						trace.trace_ds(" SetBufferAddress%s", trace.rcba(baddr));
						while (!IS_FA(screen_buf[baddr])) 
						{
							if (send_data && screen_buf[baddr]!=0) 
							{
								insert_sa(obptr, baddr,
									ref current_fg,
									ref current_gr,
									ref current_cs,
									ref any);
								if ((ea_buf[baddr].cs & CS_GE) !=0)
								{
									obptr.Add(ORDER_GE);
									if (any)
										trace.trace_ds("'");
									trace.trace_ds(" GraphicEscape");
									any = false;
								}
								obptr.Add(Tables.cg2ebc[screen_buf[baddr]]);
								if (!any)
									trace.trace_ds(" '");
								trace.trace_ds("%s", see.see_ebc(Tables.cg2ebc[screen_buf[baddr]]));
								any = true;
							}
							INC_BA(ref baddr);
						}
						if (any)
							trace.trace_ds("'");
					}
					else 
					{	/* not modified - skip */
						do 
						{
							INC_BA(ref baddr);
						} while (!IS_FA(screen_buf[baddr]));
					}
				} while (baddr != sbaddr);
			} 
			else 
			{
				bool	any = false;
				int nbytes = 0;

				/*
				 * If we're in SSCP-LU mode, the starting point is where the
				 * host left the cursor.
				 */
				if (telnet.IN_SSCP)
					baddr = sscp_start;

				do 
				{
					if (screen_buf[baddr]!=0) 
					{
						insert_sa(obptr, baddr,
							ref current_fg,
							ref current_gr,
							ref current_cs,
							ref any);
						if ((ea_buf[baddr].cs & CS_GE) !=0)
						{
							obptr.Add(ORDER_GE);
							if (any)
								trace.trace_ds("' ");
							trace.trace_ds(" GraphicEscape ");
							any = false;
						}
						obptr.Add(Tables.cg2ebc[screen_buf[baddr]]);
						if (!any)
							trace.trace_ds("'");
						trace.trace_ds(see.see_ebc(Tables.cg2ebc[screen_buf[baddr]]));
						any = true;
						nbytes++;
					}
					INC_BA(ref baddr);

					/*
					 * If we're in SSCP-LU mode, end the return value at
					 * 255 bytes, or where the screen wraps.
					 */
					if (telnet.IN_SSCP && (nbytes >= 255 || baddr==0))
						break;
				} while (baddr != 0);
				if (any)
					trace.trace_ds("'");
			}

			rm_done:
				trace.trace_ds("\n");
			telnet.net_output(obptr);
		}


		/*
		 * Calculate the proper 3270 DS value for an internal field attribute.
		 */
		byte calc_fa(byte fa)
		{
			byte r = 0x00;

			if (FA_IS_PROTECTED(fa))
				r |= 0x20;
			if (FA_IS_NUMERIC(fa))
				r |= 0x10;
			if (FA_IS_MODIFIED(fa))
				r |= 0x01;
			r |= (byte)((fa & FA_INTENSITY) << 2);
			return r;
		}

		/*
		 * Process a 3270 Read-Buffer command and transmit the data back to the
		 * host.
		 */
		public void ctlr_read_buffer(byte aid_byte)
		{
			int	baddr;
			byte	fa;
			bool		any = false;
			int		attr_count = 0;
			byte	current_fg = 0x00;
			byte	current_gr = 0x00;
			byte	current_cs = 0x00;

			trace.trace_ds("> ");
			NetBuffer obptr = new NetBuffer();
			obptr.Add(aid_byte);
			ENCODE_BADDR(obptr, cursor_addr);
			trace.trace_ds("%s%s", see.see_aid(aid_byte), trace.rcba(cursor_addr));

			baddr = 0;
			do 
			{
				if (IS_FA(screen_buf[baddr])) 
				{
					if (reply_mode == SF_SRM_FIELD) 
					{
						obptr.Add(ORDER_SF);
					} 
					else 
					{
						obptr.Add(ORDER_SFE);
						attr_count = obptr.Index;
						obptr.Add(1); /* for now */
						obptr.Add(see.XA_3270);
					}
					fa = calc_fa(screen_buf[baddr]);
					obptr.Add(code_table[fa]);
					if (any)
						trace.trace_ds("'");
					trace.trace_ds(" StartField%s%s%s",
						(reply_mode == SF_SRM_FIELD) ? "" : "Extended",
						trace.rcba(baddr), see.see_attr(fa));
					if (reply_mode != SF_SRM_FIELD) 
					{
						if (ea_buf[baddr].fg!=0) 
						{
							obptr.Add(see.XA_FOREGROUND);
							obptr.Add(ea_buf[baddr].fg);
							trace.trace_ds("%s", see.see_efa(see.XA_FOREGROUND,ea_buf[baddr].fg));
							obptr.IncrementAt(attr_count, 1);
						}
						if (ea_buf[baddr].gr!=0) 
						{
							obptr.Add(see.XA_HIGHLIGHTING);
							obptr.Add(ea_buf[baddr].gr | 0xf0);
							trace.trace_ds("%s", see.see_efa(see.XA_HIGHLIGHTING,(byte)(ea_buf[baddr].gr | 0xf0)));
							obptr.IncrementAt(attr_count, 1);
						}
						if ((ea_buf[baddr].cs & ExtendedAttribute.CS_MASK) !=0)
						{
							obptr.Add(see.XA_CHARSET);
							obptr.Add((ea_buf[baddr].cs & ExtendedAttribute.CS_MASK) | 0xf0);
							trace.trace_ds("%s", see.see_efa(see.XA_CHARSET,(byte)((ea_buf[baddr].cs & ExtendedAttribute.CS_MASK) | 0xf0)));
							obptr.IncrementAt(attr_count, 1);
						}
					}
					any = false;
				} 
				else 
				{
					insert_sa(obptr, baddr,
						ref current_fg,
						ref current_gr,
						ref current_cs,
						ref any);
					if ((ea_buf[baddr].cs & CS_GE) !=0)
					{
						obptr.Add(ORDER_GE);
						if (any)
							trace.trace_ds("'");
						trace.trace_ds(" GraphicEscape");
						any = false;
					}
					obptr.Add(Tables.cg2ebc[screen_buf[baddr]]);
					if (Tables.cg2ebc[screen_buf[baddr]] <= 0x3f ||
						Tables.cg2ebc[screen_buf[baddr]] == 0xff) 
					{
						if (any)
							trace.trace_ds("'");

						trace.trace_ds(" %s", see.see_ebc(Tables.cg2ebc[screen_buf[baddr]]));
						any = false;
					} 
					else 
					{
						if (!any)
							trace.trace_ds(" '");
						trace.trace_ds("%s", see.see_ebc(Tables.cg2ebc[screen_buf[baddr]]));
						any = true;
					}
				}
				INC_BA(ref baddr);
			} 
			while (baddr != 0);
			if (any)
				trace.trace_ds("'");

			trace.trace_ds("\n");
			telnet.net_output(obptr);
		}

		/*
		 * Construct a 3270 command to reproduce the current state of the display.
		 */
		public void ctlr_snap_buffer(NetBuffer obptr)
		{
			int	baddr = 0;
			int		attr_count;
			byte	current_fg = 0x00;
			byte	current_gr = 0x00;
			byte	current_cs = 0x00;
			byte   av;

			obptr.Add(screen_alt ? CMD_EWA : CMD_EW);
			obptr.Add(code_table[0]);

			do 
			{
				if (IS_FA(screen_buf[baddr])) 
				{
					obptr.Add(ORDER_SFE);
					attr_count = obptr.Index;//obptr - obuf;
					obptr.Add(1); /* for now */
					obptr.Add(see.XA_3270);
					obptr.Add(code_table[calc_fa(screen_buf[baddr])]);
					if (ea_buf[baddr].fg!=0) 
					{
						//space3270out(2);
						obptr.Add(see.XA_FOREGROUND);
						obptr.Add(ea_buf[baddr].fg);
						obptr.IncrementAt(attr_count, 1);
					}
					if (ea_buf[baddr].gr!=0) 
					{
						obptr.Add(see.XA_HIGHLIGHTING);
						obptr.Add(ea_buf[baddr].gr | 0xf0);
						obptr.IncrementAt(attr_count, 1);
					}
					if ((ea_buf[baddr].cs & ExtendedAttribute.CS_MASK) !=0)
					{
						obptr.Add(see.XA_CHARSET);
						obptr.Add((ea_buf[baddr].cs & ExtendedAttribute.CS_MASK) | 0xf0);
						obptr.IncrementAt(attr_count, 1);
					}
				} 
				else 
				{
					av = ea_buf[baddr].fg;
					if (current_fg != av) 
					{
						current_fg = av;
						obptr.Add(ORDER_SA);
						obptr.Add(see.XA_FOREGROUND);
						obptr.Add(av);
					}
					av = ea_buf[baddr].gr;
					if (av!=0)
						av |= 0xf0;
					if (current_gr != av) 
					{
						current_gr = av;
						obptr.Add(ORDER_SA);
						obptr.Add(see.XA_HIGHLIGHTING);
						obptr.Add(av);
					}
					av = (byte)(ea_buf[baddr].cs & ExtendedAttribute.CS_MASK);
					if (av!=0)
						av |= 0xf0;
					if (current_cs != av) 
					{
						current_cs = av;
						obptr.Add(ORDER_SA);
						obptr.Add(see.XA_CHARSET);
						obptr.Add(av);
					}
					if ((ea_buf[baddr].cs & CS_GE) !=0)
					{
						obptr.Add(ORDER_GE);
					}
					obptr.Add(Tables.cg2ebc[screen_buf[baddr]]);
				}
				INC_BA(ref baddr);
			} 
			while (baddr != 0);

			obptr.Add(ORDER_SBA);
			ENCODE_BADDR(obptr, cursor_addr);
			obptr.Add(ORDER_IC);
		}

		/*
		 * Construct a 3270 command to reproduce the reply mode.
		 * Returns a bool indicating if one is necessary.
		 */
		bool ctlr_snap_modes(NetBuffer obptr)
		{
			int i;

			if (!telnet.IN_3270 || reply_mode == SF_SRM_FIELD)
				return false;

			obptr.Add(CMD_WSF);
			obptr.Add(0x00);	/* implicit length */
			obptr.Add(0x00);
			obptr.Add(SF_SET_REPLY_MODE);
			obptr.Add(0x00);	/* partition 0 */
			obptr.Add(reply_mode);
			if (reply_mode == SF_SRM_CHAR)
			{
				for (i = 0; i < crm_nattr; i++)
				{
					obptr.Add(crm_attr[i]);
				}
			}
			return true;
		}

		/*
		 * Process a 3270 Erase All Unprotected command.
		 */
		public void ctlr_erase_all_unprotected()
		{
			int	baddr, sbaddr;
			byte fa;
			bool		f;

			telnet.keyboard.kybd_inhibit(false);

			ALL_CHANGED();
			if (formatted) 
			{
				/* find first field attribute */
				baddr = 0;
				do 
				{
					if (IS_FA(screen_buf[baddr]))
						break;
					INC_BA(ref baddr);
				} 
				while (baddr != 0);
				sbaddr = baddr;
				f = false;
				do 
				{
					fa = screen_buf[baddr];
					if (!FA_IS_PROTECTED(fa)) 
					{
						mdt_clear(screen_buf, baddr);//&screen_buf[baddr]);
						do 
						{
							INC_BA(ref baddr);
							if (!f) 
							{
								cursor_move(baddr);
								f = true;
							}
							if (!IS_FA(screen_buf[baddr])) 
							{
								ctlr_add(baddr, CG.CG_null, 0);
							}
						} while (!IS_FA(screen_buf[baddr]));
					}
					else 
					{
						do 
						{
							INC_BA(ref baddr);
						} while (!IS_FA(screen_buf[baddr]));
					}
				} while (baddr != sbaddr);
				if (!f)
					cursor_move(0);
			} 
			else 
			{
				ctlr_clear(true);
			}
			aid = AID.AID_NO;
			telnet.keyboard.do_reset(false);
		}

		enum PreviousEnum { NONE, ORDER, SBA, TEXT, NULLCH };
		PreviousEnum previous = PreviousEnum.NONE;
		private void END_TEXT0()
		{
			if (previous==PreviousEnum.TEXT)
				trace.trace_ds("'");
		}
		private void END_TEXT(string cmd)
		{
			END_TEXT0();
			trace.trace_ds(" "+cmd);
		}



		private byte ATTR2FA(byte attr)
		{
			return (byte)(FA_BASE | 
				(((attr) & 0x20)!=0 ? FA_PROTECT : (byte)0) | 
				(((attr) & 0x10)!=0 ? FA_NUMERIC : (byte)0) | 
				(((attr) & 0x01)!=0 ? FA_MODIFY : (byte)0) | 
				(((attr) >> 2) & FA_INTENSITY));
		}
		private void START_FIELDx(byte fa)
		{
			//current_fa = screen_buf[buffer_addr]; 
			current_fa_index = buffer_addr;
			ctlr_add(buffer_addr, fa, 0); 
			ctlr_add_fg(buffer_addr, 0); 
			ctlr_add_gr(buffer_addr, 0);
			trace.trace_ds(see.see_attr(fa)); 
			formatted = true; 
		}
		private void STARTFIELD0()
		{
			START_FIELDx(FA_BASE); 
		}
		private void START_FIELD(byte attr)
		{
			byte new_attr = ATTR2FA(attr);
			START_FIELDx(new_attr);
		}

		/*
		 * Process a 3270 Write command.
		 */
		public pds ctlr_write(byte[] buf, int start, int length, bool erase)
		{
			bool packetwasjustresetrewrite = false;
			//
			trace.WriteLine("::ctlr_write::"+((DateTime.Now.Ticks-startTime)/10000)+" "+length+" bytes");
			// resetrewrite is just : 00 00 00 00 00 f1 c2 ff ef
			if (length==4 && 
				buf[start+0]==0xf1 &&
				buf[start+1]==0xc2 &&
				buf[start+2]==0xff &&
				buf[start+3]==0xef)
			{
				trace.WriteLine("****Identified packet as a reset/rewrite combination. patch 29/Mar/2005 assumes more data will follow so does not notify user yet");
				packetwasjustresetrewrite = true;
			}

			pds rv = pds.PDS_OKAY_NO_OUTPUT;
			//register byte	*cp;
			int	baddr;
			//byte	buf[current_fa];
			byte	new_attr;
			bool		last_cmd;
			bool		last_zpt;
			bool		wcc_keyboard_restore, wcc_sound_alarm;
			bool		ra_ge;
			int		i;
			byte	na;
			int		any_fa;
			byte	efa_fg;
			byte	efa_gr;
			byte	efa_cs;
			string paren = "(";
			//enum { NONE, ORDER, SBA, TEXT, NULLCH } previous = NONE;


			telnet.keyboard.kybd_inhibit(false);

			if (buf.Length < 2)
				return pds.PDS_BAD_CMD;

			default_fg = 0;
			default_gr = 0;
			default_cs = 0;
			trace_primed = true;
			buffer_addr = cursor_addr;
			if (see.WCC_RESET(buf[start+1])) 
			{
				if (erase)
					reply_mode = SF_SRM_FIELD;
				trace.trace_ds("%sreset", paren);
				paren = ",";
			}
			wcc_sound_alarm = see.WCC_SOUND_ALARM(buf[start+1]);
			if (wcc_sound_alarm) 
			{
				trace.trace_ds("%salarm", paren);
				paren = ",";
			}
			wcc_keyboard_restore = see.WCC_KEYBOARD_RESTORE(buf[start+1]);
			if (wcc_keyboard_restore)
			{
				//Console.WriteLine("2218::ticking_stop");
				//ticking_stop();
			}
			if (wcc_keyboard_restore) 
			{
				trace.trace_ds("%srestore", paren);
				paren = ",";
			}

			if (see.WCC_RESET_MDT(buf[start+1])) 
			{
				trace.trace_ds("%sresetMDT", paren);
				paren = ",";
				baddr = 0;
				if (appres.modified_sel)
					ALL_CHANGED();
				do 
				{
					if (IS_FA(screen_buf[baddr])) 
					{
						mdt_clear(screen_buf, baddr);
					}
					INC_BA(ref baddr);
				} 
				while (baddr != 0);
			}
			if (paren != "(")
				trace.trace_ds(")");

			last_cmd = true;
			last_zpt = false;
			current_fa_index = get_field_attribute(buffer_addr);
			//current_fa = 
			int cp;
			for (cp = 2; cp < (length); cp++)
			{
				switch (buf[cp+start]) 
				{
					case ORDER_SF:	/* start field */
						END_TEXT("StartField");
						if (previous != PreviousEnum.SBA)
							trace.trace_ds(trace.rcba(buffer_addr));
						previous = PreviousEnum.ORDER;
						cp++;		/* skip field attribute */
						START_FIELD(buf[cp+start]);
						ctlr_add_fg(buffer_addr, 0);
						INC_BA(ref buffer_addr);
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_SBA:	/* set buffer address */
						cp += 2;	/* skip buffer address */
						buffer_addr = DECODE_BADDR(buf[cp+start-1], buf[cp+start]);
						END_TEXT("SetBufferAddress");
						previous = PreviousEnum.SBA;
						trace.trace_ds(trace.rcba(buffer_addr));
						if (buffer_addr >= COLS * ROWS) 
						{
							trace.trace_ds(" [invalid address, write command terminated]\n");
							/* Let a script go. */
							telnet.events.RunScript("ctlr_write SBA_ERROR");
							//sms_host_output();
							return pds.PDS_BAD_ADDR;
						}
						current_fa_index = get_field_attribute(buffer_addr);
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_IC:	/* insert cursor */
						END_TEXT("InsertCursor");
						if (previous != PreviousEnum.SBA)
							trace.trace_ds(trace.rcba(buffer_addr));
						previous = PreviousEnum.ORDER;
						cursor_move(buffer_addr);
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_PT:	/* program tab */
						END_TEXT("ProgramTab");
						previous = PreviousEnum.ORDER;
						/*
						 * If the buffer address is the field attribute of
						 * of an unprotected field, simply advance one
						 * position.
						 */
						if (IS_FA(screen_buf[buffer_addr]) &&
							!FA_IS_PROTECTED(screen_buf[buffer_addr])) 
						{
							INC_BA(ref buffer_addr);
							last_zpt = false;
							last_cmd = true;
							break;
						}
						/*
						 * Otherwise, advance to the first position of the
						 * next unprotected field.
						 */
						baddr = next_unprotected(buffer_addr);
						if (baddr < buffer_addr)
							baddr = 0;
						/*
						 * Null out the remainder of the current field -- even
						 * if protected -- if the PT doesn't follow a command
						 * or order, or (honestly) if the last order we saw was
						 * a null-filling PT that left the buffer address at 0.
						 */
						if (!last_cmd || last_zpt) 
						{
							trace.trace_ds("(nulling)");
							while ((buffer_addr != baddr) &&
								(!IS_FA(screen_buf[buffer_addr]))) 
							{
								ctlr_add(buffer_addr, CG.CG_null, 0);
								INC_BA(ref buffer_addr);
							}
							if (baddr == 0)
								last_zpt = true;
						} 
						else
							last_zpt = false;
						buffer_addr = baddr;
						last_cmd = true;
						break;
					case ORDER_RA:	/* repeat to address */
						END_TEXT("RepeatToAddress");
						cp += 2;	/* skip buffer address */
						baddr = DECODE_BADDR(buf[cp+start-1], buf[cp+start]);
						trace.trace_ds(trace.rcba(baddr));
						cp++;		/* skip char to repeat */
						if (buf[cp+start] == ORDER_GE)
						{
							ra_ge = true;
							trace.trace_ds("GraphicEscape");
							cp++;
						} 
						else
							ra_ge = false;
						previous = PreviousEnum.ORDER;
						if (buf[cp+start]!=0)
							trace.trace_ds("'");
						trace.trace_ds("%s", see.see_ebc(buf[cp+start]));
						if (buf[cp+start]!=0)
							trace.trace_ds("'");
						if (baddr >= COLS * ROWS) 
						{
							trace.trace_ds(" [invalid address, write command terminated]\n");
							/* Let a script go. */
							//sms_host_output();
							telnet.events.RunScript("ctlr_write baddr>COLS*ROWS");
							return pds.PDS_BAD_ADDR;
						}
						do 
						{
							if (ra_ge)
								ctlr_add(buffer_addr, Tables.ebc2cg0[buf[cp+start]],
									CS_GE);
							else if (default_cs!=0)
								ctlr_add(buffer_addr, Tables.ebc2cg0[buf[cp+start]], 1);
							else
								ctlr_add(buffer_addr, Tables.ebc2cg[buf[cp+start]], 0);
							ctlr_add_fg(buffer_addr, default_fg);
							ctlr_add_gr(buffer_addr, default_gr);
							INC_BA(ref buffer_addr);
						} while (buffer_addr != baddr);
						current_fa_index = get_field_attribute(buffer_addr);
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_EUA:	/* erase unprotected to address */
						cp += 2;	/* skip buffer address */
						baddr = DECODE_BADDR(buf[cp+start-1], buf[cp+start]);
						END_TEXT("EraseUnprotectedAll");
						if (previous != PreviousEnum.SBA)
							trace.trace_ds(trace.rcba(baddr));
						previous = PreviousEnum.ORDER;
						if (baddr >= COLS * ROWS) 
						{
							trace.trace_ds(" [invalid address, write command terminated]\n");
							/* Let a script go. */
							//				sms_host_output();
							telnet.events.RunScript("ctlr_write baddr>COLS*ROWS#2");
							return pds.PDS_BAD_ADDR;
						}
						do 
						{
							if (IS_FA(screen_buf[buffer_addr]))
								current_fa_index = buffer_addr;
							else if (!FA_IS_PROTECTED(screen_buf[current_fa_index])) 
							{
								ctlr_add(buffer_addr, CG.CG_null, 0);
							}
							INC_BA(ref buffer_addr);
						} while (buffer_addr != baddr);
						current_fa_index = get_field_attribute(buffer_addr);
			
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_GE:	/* graphic escape */
						END_TEXT("GraphicEscape ");
						cp++;		/* skip char */
						previous = PreviousEnum.ORDER;
						if (buf[cp+start]!=0)
							trace.trace_ds("'");
						trace.trace_ds("%s", see.see_ebc(buf[cp+start]));
						if (buf[cp+start]!=0)
							trace.trace_ds("'");
						ctlr_add(buffer_addr, Tables.ebc2cg0[buf[cp+start]], CS_GE);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						current_fa_index = get_field_attribute(buffer_addr);
						last_cmd = false;
						last_zpt = false;
						break;
					case ORDER_MF:	/* modify field */
						END_TEXT("ModifyField");
						if (previous != PreviousEnum.SBA)
							trace.trace_ds(trace.rcba(buffer_addr));
						previous = PreviousEnum.ORDER;
						cp++;
						na = buf[cp+start];
						if (IS_FA(screen_buf[buffer_addr])) 
						{
							for (i = 0; i < (int)na; i++) 
							{
								cp++;
								if (buf[cp+start] == see.XA_3270) 
								{
									trace.trace_ds(" 3270");
									cp++;
									new_attr = ATTR2FA(buf[cp+start]);
									ctlr_add(buffer_addr,
										new_attr,
										0);
									trace.trace_ds(see.see_attr(new_attr));
								} 
								else if (buf[cp+start] == see.XA_FOREGROUND) 
								{
									trace.trace_ds("%s",
										see.see_efa(buf[cp+start],
										buf[cp+start+1]));
									cp++;
									if (appres.m3279)
										ctlr_add_fg(buffer_addr, buf[cp+start]);
								} 
								else if (buf[cp+start] == see.XA_HIGHLIGHTING) 
								{
									trace.trace_ds("%s",
										see.see_efa(buf[cp+start],
										buf[cp+start+1]));
									cp++;
									ctlr_add_gr(buffer_addr, (byte)(buf[cp+start] & 0x07));
								} 
								else if (buf[cp+start] == see.XA_CHARSET) 
								{
									int cs = 0;

									trace.trace_ds("%s",
										see.see_efa(buf[cp+start],
										buf[cp+start+1]));
									cp++;
									if (buf[cp+start] == 0xf1)
										cs = 1;
									ctlr_add(buffer_addr,
										screen_buf[buffer_addr], (byte)cs);
								} 
								else if (buf[cp+start] == see.XA_ALL) 
								{
									trace.trace_ds("%s",
										see.see_efa(buf[cp+start],
										buf[cp+start+1]));
									cp++;
								} 
								else 
								{
									trace.trace_ds("%s[unsupported]", see.see_efa(buf[cp+start], buf[cp+start+1]));
									cp++;
								}
							}
							INC_BA(ref buffer_addr);
						} 
						else
							cp += na * 2;
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_SFE:	/* start field extended */
						END_TEXT("StartFieldExtended");
						if (previous != PreviousEnum.SBA)
							trace.trace_ds(trace.rcba(buffer_addr));
						previous = PreviousEnum.ORDER;
						cp++;	/* skip order */
						na = buf[cp+start];
						any_fa = 0;
						efa_fg = 0;
						efa_gr = 0;
						efa_cs = 0;
						for (i = 0; i < (int)na; i++) 
						{
							cp++;
							if (buf[cp+start] == see.XA_3270) 
							{
								trace.trace_ds(" 3270");
								cp++;
								START_FIELD(buf[cp+start]);
								any_fa++;
							} 
							else if (buf[cp+start] == see.XA_FOREGROUND) 
							{
								trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
								cp++;
								if (appres.m3279)
									efa_fg = buf[cp+start];
							} 
							else if (buf[cp+start] == see.XA_HIGHLIGHTING) 
							{
								trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
								cp++;
								efa_gr = (byte)(buf[cp+start] & 0x07);
							} 
							else if (buf[cp+start] == see.XA_CHARSET) 
							{
								trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
								cp++;
								if (buf[cp+start] == 0xf1)
									efa_cs = 1;
							} 
							else if (buf[cp+start] == see.XA_ALL) 
							{
								trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
								cp++;
							} 
							else 
							{
								trace.trace_ds("%s[unsupported]", see.see_efa(buf[cp+start], buf[cp+start+1]));
								cp++;
							}
						}
						if (any_fa==0)
						{
							START_FIELDx(FA_BASE);
						}
						ctlr_add(buffer_addr, screen_buf[buffer_addr], efa_cs);
						ctlr_add_fg(buffer_addr, efa_fg);
						ctlr_add_gr(buffer_addr, efa_gr);
						INC_BA(ref buffer_addr);
						last_cmd = true;
						last_zpt = false;
						break;
					case ORDER_SA:	/* set attribute */
						END_TEXT("SetAttribtue");
						previous = PreviousEnum.ORDER;
						cp++;
						if (buf[cp+start] == see.XA_FOREGROUND)  
						{
							trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
							if (appres.m3279)
								default_fg = buf[cp+start+1];
						} 
						else if (buf[cp+start] == see.XA_HIGHLIGHTING)  
						{
							trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
							default_gr = (byte)(buf[cp+start+1] & 0x07);
						} 
						else if (buf[cp+start] == see.XA_ALL)  
						{
							trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
							default_fg = 0;
							default_gr = 0;
							default_cs = 0;
						} 
						else if (buf[cp+start] == see.XA_CHARSET) 
						{
							trace.trace_ds("%s", see.see_efa(buf[cp+start], buf[cp+start+1]));
							default_cs = (buf[cp+start+1] == 0xf1) ? (byte)1 : (byte)0;
						} 
						else
							trace.trace_ds("%s[unsupported]",
								see.see_efa(buf[cp+start], buf[cp+start+1]));
						cp++;
						last_cmd = true;
						last_zpt = false;
						break;
					case FCORDER_SUB:	/* format control orders */
					case FCORDER_DUP:
					case FCORDER_FM:
					case FCORDER_FF:
					case FCORDER_CR:
					case FCORDER_NL:
					case FCORDER_EM:
					case FCORDER_EO:
						END_TEXT(see.see_ebc(buf[cp+start]));
						previous = PreviousEnum.ORDER;
						ctlr_add(buffer_addr, Tables.ebc2cg[buf[cp+start]], default_cs);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						last_cmd = true;
						last_zpt = false;
						break;
					case FCORDER_NULL:
						END_TEXT("NULL");
						previous = PreviousEnum.NULLCH;
						ctlr_add(buffer_addr, Tables.ebc2cg[buf[cp+start]], default_cs);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						last_cmd = false;
						last_zpt = false;
						break;
					default:	/* enter character */
						if (buf[cp+start] <= 0x3F) 
						{
							END_TEXT("ILLEGAL_ORDER");
							trace.trace_ds("%s", see.see_ebc(buf[cp+start]));
							last_cmd = true;
							last_zpt = false;
							break;
						}
						if (previous != PreviousEnum.TEXT)
							trace.trace_ds(" '");
						previous = PreviousEnum.TEXT;
						trace.trace_ds("%s", see.see_ebc(buf[cp+start]));
						ctlr_add(buffer_addr, Tables.ebc2cg[buf[cp+start]], default_cs);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						last_cmd = false;
						last_zpt = false;
						break;
				}
			}
			set_formatted();
			if (previous == PreviousEnum.TEXT) 
				trace.trace_ds("'");
			//
			trace.trace_ds("\n");
			if (wcc_keyboard_restore) 
			{
				aid = AID.AID_NO;
				telnet.keyboard.do_reset(false);
			} 
			else if ((telnet.keyboard.kybdlock & Keyboard.KL_OIA_TWAIT)!=0)
			{
				telnet.keyboard.kybdlock_clr(Keyboard.KL_OIA_TWAIT, "ctlr_write");
				//status_syswait();
			}
			if (wcc_sound_alarm)
			{
				//	ring_bell();
			}

			trace_primed = false;

			ps_process();

			/* Let a script go. */
			if (!packetwasjustresetrewrite)
				telnet.events.RunScript("ctlr_write - end");
			//sms_host_output();

			return rv;
		}

		/*
		 * Write SSCP-LU data, which is quite a bit dumber than regular 3270
		 * output.
		 */
		public void ctlr_write_sscp_lu(byte[] buf, int start, int buflen)
		{
			int i;
			int cp = start;
			//byte *cp = buf;
			int s_row;
			byte c;
			int baddr;
			byte fa;

			/*
			 * The 3174 Functionl Description says that anything but NL, NULL, FM,
			 * or DUP is to be displayed as a graphic.  However, to deal with
			 * badly-behaved hosts, we filter out SF, IC and SBA sequences, and
			 * we display other control codes as spaces.
			 */

			trace.trace_ds("SSCP-LU data\n");
			cp = start;
			for (i = 0; i < buflen; cp++, i++) 
			{
				switch (buf[cp]) 
				{
					case FCORDER_NL:
						/*
						 * Insert NULLs to the end of the line and advance to
						 * the beginning of the next line.
						 */
						s_row = buffer_addr / COLS;
						while ((buffer_addr / COLS) == s_row) 
						{
							ctlr_add(buffer_addr, Tables.ebc2cg[0], default_cs);
							ctlr_add_fg(buffer_addr, default_fg);
							ctlr_add_gr(buffer_addr, default_gr);
							INC_BA(ref buffer_addr);
						}
						break;

					case ORDER_SF:	/* some hosts forget their talking SSCP-LU */
						cp++;
						i++;
						fa = ATTR2FA(buf[cp]);
						trace.trace_ds(" StartField"+trace.rcba(buffer_addr)+" "+see.see_attr(fa)+" [translated to space]\n");
						ctlr_add(buffer_addr, CG.CG_space, default_cs);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						break;
					case ORDER_IC:
						trace.trace_ds(" InsertCursor%s [ignored]\n",trace.rcba(buffer_addr));
						break;
					case ORDER_SBA:
						baddr = DECODE_BADDR(buf[cp+1], buf[cp+2]);
						trace.trace_ds(" SetBufferAddress%s [ignored]\n", trace.rcba(baddr));
						cp += 2;
						i += 2;
						break;

					case ORDER_GE:
						cp++;
						if (++i >= buflen)
							break;
						if (buf[cp] <= 0x40)
							c = CG.CG_space;
						else
							c = Tables.ebc2cg0[buf[cp]];
						ctlr_add(buffer_addr, c, CS_GE);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						break;

					default:
						if (buf[cp] == FCORDER_NULL)
							c = CG.CG_space;
						else if (buf[cp] == FCORDER_FM)
							c = CG.CG_asterisk;
						else if (buf[cp] == FCORDER_DUP)
							c = CG.CG_semicolon;
						else if (buf[cp] < 0x40) 
						{
							trace.trace_ds(" X'"+buf[cp]+"') [translated to space]\n");
							c = CG.CG_space; /* technically not necessary */
						} 
						else
							c = Tables.ebc2cg[buf[cp]];
						ctlr_add(buffer_addr, c, default_cs);
						ctlr_add_fg(buffer_addr, default_fg);
						ctlr_add_gr(buffer_addr, default_gr);
						INC_BA(ref buffer_addr);
						break;
				}
			}
			cursor_move(buffer_addr);
			sscp_start = buffer_addr;

			/* Unlock the keyboard. */
			aid = AID.AID_NO;
			telnet.keyboard.do_reset(false);

			/* Let a script go. */
			telnet.events.RunScript("ctlr_write_sscp_lu done");
			//sms_host_output();
		}


		/*
		 * Process pending input.
		 */
		public void ps_process()
		{
			// process type ahead queue
			while (telnet.keyboard.run_ta())
				;
			// notify script we're ok
			//Console.WriteLine("--sms_continue");

			sms_continue();
		}
		public void sms_continue()
		{
			lock (telnet)
			{
				switch (telnet.WaitState)
				{
					case sms_state.SS_IDLE:
						break;
					case sms_state.SS_KBWAIT:
						if (telnet.KBWAIT)
						{
							telnet.WaitEvent.Set();
						}
						break;
					case sms_state.SS_WAIT_ANSI:
						if (telnet.IN_ANSI)
						{
							telnet.WaitEvent.Set();
						}
						break;
					case sms_state.SS_WAIT_3270:
						if (telnet.IN_3270 | telnet.IN_SSCP)
						{
							telnet.WaitEvent.Set();
						}
						break;
					case sms_state.SS_WAIT:
						if (!telnet.CAN_PROCEED)
							break;
						if (telnet.HALF_CONNECTED ||
							(telnet.CONNECTED && (telnet.keyboard.kybdlock & Keyboard.KL_AWAITING_FIRST)!=0)) 
							break;
						// do stuff
						telnet.WaitEvent.Set();

						break;
					case sms_state.SS_CONNECT_WAIT:
						if (telnet.HALF_CONNECTED ||
							(telnet.CONNECTED && (telnet.keyboard.kybdlock & Keyboard.KL_AWAITING_FIRST)!=0)) 
							break;
						// do stuff
						telnet.WaitEvent.Set();
						break;
					default:
						Console.WriteLine("**BUGBUG**IGNORED STATE "+telnet.WaitState);
						break;
				}
			}
		}
		/*
		 * Tell me if there is any data on the screen.
		 */
		bool ctlr_any_data()
		{
			int c = 0;
			int i;
			byte oc;

			for (i = 0; i < ROWS*COLS; i++) 
			{
				oc = screen_buf[c++];
				if (!IS_FA(oc) && !IsBlank(oc))
					return true;
			}
			return false;
		}

		/*
		 * Clear the text (non-status) portion of the display.  Also resets the cursor
		 * and buffer addresses and extended attributes.
		 */
		public void ctlr_clear(bool can_snap)
		{
			/* Snap any data that is about to be lost into the trace file. */
			if (ctlr_any_data()) 
			{
				if (can_snap && !trace_skipping && appres.toggled(Appres.SCREEN_TRACE))
				{
					trace.trace_screen();
				}
				//		scroll_save(maxROWS, ever_3270 ? false : true);
			}
			trace_skipping = false;

			/* Clear the screen. */
			int i;
			for (i=0; i<ROWS*COLS; i++)
			{
				screen_buf[i] = 0;
				ea_buf[i] = new ExtendedAttribute();
			}
			//memset((char *)screen_buf, 0, ROWS*COLS);
			//memset((char *)ea_buf, 0, ROWS*COLS*sizeof(struct ea));
			ALL_CHANGED();
			cursor_move(0);
			buffer_addr = 0;
			//	unselect(0, ROWS*COLS);
			formatted = false;
			default_fg = 0;
			default_gr = 0;
			sscp_start = 0;
		}

		/*
		 * Fill the screen buffer with blanks.
		 */
		void ctlr_blanks()
		{
			int i;
			for (i=0; i<ROWS*COLS; i++)
			{
				screen_buf[i] = CG.CG_space;
			}
			ALL_CHANGED();
			cursor_move(0);
			buffer_addr = 0;
			//	unselect(0, ROWS*COLS);
			formatted = false;
		}


		/*
		 * Change a character in the 3270 buffer.
		 */
		public void ctlr_add(int baddr, byte c, byte cs)
		{
			byte oc;
			// debug
			char ch = System.Convert.ToChar(Tables.cg2asc[c]);

			if ((oc = screen_buf[baddr]) != c || ea_buf[baddr].cs != cs) 
			{
				if (trace_primed && !IsBlank(oc)) 
				{
					if (appres.toggled(Appres.SCREEN_TRACE))
					{
						trace.trace_screen();
					}
					//			scroll_save(maxROWS, false);
					trace_primed = false;
				}
				//		if (SELECTED(baddr))
				//			unselect(baddr, 1);
				ONE_CHANGED(baddr);
				screen_buf[baddr] = c;
				ea_buf[baddr].cs = cs;
			}
			//			Dump();
		}

		/*
		 * Change the graphic rendition of a character in the 3270 buffer.
		 */
		public void ctlr_add_gr(int baddr, byte gr)
		{
			if (ea_buf[baddr].gr != gr) 
			{
				//		if (SELECTED(baddr))
				//			unselect(baddr, 1);
				ONE_CHANGED(baddr);
				ea_buf[baddr].gr = gr;
				//		if (gr & GR_BLINK)
				//			blink_start();
			}
		}

		/*
		 * Change the foreground color for a character in the 3270 buffer.
		 */
		public void ctlr_add_fg(int baddr, byte color)
		{
			if (!appres.m3279)
				return;
			if ((color & 0xf0) != 0xf0)
				color = 0;
			if (ea_buf[baddr].fg != color) 
			{
				//		if (SELECTED(baddr))
				//			unselect(baddr, 1);
				ONE_CHANGED(baddr);
				ea_buf[baddr].fg = color;
			}
		}

		/*
		 * Change the background color for a character in the 3270 buffer.
		 */
		public void ctlr_add_bg(int baddr, byte color)
		{
			if (!appres.m3279)
				return;
			if ((color & 0xf0) != 0xf0)
				color = 0;
			if (ea_buf[baddr].bg != color) 
			{
				//		if (SELECTED(baddr))
				//			unselect(baddr, 1);
				ONE_CHANGED(baddr);
				ea_buf[baddr].bg = color;
			}
		}

		/*
		 * Copy a block of characters in the 3270 buffer, optionally including all of
		 * the extended attributes.  (The character set, which is actually kept in the
		 * extended attributes, is considered part of the characters here.)
		 */
		public void ctlr_bcopy(int baddr_from, int baddr_to, int count, bool move_ea)
		{
			bool changed = false;
			int i;
			for (i=0; i<count; i++)
			{
				if (screen_buf[baddr_from+i] != screen_buf[baddr_to+i])
				{
					screen_buf[baddr_from+i] = screen_buf[baddr_to+i];
					changed = true;
				}
			}
			if (changed)
			{
				REGION_CHANGED(baddr_to, baddr_to + count);
				/*
				 * For the time being, if any selected text shifts around on
				 * the screen, unhighlight it.  Eventually there should be
				 * logic for preserving the highlight if the *all* of the
				 * selected text moves.
				 */
				//if (area_is_selected(baddr_to, count))
				//	unselect(baddr_to, count);
			}

			/*
			 * If we aren't supposed to move all the extended attributes, move
			 * the character sets separately.
			 */
			if (!move_ea) 
			{
				int any = 0;
				int start, end, inc;

				if (baddr_to < baddr_from || baddr_from + count < baddr_to) 
				{
					/* Scan forward. */
					start = 0;
					end = count + 1;
					inc = 1;
				} 
				else 
				{
					/* Scan backward. */
					start = count - 1;
					end = -1;
					inc = -1;
				}

				for (i = start; i != end; i += inc) 
				{
					if (ea_buf[baddr_to+i].cs != ea_buf[baddr_from+i].cs) 
					{
						ea_buf[baddr_to+i].cs = ea_buf[baddr_from+i].cs;
						REGION_CHANGED(baddr_to + i, baddr_to + i + 1);
						any++;
					}
				}
				//if (any && area_is_selected(baddr_to, count))
				//	unselect(baddr_to, count);
			}

			/* Move extended attributes. */
			if (move_ea)
			{
				changed = false;
				for (i=0; i<count; i++)
				{
					if (ea_buf[baddr_from+i] != ea_buf[baddr_to+i])
					{
						ea_buf[baddr_from+i] = ea_buf[baddr_to+i];
						changed = true;
					}
				}
				if (changed)
				{
					REGION_CHANGED(baddr_to, baddr_to + count);
				}
			}
		}

		/*
		 * Erase a region of the 3270 buffer, optionally clearing extended attributes
		 * as well.
		 */
		public void ctlr_aclear(int baddr, int count, bool clear_ea)
		{
			//Console.WriteLine("ctlr_aclear - bugbug - compare to c code");
			int i;
			bool changed = false;
			for (i=0; i<count; i++)
			{
				if (screen_buf[baddr] != 0)
				{
					screen_buf[baddr] = 0;
					changed = true;
				}
			}
			if (changed)
			{
				REGION_CHANGED(baddr, baddr + count);
				//		if (area_is_selected(baddr, count))
				//			unselect(baddr, count);
			}
			if (clear_ea)
			{
				changed = false;
				for (i=0; i<count; i++)
				{
					if (!ea_buf[baddr+i].IsZero)
					{
						ea_buf[baddr+i] = new ExtendedAttribute();
						changed = true;
					}
				}
				if (changed)
				{
					REGION_CHANGED(baddr, baddr + count);
				}
			}
		}

		/*
		 * Scroll the screen 1 row.
		 *
		 * This could be accomplished with ctlr_bcopy() and ctlr_aclear(), but this
		 * operation is common enough to warrant a separate path.
		 */
		public void ctlr_scroll()
		{
			throw new ApplicationException("ctlr_scroll not implemented");
		}

		/*
		 * Note that a particular region of the screen has changed.
		 */
		void ctlr_changed(int bstart, int bend)
		{
			REGION_CHANGED(bstart, bend);
		}

		/*
		 * Swap the regular and alternate screen buffers
		 */
		public void ctlr_altbuffer(bool alt)
		{
	
			byte[] stmp;
			ExtendedAttribute[] etmp;


			if (alt != is_altbuffer) 
			{

				stmp = screen_buf;
				screen_buf = ascreen_buf;
				ascreen_buf = stmp;

				etmp = ea_buf;
				ea_buf = aea_buf;
				aea_buf = etmp;

				is_altbuffer = alt;
				ALL_CHANGED();
				//		unselect(0, ROWS*COLS);

				/*
				 * There may be blinkers on the alternate screen; schedule one
				 * iteration just in case.
				 */
				//		blink_start();
			}
		}


		/*
		 * Set or clear the MDT on an attribute
		 */

		public void mdt_set(byte[] data, int offset)
		{
			// mfw
			if (offset==-1)
				return; 

			if ((data[offset] & FA_MODIFY)!=0)
				return;
			data[offset] |= FA_MODIFY;
			if (appres.modified_sel)
				ALL_CHANGED();
		}

		public void mdt_clear(byte[] data, int offset)
		{
			if ((data[offset] & FA_MODIFY)==0)
				return;
			data[offset] &= FA_MODIFY_MASK;//(byte)~FA_MODIFY;
			if (appres.modified_sel)
				ALL_CHANGED();
		}


		/*
		 * Support for screen-size swapping for scrolling
		 */
		void ctlr_shrink()
		{
			int i;
			for (i=0; i<ROWS*COLS; i++)
			{
				screen_buf[i] = debugging_font ? CG.CG_space : CG.CG_null;
			}
			ALL_CHANGED();
			//	screen_disp(false);
		}

		public int cursorX
		{
			get { return BA_TO_COL(cursor_addr);}
		}
		public int cursorY
		{
			get { return BA_TO_ROW(cursor_addr);}
		}

		public void cursor_move(int addr)
		{
			//Console.WriteLine("cursor_move @"+addr);
			cursor_addr = addr;
		}
		public int BA_TO_ROW(int ba)	
		{
			return ((ba) / COLS);
		}
		public int BA_TO_COL(int ba)
		{
			return ba%COLS;
		}
		public int ROWCOL_TO_BA(int r, int c)
		{
			return (((r) * COLS) + c);
		}
		public void INC_BA(ref int ba)
		{ 
			(ba) = ((ba) + 1) % (COLS * ROWS); 
		}
		public void DEC_BA(ref int ba)
		{
			(ba) = (ba!=0) ? (ba - 1) : ((COLS*ROWS) - 1); 
		}

		public void RemoveTimeOut(System.Threading.Timer id)
		{
			//Console.WriteLine("remove timeout");
			if (id != null)
				id.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

		}
		public  System.Threading.Timer AddTimeout(int ms, System.Threading.TimerCallback callback)
		{
			//Console.WriteLine("add timeout");
			System.Threading.Timer timer = new System.Threading.Timer(callback, this, ms, 0);
			return timer;

		}
		// cursor ops
		public bool MoveCursor(CursorOp op, int x, int y)
		{
			int baddr;
			int sbaddr;
			int nbaddr;
			switch (op)
			{
				case CursorOp.Exact:
				case CursorOp.NearestUnprotectedField:
					if (!telnet.IN_3270) 
					{
						x--;
						y--;
					}
					if (x < 0)
						x = 0;
					if (y < 0)
						y = 0;
					baddr = ((y * COLS) + x) % (ROWS * COLS);
					if (op==CursorOp.Exact)
						cursor_move(baddr);
					else
						cursor_move(next_unprotected(cursor_addr));
					return true;
					

				case CursorOp.Tab:
					if (telnet.IN_ANSI) 
					{
						telnet.net_sendc('\t');
						return true;
					}
					else
						cursor_move(next_unprotected(cursor_addr));
					return true;

				case CursorOp.BackTab:
					if (!telnet.IN_3270)
						return false;
					//
					baddr = cursor_addr;
					DEC_BA(ref baddr);
					if (IS_FA(screen_buf[baddr]))	/* at bof */
						DEC_BA(ref baddr);
					sbaddr = baddr;
					while (true) 
					{
						nbaddr = baddr;
						INC_BA(ref nbaddr);
						if (IS_FA(screen_buf[baddr])
							&&  !FA_IS_PROTECTED(screen_buf[baddr])
							&&  !IS_FA(screen_buf[nbaddr]))
							break;
						DEC_BA(ref baddr);
						if (baddr == sbaddr) 
						{
							cursor_move(0);
							return true;
						}
					}
					INC_BA(ref baddr);
					cursor_move(baddr);
					return true;
				default:
					throw new ApplicationException("Sorry, cursor op '"+op+"' not implemented");
			}
		}

		// start
		public void dump_range(int first, int len, bool in_ascii, byte[] buf, int rel_rows, int rel_cols)
		{
			int i;
			bool any = false;
			byte[] linebuf = new byte[maxCOLS*3+1];
			int s=0;
			string debug="";

			/*
			 * If the client has looked at the live screen, then if they later
			 * execute 'Wait(output)', they will need to wait for output from the
			 * host.  output_wait_needed is cleared by sms_host_output,
			 * which is called from the write logic in ctlr.c.
			 */     
			//	if (sms != SN && buf == screen_buf)
			//		sms->output_wait_needed = True;

			for (i = 0; i < len; i++) 
			{
				byte c;

				if (i!=0 && 0==((first + i) % rel_cols)) 
				{
					linebuf[s] = 0;
					telnet.action.action_output(linebuf, s);
					s = 0;
					debug="";
					any = false;
				}
				if (!any)
					any = true;
				if (in_ascii) 
				{
					c = Tables.cg2asc[buf[first + i]];
					linebuf[s++] = (c==0)?(byte)' ':c;
					if (c==0)
						debug+=" ";
					else
						debug+=System.Convert.ToChar(c);
				} 
				else 
				{
					string temp = String.Format("{0}{1:x2}", i!=0?" ":"", Tables.cg2ebc[buf[first + i]]);
					int tt;
					for (tt=0;tt<temp.Length;tt++)
					{
						linebuf[s++] = (byte)temp[tt];
					}
				}
			}
			if (any) 
			{
				linebuf[s] = 0;
				telnet.action.action_output(linebuf, s);
			}
		}
		
		void dump_rangeXML(int first, int len, bool in_ascii, byte[] buf, int rel_rows, int rel_cols)
		{
			int i;
			bool any = false;
			byte[] linebuf = new byte[maxCOLS*3*5+1];
			int s = 0;
			if (!in_ascii)
				throw new ApplicationException("sorry, dump_rangeXML only valid for ascii buffer");

			/*
			 * If the client has looked at the live screen, then if they later
			 * execute 'Wait(output)', they will need to wait for output from the
			 * host.  output_wait_needed is cleared by sms_host_output,
			 * which is called from the write logic in ctlr.c.
			 */     
			//if (sms != SN && buf == screen_buf)
			//	sms->output_wait_needed = True;

			for (i = 0; i < len; i++) 
			{
				byte c;

				if (i!=0 && 0==((first + i) % rel_cols)) 
				{
					linebuf[s] = 0;
					telnet.action.action_output(linebuf, s);
					s = 0;
					any = false;
				}
				if (!any)
					any = true;
				//
				c = Tables.cg2asc[buf[first + i]];
				if (c==0) c=(byte)' ';
				string temp = "";
				/*switch ((char)c)
				{
					case '&': temp ="&amp;"; break;
					case '<': temp ="&lt;"; break;
					case '>': temp ="&gt;"; break;
					//case '\'': temp ="&apos;"; break;
					//case '\"': temp ="&quot;"; break;
				 
					default:
						temp = ""+System.Convert.ToChar(c);
						break;
				}
				*/
				temp = ""+System.Convert.ToChar(c);
				int tt;
				for (tt=0; tt<temp.Length; tt++)
				{
					linebuf[s++] = (byte)temp[tt];
				}
			}
			if (any) 
			{
				linebuf[s] = 0;
				telnet.action.action_output(linebuf, s, true);
			}
		}



		bool dump_fixed(object[] args, string name, bool in_ascii,byte[] buf, int rel_rows, int rel_cols, int caddr)
		{
			int row, col, len, rows = 0, cols = 0;

			switch (args.Length) 
			{
				case 0:	/* everything */
					row = 0;
					col = 0;
					len = rel_rows*rel_cols;
					break;
				case 1:	/* from cursor, for n */
					row = caddr / rel_cols;
					col = caddr % rel_cols;
					len = (int)args[0];
					break;
				case 3:	/* from (row,col), for n */
					row = (int)args[0];
					col = (int)args[1];
					len = (int)args[2];
					break;
				case 4:	/* from (row,col), for rows x cols */
					row = (int)args[0];
					col = (int)args[1];
					rows = (int)args[2];
					cols = (int)args[3];
					len = 0;
					break;
				default:
					telnet.events.popup_an_error(name+" requires 0, 1, 3 or 4 arguments");
					return false;
			}

			if (
				(row < 0 || row > rel_rows || col < 0 || col > rel_cols || len < 0) ||
				((args.Length < 4)  && ((row * rel_cols) + col + len > rel_rows * rel_cols)) ||
				((args.Length == 4) && (cols < 0 || rows < 0 ||
				col + cols > rel_cols || row + rows > rel_rows))
				) 
			{
				telnet.events.popup_an_error(name+": Invalid argument", name);
				return false;
			}
			if (args.Length< 4)
				dump_range((row * rel_cols) + col, len, in_ascii, buf,
					rel_rows, rel_cols);
			else 
			{
				int i;

				for (i = 0; i < rows; i++)
					dump_range(((row+i) * rel_cols) + col, cols, in_ascii,
						buf, rel_rows, rel_cols);
			}
			return true;
		}

		bool dump_field(string name, bool in_ascii)
		{
			int fa_index;
			byte fa = this.fake_fa;
			int start, baddr;
			int len = 0;

			if (!formatted) 
			{
				telnet.events.popup_an_error(name+": Screen is not formatted");
				return false;
			}
			fa_index = get_field_attribute(cursor_addr);
			start = fa_index;
			INC_BA(ref start);
			baddr = start;
			do 
			{
				if (IS_FA(screen_buf[baddr]))
					break;
				len++;
				INC_BA(ref baddr);
			} while (baddr != start);
			dump_range(start, len, in_ascii, screen_buf, ROWS, COLS);
			return true;
		}

		int dump_fieldAsXML(int address, ExtendedAttribute ea)
		{
			byte fa = this.fake_fa;
			int fa_index;
			int start, baddr;
			int len = 0;
	

			fa_index = get_field_attribute(address);
			if (fa_index != -1)
				fa = screen_buf[fa_index];
			start = fa_index;
			INC_BA(ref start);
			baddr = start;
			do 
			{
				if (IS_FA(screen_buf[baddr]))
				{
				
					if (ea_buf[baddr].fg != 0)	ea.fg = ea_buf[baddr].fg;
					if (ea_buf[baddr].bg != 0)	ea.bg = ea_buf[baddr].bg;
					if (ea_buf[baddr].cs != 0)	ea.cs = ea_buf[baddr].cs;
					if (ea_buf[baddr].gr != 0)	ea.gr = ea_buf[baddr].gr;
		
					break;
				}
				len++;
				INC_BA(ref baddr);
			} 
			while (baddr != start);

			int col_start = BA_TO_COL(start);
			int row_start = BA_TO_ROW(start);
			int row_end   = BA_TO_ROW(baddr)+1;
			int remaining_len = len;

			int rowcount;

			for (rowcount = row_start; rowcount < row_end; rowcount++)
			{
				if (rowcount==row_start)
				{
					if (len > (COLS-col_start))
						len = COLS-col_start;
					remaining_len -= len;
				}
				else
				{
					start = ROWCOL_TO_BA(rowcount, 0);
					len = Math.Min(COLS, remaining_len);
					remaining_len -= len;
				}


				telnet.action.action_output("<Field>");
				telnet.action.action_output("<Location position=\""+start+"\" left=\""+BA_TO_COL(start)+"\" top=\""+BA_TO_ROW(start)+"\" length=\""+len+"\"/>");
				//
				string temp = "";
				temp+="<Attributes Base=\""+fa+"\"";
				if (FA_IS_PROTECTED(fa))
				{
					temp+= " Protected=\"true\"";
				}
				else
					temp+= " Protected=\"false\"";
				if (FA_IS_ZERO(fa))
				{
					temp+= " FieldType=\"Hidden\"";
				}
				else if (FA_IS_HIGH(fa))
				{
					temp+= " FieldType=\"High\"";
				} 
				else if (FA_IS_INTENSE(fa))
				{
					temp+= " FieldType=\"Intense\"";
				}
				else
				{
					if (ea.fg!=0)
					{
						temp+= " Foreground=\""+see.see_efa_unformatted(see.XA_FOREGROUND, ea.fg)+"\"";
					}
					if (ea.bg!=0)
					{
						temp+= " Background=\""+see.see_efa_unformatted(see.XA_BACKGROUND, ea.bg)+"\"";
					}
					if (ea.gr!=0)
					{
						temp+= " Highlighting=\""+see.see_efa_unformatted(see.XA_HIGHLIGHTING, (byte)(ea.bg | 0xf0))+"\"";
					}
					if ((ea.cs & ExtendedAttribute.CS_MASK)!=0)
					{
						temp+= " Mask=\""+see.see_efa_unformatted(see.XA_CHARSET, (byte)((ea.cs & ExtendedAttribute.CS_MASK) | 0xf0))+"\"";
					}
				}
				temp+="/>";
				telnet.action.action_output(temp);
				dump_rangeXML(start, len, true, screen_buf, ROWS, COLS);
				telnet.action.action_output("</Field>");
			}
			if (baddr <= address) return -1;
			return baddr;
		}


		//endif

		public void Dump()
		{
			// mfw
			int x,y;
			Console.WriteLine("dump starting.... Cursor@"+cursor_addr);
			for (y=0; y<24; y++)
			{
				string temp = "";
				for (x=0; x<80; x++)
				{
					byte ch = Tables.cg2asc[screen_buf[x+y*80]];
					if (ch==0)
						temp+=" ";
					else
						temp+=""+System.Convert.ToChar(ch);
				}
				Console.WriteLine("{0:d2} {1}", y, temp);
			}
		}
		public bool Ascii_action(params object[] args)
		{
			return dump_fixed(args, "Ascii_action", true, screen_buf, ROWS, COLS, cursor_addr);
		}

		public bool AsciiField_action(params object[] args)
		{
			return dump_field("AsciiField_action", true);
		}

		public bool DumpXML_action(params object[] args)
		{
			int pos = 0;
			//string name = "DumpXML_action";
			telnet.action.action_output("<?xml version=\"1.0\"?>");// encoding=\"utf-16\"?>");
			telnet.action.action_output("<XMLScreen xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
			telnet.action.action_output("<CX>"+COLS+"</CX>");
			telnet.action.action_output("<CY>"+ROWS+"</CY>");
			if (formatted)
			{
				telnet.action.action_output("<Formatted>true</Formatted>");
				ExtendedAttribute ea = new ExtendedAttribute();
				do
				{
					pos = dump_fieldAsXML(pos, ea);
				}
				while (pos != -1);
			}
			else
			{
				telnet.action.action_output("<Formatted>false</Formatted>");
			}
			// output unformatted image anyway
			int i;
			telnet.action.action_output("<Unformatted>");
			for (i=0; i<ROWS; i++)
			{
				int start = ROWCOL_TO_BA(i, 0);

				int len = COLS;
				telnet.action.action_output("<Text>");
				dump_rangeXML(start, len, true, screen_buf, ROWS, COLS);
				telnet.action.action_output("</Text>");

			}
			telnet.action.action_output("</Unformatted>");

			telnet.action.action_output("</XMLScreen>");
			return true;
		}




		#region IDisposable Members

		public void Dispose()
		{
			// TODO:  Add Ctlr.Dispose implementation
		}

		#endregion
	}
}
