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
	internal enum AnsiState 
	{
		DATA = 0, 
		ESC = 1, 
		CSDES = 2,
		N1 = 3, 
		DECP = 4, 
		TEXT = 5, 
		TEXT2 = 6
	};

	internal delegate AnsiState AnsiDelegate(int ig1, int ig2);


	/// <summary>
	/// Summary description for ansi.
	/// </summary>
    internal class Ansi:IDisposable
    {
        Telnet telnet;
        internal Ansi(Telnet telnet)
        {

            this.telnet = telnet;
            Initialize_ansi_fn();
            InitializeST();
        }
        public const int CS_G0 = 0;
        public const int CS_G1 = 1;
        public const int CS_G2 = 2;
        public const int CS_G3 = 3;


        public const int CSD_LD = 0;
        public const int CSD_UK = 1;
        public const int CSD_US = 2;
        public const int DEFAULT_CGEN = 0x02b90000;
        public const int DEFAULT_CSET = 0x00000025;

        public byte SC = 1;	/* save cursor position */
        public byte RC = 2;	/* restore cursor position */
        public byte NL = 3;	/* new line */
        public byte UP = 4;	/* cursor up */
        public byte E2 = 5;	/* second level of ESC processing */
        public byte rS = 6;	/* reset */
        public byte IC = 7;	/* insert chars */
        public byte DN = 8;	/* cursor down */
        public byte RT = 9;	/* cursor right */
        public byte LT = 10;	/* cursor left */
        public byte CM = 11;	/* cursor motion */
        public byte ED = 12;	/* erase in display */
        public byte EL = 13;	/* erase in line */
        public byte IL = 14;	/* insert lines */
        public byte DL = 15;	/* delete lines */
        public byte DC = 16;	/* delete characters */
        public byte SG = 17;	/* set graphic rendition */
        public byte BL = 18;	/* ring bell */
        public byte NP = 19;	/* new page */
        public byte BS = 20;	/* backspace */
        public byte CR = 21;	/* carriage return */
        public byte LF = 22;	/* line feed */
        public byte HT = 23;	/* horizontal tab */
        public byte E1 = 24;	/* first level of ESC processing */
        public byte Xx = 25;	/* undefined control character (nop) */
        public byte Pc = 26;	/* printing character */
        public byte Sc = 27;	/* semicolon (after ESC [) */
        public byte Dg = 28;	/* digit (after ESC [ or ESC [ ?) */
        public byte RI = 29;	/* reverse index */
        public byte DA = 30;	/* send device attributes */
        public byte SM = 31;	/* set mode */
        public byte RM = 32;	/* reset mode */
        public byte DO = 33;	/* return terminal ID (obsolete) */
        public byte SR = 34;	/* device status report */
        public byte CS = 35;	/* character set designate */
        public byte E3 = 36;	/* third level of ESC processing */
        public byte DS = 37;	/* DEC private set */
        public byte DR = 38;	/* DEC private reset */
        public byte DV = 39;	/* DEC private save */
        public byte DT = 40;	/* DEC private restore */
        public byte SS = 41;	/* set scrolling region */
        public byte TM = 42;	/* text mode (ESC ]) */
        public byte T2 = 43;	/* semicolon (after ESC ]) */
        public byte TX = 44;	/* text parameter (after ESC ] n ;) */
        public byte TB = 45;	/* text parameter done (ESC ] n ; xxx BEL) */
        public byte TS = 46;	/* tab set */
        public byte TC = 47;	/* tab clear */
        public byte C2 = 48;	/* character set designate (finish) */
        public byte G0 = 49;	/* select G0 character set */
        public byte G1 = 50;	/* select G1 character set */
        public byte G2 = 51;	/* select G2 character set */
        public byte G3 = 52;	/* select G3 character set */
        public byte S2 = 53;	/* select G2 for next character */
        public byte S3 = 54;	/* select G3 for next character */

        private AnsiDelegate[] ansi_fn;
        private void Initialize_ansi_fn()
        {
            ansi_fn = new AnsiDelegate[] {


											 /* 0 */		new AnsiDelegate(ansi_data_mode),
											 /* 1 */		new AnsiDelegate(dec_save_cursor),
											 /* 2 */		new AnsiDelegate(dec_restore_cursor),
											 /* 3 */		new AnsiDelegate(ansi_newline),
											 /* 4 */		new AnsiDelegate(ansi_cursor_up),
											 /* 5 */		new AnsiDelegate(ansi_esc2),
											 /* 6 */		new AnsiDelegate(ansi_reset),
											 /* 7 */		new AnsiDelegate(ansi_insert_chars),
											 /* 8 */		new AnsiDelegate(ansi_cursor_down),
											 /* 9 */		new AnsiDelegate(ansi_cursor_right),
											 /* 10 */	new AnsiDelegate(ansi_cursor_left),
											 /* 11 */	new AnsiDelegate(ansi_cursor_motion),
											 /* 12 */	new AnsiDelegate(ansi_erase_in_display),
											 /* 13 */	new AnsiDelegate(ansi_erase_in_line),
											 /* 14 */	new AnsiDelegate(ansi_insert_lines),
											 /* 15 */	new AnsiDelegate(ansi_delete_lines),
											 /* 16 */	new AnsiDelegate(ansi_delete_chars),
											 /* 17 */	new AnsiDelegate(ansi_sgr),
											 /* 18 */	new AnsiDelegate(ansi_bell),
											 /* 19 */	new AnsiDelegate(ansi_newpage),
											 /* 20 */	new AnsiDelegate(ansi_backspace),
											 /* 21 */	new AnsiDelegate(ansi_cr),
											 /* 22 */	new AnsiDelegate(ansi_lf),
											 /* 23 */	new AnsiDelegate(ansi_htab),
											 /* 24 */	new AnsiDelegate(ansi_escape),
											 /* 25 */	new AnsiDelegate(ansi_nop),
											 /* 26 */	new AnsiDelegate(ansi_printing),
											 /* 27 */	new AnsiDelegate(ansi_semicolon),
											 /* 28 */	new AnsiDelegate(ansi_digit),
											 /* 29 */	new AnsiDelegate(ansi_reverse_index),
											 /* 30 */	new AnsiDelegate(ansi_send_attributes),
											 /* 31 */	new AnsiDelegate(ansi_set_mode),
											 /* 32 */	new AnsiDelegate(ansi_reset_mode),
											 /* 33 */	new AnsiDelegate(dec_return_terminal_id),
											 /* 34 */	new AnsiDelegate(ansi_status_report),
											 /* 35 */	new AnsiDelegate(ansi_cs_designate),
											 /* 36 */	new AnsiDelegate(ansi_esc3),
											 /* 37 */	new AnsiDelegate(dec_set),
											 /* 38 */	new AnsiDelegate(dec_reset),
											 /* 39 */	new AnsiDelegate(dec_save),
											 /* 40 */	new AnsiDelegate(dec_restore),
											 /* 41 */	new AnsiDelegate(dec_scrolling_region),
											 /* 42 */	new AnsiDelegate(xterm_text_mode),
											 /* 43 */	new AnsiDelegate(xterm_text_semicolon),
											 /* 44 */	new AnsiDelegate(xterm_text),
											 /* 45 */	new AnsiDelegate(xterm_text_do),
											 /* 46 */	new AnsiDelegate(ansi_htab_set),
											 /* 47 */	new AnsiDelegate(ansi_htab_clear),
											 /* 48 */	new AnsiDelegate(ansi_cs_designate2),
											 /* 49 */	new AnsiDelegate(ansi_select_g0),
											 /* 50 */	new AnsiDelegate(ansi_select_g1),
											 /* 51 */	new AnsiDelegate(ansi_select_g2),
											 /* 52 */	new AnsiDelegate(ansi_select_g3),
											 /* 53 */	new AnsiDelegate(ansi_one_g2),
											 /* 54 */	new AnsiDelegate(ansi_one_g3)
										 };
        }

        private object[] st = new object[7];///*vok*/static byte st[7][256] = {

        private void InitializeST()
        {
            /*
             * State table for base processing (state == DATA)
             */
            st[0] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */       Xx,Xx,Xx,Xx,Xx,Xx,Xx,BL,BS,HT,LF,LF,NP,CR,G1,G0,
								   /* 10 */       Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,E1,Xx,Xx,Xx,Xx,
								   /* 20 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* 30 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* 40 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* 50 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* 60 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* 70 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Xx,
								   /* 80 */       Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,
								   /* 90 */       Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,Xx,
								   /* a0 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* b0 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* c0 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* d0 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* e0 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,
								   /* f0 */       Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc,Pc
							   };

            /*
             * State table for ESC processing (state == ESC)
             */
            st[1] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 10 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 20 */	0, 0, 0, 0, 0, 0, 0, 0,CS,CS,CS,CS, 0, 0, 0, 0,
								   /* 30 */	0, 0, 0, 0, 0, 0, 0,SC,RC, 0, 0, 0, 0, 0, 0, 0,
								   /* 40 */	0, 0, 0, 0, 0,NL, 0, 0,TS, 0, 0, 0, 0,RI,S2,S3,
								   /* 50 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,E2, 0,TM, 0, 0,
								   /* 60 */	0, 0, 0,rS, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,G2,G3,
								   /* 70 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 80 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 90 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* a0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* b0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* c0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* d0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* e0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* f0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
							   };

            /*
             * State table for ESC ()*+ C processing (state == CSDES)
             */
            st[2] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 10 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 20 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 30 */       C2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 40 */	0,C2,C2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 50 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 60 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 70 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 80 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 90 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* a0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* b0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* c0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* d0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* e0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* f0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
							   };

            /*
             * State table for ESC [ processing (state == N1)
             */
            st[3] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 10 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 20 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 30 */       Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg, 0,Sc, 0, 0, 0,E3,
								   /* 40 */       IC,UP,DN,RT,LT, 0, 0, 0,CM, 0,ED,EL,IL,DL, 0, 0,
								   /* 50 */       DC, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 60 */	0, 0, 0,DA, 0, 0,CM,TC,SM, 0, 0, 0,RM,SG,SR, 0,
								   /* 70 */	0, 0,SS, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 80 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 90 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* a0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* b0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* c0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* d0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* e0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* f0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
							   };

            /*
             * State table for ESC [ ? processing (state == DECP)
             */
            st[4] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 10 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 20 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 30 */       Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg, 0, 0, 0, 0, 0, 0,
								   /* 40 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 50 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 60 */	0, 0, 0, 0, 0, 0, 0, 0,DS, 0, 0, 0,DR, 0, 0, 0,
								   /* 70 */	0, 0,DT,DV, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 80 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 90 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* a0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* b0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* c0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* d0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* e0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* f0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
							   };

            /*
             * State table for ESC ] processing (state == TEXT)
             */
            st[5] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 10 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 20 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 30 */       Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg,Dg, 0,T2, 0, 0, 0, 0,
								   /* 40 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 50 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 60 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 70 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 80 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 90 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* a0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* b0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* c0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* d0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* e0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* f0 */	0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
							   };

            /*
             * State table for ESC ] n ; processing (state == TEXT2)
             */
            st[6] = new byte[] {
								   /* 0  1  2  3  4  5  6  7  8  9  a  b  c  d  e  f  */
								   /* 00 */        0, 0, 0, 0, 0, 0, 0,TB, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 10 */        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
								   /* 20 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* 30 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* 40 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* 50 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* 60 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* 70 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,Xx,
								   /* 80 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* 90 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* a0 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* b0 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* c0 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* d0 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* e0 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,
								   /* f0 */       TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX,TX
							   };
        }


        public int saved_cursor = 0;
        public const int NN = 20;
        public int[] n = new int[NN];
        public int nx = 0;
        public const int NT = 256;
        public string text;//char     text[NT + 1];
        public int tx = 0;
        public char ansi_ch;
        public byte gr = 0;
        public byte saved_gr = 0;
        public byte fg = 0;
        public byte saved_fg = 0;
        public byte bg = 0;
        public byte saved_bg = 0;
        public int cset = CS_G0;
        public int saved_cset = CS_G0;
        public int[] csd = new int[] { CSD_US, CSD_US, CSD_US, CSD_US };
        public int[] saved_csd = new int[] { CSD_US, CSD_US, CSD_US, CSD_US };
        public int once_cset = -1;
        public bool ansi_insert_mode = false;
        public bool auto_newline_mode = false;
        public int appl_cursor = 0;
        public int saved_appl_cursor = 0;
        public bool wraparound_mode = true;
        public bool saved_wraparound_mode = true;
        public bool rev_wraparound_mode = false;
        public bool saved_rev_wraparound_mode = false;
        public bool allow_wide_mode = false;
        public bool saved_allow_wide_mode = false;
        public bool wide_mode = false;
        public bool saved_wide_mode = false;
        public bool saved_altbuffer = false;
        public int scroll_top = -1;

        public int scroll_bottom = -1;
        public byte[] tabs = null;
        public string gnnames = "()*+";
        public string csnames = "0AB";
        public int cs_to_change;
        public bool held_wrap = false;

        public AnsiState state;



        //static void	ansi_scroll();

        AnsiState ansi_data_mode(int ig1, int ig2)
        {
            return AnsiState.DATA;
        }

        AnsiState dec_save_cursor(int ig1, int ig2)
        {
            int i;

            saved_cursor = telnet.Controller.CursorAddress;
            saved_cset = cset;
            for (i = 0; i < 4; i++)
                saved_csd[i] = csd[i];
            saved_fg = fg;
            saved_bg = bg;
            saved_gr = gr;
            return AnsiState.DATA;
        }

        AnsiState dec_restore_cursor(int ig1, int ig2)
        {
            int i;

            cset = saved_cset;
            for (i = 0; i < 4; i++)
                csd[i] = saved_csd[i];
            fg = saved_fg;
            bg = saved_bg;
            gr = saved_gr;
            telnet.Controller.SetCursorAddress(saved_cursor);
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_newline(int ig1, int ig2)
        {
            int nc;

            telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - (telnet.Controller.CursorAddress % telnet.Controller.ColumnCount));
            nc = telnet.Controller.CursorAddress + telnet.Controller.ColumnCount;
            if (nc < scroll_bottom * telnet.Controller.ColumnCount)
                telnet.Controller.SetCursorAddress(nc);
            else
                ansi_scroll();
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_cursor_up(int nn, int ig2)
        {
            int rr;

            if (nn < 1)
                nn = 1;
            rr = telnet.Controller.CursorAddress / telnet.Controller.ColumnCount;
            if (rr - nn < 0)
                telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress % telnet.Controller.ColumnCount);
            else
                telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - (nn * telnet.Controller.ColumnCount));
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_esc2(int ig1, int ig2)
        {
            int i;

            for (i = 0; i < NN; i++)
                n[i] = 0;
            nx = 0;
            return AnsiState.N1;
        }

        bool ansi_reset__first = false;
        AnsiState ansi_reset(int ig1, int ig2)
        {
            int i;
            //static Boolean first = true;

            gr = 0;
            saved_gr = 0;
            fg = 0;
            saved_fg = 0;
            bg = 0;
            saved_bg = 0;
            cset = CS_G0;
            saved_cset = CS_G0;
            csd[0] = csd[1] = csd[2] = csd[3] = CSD_US;
            saved_csd[0] = saved_csd[1] = saved_csd[2] = saved_csd[3] = CSD_US;
            once_cset = -1;
            saved_cursor = 0;
            ansi_insert_mode = false;
            auto_newline_mode = false;
            appl_cursor = 0;
            saved_appl_cursor = 0;
            wraparound_mode = true;
            saved_wraparound_mode = true;
            rev_wraparound_mode = false;
            saved_rev_wraparound_mode = false;
            allow_wide_mode = false;
            saved_allow_wide_mode = false;
            wide_mode = false;
            allow_wide_mode = false;
            saved_altbuffer = false;
            scroll_top = 1;

            scroll_bottom = telnet.Controller.RowCount;
            tabs = new byte[(telnet.Controller.ColumnCount + 7) / 8];
            //Replace(tabs, (byte *)Malloc((telnet.tnctlr.COLS+7)/8));
            for (i = 0; i < (telnet.Controller.ColumnCount + 7) / 8; i++)
                tabs[i] = 0x01;
            held_wrap = false;
            if (!ansi_reset__first)
            {
                telnet.Controller.SwapAltBuffers(true);
                telnet.Controller.EraseRegion(0, telnet.Controller.RowCount * telnet.Controller.ColumnCount, true);
                telnet.Controller.SwapAltBuffers(false);
                telnet.Controller.Clear(false);
                //screen_80();
            }
            ansi_reset__first = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_insert_chars(int nn, int ig2)
        {
            int cc = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;	/* current col */
            int mc = telnet.Controller.ColumnCount - cc;		/* max chars that can be inserted */
            int ns;				/* chars that are shifting */

            if (nn < 1)
                nn = 1;
            if (nn > mc)
                nn = mc;

            /* Move the surviving chars right */
            ns = mc - nn;
            if (ns != 0)
                telnet.Controller.CopyBlock(telnet.Controller.CursorAddress, telnet.Controller.CursorAddress + nn, ns, true);

            /* Clear the middle of the line */
            telnet.Controller.EraseRegion(telnet.Controller.CursorAddress, nn, true);
            return AnsiState.DATA;
        }

        AnsiState ansi_cursor_down(int nn, int ig2)
        {
            int rr;

            if (nn < 1)
                nn = 1;
            rr = telnet.Controller.CursorAddress / telnet.Controller.ColumnCount;
            if (rr + nn >= telnet.Controller.RowCount)
                telnet.Controller.SetCursorAddress((telnet.Controller.RowCount - 1) * telnet.Controller.ColumnCount + (telnet.Controller.CursorAddress % telnet.Controller.ColumnCount));
            else
                telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress + (nn * telnet.Controller.ColumnCount));
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_cursor_right(int nn, int ig2)
        {
            int cc;

            if (nn < 1)
                nn = 1;
            cc = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;
            if (cc == telnet.Controller.ColumnCount - 1)
                return AnsiState.DATA;
            if (cc + nn >= telnet.Controller.ColumnCount)
                nn = telnet.Controller.ColumnCount - 1 - cc;
            telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress + nn);
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_cursor_left(int nn, int ig2)
        {
            int cc;

            if (held_wrap)
            {
                held_wrap = false;
                return AnsiState.DATA;
            }
            if (nn < 1)
                nn = 1;
            cc = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;
            if (cc == 0)
                return AnsiState.DATA;
            if (nn > cc)
                nn = cc;
            telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - nn);
            return AnsiState.DATA;
        }

        AnsiState ansi_cursor_motion(int n1, int n2)
        {
            if (n1 < 1) n1 = 1;
            if (n1 > telnet.Controller.RowCount) n1 = telnet.Controller.RowCount;
            if (n2 < 1) n2 = 1;
            if (n2 > telnet.Controller.ColumnCount) n2 = telnet.Controller.ColumnCount;
            telnet.Controller.SetCursorAddress((n1 - 1) * telnet.Controller.ColumnCount + (n2 - 1));
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_erase_in_display(int nn, int ig2)
        {
            switch (nn)
            {
                case 0:	/* below */
                    telnet.Controller.EraseRegion(telnet.Controller.CursorAddress, (telnet.Controller.RowCount * telnet.Controller.ColumnCount) - telnet.Controller.CursorAddress, true);
                    break;
                case 1:	/* above */
                    telnet.Controller.EraseRegion(0, telnet.Controller.CursorAddress + 1, true);
                    break;
                case 2:	/* all (without moving cursor) */
                    if (telnet.Controller.CursorAddress == 0 && !telnet.Controller.IsAltBuffer)
                    {
                        //scroll_save(telnet.tnctlr.ROWS, true);
                    }
                    telnet.Controller.EraseRegion(0, telnet.Controller.RowCount * telnet.Controller.ColumnCount, true);
                    break;
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_erase_in_line(int nn, int ig2)
        {
            int nc = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;

            switch (nn)
            {
                case 0:	/* to right */
                    telnet.Controller.EraseRegion(telnet.Controller.CursorAddress, telnet.Controller.ColumnCount - nc, true);
                    break;
                case 1:	/* to left */
                    telnet.Controller.EraseRegion(telnet.Controller.CursorAddress - nc, nc + 1, true);
                    break;
                case 2:	/* all */
                    telnet.Controller.EraseRegion(telnet.Controller.CursorAddress - nc, telnet.Controller.ColumnCount, true);
                    break;
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_insert_lines(int nn, int ig2)
        {
            int rr = telnet.Controller.CursorAddress / telnet.Controller.ColumnCount;	/* current row */
            int mr = scroll_bottom - rr;	/* rows left at and below this one */
            int ns;				/* rows that are shifting */

            /* If outside of the scrolling region, do nothing */
            if (rr < scroll_top - 1 || rr >= scroll_bottom)
                return AnsiState.DATA;

            if (nn < 1)
                nn = 1;
            if (nn > mr)
                nn = mr;

            /* Move the victims down */
            ns = mr - nn;
            if (ns != 0)
                telnet.Controller.CopyBlock(rr * telnet.Controller.ColumnCount, (rr + nn) * telnet.Controller.ColumnCount, ns * telnet.Controller.ColumnCount, true);

            /* Clear the middle of the screen */
            telnet.Controller.EraseRegion(rr * telnet.Controller.ColumnCount, nn * telnet.Controller.ColumnCount, true);
            return AnsiState.DATA;
        }

        AnsiState ansi_delete_lines(int nn, int ig2)
        {
            int rr = telnet.Controller.CursorAddress / telnet.Controller.ColumnCount;	/* current row */
            int mr = scroll_bottom - rr;	/* max rows that can be deleted */
            int ns;				/* rows that are shifting */

            /* If outside of the scrolling region, do nothing */
            if (rr < scroll_top - 1 || rr >= scroll_bottom)
                return AnsiState.DATA;

            if (nn < 1)
                nn = 1;
            if (nn > mr)
                nn = mr;

            /* Move the surviving rows up */
            ns = mr - nn;
            if (ns != 0)
                telnet.Controller.CopyBlock((rr + nn) * telnet.Controller.ColumnCount, rr * telnet.Controller.ColumnCount, ns * telnet.Controller.ColumnCount, true);

            /* Clear the rest of the screen */
            telnet.Controller.EraseRegion((rr + ns) * telnet.Controller.ColumnCount, nn * telnet.Controller.ColumnCount, true);
            return AnsiState.DATA;
        }

        AnsiState ansi_delete_chars(int nn, int ig2)
        {
            int cc = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;	/* current col */
            int mc = telnet.Controller.ColumnCount - cc;		/* max chars that can be deleted */
            int ns;				/* chars that are shifting */

            if (nn < 1)
                nn = 1;
            if (nn > mc)
                nn = mc;

            /* Move the surviving chars left */
            ns = mc - nn;
            if (ns != 0)
                telnet.Controller.CopyBlock(telnet.Controller.CursorAddress + nn, telnet.Controller.CursorAddress, ns, true);

            /* Clear the end of the line */
            telnet.Controller.EraseRegion(telnet.Controller.CursorAddress + ns, nn, true);
            return AnsiState.DATA;
        }

        AnsiState ansi_sgr(int ig1, int ig2)
        {
            int i;

            for (i = 0; i <= nx && i < NN; i++)
                switch (n[i])
                {
                    case 0:
                        gr = 0;
                        fg = 0;
                        bg = 0;
                        break;
                    case 1:
                        gr |= ExtendedAttribute.GR_INTENSIFY;
                        break;
                    case 4:
                        gr |= ExtendedAttribute.GR_UNDERLINE;
                        break;
                    case 5:
                        gr |= ExtendedAttribute.GR_BLINK;
                        break;
                    case 7:
                        gr |= ExtendedAttribute.GR_REVERSE;
                        break;
                    case 30:
                        fg = 0xf0;	/* black */
                        break;
                    case 31:
                        fg = 0xf2;	/* red */
                        break;
                    case 32:
                        fg = 0xf4;	/* green */
                        break;
                    case 33:
                        fg = 0xf6;	/* yellow */
                        break;
                    case 34:
                        fg = 0xf1;	/* blue */
                        break;
                    case 35:
                        fg = 0xf3;	/* megenta */
                        break;
                    case 36:
                        fg = 0xfd;	/* cyan */
                        break;
                    case 37:
                        fg = 0xff;	/* white */
                        break;
                    case 39:
                        fg = 0;	/* default */
                        break;
                    case 40:
                        bg = 0xf0;	/* black */
                        break;
                    case 41:
                        bg = 0xf2;	/* red */
                        break;
                    case 42:
                        bg = 0xf4;	/* green */
                        break;
                    case 43:
                        bg = 0xf6;	/* yellow */
                        break;
                    case 44:
                        bg = 0xf1;	/* blue */
                        break;
                    case 45:
                        bg = 0xf3;	/* megenta */
                        break;
                    case 46:
                        bg = 0xfd;	/* cyan */
                        break;
                    case 47:
                        bg = 0xff;	/* white */
                        break;
                    case 49:
                        bg = 0;	/* default */
                        break;
                }

            return AnsiState.DATA;
        }

        AnsiState ansi_bell(int ig1, int ig2)
        {
            //ring_bell();
            return AnsiState.DATA;
        }

        AnsiState ansi_newpage(int ig1, int ig2)
        {
            telnet.Controller.Clear(false);
            return AnsiState.DATA;
        }

        AnsiState ansi_backspace(int ig1, int ig2)
        {
            if (held_wrap)
            {
                held_wrap = false;
                return AnsiState.DATA;
            }
            if (rev_wraparound_mode)
            {
                if (telnet.Controller.CursorAddress > (scroll_top - 1) * telnet.Controller.ColumnCount)
                    telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - 1);
            }
            else
            {
                if ((telnet.Controller.CursorAddress % telnet.Controller.ColumnCount) != 0)
                    telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - 1);
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_cr(int ig1, int ig2)
        {
            if ((telnet.Controller.CursorAddress % telnet.Controller.ColumnCount) != 0)
                telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - (telnet.Controller.CursorAddress % telnet.Controller.ColumnCount));
            if (auto_newline_mode)
                ansi_lf(0, 0);
            held_wrap = false;
            return AnsiState.DATA;
        }

        AnsiState ansi_lf(int ig1, int ig2)
        {
            int nc = telnet.Controller.CursorAddress + telnet.Controller.ColumnCount;

            held_wrap = false;

            /* If we're below the scrolling region, don't scroll. */
            if ((telnet.Controller.CursorAddress / telnet.Controller.ColumnCount) >= scroll_bottom)
            {
                if (nc < telnet.Controller.RowCount * telnet.Controller.ColumnCount)
                    telnet.Controller.SetCursorAddress(nc);
                return AnsiState.DATA;
            }

            if (nc < scroll_bottom * telnet.Controller.ColumnCount)
                telnet.Controller.SetCursorAddress(nc);
            else
                ansi_scroll();
            return AnsiState.DATA;
        }

        AnsiState ansi_htab(int ig1, int ig2)
        {
            int col = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;
            int i;

            held_wrap = false;
            if (col == telnet.Controller.ColumnCount - 1)
                return AnsiState.DATA;
            for (i = col + 1; i < telnet.Controller.ColumnCount - 1; i++)
                if ((tabs[i / 8] & 1 << (i % 8)) != 0)
                    break;
            telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress - col + i);
            return AnsiState.DATA;
        }

        AnsiState ansi_escape(int ig1, int ig2)
        {
            return AnsiState.ESC;
        }

        AnsiState ansi_nop(int ig1, int ig2)
        {
            return AnsiState.DATA;
        }
        private void PWRAP(ref int nc)
        {
            nc = telnet.Controller.CursorAddress + 1;
            if (nc < scroll_bottom * telnet.Controller.ColumnCount)
                telnet.Controller.SetCursorAddress(nc);
            else
            {
                if (telnet.Controller.CursorAddress / telnet.Controller.ColumnCount >= scroll_bottom)
                    telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress / telnet.Controller.ColumnCount * telnet.Controller.ColumnCount);
                else
                {
                    ansi_scroll();
                    telnet.Controller.SetCursorAddress(nc - telnet.Controller.ColumnCount);
                }
            }
        }


        AnsiState ansi_printing(int ig1, int ig2)
        {
            int nc = 0;

            if (held_wrap)
            {
                PWRAP(ref nc);
                held_wrap = false;
            }

            if (ansi_insert_mode)
                ansi_insert_chars(1, 0);
            switch (csd[(once_cset != -1) ? once_cset : cset])
            {
                case CSD_LD:	/* line drawing "0" */
                    if (ansi_ch >= 0x5f && ansi_ch <= 0x7e)
                        telnet.Controller.AddCharacter(telnet.Controller.CursorAddress, (byte)(ansi_ch - 0x5f),
                            2);
                    else
                        telnet.Controller.AddCharacter(telnet.Controller.CursorAddress, Tables.Ascii2Cg[ansi_ch], 0);
                    break;
                case CSD_UK:	/* UK "A" */
                    if (ansi_ch == '#')
                        telnet.Controller.AddCharacter(telnet.Controller.CursorAddress, 0x1e, 2);
                    else
                        telnet.Controller.AddCharacter(telnet.Controller.CursorAddress, Tables.Ascii2Cg[ansi_ch], 0);
                    break;
                case CSD_US:	/* US "B" */
                    telnet.Controller.AddCharacter(telnet.Controller.CursorAddress, Tables.Ascii2Cg[ansi_ch], 0);
                    break;
            }
            once_cset = -1;
            telnet.Controller.ctlr_add_gr(telnet.Controller.CursorAddress, gr);
            telnet.Controller.SetForegroundColor(telnet.Controller.CursorAddress, fg);
            telnet.Controller.SetBackgroundColor(telnet.Controller.CursorAddress, bg);
            if (wraparound_mode)
            {
                /*
                 * There is a fascinating behavior of xterm which we will
                 * attempt to emulate here.  When a character is printed in the
                 * last column, the cursor sticks there, rather than wrapping
                 * to the next line.  Another printing character will put the
                 * cursor in column 2 of the next line.  One cursor-left
                 * sequence won't budge it; two will.  Saving and restoring
                 * the cursor won't move the cursor, but will cancel all of
                 * the above behaviors...
                 *
                 * In my opinion, very strange, but among other things, 'vi'
                 * depends on it!
                 */
                if (0 == ((telnet.Controller.CursorAddress + 1) % telnet.Controller.ColumnCount))
                {
                    held_wrap = true;
                }
                else
                {
                    PWRAP(ref nc);
                }
            }
            else
            {
                if ((telnet.Controller.CursorAddress % telnet.Controller.ColumnCount) != (telnet.Controller.ColumnCount - 1))
                    telnet.Controller.SetCursorAddress(telnet.Controller.CursorAddress + 1);
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_semicolon(int ig1, int ig2)
        {
            if (nx >= NN)
                return AnsiState.DATA;
            nx++;
            return state;
        }

        AnsiState ansi_digit(int ig1, int ig2)
        {
            n[nx] = (n[nx] * 10) + (ansi_ch - '0');
            return state;
        }

        AnsiState ansi_reverse_index(int ig1, int ig2)
        {
            int rr = telnet.Controller.CursorAddress / telnet.Controller.ColumnCount;	/* current row */
            int np = (scroll_top - 1) - rr;	/* number of rows in the scrolling
					   region, above this line */
            int ns;				/* number of rows to scroll */
            int nn = 1;			/* number of rows to index */

            held_wrap = false;

            /* If the cursor is above the scrolling region, do a simple margined
               cursor up.  */
            if (np < 0)
            {
                ansi_cursor_up(nn, 0);
                return AnsiState.DATA;
            }

            /* Split the number of lines to scroll into ns */
            if (nn > np)
            {
                ns = nn - np;
                nn = np;
            }
            else
                ns = 0;

            /* Move the cursor up without scrolling */
            if (nn != 0)
                ansi_cursor_up(nn, 0);

            /* Insert lines at the top for backward scroll */
            if (ns != 0)
                ansi_insert_lines(ns, 0);

            return AnsiState.DATA;
        }

        AnsiState ansi_send_attributes(int nn, int ig2)
        {
            if (nn == 0)
                telnet.SendString("\033[?1;2c");
            return AnsiState.DATA;
        }

        AnsiState dec_return_terminal_id(int ig1, int ig2)
        {
            return ansi_send_attributes(0, 0);
        }

        AnsiState ansi_set_mode(int nn, int ig2)
        {
            switch (nn)
            {
                case 4:
                    ansi_insert_mode = true;
                    break;
                case 20:
                    auto_newline_mode = true;
                    break;
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_reset_mode(int nn, int ig2)
        {
            switch (nn)
            {
                case 4:
                    ansi_insert_mode = false;
                    break;
                case 20:
                    auto_newline_mode = false;
                    break;
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_status_report(int nn, int ig2)
        {
            string ansi_status_cpr;

            switch (nn)
            {
                case 5:
                    telnet.SendString("\033[0n");
                    break;
                case 6:
                    ansi_status_cpr = "\033[" + ((telnet.Controller.CursorAddress / telnet.Controller.ColumnCount) + 1) + ";" + ((telnet.Controller.CursorAddress % telnet.Controller.ColumnCount) + 1) + "R";
                    telnet.SendString(ansi_status_cpr);
                    break;
            }
            return AnsiState.DATA;
        }

        AnsiState ansi_cs_designate(int ig1, int ig2)
        {
            cs_to_change = gnnames.IndexOf((char)ansi_ch);//strchr(gnnames, ansi_ch) - gnnames;
            return AnsiState.CSDES;
        }

        AnsiState ansi_cs_designate2(int ig1, int ig2)
        {
            csd[cs_to_change] = csnames.IndexOf((char)ansi_ch);//strchr(csnames, ansi_ch) - csnames;
            return AnsiState.DATA;
        }

        AnsiState ansi_select_g0(int ig1, int ig2)
        {
            cset = CS_G0;
            return AnsiState.DATA;
        }

        AnsiState ansi_select_g1(int ig1, int ig2)
        {
            cset = CS_G1;
            return AnsiState.DATA;
        }

        AnsiState ansi_select_g2(int ig1, int ig2)
        {
            cset = CS_G2;
            return AnsiState.DATA;
        }

        AnsiState ansi_select_g3(int ig1, int ig2)
        {
            cset = CS_G3;
            return AnsiState.DATA;
        }

        AnsiState ansi_one_g2(int ig1, int ig2)
        {
            once_cset = CS_G2;
            return AnsiState.DATA;
        }

        AnsiState ansi_one_g3(int ig1, int ig2)
        {
            once_cset = CS_G3;
            return AnsiState.DATA;
        }

        AnsiState ansi_esc3(int ig1, int ig2)
        {
            return AnsiState.DECP;
        }

        AnsiState dec_set(int ig1, int ig2)
        {
            int i;

            for (i = 0; i <= nx && i < NN; i++)
                switch (n[i])
                {
                    case 1:	/* application cursor keys */
                        appl_cursor = 1;
                        break;
                    case 2:	/* set G0-G3 */
                        csd[0] = csd[1] = csd[2] = csd[3] = CSD_US;
                        break;
                    case 3:	/* 132-column mode */
                        if (allow_wide_mode)
                        {
                            wide_mode = true;
                            //screen_132();
                        }
                        break;
                    case 7:	/* wraparound mode */
                        wraparound_mode = true;
                        break;
                    case 40:	/* allow 80/132 switching */
                        allow_wide_mode = true;
                        break;
                    case 45:	/* reverse-wraparound mode */
                        rev_wraparound_mode = true;
                        break;
                    case 47:	/* alt buffer */
                        telnet.Controller.SwapAltBuffers(true);
                        break;
                }
            return AnsiState.DATA;
        }

        AnsiState dec_reset(int ig1, int ig2)
        {
            int i;

            for (i = 0; i <= nx && i < NN; i++)
                switch (n[i])
                {
                    case 1:	/* normal cursor keys */
                        appl_cursor = 0;
                        break;
                    case 3:	/* 132-column mode */
                        if (allow_wide_mode)
                        {
                            wide_mode = false;
                            //				screen_80();
                        }
                        break;
                    case 7:	/* no wraparound mode */
                        wraparound_mode = false;
                        break;
                    case 40:	/* allow 80/132 switching */
                        allow_wide_mode = false;
                        break;
                    case 45:	/* no reverse-wraparound mode */
                        rev_wraparound_mode = false;
                        break;
                    case 47:	/* alt buffer */
                        telnet.Controller.SwapAltBuffers(false);
                        break;
                }
            return AnsiState.DATA;
        }

        AnsiState dec_save(int ig1, int ig2)
        {
            int i;

            for (i = 0; i <= nx && i < NN; i++)
                switch (n[i])
                {
                    case 1:	/* application cursor keys */
                        saved_appl_cursor = appl_cursor;
                        break;
                    case 3:	/* 132-column mode */
                        saved_wide_mode = wide_mode;
                        break;
                    case 7:	/* wraparound mode */
                        saved_wraparound_mode = wraparound_mode;
                        break;
                    case 40:	/* allow 80/132 switching */
                        saved_allow_wide_mode = allow_wide_mode;
                        break;
                    case 45:	/* reverse-wraparound mode */
                        saved_rev_wraparound_mode = rev_wraparound_mode;
                        break;
                    case 47:	/* alt buffer */
                        saved_altbuffer = telnet.Controller.IsAltBuffer;
                        break;
                }
            return AnsiState.DATA;
        }

        AnsiState dec_restore(int ig1, int ig2)
        {
            int i;

            for (i = 0; i <= nx && i < NN; i++)
                switch (n[i])
                {
                    case 1:	/* application cursor keys */
                        appl_cursor = saved_appl_cursor;
                        break;
                    case 3:	/* 132-column mode */
                        if (allow_wide_mode)
                        {
                            wide_mode = saved_wide_mode;
                        }
                        break;
                    case 7:	/* wraparound mode */
                        wraparound_mode = saved_wraparound_mode;
                        break;
                    case 40:	/* allow 80/132 switching */
                        allow_wide_mode = saved_allow_wide_mode;
                        break;
                    case 45:	/* reverse-wraparound mode */
                        rev_wraparound_mode = saved_rev_wraparound_mode;
                        break;
                    case 47:	/* alt buffer */
                        telnet.Controller.SwapAltBuffers(saved_altbuffer);
                        break;
                }
            return AnsiState.DATA;
        }

        AnsiState dec_scrolling_region(int top, int bottom)
        {
            if (top < 1)
                top = 1;
            if (bottom > telnet.Controller.RowCount)
                bottom = telnet.Controller.RowCount;
            if (top <= bottom && (top > 1 || bottom < telnet.Controller.RowCount))
            {
                scroll_top = top;
                scroll_bottom = bottom;
                telnet.Controller.SetCursorAddress(0);
            }
            else
            {
                scroll_top = 1;
                scroll_bottom = telnet.Controller.RowCount;
            }
            return AnsiState.DATA;
        }

        AnsiState xterm_text_mode(int ig1, int ig2)
        {
            nx = 0;
            n[0] = 0;
            return AnsiState.TEXT;
        }

        AnsiState xterm_text_semicolon(int ig1, int ig2)
        {
            tx = 0;
            return AnsiState.TEXT2;
        }

        AnsiState xterm_text(int ig1, int ig2)
        {
            if (tx < NT)
            {
                text += ansi_ch;
                tx++;
            }
            return state;
        }

        AnsiState xterm_text_do(int ig1, int ig2)
        {
            return AnsiState.DATA;
        }

        AnsiState ansi_htab_set(int ig1, int ig2)
        {
            int col = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;

            tabs[col / 8] = (byte)(tabs[col / 8] | 1 << (col % 8));
            return AnsiState.DATA;
        }

        AnsiState ansi_htab_clear(int nn, int ig2)
        {
            int col, i;

            switch (nn)
            {
                case 0:
                    col = telnet.Controller.CursorAddress % telnet.Controller.ColumnCount;
                    tabs[col / 8] = (byte)(tabs[col / 8] & ~(1 << (col % 8)));
                    break;
                case 3:
                    for (i = 0; i < (telnet.Controller.ColumnCount + 7) / 8; i++)
                        tabs[i] = 0;
                    break;
            }
            return AnsiState.DATA;
        }

        /*
         * Scroll the screen or the scrolling region.
         */
        void ansi_scroll()
        {
            held_wrap = false;

            /* Save the top line */
            if (scroll_top == 1 && scroll_bottom == telnet.Controller.RowCount)
            {
                telnet.Controller.ScrollOne();
                return;
            }

            /* Scroll all but the last line up */
            if (scroll_bottom > scroll_top)
                telnet.Controller.CopyBlock(scroll_top * telnet.Controller.ColumnCount,
                    (scroll_top - 1) * telnet.Controller.ColumnCount,
                    (scroll_bottom - scroll_top) * telnet.Controller.ColumnCount,
                    true);

            /* Clear the last line */
            telnet.Controller.EraseRegion((scroll_bottom - 1) * telnet.Controller.ColumnCount, telnet.Controller.ColumnCount, true);
        }

        /* Callback for when we enter ANSI mode. */
        public void ansi_in3270(bool in3270)
        {
            if (!in3270)
                ansi_reset(0, 0);
        }
        /*
         * External entry points
         */
        public void ansi_init()
        {
			this.telnet.Connected3270 += telnet_Connected3270;
        }

		void telnet_Connected3270(object sender, Connected3270EventArgs e)
		{
			this.ansi_in3270(e.Is3270);
		}


        public void ansi_process(byte c)
        {
            c &= 0xff;
            ansi_ch = (char)c;


            if (telnet.Appres.Toggled(Appres.ScreenTrace))
            {
                telnet.Trace.trace_char((char)c);
            }

            object s = st[(int)state];
            byte[] bs = (byte[])s;
            int fnindex = bs[c];
            AnsiDelegate fn = ansi_fn[fnindex];
            state = fn(n[0], n[1]);
        }

        public void ansi_send_up()
        {
            if (appl_cursor != 0)
                telnet.SendString("\033OA");
            else
                telnet.SendString("\033[A");
        }

        public void ansi_send_down()
        {
            if (appl_cursor != 0)
                telnet.SendString("\033OB");
            else
                telnet.SendString("\033[B");
        }

        public void ansi_send_right()
        {
            if (appl_cursor != 0)
                telnet.SendString("\033OC");
            else
                telnet.SendString("\033[C");
        }

        public void ansi_send_left()
        {
            if (appl_cursor != 0)
                telnet.SendString("\033OD");
            else
                telnet.SendString("\033[D");
        }

        public void ansi_send_home()
        {
            telnet.SendString("\033[H");
        }

        public void ansi_send_clear()
        {
            telnet.SendString("\033[2K");
        }

        public void ansi_send_pf(int nn)
        {
            throw new ApplicationException("ansi_send_pf not implemented");
        }

        public void ansi_send_pa(int nn)
        {
            throw new ApplicationException("ansi_send_pa not implemented");
        }

		public void Dispose()
		{
			this.telnet.Connected3270 -= telnet_Connected3270;
		}
	}
}
