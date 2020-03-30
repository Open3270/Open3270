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
using System.Collections.Generic;
using System.Text;

namespace Open3270.TN3270
{
	public static class TelnetConstants
	{
		public const byte IAC = 255;
		public const byte DO = 253;
		public const byte DONT = 254;
		public const byte WILL = 251;
		public const byte WONT = 252;
		public const byte SB = 250;		/* interpret as subnegotiation */
		public const byte GA = 249;		/* you may reverse the line */
		public const byte EL = 248;		/* erase the current line */
		public const byte EC = 247;		/* erase the current character */
		public const byte AYT = 246;		/* are you there */
		public const byte AO = 245;		/* abort output--but let prog finish */
		public const byte IP = 244;		/* interrupt process--permanently */
		public const byte BREAK = 243;		/* break */
		public const byte DM = 242;		/* data mark--for connect. cleaning */
		public const byte NOP = 241;		/* nop */
		public const byte SE = 240;		/* end sub negotiation */
		public const byte EOR = 239;            /* end of record (transparent mode) */
		public const byte SUSP = 237;		/* suspend process */
		public const byte xEOF = 236;		/* end of file */


		public const byte SYNCH = 242;		/* for telfunc calls */

		public const Char IS = '0';
		public const Char SEND = '1';
		public const Char INFO = '2';
		public const Char VAR = '0';
		public const Char VALUE = '1';
		public const Char ESC = '2';
		public const Char USERVAR = '3';

		public const int TELQUAL_IS = 0;	/* option is... */
		public const int TELQUAL_SEND = 1;	/* send option */
		public const int LU_MAX = 32;


		public static readonly string[] TelQuals = new string[] { "IS", "SEND" };


		//Telnet options
		public const int TELOPT_BINARY = 0;	/* 8-bit data path */
		public const int TELOPT_ECHO = 1;	/* echo */
		public const int TELOPT_RCP = 2;	/* prepare to reconnect */
		public const int TELOPT_SGA = 3;	/* suppress go ahead */
		public const int TELOPT_NAMS = 4;	/* approximate message size */
		public const int TELOPT_STATUS = 5;	/* give status */
		public const int TELOPT_TM = 6;	/* timing mark */
		public const int TELOPT_RCTE = 7;	/* remote controlled transmission and echo */
		public const int TELOPT_NAOL = 8;	/* negotiate about output line width */
		public const int TELOPT_NAOP = 9;	/* negotiate about output page size */
		public const int TELOPT_NAOCRD = 10;	/* negotiate about CR disposition */
		public const int TELOPT_NAOHTS = 11;	/* negotiate about horizontal tabstops */
		public const int TELOPT_NAOHTD = 12;	/* negotiate about horizontal tab disposition */
		public const int TELOPT_NAOFFD = 13;	/* negotiate about formfeed disposition */
		public const int TELOPT_NAOVTS = 14;	/* negotiate about vertical tab stops */
		public const int TELOPT_NAOVTD = 15;	/* negotiate about vertical tab disposition */
		public const int TELOPT_NAOLFD = 16;	/* negotiate about output LF disposition */
		public const int TELOPT_XASCII = 17;	/* extended ascic character set */
		public const int TELOPT_LOGOUT = 18;	/* force logout */
		public const int TELOPT_BM = 19;	/* byte macro */
		public const int TELOPT_DET = 20;	/* data entry terminal */
		public const int TELOPT_SUPDUP = 21;	/* supdup protocol */
		public const int TELOPT_SUPDUPOUTPUT = 22;	/* supdup output */
		public const int TELOPT_SNDLOC = 23;	/* send location */
		public const int TELOPT_TTYPE = 24;	/* terminal type */
		public const int TELOPT_EOR = 25;	/* end or record */
		public const int TELOPT_TUID = 26;      /* TACACS user identification */
		public const int TELOPT_OUTMRK = 27;      /* output marking */
		public const int TELOPT_TTYLOC = 28;      /* terminal location number */
		public const int TELOPT_3270REGIME = 29;    /* 3270 regime */
		public const int TELOPT_X3PAD = 30;      /* X.3 PAD */
		public const int TELOPT_NAWS = 31;      /* window size */
		public const int TELOPT_TSPEED = 32;      /* terminal speed */
		public const int TELOPT_LFLOW = 33;      /* remote flow control */
		public const int TELOPT_LINEMODE = 34;      /* linemode option */
		public const int TELOPT_XDISPLOC = 35;      /* X Display Location */
		public const int TELOPT_OLD_ENVIRON = 36;   /* old - Environment variables */
		public const int TELOPT_AUTHENTICATION = 37;/* authenticate */
		public const int TELOPT_ENCRYPT = 38;      /* encryption option */
		public const int TELOPT_NEW_ENVIRON = 39;   /* new - environment variables */
		public const int TELOPT_TN3270E = 40;	/* extended 3270 regime */
		public const int TELOPT_EXOPL = 255;	/* extended-options-list */


