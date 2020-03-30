 /*
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

using System;
using System.Collections.Generic;
using System.Text;

namespace Open3270.TN3270
{
	public static class ControllerConstant
	{
		/* Configuration change masks. */
		public const int NO_CHANGE = 0x0000;	/* no change */
		public const int MODEL_CHANGE = 0x0001;	/* screen dimensions changed */
		public const int FONT_CHANGE = 0x0002;	/* emulator font changed */
		public const int COLOR_CHANGE = 0x0004;	/* color scheme or 3278/9 mode changed */
		public const int SCROLL_CHANGE = 0x0008;	/* scrollbar snapped on or off */
		public const int CHARSET_CHANGE = 0x0010;	/* character set changed */
		public const int ALL_CHANGE = 0xffff;	/* everything changed */

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
		public const byte FA_BASE = 0xc0;
		public const byte FA_MASK = 0xd0;
		public const byte FA_MODIFY = 0x20;
		public const byte FA_MODIFY_MASK = 0xdf;
		public const byte FA_NUMERIC = 0x08;
		public const byte FA_PROTECT = 0x04;
		public const byte FA_INTENSITY = 0x03;
		public const byte FA_INT_NORM_NSEL = 0x00;
		public const byte FA_INT_NORM_SEL = 0x01;
		public const byte FA_INT_HIGH_SEL = 0x02;
		public const byte FA_INT_ZERO_NSEL = 0x03;
		



		/* 3270 commands */
		public const int CMD_W = 0x01;	/* write */
		public const int CMD_RB = 0x02;	/* read buffer */
		public const int CMD_NOP = 0x03;	/* no-op */
		public const int CMD_EW = 0x05;	/* erase/write */
		public const int CMD_RM = 0x06;	/* read modified */
		public const int CMD_EWA = 0x0d;	/* erase/write alternate */
		public const int CMD_RMA = 0x0e;	/* read modified all */
		public const int CMD_EAU = 0x0f;	/* erase all unprotected */
		public const int CMD_WSF = 0x11;	/* write structured field */

		/* SNA 3270 commands */
		public const int SNA_CMD_RMA = 0x6e;	/* read modified all */
		public const int SNA_CMD_EAU = 0x6f;	/* erase all unprotected */
		public const int SNA_CMD_EWA = 0x7e;	/* erase/write alternate */
		public const int SNA_CMD_W = 0xf1;	/* write */
		public const int SNA_CMD_RB = 0xf2;	/* read buffer */
		public const int SNA_CMD_WSF = 0xf3;	/* write structured field */
		public const int SNA_CMD_EW = 0xf5;	/* erase/write */
		public const int SNA_CMD_RM = 0xf6;	/* read modified */

		/* 3270 orders */
		public const int ORDER_PT = 0x05;	/* program tab */
		public const int ORDER_GE = 0x08;	/* graphic escape */
		public const int ORDER_SBA = 0x11;	/* set buffer address */
		public const int ORDER_EUA = 0x12;	/* erase unprotected to address */
		public const int ORDER_IC = 0x13;	/* insert cursor */
		public const int ORDER_SF = 0x1d;	/* start field */
		public const int ORDER_SA = 0x28;	/* set attribute */
		public const int ORDER_SFE = 0x29;	/* start field extended */
		public const int ORDER_YALE = 0x2b;	/* Yale sub command */
		public const int ORDER_MF = 0x2c;	/* modify field */
		public const int ORDER_RA = 0x3c;	/* repeat to address */

		public const int FCORDER_NULL = 0x00;	/* format control: null */
		public const int FCORDER_FF = 0x0c;	/*		   form feed */
		public const int FCORDER_CR = 0x0d;	/*		   carriage return */
		public const int FCORDER_NL = 0x15;	/*		   new line */
		public const int FCORDER_EM = 0x19;	/*		   end of medium */
		public const int FCORDER_DUP = 0x1c;	/*		   duplicate */
		public const int FCORDER_FM = 0x1e;	/*		   field mark */
		public const int FCORDER_SUB = 0x3f;	/*		   substitute */
		public const int FCORDER_EO = 0xff;	/*		   eight ones */

		/* SCS control code, some overlap orders */
		public const int SCS_BS = 0x16;	/* Back Space  */
		public const int SCS_BEL = 0x2f;	/* Bell Function */
		public const int SCS_CR = 0x0d;	/* Carriage Return */
		public const int SCS_ENP = 0x14;	/* Enable Presentation */
		public const int SCS_FF = 0x0c;	/* Forms Feed */
		public const int SCS_GE = 0x08;	/* Graphic Escape */
		public const int SCS_HT = 0x05;	/* Horizontal Tab */
		public const int SCS_INP = 0x24;	/* Inhibit Presentation */
		public const int SCS_IRS = 0x1e;	/* Interchange-Record Separator */
		public const int SCS_LF = 0x25;	/* Line Feed */
		public const int SCS_NL = 0x15;	/* New Line */
		public const int SCS_SA = 0x28;	/* Set Attribute */
		public const int SCS_SET = 0x2b;	/* Set: */
		public const int SCS_SHF = 0xc1;	/*  Horizontal format */
		public const int SCS_SLD = 0xc6;	/*  Line Density */
		public const int SCS_SVF = 0xc2;	/*  Vertical Format */
		public const int SCS_TRN = 0x35;	/* Transparent */
		public const int SCS_VCS = 0x04;	/* Vertical Channel Select */
		public const int SCS_VT = 0x0b;	/* Vertical Tab */

		/* Structured fields */
		public const int SF_READ_PART = 0x01;	/* read partition */
		public const int SF_RP_QUERY = 0x02;	/*  query */
		public const int SF_RP_QLIST = 0x03;	/*  query list */
		public const int SF_RPQ_LIST = 0x00;	/*   QCODE list */
		public const int SF_RPQ_EQUIV = 0x40;	/*   equivalent+ QCODE list */
		public const int SF_RPQ_ALL = 0x80;	/*   all */
		public const int SF_ERASE_RESET = 0x03;	/* erase/reset */
		public const int SF_ER_DEFAULT = 0x00;	/*  default */
		public const int SF_ER_ALT = 0x80;	/*  alternate */
		public const int SF_SET_REPLY_MODE = 0x09;	/* set reply mode */
		public const int SF_SRM_FIELD = 0x00;	/*  field */
		public const int SF_SRM_XFIELD = 0x01;	/*  extended field */
		public const int SF_SRM_CHAR = 0x02;	/*  character */
		public const int SF_CREATE_PART = 0x0c;	/* create partition */
		public const int CPFLAG_PROT = 0x40;	/*  protected flag */
		public const int CPFLAG_COPY_PS = 0x20;	/*  local copy to presentation space */
		public const int CPFLAG_BASE = 0x07;	/*  base character set index */
		public const int SF_OUTBOUND_DS = 0x40;	/* outbound 3270 DS */
		public const int SF_TRANSFER_DATA = 0xd0; /* file transfer open request */

		/* Query replies */
		public const int QR_SUMMARY = 0x80;	/* summary */
		public const int QR_USABLE_AREA = 0x81;	/* usable area */
		public const int QR_ALPHA_PART = 0x84;	/* alphanumeric partitions */
		public const int QR_CHARSETS = 0x85;	/* character sets */
		public const int QR_COLOR = 0x86;	/* color */
		public const int QR_HIGHLIGHTING = 0x87;	/* highlighting */
		public const int QR_REPLY_MODES = 0x88;	/* reply modes */
		public const int QR_PC3270 = 0x93; /* PC3270 */
		public const int QR_DDM = 0x95; /* distributed data management */
		public const int QR_IMP_PART = 0xa6;	/* implicit partition */
		public const int QR_NULL = 0xff;	/* null */

		public const int CS_GE = 0x04;/* cs flag for Graphic Escape */


		// The copde table is used to translate buffer addresses and attributes to the 3270 datastream representation
		public static byte[] CodeTable = new byte[] 
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
	}


}
