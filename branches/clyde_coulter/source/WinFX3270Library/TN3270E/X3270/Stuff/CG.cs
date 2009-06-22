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
	/// <summary>
	/// Summary description for CG.
	/// </summary>
	internal class CG
	{
		/*
		 *	cg.h
		 *
		 *		Character encoding for the 3270 character generator font,
		 *		using the same suffixes as Latin-1 XK_xxx keysyms.
		 *
		 *		Charaters that represent unique EBCDIC or status line codes
		 *		are noted with comments.
		 */

		public const byte CG_null		= 0x00;	/* EBCDIC 00 */
		public const byte CG_nobreakspace	= 0x01;
		public const byte CG_ff		= 0x02;	/* EBCDIC 0C */
		public const byte CG_cr		= 0x03;	/* EBCDIC 0D */
		public const byte CG_nl		= 0x04;	/* EBCDIC 15 */
		public const byte CG_em		= 0x05;	/* EBCDIC 19 */
		public const byte CG_eightones	= 0x06;	/* EBCDIC FF */
		public const byte CG_hyphen	= 0x07;
		public const byte CG_greater	= 0x08;
		public const byte CG_less		= 0x09;
		public const byte CG_bracketleft	= 0x0a;
		public const byte CG_bracketright	= 0x0b;
		public const byte CG_parenleft	= 0x0c;
		public const byte CG_parenright	= 0x0d;
		public const byte CG_braceleft	= 0x0e;
		public const byte CG_braceright	= 0x0f;
		public const byte CG_space	= 0x10;
		public const byte CG_equal	= 0x11;
		public const byte CG_apostrophe	= 0x12;
		public const byte CG_quotedbl	= 0x13;
		public const byte CG_slash	= 0x14;
		public const byte CG_backslash	= 0x15;
		public const byte CG_bar		= 0x16;
		public const byte CG_brokenbar	= 0x17;
		public const byte CG_question	= 0x18;
		public const byte CG_exclam	= 0x19;
		public const byte CG_dollar	= 0x1a;
		public const byte CG_cent		= 0x1b;
		public const byte CG_sterling	= 0x1c;
		public const byte CG_yen		= 0x1d;
		public const byte CG_paragraph	= 0x1e;
		public const byte CG_currency	= 0x1f;
		public const byte CG_0		= 0x20;
		public const byte CG_1		= 0x21;
		public const byte CG_2		= 0x22;
		public const byte CG_3		= 0x23;
		public const byte CG_4		= 0x24;
		public const byte CG_5		= 0x25;
		public const byte CG_6		= 0x26;
		public const byte CG_7		= 0x27;
		public const byte CG_8		= 0x28;
		public const byte CG_9		= 0x29;
		public const byte CG_ssharp	= 0x2a;
		public const byte CG_section	= 0x2b;
		public const byte CG_numbersign	= 0x2c;
		public const byte CG_at		= 0x2d;
		public const byte CG_percent	= 0x2e;
		public const byte CG_underscore	= 0x2f;
		public const byte CG_ampersand	= 0x30;
		public const byte CG_minus	= 0x31;
		public const byte CG_period	= 0x32;
		public const byte CG_comma	= 0x33;
		public const byte CG_colon	= 0x34;
		public const byte CG_plus		= 0x35;
		public const byte CG_notsign	= 0x36;
		public const byte CG_macron	= 0x37;
		public const byte CG_degree	= 0x38;
		public const byte CG_periodcentered	= 0x39;
		public const byte CG_asciicircum	= 0x3a;
		public const byte CG_asciitilde	= 0x3b;
		public const byte CG_diaeresis	= 0x3c;
		public const byte CG_grave	= 0x3d;
		public const byte CG_acute	= 0x3e;
		public const byte CG_cedilla	= 0x3f;
		public const byte CG_agrave	= 0x40;
		public const byte CG_egrave	= 0x41;
		public const byte CG_igrave	= 0x42;
		public const byte CG_ograve	= 0x43;
		public const byte CG_ugrave	= 0x44;
		public const byte CG_atilde	= 0x45;
		public const byte CG_otilde	= 0x46;
		public const byte CG_ydiaeresis	= 0x47;
		public const byte CG_Yacute	= 0x48;
		public const byte CG_yacute	= 0x49;
		public const byte CG_eacute	= 0x4a;
		public const byte CG_onequarter	= 0x4b;
		public const byte CG_onehalf	= 0x4c;
		public const byte CG_threequarters	= 0x4d;
		public const byte CG_udiaeresis	= 0x4e;
		//public const byte CG_udiaeresis	= 0x4e;
		public const byte CG_ccedilla	= 0x4f;
		public const byte CG_adiaeresis	= 0x50;
		public const byte CG_ediaeresis	= 0x51;
		public const byte CG_idiaeresis	= 0x52;
		public const byte CG_odiaeresis	= 0x53;
		public const byte CG_mu		= 0x54;
		public const byte CG_acircumflex	= 0x55;
		public const byte CG_ecircumflex	= 0x56;
		public const byte CG_icircumflex	= 0x57;
		public const byte CG_ocircumflex	= 0x58;
		public const byte CG_ucircumflex	= 0x59;
		public const byte CG_aacute	= 0x5a;
		public const byte CG_multiply	= 0x5b;
		public const byte CG_iacute	= 0x5c;
		public const byte CG_oacute	= 0x5d;
		public const byte CG_uacute	= 0x5e;
		public const byte CG_ntilde	= 0x5f;
		public const byte CG_Agrave	= 0x60;
		public const byte CG_Egrave	= 0x61;
		public const byte CG_Igrave	= 0x62;
		public const byte CG_Ograve	= 0x63;
		public const byte CG_Ugrave	= 0x64;
		public const byte CG_Atilde	= 0x65;
		public const byte CG_Otilde	= 0x66;
		public const byte CG_onesuperior	= 0x67;
		public const byte CG_twosuperior	= 0x68;
		public const byte CG_threesuperior	= 0x69;
		public const byte CG_ordfeminine	= 0x6a;
		public const byte CG_masculine	= 0x6b;
		public const byte CG_guillemotleft	= 0x6c;
		public const byte CG_guillemotright	= 0x6d;
		public const byte CG_exclamdown	= 0x6e;
		public const byte CG_questiondown	= 0x6f;
		public const byte CG_Adiaeresis	= 0x70;
		public const byte CG_Ediaeresis	= 0x71;
		public const byte CG_Idiaeresis	= 0x72;
		public const byte CG_Odiaeresis	= 0x73;
		public const byte CG_Udiaeresis	= 0x74;
		public const byte CG_Acircumflex	= 0x75;
		public const byte CG_Ecircumflex	= 0x76;
		public const byte CG_Icircumflex	= 0x77;
		public const byte CG_Ocircumflex	= 0x78;
		public const byte CG_Ucircumflex	= 0x79;
		public const byte CG_Aacute	= 0x7a;
		public const byte CG_Eacute	= 0x7b;
		public const byte CG_Iacute	= 0x7c;
		public const byte CG_Oacute	= 0x7d;
		public const byte CG_Uacute	= 0x7e;
		public const byte CG_Ntilde	= 0x7f;
		public const byte CG_a		= 0x80;
		public const byte CG_b		= 0x81;
		public const byte CG_c		= 0x82;
		public const byte CG_d		= 0x83;
		public const byte CG_e		= 0x84;
		public const byte CG_f		= 0x85;
		public const byte CG_g		= 0x86;
		public const byte CG_h		= 0x87;
		public const byte CG_i		= 0x88;
		public const byte CG_j		= 0x89;
		public const byte CG_k		= 0x8a;
		public const byte CG_l		= 0x8b;
		public const byte CG_m		= 0x8c;
		public const byte CG_n		= 0x8d;
		public const byte CG_o		= 0x8e;
		public const byte CG_p		= 0x8f;
		public const byte CG_q		= 0x90;
		public const byte CG_r		= 0x91;
		public const byte CG_s		= 0x92;
		public const byte CG_t		= 0x93;
		public const byte CG_u		= 0x94;
		public const byte CG_v		= 0x95;
		public const byte CG_w		= 0x96;
		public const byte CG_x		= 0x97;
		public const byte CG_y		= 0x98;
		public const byte CG_z		= 0x99;
		public const byte CG_ae		= 0x9a;
		public const byte CG_oslash	= 0x9b;
		public const byte CG_aring	= 0x9c;
		public const byte CG_division	= 0x9d;
		public const byte CG_fm		= 0x9e;	/* EBCDIC 1E */
		public const byte CG_dup		= 0x9f;	/* EBCDIC 1C */
		public const byte CG_A		= 0xa0;
		public const byte CG_B		= 0xa1;
		public const byte CG_C		= 0xa2;
		public const byte CG_D		= 0xa3;
		public const byte CG_E		= 0xa4;
		public const byte CG_F		= 0xa5;
		public const byte CG_G		= 0xa6;
		public const byte CG_H		= 0xa7;
		public const byte CG_I		= 0xa8;
		public const byte CG_J		= 0xa9;
		public const byte CG_K		= 0xaa;
		public const byte CG_L		= 0xab;
		public const byte CG_M		= 0xac;
		public const byte CG_N		= 0xad;
		public const byte CG_O		= 0xae;
		public const byte CG_P		= 0xaf;
		public const byte CG_Q		= 0xb0;
		public const byte CG_R		= 0xb1;
		public const byte CG_S		= 0xb2;
		public const byte CG_T		= 0xb3;
		public const byte CG_U		= 0xb4;
		public const byte CG_V		= 0xb5;
		public const byte CG_W		= 0xb6;
		public const byte CG_X		= 0xb7;
		public const byte CG_Y		= 0xb8;
		public const byte CG_Z		= 0xb9;
		public const byte CG_AE		= 0xba;
		public const byte CG_Ooblique	= 0xbb;
		public const byte CG_Aring	= 0xbc;
		public const byte CG_Ccedilla	= 0xbd;
		public const byte CG_semicolon	= 0xbe;
		public const byte CG_asterisk	= 0xbf;

		/* codes = 0xc0 through = 0xcf are for field attributes */

		public const byte CG_copyright	= 0xd0;
		public const byte CG_registered	= 0xd1;
		public const byte CG_boxA		= 0xd2;	/* status boxed A */
		public const byte CG_insert	= 0xd3;	/* status insert mode indicator */
		public const byte CG_boxB		= 0xd4;	/* status boxed B */
		public const byte CG_box6		= 0xd5;	/* status boxed 6 */
		public const byte CG_plusminus	= 0xd6;
		public const byte CG_ETH		= 0xd7;
		public const byte CG_rightarrow	= 0xd8;
		public const byte CG_THORN	= 0xd9;
		public const byte CG_upshift	= 0xda;	/* status upshift indicator */
		public const byte CG_human	= 0xdb;	/* status illegal position indicator */
		public const byte CG_underB	= 0xdc;	/* status underlined B */
		public const byte CG_downshift	= 0xdd;	/* status downshift indicator */
		public const byte CG_boxquestion	= 0xde;	/* status boxed question mark */
		public const byte CG_boxsolid	= 0xdf;	/* status solid block */

		/* codes = 0xe0 through = 0xef are for field attributes */

		public const byte CG_badcommhi	= 0xf0;	/* status bad communication indicator */
		public const byte CG_commhi	= 0xf1;	/* status communication indicator */
		public const byte CG_commjag	= 0xf2;	/* status communication indicator */
		public const byte CG_commlo	= 0xf3;	/* status communication indicator */
		public const byte CG_clockleft	= 0xf4;	/* status wait symbol */
		public const byte CG_clockright	= 0xf5;	/* status wait symbol */
		public const byte CG_lock		= 0xf6;	/* status keyboard lock X symbol */
		public const byte CG_eth		= 0xf7;
		public const byte CG_leftarrow	= 0xf8;
		public const byte CG_thorn	= 0xf9;
		public const byte CG_keyleft	= 0xfa;	/* status key lock indicator */
		public const byte CG_keyright	= 0xfb;	/* status key lock indicator */
		public const byte CG_box4		= 0xfc;	/* status boxed 4 */
		public const byte CG_underA	= 0xfd;	/* status underlined A */
		public const byte CG_magcard	= 0xfe;	/* status magnetic card indicator */
		public const byte CG_boxhuman	= 0xff;	/* status boxed position indicator */
	}
}