		//Negotiation function Names.
		public const int TN3270E_FUNC_BIND_IMAGE = 0;
		public const int TN3270E_FUNC_DATA_STREAM_CTL = 1;
		public const int TN3270E_FUNC_RESPONSES = 2;
		public const int TN3270E_FUNC_SCS_CTL_CODES = 3;
		public const int TN3270E_FUNC_SYSREQ = 4;

		public const int TELCMD_FIRST = TelnetConstants.xEOF;

		public static readonly string[] TelnetCommands = new string[]
								{
									"EOF", "SUSP", "ABORT", "EOR", "SE", "NOP", "DMARK", "BRK", "IP",
									"AO", "AYT", "EC", "EL", "GA", "SB", "WILL", "WONT", "DO", "DONT",
									"IAC"
								};

		public static readonly string[] TelnetOptions = new string[] 
										  {
											  "BINARY", "ECHO", "RCP", "SUPPRESS GO AHEAD", "NAME",
											  "STATUS", "TIMING MARK", "RCTE", "NAOL", "NAOP",
											  "NAOCRD", "NAOHTS", "NAOHTD", "NAOFFD", "NAOVTS",
											  "NAOVTD", "NAOLFD", "EXTEND ASCII", "LOGOUT", "BYTE MACRO",
											  "DATA ENTRY TERMINAL", "SUPDUP", "SUPDUP OUTPUT",
											  "SEND LOCATION", "TERMINAL TYPE", "END OF RECORD",
											  "TACACS UID", "OUTPUT MARKING", "TTYLOC",
											  "3270 REGIME", "X.3 PAD", "NAWS", "TSPEED", "LFLOW",
											  "LINEMODE", "XDISPLOC", "OLD-ENVIRON", "AUTHENTICATION",
											  "ENCRYPT", "NEW-ENVIRON", "TN3270E"
											  
										  };

		public static readonly byte[] FunctionsReq = new byte[] { TelnetConstants.IAC, TelnetConstants.SB, TelnetConstants.TELOPT_TN3270E,TnHeader.Ops.Functions };

		public static readonly string[] FunctionNames = new string[] { "BIND-IMAGE", "DATA-STREAM-CTL", "RESPONSES", "SCS-CTL-CODES", "SYSREQ" };


		public static string GetReason(int reasonCode)
		{
			switch (reasonCode)
			{
				case 0: return "CONN-PARTNER";
				case 1: return "DEVICE-IN-USE";
				case 2: return "INV-ASSOCIATE";
				case 3: return "INV-NAME";
				case 4: return "INV-DEVICE-TYPE";
				case 5: return "TYPE-NAME-ERROR";
				case 6: return "UNKNOWN-ERROR";
				case 7: return "UNSUPPORTED-REQ";
				default: return "??";
			}
		}
	}
}
