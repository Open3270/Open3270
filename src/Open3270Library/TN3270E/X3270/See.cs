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

	internal class See
	{

		// Extended attributes
		public const byte XA_ALL = 0x00;
		public const byte XA_3270 = 0xc0;
		public const byte XA_VALIDATION = 0xc1;
		public const byte XAV_FILL = 0x04;
		public const byte XAV_ENTRY = 0x02;
		public const byte XAV_TRIGGER = 0x01;
		public const byte XA_OUTLINING = 0xc2;
		public const byte XAO_UNDERLINE = 0x01;
		public const byte XAO_RIGHT = 0x02;
		public const byte XAO_OVERLINE = 0x04;
		public const byte XAO_LEFT = 0x08;
		public const byte XA_HIGHLIGHTING = 0x41;
		public const byte XAH_DEFAULT = 0x00;
		public const byte XAH_NORMAL = 0xf0;
		public const byte XAH_BLINK = 0xf1;
		public const byte XAH_REVERSE = 0xf2;
		public const byte XAH_UNDERSCORE = 0xf4;
		public const byte XAH_INTENSIFY = 0xf8;
		public const byte XA_FOREGROUND = 0x42;
		public const byte XAC_DEFAULT = 0x00;
		public const byte XA_CHARSET = 0x43;
		public const byte XA_BACKGROUND = 0x45;
		public const byte XA_TRANSPARENCY = 0x46;
		public const byte XAT_DEFAULT = 0x00;
		public const byte XAT_OR = 0xf0;
		public const byte XAT_XOR = 0xf1;
		public const byte XAT_OPAQUE = 0xff;


		// 3270 orders
		public const byte ORDER_PT = 0x05;	/* program tab */
		public const byte ORDER_GE = 0x08;	/* graphic escape */
		public const byte ORDER_SBA = 0x11;	/* set buffer address */
		public const byte ORDER_EUA = 0x12;	/* erase unprotected to address */
		public const byte ORDER_IC = 0x13;	/* insert cursor */
		public const byte ORDER_SF = 0x1d;	/* start field */
		public const byte ORDER_SA = 0x28;	/* set attribute */
		public const byte ORDER_SFE = 0x29;	/* start field extended */
		public const byte ORDER_YALE = 0x2b;	/* Yale sub command */
		public const byte ORDER_MF = 0x2c;	/* modify field */
		public const byte ORDER_RA = 0x3c;	/* repeat to address */

		public const byte FCORDER_NULL = 0x00;	/* format control: null */
		public const byte FCORDER_FF = 0x0c;	/*		   form feed */
		public const byte FCORDER_CR = 0x0d;	/*		   carriage return */
		public const byte FCORDER_NL = 0x15;	/*		   new line */
		public const byte FCORDER_EM = 0x19;	/*		   end of medium */
		public const byte FCORDER_DUP = 0x1c;	/*		   duplicate */
		public const byte FCORDER_FM = 0x1e;	/*		   field mark */
		public const byte FCORDER_SUB = 0x3f;	/*		   substitute */
		public const byte FCORDER_EO = 0xff;	/*		   eight ones */


		// SCS control code, some overlap orders
		public const byte SCS_BS = 0x16;	/* Back Space  */
		public const byte SCS_BEL = 0x2f;	/* Bell Function */
		public const byte SCS_CR = 0x0d;	/* Carriage Return */
		public const byte SCS_ENP = 0x14;	/* Enable Presentation */
		public const byte SCS_FF = 0x0c;	/* Forms Feed */
		public const byte SCS_GE = 0x08;	/* Graphic Escape */
		public const byte SCS_HT = 0x05;	/* Horizontal Tab */
		public const byte SCS_INP = 0x24;	/* Inhibit Presentation */
		public const byte SCS_IRS = 0x1e;	/* Interchange-Record Separator */
		public const byte SCS_LF = 0x25;	/* Line Feed */
		public const byte SCS_NL = 0x15;	/* New Line */
		public const byte SCS_SA = 0x28;	/* Set Attribute */
		public const byte SCS_SET = 0x2b;	/* Set: */
		public const byte SCS_SHF = 0xc1;	/*  Horizontal format */
		public const byte SCS_SLD = 0xc6;	/*  Line Density */
		public const byte SCS_SVF = 0xc2;	/*  Vertical Format */
		public const byte SCS_TRN = 0x35;	/* Transparent */
		public const byte SCS_VCS = 0x04;	/* Vertical Channel Select */
		public const byte SCS_VT = 0x0b;	/* Vertical Tab */


		// Structured fields
		public const byte SF_READ_PART = 0x01;	/* read partition */
		public const byte SF_RP_QUERY = 0x02;	/*  query */
		public const byte SF_RP_QLIST = 0x03;	/*  query list */
		public const byte SF_RPQ_LIST = 0x00;	/*   QCODE list */
		public const byte SF_RPQ_EQUIV = 0x40;	/*   equivalent+ QCODE list */
		public const byte SF_RPQ_ALL = 0x80;	/*   all */
		public const byte SF_ERASE_RESET = 0x03;	/* erase/reset */
		public const byte SF_ER_DEFAULT = 0x00;	/*  default */
		public const byte SF_ER_ALT = 0x80;	/*  alternate */
		public const byte SF_SET_REPLY_MODE = 0x09;	/* set reply mode */
		public const byte SF_SRM_FIELD = 0x00;	/*  field */
		public const byte SF_SRM_XFIELD = 0x01;	/*  extended field */
		public const byte SF_SRM_CHAR = 0x02;	/*  character */
		public const byte SF_CREATE_PART = 0x0c;	/* create partition */
		public const byte CPFLAG_PROT = 0x40;	/*  protected flag */
		public const byte CPFLAG_COPY_PS = 0x20;	/*  local copy to presentation space */
		public const byte CPFLAG_BASE = 0x07;	/*  base character set index */
		public const byte SF_OUTBOUND_DS = 0x40;	/* outbound 3270 DS */
		public const byte SF_TRANSFER_DATA = 0xd0;   /* file transfer open request */


		// Query replies
		public const byte QR_SUMMARY = 0x80;	/* summary */
		public const byte QR_USABLE_AREA = 0x81;	/* usable area */
		public const byte QR_ALPHA_PART = 0x84;	/* alphanumeric partitions */
		public const byte QR_CHARSETS = 0x85;	/* character sets */
		public const byte QR_COLOR = 0x86;	/* color */
		public const byte QR_HIGHLIGHTING = 0x87;	/* highlighting */
		public const byte QR_REPLY_MODES = 0x88;	/* reply modes */
		public const byte QR_PC3270 = 0x93;    /* PC3270 */
		public const byte QR_DDM = 0x95;    /* distributed data management */
		public const byte QR_IMP_PART = 0xa6;	/* implicit partition */
		public const byte QR_NULL = 0xff;	/* null */


		// WCC definitions
		static public bool WCC_RESET(byte c) { return (c & 0x40) != 0; }
		static public bool WCC_START_PRINTER(byte c) { return (c & 0x08) != 0; }
		static public bool WCC_SOUND_ALARM(byte c) { return (c & 0x04) != 0; }
		static public bool WCC_KEYBOARD_RESTORE(byte c) { return (c & 0x02) != 0; }
		static public bool WCC_RESET_MDT(byte c) { return (c & 0x01) != 0; }

		// Attention Identifiers
		public const byte AID_NO = 0x60;	// No AID generated
		public const byte AID_QREPLY = 0x61;
		public const byte AID_ENTER = 0x7d;
		public const byte AID_PF1 = 0xf1;
		public const byte AID_PF2 = 0xf2;
		public const byte AID_PF3 = 0xf3;
		public const byte AID_PF4 = 0xf4;
		public const byte AID_PF5 = 0xf5;
		public const byte AID_PF6 = 0xf6;
		public const byte AID_PF7 = 0xf7;
		public const byte AID_PF8 = 0xf8;
		public const byte AID_PF9 = 0xf9;
		public const byte AID_PF10 = 0x7a;
		public const byte AID_PF11 = 0x7b;
		public const byte AID_PF12 = 0x7c;
		public const byte AID_PF13 = 0xc1;
		public const byte AID_PF14 = 0xc2;
		public const byte AID_PF15 = 0xc3;
		public const byte AID_PF16 = 0xc4;
		public const byte AID_PF17 = 0xc5;
		public const byte AID_PF18 = 0xc6;
		public const byte AID_PF19 = 0xc7;
		public const byte AID_PF20 = 0xc8;
		public const byte AID_PF21 = 0xc9;
		public const byte AID_PF22 = 0x4a;
		public const byte AID_PF23 = 0x4b;
		public const byte AID_PF24 = 0x4c;
		public const byte AID_OICR = 0xe6;
		public const byte AID_MSR_MHS = 0xe7;
		public const byte AID_SELECT = 0x7e;
		public const byte AID_PA1 = 0x6c;
		public const byte AID_PA2 = 0x6e;
		public const byte AID_PA3 = 0x6b;
		public const byte AID_CLEAR = 0x6d;
		public const byte AID_SYSREQ = 0xf0;

		public const byte AID_SF = 0x88;
		public const byte SFID_QREPLY = 0x81;

		// Colors
		public const byte COLOR_NEUTRAL_BLACK = 0;
		public const byte COLOR_BLUE = 1;
		public const byte COLOR_RED = 2;
		public const byte COLOR_PINK = 3;
		public const byte COLOR_GREEN = 4;
		public const byte COLOR_TURQUOISE = 5;
		public const byte COLOR_YELLOW = 6;
		public const byte COLOR_NEUTRAL_WHITE = 7;
		public const byte COLOR_BLACK = 8;
		public const byte COLOR_DEEP_BLUE = 9;
		public const byte COLOR_ORANGE = 10;
		public const byte COLOR_PURPLE = 11;
		public const byte COLOR_PALE_GREEN = 12;
		public const byte COLOR_PALE_TURQUOISE = 13;
		public const byte COLOR_GREY = 14;
		public const byte COLOR_WHITE = 15;



		static public string FormatUnknown(byte vvalue)
		{
			return "unknown[= 0x" + vvalue + "]";
		}

		static public string GetEbc(byte ch)
		{
			switch (ch)
			{
				case FCORDER_NULL:
					return "NULL";
				case FCORDER_SUB:
					return "SUB";
				case FCORDER_DUP:
					return "DUP";
				case FCORDER_FM:
					return "FM";
				case FCORDER_FF:
					return "FF";
				case FCORDER_CR:
					return "CR";
				case FCORDER_NL:
					return "NL";
				case FCORDER_EM:
					return "EM";
				case FCORDER_EO:
					return "EO";
			}
			if (Tables.Ebc2Ascii[ch] != 0)
				return "" + System.Convert.ToChar(Tables.Ebc2Ascii[ch]);
			else
				return "" + System.Convert.ToChar(ch);
		}

		static public string GetAidFromCode(byte code)
		{
			switch (code)
			{
				case AID_NO:
					return "NoAID";
				case AID_ENTER:
					return "Enter";
				case AID_PF1:
					return "PF1";
				case AID_PF2:
					return "PF2";
				case AID_PF3:
					return "PF3";
				case AID_PF4:
					return "PF4";
				case AID_PF5:
					return "PF5";
				case AID_PF6:
					return "PF6";
				case AID_PF7:
					return "PF7";
				case AID_PF8:
					return "PF8";
				case AID_PF9:
					return "PF9";
				case AID_PF10:
					return "PF10";
				case AID_PF11:
					return "PF11";
				case AID_PF12:
					return "PF12";
				case AID_PF13:
					return "PF13";
				case AID_PF14:
					return "PF14";
				case AID_PF15:
					return "PF15";
				case AID_PF16:
					return "PF16";
				case AID_PF17:
					return "PF17";
				case AID_PF18:
					return "PF18";
				case AID_PF19:
					return "PF19";
				case AID_PF20:
					return "PF20";
				case AID_PF21:
					return "PF21";
				case AID_PF22:
					return "PF22";
				case AID_PF23:
					return "PF23";
				case AID_PF24:
					return "PF24";
				case AID_OICR:
					return "OICR";
				case AID_MSR_MHS:
					return "MSR_MHS";
				case AID_SELECT:
					return "Select";
				case AID_PA1:
					return "PA1";
				case AID_PA2:
					return "PA2";
				case AID_PA3:
					return "PA3";
				case AID_CLEAR:
					return "Clear";
				case AID_SYSREQ:
					return "SysReq";
				case AID_QREPLY:
					return "QueryReplyAID";
				default:
					return FormatUnknown(code);
			}
		}

		static public string GetSeeAttribute(byte fa)
		{
			string seeAttributeBuffer = "";
			string paren = "(";


			if ((fa & 0x04) != 0)
			{
				seeAttributeBuffer += paren;
				seeAttributeBuffer += "protected";
				paren = ",";
				if ((fa & 0x08) != 0)
				{
					seeAttributeBuffer += paren;
					seeAttributeBuffer += "skip";
					paren = ",";
				}
			}
			else if ((fa & 0x08) != 0)
			{
				seeAttributeBuffer += paren;
				seeAttributeBuffer += "numeric";
				paren = ",";
			}
			switch (fa & 0x03)
			{
				case 0:
					break;
				case 1:
					seeAttributeBuffer += paren;
					seeAttributeBuffer += "detectable";
					paren = ",";
					break;
				case 2:
					seeAttributeBuffer += paren;
					seeAttributeBuffer += "intensified";
					paren = ",";
					break;
				case 3:
					seeAttributeBuffer += paren;
					seeAttributeBuffer += "nondisplay";
					paren = ",";
					break;
			}
			if ((fa & 0x20) != 0)
			{
				seeAttributeBuffer += paren;
				seeAttributeBuffer += "modified";
				paren = ",";
			}
			if (paren != "(")
			{
				seeAttributeBuffer += ")";
			}
			else
			{
				seeAttributeBuffer += "(default)";
			}

			return seeAttributeBuffer;
		}

		static public string GetHighlight(byte setting)
		{
			switch (setting)
			{
				case XAH_DEFAULT:
					return "default";
				case XAH_NORMAL:
					return "normal";
				case XAH_BLINK:
					return "blink";
				case XAH_REVERSE:
					return "reverse";
				case XAH_UNDERSCORE:
					return "underscore";
				case XAH_INTENSIFY:
					return "intensify";
				default:
					return FormatUnknown(setting);
			}
		}

		static public string GetColor(byte setting)
		{
			string[] colorName = new string[] {
												   "neutralBlack",
												   "blue",
												   "red",
												   "pink",
												   "green",
												   "turquoise",
												   "yellow",
												   "neutralWhite",
												   "black",
												   "deepBlue",
												   "orange",
												   "purple",
												   "paleGreen",
												   "paleTurquoise",
												   "grey",
												   "white"
											   };

			if (setting == XAC_DEFAULT)
			{
				return "default";
			}
			else if (setting < 0xf0 || setting > 0xff)
			{
				return FormatUnknown(setting);
			}
			else
			{
				return colorName[setting - 0xf0];
			}
		}

		static public string GetTransparency(byte setting)
		{
			switch (setting)
			{
				case XAT_DEFAULT:
					return "default";
				case XAT_OR:
					return "or";
				case XAT_XOR:
					return "xor";
				case XAT_OPAQUE:
					return "opaque";
				default:
					return FormatUnknown(setting);
			}
		}

		static public string GetValidation(byte setting)
		{
			string see_validation_buf = "";
			string paren = "(";

			if ((setting & XAV_FILL) != 0)
			{
				see_validation_buf += paren;
				see_validation_buf += "fill";
				paren = ",";
			}

			if ((setting & XAV_ENTRY) != 0)
			{
				see_validation_buf += paren;
				see_validation_buf += "entry";
				paren = ",";
			}

			if ((setting & XAV_TRIGGER) != 0)
			{
				see_validation_buf += paren;
				see_validation_buf += "trigger";
				paren = ",";
			}

			if (paren != "(")
			{
				see_validation_buf += ")";
			}
			else
			{
				see_validation_buf += "(none)";
			}

			return see_validation_buf;
		}

		static public string GetOutline(byte setting)
		{
			string seeOutlineBuffer = "";
			string paren = "(";

			if ((setting & XAO_UNDERLINE) != 0)
			{
				seeOutlineBuffer += seeOutlineBuffer += paren;
				seeOutlineBuffer += "underline";
				paren = ",";
			}

			if ((setting & XAO_RIGHT) != 0)
			{
				seeOutlineBuffer += paren;
				seeOutlineBuffer += "right";
				paren = ",";
			}

			if ((setting & XAO_OVERLINE) != 0)
			{
				seeOutlineBuffer += paren;
				seeOutlineBuffer += "overline";
				paren = ",";
			}

			if ((setting & XAO_LEFT) != 0)
			{
				seeOutlineBuffer += paren;
				seeOutlineBuffer += "left";
				paren = ",";
			}

			if (paren != "(")
			{
				seeOutlineBuffer += ")";
			}
			else
			{
				seeOutlineBuffer += "(none)";
			}

			return seeOutlineBuffer;
		}

		static public string GetEfa(byte efa, byte value)
		{

			switch (efa)
			{
				case XA_ALL:
					return " all(" + value + ")";
				case XA_3270:
					return " 3270" + GetSeeAttribute(value);
				case XA_VALIDATION:
					return " validation" + GetValidation(value);
				case XA_OUTLINING:
					return " outlining(" + GetOutline(value) + ")";
				case XA_HIGHLIGHTING:
					return " highlighting(" + GetHighlight(value) + ")";
				case XA_FOREGROUND:
					return " foreground(" + GetColor(value) + ")";
				case XA_CHARSET:
					return " charset(" + value + ")";
				case XA_BACKGROUND:
					return " background(" + GetColor(value) + ")";
				case XA_TRANSPARENCY:
					return " transparency(" + GetTransparency(value) + ")";
				default:
					return " " + FormatUnknown(efa) + "[0x" + value + "]";
			}
		}

		static public string GetEfaUnformatted(byte efa, byte vvalue)
		{
			switch (efa)
			{
				case XA_ALL:
					return "" + vvalue;
				case XA_3270:
					return " 3270" + GetSeeAttribute(vvalue);
				case XA_VALIDATION:
					return GetValidation(vvalue);
				case XA_OUTLINING:
					return GetOutline(vvalue);
				case XA_HIGHLIGHTING:
					return GetHighlight(vvalue);
				case XA_FOREGROUND:
					return GetColor(vvalue);
				case XA_CHARSET:
					return "" + vvalue;
				case XA_BACKGROUND:
					return GetColor(vvalue);
				case XA_TRANSPARENCY:
					return GetTransparency(vvalue);
				default:
					return FormatUnknown(efa) + "[0x" + vvalue + "]";
			}
		}


		static public string GetEfaOnly(byte efa)
		{
			switch (efa)
			{
				case XA_ALL:
					return "all";
				case XA_3270:
					return "3270";
				case XA_VALIDATION:
					return "validation";
				case XA_OUTLINING:
					return "outlining";
				case XA_HIGHLIGHTING:
					return "highlighting";
				case XA_FOREGROUND:
					return "foreground";
				case XA_CHARSET:
					return "charset";
				case XA_BACKGROUND:
					return "background";
				case XA_TRANSPARENCY:
					return "transparency";
				default:
					return FormatUnknown(efa);
			}
		}

		static public string GetQCodeode(byte id)
		{
			switch (id)
			{
				case QR_CHARSETS:
					return "CharacterSets";
				case QR_IMP_PART:
					return "ImplicitPartition";
				case QR_SUMMARY:
					return "Summary";
				case QR_USABLE_AREA:
					return "UsableArea";
				case QR_COLOR:
					return "Color";
				case QR_HIGHLIGHTING:
					return "Highlighting";
				case QR_REPLY_MODES:
					return "ReplyModes";
				case QR_ALPHA_PART:
					return "AlphanumericPartitions";
				case QR_DDM:
					return "DistributedDataManagement";
				default:
					return "unknown[0x" + id + "]";
			}
		}
	}
}

