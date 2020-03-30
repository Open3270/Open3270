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

	internal class CharacterGenerator
	{
		/*
		 *		Character encoding for the 3270 character generator font,
		 *		using the same suffixes as Latin-1 XK_xxx keysyms.
		 *
		 *		Charaters that represent unique EBCDIC or status line codes
		 *		are noted with comments.
		 */

		public const byte Null		= 0x00;	/* EBCDIC 00 */
		public const byte NonBreakingSpace	= 0x01;
		public const byte FormFeed		= 0x02;	/* EBCDIC 0C */
		public const byte Return		= 0x03;	/* EBCDIC 0D */
		public const byte NewLine		= 0x04;	/* EBCDIC 15 */
		public const byte Em		= 0x05;	/* EBCDIC 19 */
		public const byte EightOnes	= 0x06;	/* EBCDIC FF */
		public const byte Hyphen	= 0x07;
		public const byte GreaterThan	= 0x08;
		public const byte LessThan		= 0x09;
		public const byte BracketLeft	= 0x0a;
		public const byte BracketRight	= 0x0b;
		public const byte ParenLeft	= 0x0c;
		public const byte ParenRight	= 0x0d;
		public const byte BraceLeft	= 0x0e;
		public const byte BraceRight	= 0x0f;
		public const byte Space	= 0x10;
		public const byte EqualSign	= 0x11;
		public const byte Apostrophe	= 0x12;
		public const byte QuoteDouble	= 0x13;
		public const byte Slash	= 0x14;
		public const byte BackSlash	= 0x15;
		public const byte Pipe		= 0x16;
		public const byte BrokenPipe	= 0x17;
		public const byte QuestionMark	= 0x18;
		public const byte ExclamationPoint	= 0x19;
		public const byte DollarSign	= 0x1a;
		public const byte Cent		= 0x1b;
		public const byte Sterling	= 0x1c;
		public const byte Yen		= 0x1d;
		public const byte Paragraph	= 0x1e;
		public const byte Currency	= 0x1f;
		public const byte Numeral0		= 0x20;
		public const byte Numeral1		= 0x21;
		public const byte Numeral2		= 0x22;
		public const byte Numeral3		= 0x23;
		public const byte Numeral4		= 0x24;
		public const byte Numeral5		= 0x25;
		public const byte Numeral6		= 0x26;
		public const byte Numeral7		= 0x27;
		public const byte Numeral8		= 0x28;
		public const byte Numeral9		= 0x29;
		public const byte SSharp	= 0x2a;
		public const byte Section	= 0x2b;
		public const byte PoundSign	= 0x2c;
		public const byte AtSymbol		= 0x2d;
		public const byte Percent	= 0x2e;
		public const byte Underscore	= 0x2f;
		public const byte Ampersand	= 0x30;
		public const byte Minus	= 0x31;
		public const byte Period	= 0x32;
		public const byte Comma	= 0x33;
		public const byte Colon	= 0x34;
		public const byte Plus		= 0x35;
		public const byte NotSign	= 0x36;
		public const byte Macron	= 0x37;
		public const byte Degree	= 0x38;
		public const byte PeriodCentered	= 0x39;
		public const byte Asciicircum	= 0x3a;
		public const byte AsciiTilde	= 0x3b;
		public const byte Diaeresis	= 0x3c;
		public const byte GraveAccent	= 0x3d;
		public const byte AcuteAccent	= 0x3e;
		public const byte Cedilla	= 0x3f;
		public const byte aGrave	= 0x40;
		public const byte eGrave	= 0x41;
		public const byte rGrave	= 0x42;
		public const byte oGrave	= 0x43;
		public const byte uGrave	= 0x44;
		public const byte qTilde	= 0x45;
		public const byte cTilde	= 0x46;
		public const byte yDiaeresis	= 0x47;
		public const byte YAcute	= 0x48;
		public const byte yAcute	= 0x49;
		public const byte eAcute	= 0x4a;
		public const byte OneQuarter	= 0x4b;
		public const byte OneHalf	= 0x4c;
		public const byte ThreeQuarters	= 0x4d;
		public const byte uDiaeresis	= 0x4e;
		//public const byte udiaeresis	= 0x4e;
		public const byte cCedilla	= 0x4f;
		public const byte aDiaeresis	= 0x50;
		public const byte eDiaeresis	= 0x51;
		public const byte iDiaeresis	= 0x52;
		public const byte oDiaeresis	= 0x53;
		public const byte mu		= 0x54;
		public const byte aCircumflex	= 0x55;
		public const byte eCircumflex	= 0x56;
		public const byte iCircumflex	= 0x57;
		public const byte oCircumflex	= 0x58;
		public const byte uCircumflex	= 0x59;
		public const byte aacute	= 0x5a;
		public const byte multiply	= 0x5b;
		public const byte iacute	= 0x5c;
		public const byte oacute	= 0x5d;
		public const byte uacute	= 0x5e;
		public const byte ntilde	= 0x5f;
		public const byte Agrave	= 0x60;
		public const byte Egrave	= 0x61;
		public const byte Igrave	= 0x62;
		public const byte Ograve	= 0x63;
		public const byte Ugrave	= 0x64;
		public const byte Atilde	= 0x65;
		public const byte Otilde	= 0x66;
		public const byte OneSuperior	= 0x67;
		public const byte TwoSuperior	= 0x68;
		public const byte ThreeSuperior	= 0x69;
		public const byte OrdFeminine	= 0x6a;
		public const byte Masculine	= 0x6b;
		public const byte GuillemotLeft	= 0x6c;
		public const byte GuillemotRight	= 0x6d;
		public const byte ExclamationPointDown	= 0x6e;
		public const byte QuestionDown	= 0x6f;
		public const byte Adiaeresis	= 0x70;
		public const byte Ediaeresis	= 0x71;
		public const byte Idiaeresis	= 0x72;
		public const byte Odiaeresis	= 0x73;
		public const byte Udiaeresis	= 0x74;
		public const byte Acircumflex	= 0x75;
		public const byte Ecircumflex	= 0x76;
		public const byte Icircumflex	= 0x77;
		public const byte Ocircumflex	= 0x78;
		public const byte Ucircumflex	= 0x79;
		public const byte Aacute	= 0x7a;
		public const byte Eacute	= 0x7b;
		public const byte Iacute	= 0x7c;
		public const byte Oacute	= 0x7d;
		public const byte Uacute	= 0x7e;
		public const byte Ntilde	= 0x7f;
		public const byte a		= 0x80;
		public const byte b		= 0x81;
		public const byte c		= 0x82;
		public const byte d		= 0x83;
		public const byte e		= 0x84;
		public const byte f		= 0x85;
		public const byte g		= 0x86;
		public const byte h		= 0x87;
		public const byte i		= 0x88;
		public const byte j		= 0x89;
		public const byte k		= 0x8a;
		public const byte l		= 0x8b;
		public const byte m		= 0x8c;
		public const byte n		= 0x8d;
		public const byte o		= 0x8e;
		public const byte p		= 0x8f;
		public const byte q		= 0x90;
		public const byte r		= 0x91;
		public const byte s		= 0x92;
		public const byte t		= 0x93;
		public const byte u		= 0x94;
		public const byte v		= 0x95;
		public const byte w		= 0x96;
		public const byte x		= 0x97;
		public const byte y		= 0x98;
		public const byte z		= 0x99;
		public const byte ae		= 0x9a;
		public const byte oSlash	= 0x9b;
		public const byte aring	= 0x9c;
		public const byte Division	= 0x9d;
		public const byte fm		= 0x9e;	/* EBCDIC 1E */
		public const byte dup		= 0x9f;	/* EBCDIC 1C */
		public const byte A		= 0xa0;
		public const byte B		= 0xa1;
		public const byte C		= 0xa2;
		public const byte D		= 0xa3;
		public const byte E		= 0xa4;
		public const byte F		= 0xa5;
		public const byte G		= 0xa6;
		public const byte H		= 0xa7;
		public const byte I		= 0xa8;
		public const byte J		= 0xa9;
		public const byte K		= 0xaa;
		public const byte L		= 0xab;
		public const byte M		= 0xac;
		public const byte N		= 0xad;
		public const byte O		= 0xae;
		public const byte P		= 0xaf;
		public const byte Q		= 0xb0;
		public const byte R		= 0xb1;
		public const byte S		= 0xb2;
		public const byte T		= 0xb3;
		public const byte U		= 0xb4;
		public const byte V		= 0xb5;
		public const byte W		= 0xb6;
		public const byte X		= 0xb7;
		public const byte Y		= 0xb8;
		public const byte Z		= 0xb9;
		public const byte AE		= 0xba;
		public const byte Ooblique	= 0xbb;
		public const byte Aring	= 0xbc;
		public const byte Ccedilla	= 0xbd;
		public const byte Semicolon	= 0xbe;
		public const byte Asterisk	= 0xbf;

		/* codes = 0xc0 through = 0xcf are for field attributes */

		public const byte Copyright	= 0xd0;
		public const byte Registered	= 0xd1;
		public const byte BoxA		= 0xd2;	/* status boxed A */
		public const byte Insert	= 0xd3;	/* status insert mode indicator */
		public const byte BoxB		= 0xd4;	/* status boxed B */
		public const byte Box6		= 0xd5;	/* status boxed 6 */
		public const byte PlusMinus	= 0xd6;
		public const byte ETH		= 0xd7;
		public const byte RightArrow	= 0xd8;
		public const byte THORN	= 0xd9;
		public const byte Upshift	= 0xda;	/* status upshift indicator */
		public const byte Human	= 0xdb;	/* status illegal position indicator */
		public const byte UnderB	= 0xdc;	/* status underlined B */
		public const byte DownShift	= 0xdd;	/* status downshift indicator */
		public const byte BoxQuestion	= 0xde;	/* status boxed question mark */
		public const byte BoxSolid	= 0xdf;	/* status solid block */

		/* codes = 0xe0 through = 0xef are for field attributes */

		public const byte BadCommHi	= 0xf0;	/* status bad communication indicator */
		public const byte CommHi	= 0xf1;	/* status communication indicator */
		public const byte CommJag	= 0xf2;	/* status communication indicator */
		public const byte CommLo	= 0xf3;	/* status communication indicator */
		public const byte ClockLeft	= 0xf4;	/* status wait symbol */
		public const byte ClockRight	= 0xf5;	/* status wait symbol */
		public const byte Lock		= 0xf6;	/* status keyboard lock X symbol */
		public const byte Eth		= 0xf7;
		public const byte LeftArrow	= 0xf8;
		public const byte Thorn	= 0xf9;
		public const byte KeyLeft	= 0xfa;	/* status key lock indicator */
		public const byte KeyRight	= 0xfb;	/* status key lock indicator */
		public const byte Box4		= 0xfc;	/* status boxed 4 */
		public const byte UnderA	= 0xfd;	/* status underlined A */
		public const byte MagCard	= 0xfe;	/* status magnetic card indicator */
		public const byte BoxHuman	= 0xff;	/* status boxed position indicator */
	}
}
