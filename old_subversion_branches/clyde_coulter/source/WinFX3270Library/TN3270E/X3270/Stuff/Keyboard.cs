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
using System.Text;
using System.Collections;
using System.Threading;

namespace Open3270.TN3270
{
	/// <summary>
	/// Summary description for Keyboard.
	/// </summary>
	internal class Keyboard
	{
		Telnet telnet;
		TNTrace trace;
		Actions action;
		internal Keyboard(Telnet telnet)
		{
			this.action = telnet.action;
			this.telnet = telnet;
			this.trace = telnet.trace;
			PF_SZ = pf_xlate.Length;
			PA_SZ = pa_xlate.Length;

		}
		public const int KL_OERR_MASK		=0x000f;
		public const int KL_OERR_PROTECTED	=1;
		public const int KL_OERR_NUMERIC	=2;
		public const int KL_OERR_OVERFLOW	=3;
		public const int KL_NOT_CONNECTED	=0x0010;
		public const int KL_AWAITING_FIRST	=0x0020;
		public const int KL_OIA_TWAIT		=0x0040;
		public const int KL_OIA_LOCKED		=0x0080;
		public const int KL_DEFERRED_UNLOCK	=0x0100;
		public const int KL_ENTER_INHIBIT	=0x0200;
		public const int KL_SCROLLED		=0x0400;
		public const int KL_OIA_MINUS		=0x0800;

		public int kybdlock = Keyboard.KL_NOT_CONNECTED;
		public bool insert = false;		/* insert mode */
		public bool reverse = false;	/* reverse-input mode */

		
		const int NoSymbol =0;
		bool flipped = false;

		public enum enum_keytype { KT_STD, KT_GE };
		public enum enum_composing { NONE, COMPOSE, FIRST };
		internal class akeysym 
		{
			public byte keysym;
			public enum_keytype keytype;
		};
#if N_COMPOSITES
		internal class composite 
		{
			public akeysym k1, k2;
			public  akeysym translation;
		};
		composite[] composites = null;
		int n_composites = 0;
#endif
		//akeysym cc_first = null;




		enum_composing composing = enum_composing.NONE;


		/* Statics */
		private byte[] pf_xlate = new byte[] { 
												 AID.AID_PF1,  AID.AID_PF2,  AID.AID_PF3,  AID.AID_PF4,  AID.AID_PF5,  AID.AID_PF6,
												 AID.AID_PF7,  AID.AID_PF8,  AID.AID_PF9,  AID.AID_PF10, AID.AID_PF11, AID.AID_PF12,
												 AID.AID_PF13, AID.AID_PF14, AID.AID_PF15, AID.AID_PF16, AID.AID_PF17, AID.AID_PF18,
												 AID.AID_PF19, AID.AID_PF20, AID.AID_PF21, AID.AID_PF22, AID.AID_PF23, AID.AID_PF24
											 };
		private byte[] pa_xlate = new byte[]  { 
												  AID.AID_PA1, AID.AID_PA2, AID.AID_PA3
											  };
		int PF_SZ;// = pf_xlate.Length;
		int PA_SZ;// = pa_xlate.Length;

		const int UNLOCK_MS	=350;




/* Composite key mappings. */



		public bool ak_eq(akeysym k1, akeysym k2)
		{
			return ((k1.keysym  == k2.keysym) && (k1.keytype == k2.keytype));
		}


        byte FROM_HEX(char c)
		{
			const string dx1 = "0123456789abcdef";
			const string dx2 = "0123456789ABCDEF";

			int index = dx1.IndexOf(c);
			if (index==-1)
				index = dx2.IndexOf(c);
			if (index==-1)
				throw new ApplicationException("sorry, '"+c+"' isn't a valid hex digit");
			return (byte)index;
			
		}

		bool isxdigit(char ch)
		{
			string ok = "0123456789ABCDEFabcdef";
			if (ok.IndexOf((char)ch)!=-1)
				return true;
			else
				return false;
		}
		bool isdigit(char ch)
		{
			if (ch>='0' && ch<='9')
				return true;
			else
				return false;


		}
		

//MFW--extern Widget *screen;



		System.Collections.Queue ta_queue = new Queue();
		internal class TAItem
		{
			public ActionDelegate fn;
			public object[] args;
			public TAItem(ActionDelegate fn, object[] args)
			{
				this.fn = fn;
				this.args = args;
			}
		}
		/*
		 * Put an action on the typeahead queue.
		 */
		void enq_ta(ActionDelegate fn, params object[] args)
		{
			//struct ta *ta;

			/* If no connection, forget it. */
			if (!telnet.CONNECTED) 
			{
				telnet.trace.trace_event("  dropped (not connected)\n");
				return;
			}

			/* If operator error, complain and drop it. */
			if ((kybdlock & KL_OERR_MASK)!=0) 
			{
				//ring_bell();
				telnet.trace.trace_event("  dropped (operator error)\n");
				return;
			}

			/* If scroll lock, complain and drop it. */
			if ((kybdlock & KL_SCROLLED) !=0)
			{
				//ring_bell();
				telnet.trace.trace_event("  dropped (scrolled)\n");
				return;
			}

			/* If typeahead disabled, complain and drop it. */
			if (!telnet.appres.typeahead) 
			{
				telnet.trace.trace_event("  dropped (no typeahead)\n");
				return;
			}

			ta_queue.Enqueue(new TAItem(fn, args));
			//	status_typeahead(true);

			telnet.trace.trace_event("  action queued (kybdlock 0x"+kybdlock+")\n");
		}

		/*
		 * Execute an action from the typeahead queue.
		 */
		public bool run_ta()
		{

			if (kybdlock!=0 || ta_queue.Count==0)
				return false;
	
			TAItem item = (TAItem)ta_queue.Dequeue();

			if (ta_queue.Count==0)
			{
				//		status_typeahead(false);
			}
			item.fn(item.args);

			return true;
		}

		/*
		 * Flush the typeahead queue.
		 * Returns whether or not anything was flushed.
		 */
		bool flush_ta()
		{
			bool any = false;
			if (ta_queue.Count>0)
				any = true;
			ta_queue.Clear();
			//			status_typeahead(false);
			return any;
		}

		private void ps_set(string text, bool is_hex)
		{
			// move forwards to first non protected
			// hack for mfw/FDI USA
			bool skiptounprotected = telnet.Config.AlwaysSkipToUnprotected;

			int baddr = telnet.tnctlr.cursor_addr;
			if (skiptounprotected)
			{
				//
				// Move cursor forwards to next unprotected field
				//
			
				bool ok = true;
				int fa;
				do
				{
					ok = true;
					fa = telnet.tnctlr.get_field_attribute(baddr);
					if (fa==-1)
						break;
					if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]) || (fa>=0 && telnet.tnctlr.FA_IS_PROTECTED(telnet.tnctlr.screen_buf[fa]))) 
					{
						ok = false;
						telnet.tnctlr.INC_BA(ref baddr);
						if (baddr == telnet.tnctlr.cursor_addr)
						{
							Console.WriteLine("**BUGBUG** Screen has no unprotected field!");
							return;
						}
					}
				}
				while (!ok);
				if (baddr != telnet.tnctlr.cursor_addr)
				{
					Console.WriteLine("Moved cursor to "+baddr+" to skip protected fields");
					telnet.tnctlr.cursor_move(baddr);
					Console.WriteLine("cursor position "+telnet.tnctlr.BA_TO_COL(baddr)+", "+telnet.tnctlr.BA_TO_ROW(baddr));
					Console.WriteLine("text : "+text);
				}
			}
			//
			//push_string(text, false, is_hex);
			emulate_input(text, false);
			
			//throw new ApplicationException("oops");
		}

		/* Set bits in the keyboard lock. */
		public void kybdlock_set(int bits, string cause)
		{
			int n;

			n = kybdlock | bits;
			if (n != kybdlock) 
			{
				kybdlock = n;
			}
			//Console.WriteLine("kybdlock_set "+bits+" "+cause);
		}

		/* Clear bits in the keyboard lock. */
		public void kybdlock_clr(int bits, string debug)
		{
			int n;
			//Console.WriteLine("kybdlock_clr "+bits+" "+debug);
			if (bits==-1)
				bits = 0xFFFF;

			n = kybdlock & ~bits;
			if (n != kybdlock) 
			{
				kybdlock = n;
			}
		}


		/*
		 * Set or clear enter-inhibit mode.
		 */
		public void kybd_inhibit(bool inhibit)
		{
			if (inhibit) 
			{
				kybdlock_set(KL_ENTER_INHIBIT, "kybd_inhibit");
			} 
			else 
			{
				kybdlock_clr(KL_ENTER_INHIBIT, "kybd_inhibit");
			}
		}
		/*
		 * Called when a host connects or disconnects.
		 */
		System.Threading.Timer unlock_id = null;
		public void kybd_connect(bool connected)
		{
			if ((kybdlock & KL_DEFERRED_UNLOCK)!=0)
			{
				telnet.tnctlr.RemoveTimeOut(unlock_id);
			}
			kybdlock_clr(-1, "kybd_connect");

			if (connected) 
			{
		
				/* Wait for any output or a WCC(restore) from the host */
				kybdlock_set(KL_AWAITING_FIRST, "kybd_connect");
			} 
			else 
			{
				kybdlock_set(KL_NOT_CONNECTED, "kybd_connect");
				flush_ta();
			}
		}

		/*
		 * Called when we switch between 3270 and ANSI modes.
		 */
		public void kybd_in3270(bool in3270)
		{
			if ((kybdlock & KL_DEFERRED_UNLOCK)!=0)
				telnet.tnctlr.RemoveTimeOut(unlock_id);
			kybdlock_clr(-1, "kybd_connect");
		}

		/*
		 * Called to initialize the keyboard logic.
		 */
		public void kybd_init()

		{
			/* interest in connect and disconnect events. */
			telnet.register_schange(STCALLBACK.ST_CONNECT, new SChangeDelegate(kybd_connect));
			telnet.register_schange(STCALLBACK.ST_3270_MODE, new SChangeDelegate(kybd_in3270));
		}

		/*
		 * Toggle insert mode.
		 */
		public void insert_mode(bool on)
		{
			insert = on;
			//	status_insert_mode(on);
		}

		/*
		 * Toggle reverse mode.
		 */
		public void reverse_mode(bool on)
		{
			reverse = on;
			//	status_reverse_mode(on);
		}

		/*
		 * Lock the keyboard because of an operator error.
		 */
		public void operator_error(int baddr, int error_type)
		{
			Console.WriteLine("cursor@"+baddr+" - ROW="+telnet.tnctlr.BA_TO_ROW(baddr)+" COL="+telnet.tnctlr.BA_TO_COL(baddr));
			telnet.events.popup_an_error("Keyboard locked");
			Console.WriteLine("WARNING--operator_error error_type="+error_type);
			//
			//flush_ta();
			//
			//if (sms_redirect())
			//	telnet.events.popup_an_error("Keyboard locked");
			if (telnet.Config.LockScreenOnWriteToUnprotected)//appres.oerr_lock)// || sms_redirect()) 
			{
				//		status_oerr(error_type);
				//		mcursor_locked();
				kybdlock_set(error_type, "operator_error");
				flush_ta();
			} 
			else 
			{
				//ring_bell();
			}
		}


		/*
		 * Handle an AID (Attention IDentifier) key.  This is the common stuff that
		 * gets executed for all AID keys (PFs, PAs, Clear and etc).
		 */
		public void key_AID(byte AID_code)
		{
			if (telnet.IN_ANSI) 
			{
				int i;

				if (AID_code == AID.AID_ENTER) 
				{
					telnet.net_sendc('\r');
					return;
				}
				for (i = 0; i < PF_SZ; i++)
				{
					if (AID_code == pf_xlate[i]) 
					{
						telnet.ansi.ansi_send_pf(i+1);
						return;
					}
				}
				for (i = 0; i < PA_SZ; i++)
				{
					if (AID_code == pa_xlate[i]) 
					{
						telnet.ansi.ansi_send_pa(i+1);
						return;
					}
				}
				return;
			}
			if (telnet.IN_SSCP) 
			{
				if ((kybdlock & Keyboard.KL_OIA_MINUS)!=0)
					return;
				if (AID_code != AID.AID_ENTER && AID_code != AID.AID_CLEAR) 
				{
					//			status_minus();
					kybdlock_set(Keyboard.KL_OIA_MINUS, "key_AID");
					return;
				}
			}
			if (telnet.IN_SSCP && AID_code == AID.AID_ENTER) 
			{
				/* Act as if the host had written our input. */
				telnet.tnctlr.buffer_addr = telnet.tnctlr.cursor_addr;
			}
			if (!telnet.IN_SSCP || AID_code != AID.AID_CLEAR) 
			{
				//		status_twait();
				//		mcursor_waiting();
				insert_mode(false);
				//Console.WriteLine("**BUGBUG** KL_OIA_LOCKED REMOVED");
				kybdlock_set(Keyboard.KL_OIA_TWAIT | Keyboard.KL_OIA_LOCKED, "key_AID");
			}
			//
			//Console.WriteLine("BUGBUG - reset_idle_timer");
		    telnet.idle.reset_idle_timer();
			telnet.tnctlr.aid = AID_code;
			telnet.tnctlr.ctlr_read_modified(telnet.tnctlr.aid, false);
			//Console.WriteLine("ticking-start...");
			//telnet.tnctlr.ticking_start(false);
			//if (!telnet.IN_SSCP) 
			//{
			//	status_ctlr_done();
			//	}
		}

		public bool PF_action(params object[] args)
		{
			int k;

			k = (int)args[0];
			if (k < 1 || k > PF_SZ) 
			{
				telnet.events.popup_an_error("PF_action: Invalid argument '"+args[0]+"'");
				return false;
			}
			if ((kybdlock & KL_OIA_MINUS)!=0)
				return false;
			else if (kybdlock!=0)
				enq_ta(new ActionDelegate(PF_action), args);
			else
				key_AID(pf_xlate[k-1]);
			return true;
		}

		public bool PA_action(params object[] args)
		{
			int k;

			k = (int)args[0];
			if (k < 1 || k > PA_SZ) 
			{
				telnet.events.popup_an_error("PA_action: Invalid argument '"+args[0]+"'");
				return false;
			}
			if ((kybdlock & KL_OIA_MINUS)!=0)
				return false;
			else if (kybdlock!=0)
				enq_ta(new ActionDelegate(PA_action), args);
			else
				key_AID(pa_xlate[k-1]);
			return true;
		}


		/*
		 * ATTN key, per RFC 2355.  Sends IP, regardless.
		 */
		public bool Attn_action(params object[] args)
		{
			if (!telnet.IN_3270)
				return false;
			telnet.net_interrupt();
			return true;
		}

		/*
		 * IAC IP, which works for 5250 System Request and interrupts the program
		 * on an AS/400, even when the keyboard is locked.
		 *
		 * This is now the same as the Attn action.
		 */
		public bool Interrupt_action(params object[] args)
		{
			if (!telnet.IN_3270)
				return false;
			telnet.net_interrupt();
			return true;
		}

		public const int GE_WFLAG	=0x100;
		public const int PASTE_WFLAG	=0x200;

		bool  key_Character_wrapper(int cgcode)
		{
			bool with_ge = false;
			bool  pasting = false;

			if ((cgcode & GE_WFLAG)!=0) 
			{
				with_ge = true;
				cgcode &= ~GE_WFLAG;
			}
			if ((cgcode & PASTE_WFLAG)!=0)
			{
				pasting = true;
				cgcode &= ~PASTE_WFLAG;
			}
			telnet.trace.trace_event(" %s -> Key(%s\"%s\")\n",
				"nop",/*ia_name[(int) ia_cause],*/
				with_ge ? "GE " : "",
				Util.ctl_see((byte) Tables.cg2asc[cgcode]));
			return key_Character(cgcode, with_ge, pasting);
		}

		/*
		 * Handle an ordinary displayable character key.  Lots of stuff to handle
		 * insert-mode, protected fields and etc.
		 */
		public bool key_Character(int cgcode, bool with_ge, bool pasting)
		{
			int	baddr, end_baddr;
			int fa;
			bool no_room = false;

			if (kybdlock!=0) 
			{
				Console.WriteLine("--bugbug--should enq_ta key, but since keylock is !=0, dropping it instead (not implemented properly)");
				return false;
				/*
				char code[64];

				(void) sprintf(code, "%d", cgcode |
					(with_ge ? GE_WFLAG : 0) |
					(pasting ? PASTE_WFLAG : 0));
				enq_ta(new ActionDelegate(key_Character_wrapper, code, CN);
				return false;*/
			}
			baddr = telnet.tnctlr.cursor_addr;
			fa = telnet.tnctlr.get_field_attribute(baddr);
			byte favalue = telnet.tnctlr.fake_fa;
			if (fa != -1)
				favalue = telnet.tnctlr.screen_buf[fa];
			if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]) || telnet.tnctlr.FA_IS_PROTECTED(favalue)) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}
			if (telnet.appres.numeric_lock && telnet.tnctlr.FA_IS_NUMERIC(favalue) &&
				!((cgcode >= CG.CG_0 && cgcode <= CG.CG_9) ||
				cgcode == CG.CG_minus || cgcode == CG.CG_period)) 
			{
				operator_error(baddr, KL_OERR_NUMERIC);
				return false;
			}
			if (reverse || (insert && telnet.tnctlr.screen_buf[baddr]!=0)) 
			{
				int last_blank = -1;

				/* Find next null, next fa, or last blank */
				end_baddr = baddr;
				if (telnet.tnctlr.screen_buf[end_baddr] == CG.CG_space)
					last_blank = end_baddr;
				do 
				{
					telnet.tnctlr.INC_BA(ref end_baddr);
					if (telnet.tnctlr.screen_buf[end_baddr] == CG.CG_space)
						last_blank = end_baddr;
					if (telnet.tnctlr.screen_buf[end_baddr] == CG.CG_null
						||  telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[end_baddr]))
						break;
				} while (end_baddr != baddr);

				/* Pretend a trailing blank is a null, if desired. */
				if (telnet.appres.toggled(Appres.BLANK_FILL) && last_blank != -1) 
				{
					telnet.tnctlr.INC_BA(ref last_blank);
					if (last_blank == end_baddr) 
					{
						telnet.tnctlr.DEC_BA(ref end_baddr);
						telnet.tnctlr.ctlr_add(end_baddr, CG.CG_null, 0);
					}
				}

				/* Check for field overflow. */
				if (telnet.tnctlr.screen_buf[end_baddr] != CG.CG_null) 
				{
					if (insert) 
					{
						operator_error(end_baddr, KL_OERR_OVERFLOW);
						return false;
					} 
					else 
					{	/* reverse */
						no_room = true;
					}
				} 
				else 
				{
					/* Shift data over. */
					if (end_baddr > baddr) 
					{
						/* At least one byte to copy, no wrap. */
						telnet.tnctlr.ctlr_bcopy(baddr, baddr+1, end_baddr - baddr,
							false);
					}
					else if (end_baddr < baddr) 
					{
						/* At least one byte to copy, wraps to top. */
						telnet.tnctlr.ctlr_bcopy(0, 1, end_baddr, false);
						telnet.tnctlr.ctlr_add(0, telnet.tnctlr.screen_buf[(telnet.tnctlr.ROWS * telnet.tnctlr.COLS) - 1], 0);
						telnet.tnctlr.ctlr_bcopy(baddr, baddr+1,
							((telnet.tnctlr.ROWS * telnet.tnctlr.COLS) - 1) - baddr, false);
					}
				}

			}

			/* Replace leading nulls with blanks, if desired. */
			if (telnet.tnctlr.formatted && telnet.appres.toggled(Appres.BLANK_FILL)) 
			{
				int		baddr_sof = fa;//fa - telnet.tnctlr.screen_buf;
				int baddr_fill = baddr;

				telnet.tnctlr.DEC_BA(ref baddr_fill);
				while (baddr_fill != baddr_sof) 
				{

					/* Check for backward line wrap. */
					if ((baddr_fill % telnet.tnctlr.COLS) == telnet.tnctlr.COLS - 1) 
					{
						bool aborted = true;
						int baddr_scan = baddr_fill;

						/*
						 * Check the field within the preceeding line
						 * for NULLs.
						 */
						while (baddr_scan != baddr_sof) 
						{
							if (telnet.tnctlr.screen_buf[baddr_scan] != CG.CG_null) 
							{
								aborted = false;
								break;
							}
							if (0==(baddr_scan % telnet.tnctlr.COLS))
								break;
							telnet.tnctlr.DEC_BA(ref baddr_scan);
						}
						if (aborted)
							break;
					}

					if (telnet.tnctlr.screen_buf[baddr_fill] == CG.CG_null)
						telnet.tnctlr.ctlr_add(baddr_fill, CG.CG_space, 0);
					telnet.tnctlr.DEC_BA(ref baddr_fill);
				}
			}

			/* Add the character. */
			if (no_room) 
			{
				do 
				{
					telnet.tnctlr.INC_BA(ref baddr);
				} while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]));
			} 
			else 
			{
				telnet.tnctlr.ctlr_add(baddr, (byte)cgcode, (byte)(with_ge ? ExtendedAttribute.CS_GE : (byte)0));
				telnet.tnctlr.ctlr_add_fg(baddr, 0);
				telnet.tnctlr.ctlr_add_gr(baddr, 0);
				if (!reverse)
					telnet.tnctlr.INC_BA(ref baddr);
			}

			/*
			 * Implement auto-skip, and don't land on attribute bytes.
			 * This happens for all pasted data (even DUP), and for all
			 * keyboard-generated data except DUP.
			 */
			if (pasting || (cgcode != CG.CG_dup)) 
			{
				while (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
				{
					if (telnet.tnctlr.FA_IS_SKIP(telnet.tnctlr.screen_buf[baddr]))
						baddr = telnet.tnctlr.next_unprotected(baddr);
					else
					{
						telnet.tnctlr.INC_BA(ref baddr);
					}
				}
				telnet.tnctlr.cursor_move(baddr);
			}

			telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa);
			return true;
		}

		/*
		 * Handle an ordinary character key, given an ASCII code.
		 */
		void key_ACharacter(byte c, enum_keytype keytype, iaction cause)
		{
			//int i;
			akeysym ak = new akeysym();

			ak.keysym = c;
			ak.keytype = keytype;

			switch (composing) 
			{
				case enum_composing.NONE:
					break;
				case enum_composing.COMPOSE:
#if N_COMPOSITES
					for (i = 0; i < n_composites; i++)
						if (ak_eq(composites[i].k1, ak) ||
							ak_eq(composites[i].k2, ak))
							break;
					if (i < n_composites) 
					{
						cc_first.keysym = c;
						cc_first.keytype = keytype;
						composing = enum_composing.FIRST;
//						status_compose(true, c, keytype);
					} 
					else 
#endif
				   {
						//ring_bell();
						composing = enum_composing.NONE;
//						status_compose(false, 0, enum_keytype.KT_STD);
					}
					return;
				case enum_composing.FIRST:
					composing = enum_composing.NONE;
//					status_compose(false, 0, enum_keytype.KT_STD);
#if N_COMPOSITES
					for (i = 0; i < n_composites; i++)
					{
						if ((ak_eq(composites[i].k1, cc_first) &&
							ak_eq(composites[i].k2, ak)) ||
							(ak_eq(composites[i].k1, ak) &&
							ak_eq(composites[i].k2, cc_first)))
							break;
					}
					if (i < n_composites) 
					{
						c = composites[i].translation.keysym;
						keytype = composites[i].translation.keytype;
					} 
					else 
#endif
					{
						//			ring_bell();
						return;
					}
					//break;
			}

			trace.trace_event(" %s -> Key(\"%s\")\n", telnet.action.ia_name[(int) cause], Util.ctl_see(c));
			if (telnet.IN_3270) 
			{
				if (c < ' ') 
				{
					trace.trace_event("  dropped (control char)\n");
					return;
				}
				key_Character(Tables.asc2cg[c], keytype == enum_keytype.KT_GE, false);
			}
			else if (telnet.IN_ANSI) 
			{
				telnet.net_sendc((char) c);
			}
			else 
			{
				trace.trace_event("  dropped (not connected)\n");
			}
		}


		/*
		 * Simple toggles.
		 */

		public bool MonoCase_action(params object[] args)
		{
			telnet.appres.do_toggle(Appres.MONOCASE);
			return true;
		}

		/*
		 * Flip the display left-to-right
		 */
		public bool Flip_action(params object[] args)
		{
//			screen_flip();
			return true;
		}



		/*
		 * Tab forward to next field.
		 */
		public bool Tab_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Tab_action),args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_sendc('\t');
				return true;
			}
			telnet.tnctlr.cursor_move(telnet.tnctlr.next_unprotected(telnet.tnctlr.cursor_addr));
			return true;
		}


		/*
		 * Tab backward to previous field.
		 */
		public bool BackTab_action(params object[] args)
		{
			int	baddr, nbaddr;
			int		sbaddr;

			if (!telnet.IN_3270)
				return false;
	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(BackTab_action), args);
				return true;
			}
			baddr = telnet.tnctlr.cursor_addr;
			telnet.tnctlr.DEC_BA(ref baddr);
			if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]))	/* at bof */
				telnet.tnctlr.DEC_BA(ref baddr);
			sbaddr = baddr;
			while (true) 
			{
				nbaddr = baddr;
				telnet.tnctlr.INC_BA(ref nbaddr);
				if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])
					&&  !telnet.tnctlr.FA_IS_PROTECTED(telnet.tnctlr.screen_buf[baddr])
					&&  !telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[nbaddr]))
					break;
				telnet.tnctlr.DEC_BA(ref baddr);
				if (baddr == sbaddr) 
				{
					telnet.tnctlr.cursor_move(0);
					return true;
				}
			}
			telnet.tnctlr.INC_BA(ref baddr);
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}


		/*
		 * Deferred keyboard unlock.
		 */

		void defer_unlock(object state)
		{
			lock (telnet)
			{

				//
				// Only actually process the event if the keyboard is currently unlocked...
				//
				if ((telnet.keyboard.kybdlock | Keyboard.KL_DEFERRED_UNLOCK)==Keyboard.KL_DEFERRED_UNLOCK)
				{

					telnet.trace.WriteLine("--debug--defer_unlock");
					kybdlock_clr(KL_DEFERRED_UNLOCK, "defer_unlock");
					//status_reset();
					if (telnet.CONNECTED)
						telnet.tnctlr.ps_process();
				}
				else
					telnet.trace.WriteLine("--debug--defer_unlock ignored");

			}
		}
		/*
		 * Reset keyboard lock.
		 */
		public void do_reset(bool explicitvalue)
		{
			//Console.WriteLine("do_reset "+explicitvalue);
			/*
			 * If explicit (from the keyboard) and there is typeahead or
			 * a half-composed key, simply flush it.
			 */
			if (explicitvalue)
			{
				bool half_reset = false;

				if (flush_ta())
					half_reset = true;
				if (composing != enum_composing.NONE) 
				{
					composing = enum_composing.NONE;
					//			status_compose(false, 0, KT_STD);
					half_reset = true;
				}
				if (half_reset)
					return;
			}


			/* Always clear insert mode. */
			insert_mode(false);

			/* Otherwise, if not connect, reset is a no-op. */
			if (!telnet.CONNECTED)
				return;

			/*
			 * Remove any deferred keyboard unlock.  We will either unlock the
			 * keyboard now, or want to defer further into the future.
			 */
			if ((kybdlock & Keyboard.KL_DEFERRED_UNLOCK)!=0)
				telnet.tnctlr.RemoveTimeOut(unlock_id);

			/*
			 * If explicit (from the keyboard), unlock the keyboard now.
			 * Otherwise (from the host), schedule a deferred keyboard unlock.
			 */
			if (explicitvalue) 
			{
				kybdlock_clr(-1, "do_reset");
			} 
			else if ((kybdlock & (KL_DEFERRED_UNLOCK | KL_OIA_TWAIT | KL_OIA_LOCKED | KL_AWAITING_FIRST))!=0) 
			{
				telnet.trace.WriteLine("Clear lock in 1010/55");
				kybdlock_clr(~KL_DEFERRED_UNLOCK, "do_reset");
				kybdlock_set(KL_DEFERRED_UNLOCK, "do_reset");
				lock (telnet)
				{
					unlock_id = telnet.tnctlr.AddTimeout(UNLOCK_MS, new TimerCallback(defer_unlock));
				}
			}

			/* Clean up other modes. */
			//status_reset();
			//mcursor_normal();
			composing = enum_composing.NONE;
			//status_compose(false, 0, KT_STD);
		}
		public bool Reset_action(params object[] args)
		{
			do_reset(true);
			return true;
		}


		/*
		 * Move to first unprotected field on screen.
		 */
		public bool Home_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Home_action),args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.ansi.ansi_send_home();
				return true;
			}
			if (!telnet.tnctlr.formatted) 
			{
				telnet.tnctlr.cursor_move(0);
				return true;
			}
			telnet.tnctlr.cursor_move(telnet.tnctlr.next_unprotected(telnet.tnctlr.ROWS*telnet.tnctlr.COLS-1));
			return true;
		}


		/*
		 * Cursor left 1 position.
		 */
		void do_left()
		{
			int	baddr;

			baddr = telnet.tnctlr.cursor_addr;
			telnet.tnctlr.DEC_BA(ref baddr);
			telnet.tnctlr.cursor_move(baddr);
		}

		public bool Left_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Left_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.ansi.ansi_send_left();
				return true;
			}
			if (!flipped)
				do_left();
			else 
			{
				int	baddr;

				baddr = telnet.tnctlr.cursor_addr;
				telnet.tnctlr.INC_BA(ref baddr);
				telnet.tnctlr.cursor_move(baddr);
			}
			return true;
		}


		/*
		 * Delete char key.
		 * Returns "true" if succeeds, "false" otherwise.
		 */
		bool do_delete()
		{
			int	baddr, end_baddr;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;

			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			if (telnet.tnctlr.FA_IS_PROTECTED(fa) || telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}
			/* find next fa */
			if (telnet.tnctlr.formatted) 
			{
				end_baddr = baddr;
				do 
				{
					telnet.tnctlr.INC_BA(ref end_baddr);
					if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[end_baddr]))
						break;
				} while (end_baddr != baddr);
				telnet.tnctlr.DEC_BA(ref end_baddr);
			} 
			else 
			{
				if ((baddr % telnet.tnctlr.COLS) == telnet.tnctlr.COLS - 1)
					return true;
				end_baddr = baddr + (telnet.tnctlr.COLS - (baddr % telnet.tnctlr.COLS)) - 1;
			}
			if (end_baddr > baddr) 
			{
				telnet.tnctlr.ctlr_bcopy(baddr+1, baddr, end_baddr - baddr, false);
			} 
			else if (end_baddr != baddr) 
			{
				telnet.tnctlr.ctlr_bcopy(baddr+1, baddr, ((telnet.tnctlr.ROWS * telnet.tnctlr.COLS) - 1) - baddr, false);
				telnet.tnctlr.ctlr_add((telnet.tnctlr.ROWS * telnet.tnctlr.COLS) - 1, telnet.tnctlr.screen_buf[0], 0);
				telnet.tnctlr.ctlr_bcopy(1, 0, end_baddr, false);
			}
			telnet.tnctlr.ctlr_add(end_baddr, CG.CG_null, 0);
			telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa_index);
			return true;
		}

		public bool Delete_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Delete_action),args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_sendc(0x7f);
				return true;
			}
			if (!do_delete())
				return false;
			if (reverse) 
			{
				int baddr = telnet.tnctlr.cursor_addr;

				telnet.tnctlr.DEC_BA(ref baddr);
				if (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]))
					telnet.tnctlr.cursor_move(baddr);
			}
			return true;
		}


		/*
		 * Backspace.
		 */
		public bool BackSpace_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(BackSpace_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_send_erase();
				return true;
			}
			if (reverse)
				do_delete();
			else if (!flipped)
				do_left();
			else 
			{
				int	baddr;

				baddr = telnet.tnctlr.cursor_addr;
				telnet.tnctlr.DEC_BA(ref baddr);
				telnet.tnctlr.cursor_move(baddr);
			}
			return true;
		}


		/*
		 * Destructive backspace, like Unix "erase".
		 */
		public bool Erase_action(params object[] args)
		{
			int	baddr;
			byte fa = telnet.tnctlr.fake_fa;
			int fa_index;
	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Erase_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_send_erase();
				return true;
			}
			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			if (fa_index == baddr || telnet.tnctlr.FA_IS_PROTECTED(fa)) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}
			if (baddr!=0 && fa_index == baddr - 1)
				return true;
			do_left();
			do_delete();
			return true;
		}


		/*
		 * Cursor right 1 position.
		 */
		public bool Right_action(params object[] args)
		{
			int	baddr;

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Right_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.ansi.ansi_send_right();
				return true;
			}
			if (!flipped) 
			{
				baddr = telnet.tnctlr.cursor_addr;
				telnet.tnctlr.INC_BA(ref baddr);
				telnet.tnctlr.cursor_move(baddr);
			} 
			else
				do_left();
			return true;
		}


		/*
		 * Cursor left 2 positions.
		 */
		public bool Left2_action(params object[] args)
		{
			int	baddr;

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Left2_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			baddr = telnet.tnctlr.cursor_addr;
			telnet.tnctlr.DEC_BA(ref baddr);
			telnet.tnctlr.DEC_BA(ref baddr);
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}


		/*
		 * Cursor to previous word.
		 */
		public bool PreviousWord_action(params object[] args)
		{
			int baddr;
			int baddr0;
			byte  c;
			bool prot;

	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(PreviousWord_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			if (!telnet.tnctlr.formatted)
				return false;

			baddr = telnet.tnctlr.cursor_addr;
			prot = telnet.tnctlr.FA_IS_PROTECTED_AT(baddr);

			/* Skip to before this word, if in one now. */
			if (!prot) 
			{
				c = telnet.tnctlr.screen_buf[baddr];
				while (!telnet.tnctlr.IS_FA(c) && c != CG.CG_space && c != CG.CG_null) 
				{
					telnet.tnctlr.DEC_BA(ref baddr);
					if (baddr == telnet.tnctlr.cursor_addr)
						return true;
					c = telnet.tnctlr.screen_buf[baddr];
				}
			}
			baddr0 = baddr;

			/* Find the end of the preceding word. */
			do 
			{
				c = telnet.tnctlr.screen_buf[baddr];
				if (telnet.tnctlr.IS_FA(c)) 
				{
					telnet.tnctlr.DEC_BA(ref baddr);
					prot = telnet.tnctlr.FA_IS_PROTECTED_AT(baddr);
					continue;
				}
				if (!prot && c != CG.CG_space && c != CG.CG_null)
					break;
				telnet.tnctlr.DEC_BA(ref baddr);
			} while (baddr != baddr0);

			if (baddr == baddr0)
				return true;

			/* Go it its front. */
			for (;;) 
			{
				telnet.tnctlr.DEC_BA(ref baddr);
				c = telnet.tnctlr.screen_buf[baddr];
				if (telnet.tnctlr.IS_FA(c) || c == CG.CG_space || c == CG.CG_null) 
				{
					break;
				}
			}
			telnet.tnctlr.INC_BA(ref baddr);
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}


		/*
		 * Cursor right 2 positions.
		 */
		public bool Right2_action(params object[] args)
		{
			int	baddr;

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Right2_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			baddr = telnet.tnctlr.cursor_addr;
			telnet.tnctlr.INC_BA(ref baddr);
			telnet.tnctlr.INC_BA(ref baddr);
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}


		/* Find the next unprotected word, or -1 */
		int nu_word(int baddr)
		{
			int baddr0 = baddr;
			byte c;
			bool prot;

			prot = telnet.tnctlr.FA_IS_PROTECTED_AT(baddr);

			do 
			{
				c = telnet.tnctlr.screen_buf[baddr];
				if (telnet.tnctlr.IS_FA(c))
					prot = telnet.tnctlr.FA_IS_PROTECTED(c);
				else if (!prot && c != CG.CG_space && c != CG.CG_null)
					return baddr;
				telnet.tnctlr.INC_BA(ref baddr);
			} while (baddr != baddr0);

			return -1;
		}

		/* Find the next word in this field, or -1 */
		int nt_word(int baddr)
		{
			int baddr0 = baddr;
			byte c;
			bool in_word = true;

			do 
			{
				c = telnet.tnctlr.screen_buf[baddr];
				if (telnet.tnctlr.IS_FA(c))
					return -1;
				if (in_word) 
				{
					if (c == CG.CG_space || c == CG.CG_null)
						in_word = false;
				} 
				else 
				{
					if (c != CG.CG_space && c != CG.CG_null)
						return baddr;
				}
				telnet.tnctlr.INC_BA(ref baddr);
			} while (baddr != baddr0);

			return -1;
		}


		/*
		 * Cursor to next unprotected word.
		 */
		public bool NextWord_action(params object[] args)
		{
			int	baddr;
			byte c;

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(NextWord_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			if (!telnet.tnctlr.formatted)
				return false;

			/* If not in an unprotected field, go to the next unprotected word. */
			if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[telnet.tnctlr.cursor_addr]) ||
				telnet.tnctlr.FA_IS_PROTECTED_AT(telnet.tnctlr.cursor_addr)) 
			{
				baddr = nu_word(telnet.tnctlr.cursor_addr);
				if (baddr != -1)
					telnet.tnctlr.cursor_move(baddr);
				return true;
			}

			/* If there's another word in this field, go to it. */
			baddr = nt_word(telnet.tnctlr.cursor_addr);
			if (baddr != -1) 
			{
				telnet.tnctlr.cursor_move(baddr);
				return true;
			}

			/* If in a word, go to just after its end. */
			c = telnet.tnctlr.screen_buf[telnet.tnctlr.cursor_addr];
			if (c != CG.CG_space && c != CG.CG_null) 
			{
				baddr = telnet.tnctlr.cursor_addr;
				do 
				{
					c = telnet.tnctlr.screen_buf[baddr];
					if (c == CG.CG_space || c == CG.CG_null) 
					{
						telnet.tnctlr.cursor_move(baddr);
						return true;
					} 
					else if (telnet.tnctlr.IS_FA(c)) 
					{
						baddr = nu_word(baddr);
						if (baddr != -1)
							telnet.tnctlr.cursor_move(baddr);
						return true;
					}
					telnet.tnctlr.INC_BA(ref baddr);
				} while (baddr != telnet.tnctlr.cursor_addr);
			}
				/* Otherwise, go to the next unprotected word. */
			else 
			{
				baddr = nu_word(telnet.tnctlr.cursor_addr);
				if (baddr != -1)
					telnet.tnctlr.cursor_move(baddr);
			}
			return true;
		}


		/*
		 * Cursor up 1 position.
		 */
		public bool Up_action(params object[] args)
		{
			int	baddr;

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Up_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.ansi.ansi_send_up();
				return true;
			}
			baddr = telnet.tnctlr.cursor_addr - telnet.tnctlr.COLS;
			if (baddr < 0)
				baddr = (telnet.tnctlr.cursor_addr + (telnet.tnctlr.ROWS * telnet.tnctlr.COLS)) - telnet.tnctlr.COLS;
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}


		/*
		 * Cursor down 1 position.
		 */
		public bool Down_action(params object[] args)
		{
			int	baddr;

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Down_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.ansi.ansi_send_down();
				return true;
			}
			baddr = (telnet.tnctlr.cursor_addr + telnet.tnctlr.COLS) % (telnet.tnctlr.COLS * telnet.tnctlr.ROWS);
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}


		/*
		 * Cursor to first field on next line or any lines after that.
		 */
		public bool Newline_action(params object[] args)
		{
			int	baddr;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
	

			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Newline_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_sendc('\n');
				return false;
			}
			baddr = (telnet.tnctlr.cursor_addr + telnet.tnctlr.COLS) % (telnet.tnctlr.COLS * telnet.tnctlr.ROWS);	/* down */
			baddr = (baddr / telnet.tnctlr.COLS) * telnet.tnctlr.COLS;			/* 1st col */
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			//
			if (fa_index != baddr && !telnet.tnctlr.FA_IS_PROTECTED(fa))
				telnet.tnctlr.cursor_move(baddr);
			else
				telnet.tnctlr.cursor_move(telnet.tnctlr.next_unprotected(baddr));
			return true;
		}


		/*
		 * DUP key
		 */
		public bool Dup_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Dup_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			if (key_Character(CG.CG_dup, false, false))
				telnet.tnctlr.cursor_move(telnet.tnctlr.next_unprotected(telnet.tnctlr.cursor_addr));
			return true;
		}


		/*
		 * FM key
		 */
		public bool FieldMark_action(params object[] args)
		{
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(FieldMark_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			key_Character(CG.CG_fm, false, false);
			return true;
		}


		/*
		 * Vanilla AID keys.
		 */
		public bool Enter_action(params object[] args)
		{
			if ((kybdlock & KL_OIA_MINUS)!=0)
				return false;
			else if (kybdlock!=0)
				enq_ta(new ActionDelegate(Enter_action), args);
			else
				key_AID(AID.AID_ENTER);
			return true;
		}


		public bool SysReq_action(params object[] args)
		{
			if (telnet.IN_ANSI)
				return false;
			if (telnet.IN_E) 
			{
				telnet.net_abort();
			} 
			else
			{
				if ((kybdlock & KL_OIA_MINUS)!=0)
					return false;
				else if (kybdlock!=0)
					enq_ta(new ActionDelegate(SysReq_action), args);
				else
					key_AID(AID.AID_SYSREQ);
			}
			return true;
		}


		/*
		 * Clear AID key
		 */
		public bool Clear_action(params object[] args)
		{
	
			if ((kybdlock & KL_OIA_MINUS)!=0)
			{
				return false;
			}
			if (kybdlock!=0 && telnet.CONNECTED) 
			{
				enq_ta(new ActionDelegate(Clear_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.ansi.ansi_send_clear();
				return true;
			}
			telnet.tnctlr.buffer_addr = 0;
			telnet.tnctlr.ctlr_clear(true);
			telnet.tnctlr.cursor_move(0);
			if (telnet.CONNECTED)
				key_AID(AID.AID_CLEAR);
			return true;
		}


		/*
		 * Cursor Select key (light pen simulator).
		 */
		void lightpen_select(int baddr)
		{
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
			byte sel;
			//, *sel;
			int designator;

			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			//
			if (!telnet.tnctlr.FA_IS_SELECTABLE(fa)) 
			{
				//ring_bell();
				return;
			}
			sel = telnet.tnctlr.screen_buf[fa_index+1];
			//sel = fa + 1;
			designator = fa_index+1;//sel - screen_buf;
			switch (sel) 
			{
				case CG.CG_greater:		/* > */
					telnet.tnctlr.ctlr_add(designator, CG.CG_question, 0); /* change to ? */
					telnet.tnctlr.mdt_clear(telnet.tnctlr.screen_buf, fa_index);
					break;
				case CG.CG_question:		/* ? */
					telnet.tnctlr.ctlr_add(designator, CG.CG_greater, 0);	/* change to > */
					telnet.tnctlr.mdt_clear(telnet.tnctlr.screen_buf, fa_index);
					break;
				case CG.CG_space:		/* space */
				case CG.CG_null:		/* null */
					key_AID(AID.AID_SELECT);
					break;
				case CG.CG_ampersand:		/* & */
					telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa_index);
					key_AID(AID.AID_ENTER);
					break;
				default:
					//ring_bell();
					break;
			}
			return;
		}

		/*
		 * Cursor Select key (light pen simulator) -- at the current cursor location.
		 */
		public bool CursorSelect_action(params object[] args)
		{
	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(CursorSelect_action), args);
				return true;
			}

			if (telnet.IN_ANSI)
				return false;
			lightpen_select(telnet.tnctlr.cursor_addr);
			return true;
		}



		/*
		 * Erase End Of Field Key.
		 */
		public bool EraseEOF_action(params object[] args)
		{
			int	baddr;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;

	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(EraseEOF_action), args);
				return false;
			}
			if (telnet.IN_ANSI)
				return false;
			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index!=-1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			if (telnet.tnctlr.FA_IS_PROTECTED(fa) || telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}
			if (telnet.tnctlr.formatted) 
			{	/* erase to next field attribute */
				do 
				{
					telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
					telnet.tnctlr.INC_BA(ref baddr);
				} while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]));
				telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa_index);
			} 
			else 
			{	/* erase to end of screen */
				do 
				{
					telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
					telnet.tnctlr.INC_BA(ref baddr);
				} while (baddr != 0);
			}
			return true;
		}


		/*
		 * Erase all Input Key.
		 */
		public bool EraseInput_action(params object[] args)
		{
			int	baddr, sbaddr;
			byte	fa = telnet.tnctlr.fake_fa;
//			int fa_index;
			Boolean		f;

	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(EraseInput_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			if (telnet.tnctlr.formatted) 
			{
				/* find first field attribute */
				baddr = 0;
				do 
				{
					if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]))
						break;
					telnet.tnctlr.INC_BA(ref baddr);
				} while (baddr != 0);
				sbaddr = baddr;
				f = false;
				do 
				{
					fa = telnet.tnctlr.screen_buf[baddr];
					if (!telnet.tnctlr.FA_IS_PROTECTED(fa)) 
					{
						telnet.tnctlr.mdt_clear(telnet.tnctlr.screen_buf, baddr);
						do 
						{
							telnet.tnctlr.INC_BA(ref baddr);
							if (!f) 
							{
								telnet.tnctlr.cursor_move(baddr);
								f = true;
							}
							if (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
							{
								telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
							}
						}		while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]));
					} 
					else 
					{	/* skip protected */
						do 
						{
							telnet.tnctlr.INC_BA(ref baddr);
						} while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]));
					}
				} while (baddr != sbaddr);
				if (!f)
					telnet.tnctlr.cursor_move(0);
			} 
			else 
			{
				telnet.tnctlr.ctlr_clear(true);
				telnet.tnctlr.cursor_move(0);
			}
			return true;
		}



		/*
		 * Delete word key.  Backspaces the cursor until it hits the front of a word,
		 * deletes characters until it hits a blank or null, and deletes all of these
		 * but the last.
		 *
		 * Which is to say, does a ^W.
		 */
		public bool DeleteWord_action(params object[] args)
		{
			int	baddr, baddr2, front_baddr, back_baddr, end_baddr;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
	

	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(DeleteWord_action), args);
				return false;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_send_werase();
				return true;
			}
			if (!telnet.tnctlr.formatted)
				return true;

			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];

			/* Make sure we're on a modifiable field. */
			if (telnet.tnctlr.FA_IS_PROTECTED(fa) || telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}

			/* Search backwards for a non-blank character. */
			front_baddr = baddr;
			while (telnet.tnctlr.screen_buf[front_baddr] == CG.CG_space ||
				telnet.tnctlr.screen_buf[front_baddr] == CG.CG_null)
				telnet.tnctlr.DEC_BA(ref front_baddr);

			/* If we ran into the edge of the field without seeing any non-blanks,
			   there isn't any word to delete; just move the cursor. */
			if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[front_baddr])) 
			{
				telnet.tnctlr.cursor_move(front_baddr+1);
				return true;
			}

			/* front_baddr is now pointing at a non-blank character.  Now search
			   for the first blank to the left of that (or the edge of the field),
			   leaving front_baddr pointing at the the beginning of the word. */
			while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[front_baddr]) &&
				telnet.tnctlr.screen_buf[front_baddr] != CG.CG_space &&
				telnet.tnctlr.screen_buf[front_baddr] != CG.CG_null)
				telnet.tnctlr.DEC_BA(ref front_baddr);
			telnet.tnctlr.INC_BA(ref front_baddr);

			/* Find the end of the word, searching forward for the edge of the
			   field or a non-blank. */
			back_baddr = front_baddr;
			while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[back_baddr]) &&
				telnet.tnctlr.screen_buf[back_baddr] != CG.CG_space &&
				telnet.tnctlr.screen_buf[back_baddr] != CG.CG_null)
				telnet.tnctlr.INC_BA(ref back_baddr);

			/* Find the start of the next word, leaving back_baddr pointing at it
			   or at the end of the field. */
			while (telnet.tnctlr.screen_buf[back_baddr] == CG.CG_space ||
				telnet.tnctlr.screen_buf[back_baddr] == CG.CG_null)
				telnet.tnctlr.INC_BA(ref back_baddr);

			/* Find the end of the field, leaving end_baddr pointing at the field
			   attribute of the start of the next field. */
			end_baddr = back_baddr;
			while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[end_baddr]))
				telnet.tnctlr.INC_BA(ref end_baddr);

			/* Copy any text to the right of the word we are deleting. */
			baddr = front_baddr;
			baddr2 = back_baddr;
			while (baddr2 != end_baddr) 
			{
				telnet.tnctlr.ctlr_add(baddr, telnet.tnctlr.screen_buf[baddr2], 0);
				telnet.tnctlr.INC_BA(ref baddr);
				telnet.tnctlr.INC_BA(ref baddr2);
			}

			/* Insert nulls to pad out the end of the field. */
			while (baddr != end_baddr) 
			{
				telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
				telnet.tnctlr.INC_BA(ref baddr);
			}

			/* Set the MDT and move the cursor. */
			telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa_index);
			telnet.tnctlr.cursor_move(front_baddr);
			return true;
		}



		/*
		 * Delete field key.  Similar to EraseEOF, but it wipes out the entire field
		 * rather than just to the right of the cursor, and it leaves the cursor at
		 * the front of the field.
		 *
		 * Which is to say, does a ^U.
		 */
		public bool DeleteField_action(params object[] args)
		{
			int	baddr;
			byte fa = telnet.tnctlr.fake_fa;
			int fa_index;

	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(DeleteField_action), args);
				return true;
			}
			if (telnet.IN_ANSI) 
			{
				telnet.net_send_kill();
				return true;
			}
			if (!telnet.tnctlr.formatted)
				return false;

			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			if (telnet.tnctlr.FA_IS_PROTECTED(fa) || telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}
			while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]))
				telnet.tnctlr.DEC_BA(ref baddr);
			telnet.tnctlr.INC_BA(ref baddr);
			telnet.tnctlr.cursor_move(baddr);
			while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
			{
				telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
				telnet.tnctlr.INC_BA(ref baddr);
			}
			telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa_index);
			return true;
		}



		/*
		 * Set insert mode key.
		 */
		public bool Insert_action(params object[] args)
		{
	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(Insert_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			insert_mode(true);
			return true;
		}


		/*
		 * Toggle insert mode key.
		 */
		public bool ToggleInsert_action(params object[] args)
		{
	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(ToggleInsert_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			if (insert)
				insert_mode(false);
			else
				insert_mode(true);
			return true;
		}


		/*
		 * Toggle reverse mode key.
		 */
		public bool ToggleReverse_action(params object[] args)
		{
	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(ToggleReverse_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			reverse_mode(!reverse);
			return true;
		}


		/*
		 * Move the cursor to the first blank after the last nonblank in the
		 * field, or if the field is full, to the last character in the field.
		 */
		public bool FieldEnd_action(params object[] args)
		{
			int	baddr;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
			byte c;
			int	last_nonblank = -1;

	
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(FieldEnd_action), args);
				return true;
			}
			if (telnet.IN_ANSI)
				return false;
			if (!telnet.tnctlr.formatted)
				return false;
			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index != -1)
				fa = telnet.tnctlr.screen_buf[fa_index];
			//
			if (fa_index == telnet.tnctlr.screen_buf[baddr] || telnet.tnctlr.FA_IS_PROTECTED(fa))
				return false;

			baddr = fa_index;
			while (true) 
			{
				telnet.tnctlr.INC_BA(ref baddr);
				c = telnet.tnctlr.screen_buf[baddr];
				if (telnet.tnctlr.IS_FA(c))
					break;
				if (c != CG.CG_null && c != CG.CG_space)
					last_nonblank = baddr;
			}

			if (last_nonblank == -1) 
			{
				baddr = fa_index;// - telnet.tnctlr.screen_buf;
				telnet.tnctlr.INC_BA(ref baddr);
			} 
			else 
			{
				baddr = last_nonblank;
				telnet.tnctlr.INC_BA(ref baddr);
				if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]))
					baddr = last_nonblank;
			}
			telnet.tnctlr.cursor_move(baddr);
			return true;
		}

		/*
		 * MoveCursor action.  Depending on arguments, this is either a move to the
		 * mouse cursor position, or to an absolute location.
		 */
		public bool MoveCursor_action(params object[] args)
		{
			int baddr;
			int row, col;

	
			if (kybdlock!=0) 
			{
				if (args.Length == 2)
					enq_ta(new ActionDelegate(MoveCursor_action), args);
				return true;
			}

			switch (args.Length) 
			{
				case 2:		/* probably a macro call */
					row = (int)args[0];
					col = (int)args[1];
					if (!telnet.IN_3270) 
					{
						row--;
						col--;
					}
					if (row < 0)
						row = 0;
					if (col < 0)
						col = 0;
					baddr = ((row * telnet.tnctlr.COLS) + col) % (telnet.tnctlr.ROWS * telnet.tnctlr.COLS);
//					printf("--MoveCursor baddr=%d\n", baddr);
					telnet.tnctlr.cursor_move(baddr);
					break;
				default:		/* couln't say */
					telnet.events.popup_an_error("MoveCursor_action requires 0 or 2 arguments");
					break;
			}
			return true;
		}




		/*
		 * Key action.
		 */
		public bool Key_action(params object[] args)
		{
			int i;
			int k;
			enum_keytype keytype;

	
			for (i = 0; i < args.Length; i++) 
			{
				string s = args[i] as String;
		
				k = MyStringToKeysym(s, out keytype);
				if (k == NoSymbol) 
				{
					telnet.events.popup_an_error("Key_action: Nonexistent or invalid KeySym: "+s);
					continue;
				}
				if ((k & ~0xff) !=0)
				{
					telnet.events.popup_an_error("Key_action: Invalid KeySym: "+s);
					continue;
				}
				key_ACharacter((byte)(k & 0xff), keytype, iaction.IA_KEY);
			}
			return true;
		}



		/*
		 * String action.
		 */
		public bool String_action(params object[] args)
		{
			int i;
			//int len = 0;
			string s = "";
			for (i = 0; i < args.Length; i++)
			{
				s+= (string)args[i];
			}

			/* Set a pending string. */
			ps_set(s, false);
			bool ok = !telnet.events.IsError();
			if (!ok && telnet.Config.ThrowExceptionOnLockedScreen)
				throw new ApplicationException(telnet.events.GetErrorAsText());
			else
				return ok;
			//return true;
		}

		/*
		 * HexString action.
		 */
		public bool HexString_action(params object[] args)
		{
			int i;
			string s = "";
			string t;

			for (i = 0; i < args.Length; i++) 
			{
				t = (string)args[i];
				if (t.Length>2 && (t.Substring(0,2)=="0x" || t.Substring(0,2)=="0X"))
					t = t.Substring(2);
				s+=t;
			}
			if (s.Length==0)
				return false;

			/* Set a pending string. */
			ps_set(s, true);
			return true;
		}

		/*
		 * Dual-mode action for the "asciicircum" ("^") key:
		 *  If in ANSI mode, pass through untranslated.
		 *  If in 3270 mode, translate to "notsign".
		 */
		public bool CircumNot_action(params object[] args)
		{
			if (telnet.IN_3270 && composing == enum_composing.NONE)
				key_ACharacter(0xac, enum_keytype.KT_STD, iaction.IA_KEY);
			else
				key_ACharacter((byte)'^', enum_keytype.KT_STD, iaction.IA_KEY);
			return true;
		}

		/* PA key action for String actions */
		void do_pa(int n)
		{
			if (n < 1 || n > PA_SZ) 
			{
				telnet.events.popup_an_error("Unknown PA key %d", n);
				return;
			}
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(PA_action), n.ToString());
				return;
			}
			key_AID(pa_xlate[n-1]);
		}

		/* PF key action for String actions */
		void do_pf(int n)
		{
			if (n < 1 || n > PF_SZ) 
			{
				telnet.events.popup_an_error("Unknown PF key %d", n);
				return;
			}
			if (kybdlock!=0) 
			{

				enq_ta(new ActionDelegate(PF_action), n.ToString());
				return;
			}
			key_AID(pf_xlate[n-1]);
		}

		/*
		 * Set or clear the keyboard scroll lock.
		 */
		void kybd_scroll_lock(bool lockflag)
		{
			if (!telnet.IN_3270)
				return;
			if (lockflag)
				kybdlock_set(KL_SCROLLED, "kybd_scroll_lock");
			else
				kybdlock_clr(KL_SCROLLED, "kybd_scroll_lock");
		}

		/*
		 * Move the cursor back within the legal paste area.
		 * Returns a bool indicating success.
		 */
		bool remargin(int lmargin)
		{
			bool ever = false;
			int baddr, b0 = 0;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
	

			baddr = telnet.tnctlr.cursor_addr;
			while (telnet.tnctlr.BA_TO_COL(baddr) < lmargin) 
			{
				baddr = telnet.tnctlr.ROWCOL_TO_BA(telnet.tnctlr.BA_TO_ROW(baddr), lmargin);
				if (!ever) 
				{
					b0 = baddr;
					ever = true;
				}
				fa_index = telnet.tnctlr.get_field_attribute(baddr);
				if (fa_index != -1)
					fa = telnet.tnctlr.screen_buf[fa_index];

				if (fa_index == baddr || telnet.tnctlr.FA_IS_PROTECTED(fa)) 
				{
					baddr = telnet.tnctlr.next_unprotected(baddr);
					if (baddr <= b0)
						return false;
				}
			}

			telnet.tnctlr.cursor_move(baddr);
			return true;
		}

		/*
		 * Pretend that a sequence of keys was entered at the keyboard.
		 *
		 * "Pasting" means that the sequence came from the X clipboard.  Returns are
		 * ignored; newlines mean "move to beginning of next line"; tabs and formfeeds
		 * become spaces.  Backslashes are not special, but ASCII ESC characters are
		 * used to signify 3270 Graphic Escapes.
		 *
		 * "Not pasting" means that the sequence is a login string specified in the
		 * hosts file, or a parameter to the String action.  Returns are "move to
		 * beginning of next line"; newlines mean "Enter AID" and the termination of
		 * processing the string.  Backslashes are processed as in C.
		 *
		 * Returns the number of unprocessed characters.
		 */

		public bool EmulateInput_action(params object[] args)
		{
			StringBuilder sb = new StringBuilder();
			int i;
			for (i=0; i<args.Length; i++)
			{
				sb.Append(args[i].ToString());
			}
			emulate_input(sb.ToString(), false);
			return true;
		}
		enum EIState { BASE, BACKSLASH, BACKX, BACKP, BACKPA, BACKPF, OCTAL, HEX, XGE };
		int emulate_input(string s, bool pasting)
		{
			char c;
	
			EIState state = EIState.BASE;
			int literal = 0;
			int nc = 0;
			iaction ia = pasting ? iaction.IA_PASTE : iaction.IA_STRING;
			int orig_addr = telnet.tnctlr.cursor_addr;
			int orig_col = telnet.tnctlr.BA_TO_COL(telnet.tnctlr.cursor_addr);
			int len = s.Length;

			/*
			 * In the switch statements below, "break" generally means "consume
			 * this character," while "continue" means "rescan this character."
			 */
			while (s.Length>0) 
			{

				/*
				 * It isn't possible to unlock the keyboard from a string,
				 * so if the keyboard is locked, it's fatal
				 */
				if (kybdlock!=0) 
				{
					telnet.trace.trace_event("  keyboard locked, string dropped. kybdlock="+kybdlock+"\n");
					if (telnet.Config.ThrowExceptionOnLockedScreen)
						throw new ApplicationException("Keyboard locked typing data onto screen - data was lost.  Turn of configuration option 'ThrowExceptionOnLockedScreen' to ignore this exception.");
					return 0;
				}

				if (pasting && telnet.IN_3270) 
				{

					/* Check for cursor wrap to top of screen. */
					if (telnet.tnctlr.cursor_addr < orig_addr)
						return len-1;		/* wrapped */

					/* Jump cursor over left margin. */
					if (telnet.appres.toggled(Appres.MARGINED_PASTE) &&
						telnet.tnctlr.BA_TO_COL(telnet.tnctlr.cursor_addr) < orig_col) 
					{
						if (!remargin(orig_col))
							return len-1;
					}
				}

				c = s[0];
				switch (state) 
				{
					case EIState.BASE:
					switch ((char)c) 
					{
						case '\b':
							action.action_internal(new ActionDelegate(Left_action), ia);
							continue;
						case '\f':
							if (pasting) 
							{
								key_ACharacter((byte) ' ', enum_keytype.KT_STD, ia);
							} 
							else 
							{
								action.action_internal(new ActionDelegate(Clear_action), ia);
								if (telnet.IN_3270)
									return len-1;
								else
									break;
							}
							break; // mfw - added BUGBUG
						case '\n':
							if (pasting)
								action.action_internal(new ActionDelegate(Newline_action), ia);
							else 
							{
								action.action_internal(new ActionDelegate(Enter_action), ia);
								if (telnet.IN_3270)
									return len-1;
							}
							break;
						case '\r':	/* ignored */
							break;
						case '\t':
							action.action_internal(new ActionDelegate(Tab_action), ia);
							break;
						case '\\':	/* backslashes are NOT special when pasting */
							if (!pasting)
								state = EIState.BACKSLASH;
							else
								key_ACharacter((byte) c, enum_keytype.KT_STD, ia);
							break;
						case (char)0x1b: /* ESC is special only when pasting */
							if (pasting)
								state = EIState.XGE;
							break;
						case '[':	/* APL left bracket */
							/*MFW if (pasting && appres.apl_mode)
								key_ACharacter(
									(byte) XK_Yacute,
									KT_GE, ia);
							else*/
							key_ACharacter((byte) c, enum_keytype.KT_STD, ia);
							break;
						case ']':	/* APL right bracket */
							/*MFW if (pasting && appres.apl_mode)
								key_ACharacter(
									(byte) XK_diaeresis,
									KT_GE, ia);
							else*/
							key_ACharacter((byte) c, enum_keytype.KT_STD, ia);
							break;
						default:
							key_ACharacter((byte) c, enum_keytype.KT_STD, ia);
							break;
					}
						break;
					case EIState.BACKSLASH:	/* last character was a backslash */
					switch ((char)c) 
					{
						case 'a':
							telnet.events.popup_an_error("String_action: Bell not supported");
							state = EIState.BASE;
							break;
						case 'b':
							action.action_internal(new ActionDelegate(Left_action), ia);
							state = EIState.BASE;
							break;
						case 'f':
							action.action_internal(new ActionDelegate(Clear_action), ia);
							state = EIState.BASE;
							if (telnet.IN_3270)
								return len-1;
							else
								break;
						case 'n':
							action.action_internal(new ActionDelegate(Enter_action), ia);
							state = EIState.BASE;
							if (telnet.IN_3270)
								return len-1;
							else
								break;
						case 'p':
							state = EIState.BACKP;
							break;
						case 'r':
							action.action_internal(new ActionDelegate(Newline_action), ia);
							state = EIState.BASE;
							break;
						case 't':
							action.action_internal(new ActionDelegate(Tab_action), ia);
							state = EIState.BASE;
							break;
						case 'T':
							action.action_internal(new ActionDelegate(BackTab_action), ia);
							state = EIState.BASE;
							break;
						case 'v':
							telnet.events.popup_an_error("String_action: Vertical tab not supported");
							state = EIState.BASE;
							break;
						case 'x':
							state = EIState.BACKX;
							break;
						case '\\':
							key_ACharacter((byte) c, enum_keytype.KT_STD, ia);
							state = EIState.BASE;
							break;
						case '0': 
						case '1': 
						case '2': 
						case '3':
						case '4': 
						case '5': 
						case '6': 
						case '7':
							state = EIState.OCTAL;
							literal = 0;
							nc = 0;
							continue;
						default:
							state = EIState.BASE;
							continue;
					}
						break;
					case EIState.BACKP:	/* last two characters were "\p" */
					switch ((char)c) 
					{
						case 'a':
							literal = 0;
							nc = 0;
							state = EIState.BACKPA;
							break;
						case 'f':
							literal = 0;
							nc = 0;
							state = EIState.BACKPF;
							break;
						default:
							telnet.events.popup_an_error("String_action: Unknown character after \\p");
							state = EIState.BASE;
							break;
					}
						break;
					case EIState.BACKPF: /* last three characters were "\pf" */
						if (nc < 2 && isdigit(c)) 
						{
							literal = (literal * 10) + (c - '0');
							nc++;
						} 
						else if (nc==0) 
						{
							telnet.events.popup_an_error("String_action: Unknown character after \\pf");
							state = EIState.BASE;
						} 
						else 
						{
							do_pf(literal);
							if (telnet.IN_3270)
								return len-1;
							state = EIState.BASE;
							continue;
						}
						break;
					case EIState.BACKPA: /* last three characters were "\pa" */
						if (nc < 1 && isdigit(c)) 
						{
							literal = (literal * 10) + (c - '0');
							nc++;
						} 
						else if (nc==0) 
						{
							telnet.events.popup_an_error("String_action: Unknown character after \\pa");
							state = EIState.BASE;
						} 
						else 
						{
							do_pa(literal);
							if (telnet.IN_3270)
								return len-1;
							state = EIState.BASE;
							continue;
						}
						break;
					case EIState.BACKX:	/* last two characters were "\x" */
						if (isxdigit(c)) 
						{
							state = EIState.HEX;
							literal = 0;
							nc = 0;
							continue;
						} 
						else 
						{
							telnet.events.popup_an_error("String_action: Missing hex digits after \\x");
							state = EIState.BASE;
							continue;
						}
					case EIState.OCTAL:	/* have seen \ and one or more octal digits */
						if (nc < 3 && isdigit(c) && c < '8') 
						{
							literal = (literal * 8) + FROM_HEX(c);
							nc++;
							break;
						} 
						else 
						{
							key_ACharacter((byte) literal, enum_keytype.KT_STD, ia);
							state = EIState.BASE;
							continue;
						}
					case EIState.HEX:	/* have seen \ and one or more hex digits */
						if (nc < 2 && isxdigit(c)) 
						{
							literal = (literal * 16) + FROM_HEX(c);
							nc++;
							break;
						} 
						else 
						{
							key_ACharacter((byte) literal, enum_keytype.KT_STD,
								ia);
							state = EIState.BASE;
							continue;
						}
					case EIState.XGE:	/* have seen ESC */
					switch ((char)c) 
					{
						case ';':	/* FM */
							key_Character(CG.CG_fm, false, true);
							break;
						case '*':	/* DUP */
							key_Character(CG.CG_dup, false, true);
							break;
						default:
							key_ACharacter((byte) c, enum_keytype.KT_GE, ia);
							break;
					}
						state = EIState.BASE;
						break;
				}
				s = s.Substring(1);
				//s++;
				len--;
			}

			switch (state) 
			{
				case EIState.OCTAL:
				case EIState.HEX:
					key_ACharacter((byte) literal, enum_keytype.KT_STD, ia);
					state = EIState.BASE;
					break;
				case EIState.BACKPF:
					if (nc > 0) 
					{
						do_pf(literal);
						state = EIState.BASE;
					}
					break;
				case EIState.BACKPA:
					if (nc > 0) 
					{
						do_pa(literal);
						state = EIState.BASE;
					}
					break;
				default:
					break;
			}

			if (state != EIState.BASE)
				telnet.events.popup_an_error("String_action: Missing data after \\");

			return len;
		}

		/*
		 * Pretend that a sequence of hexadecimal characters was entered at the
		 * keyboard.  The input is a sequence of hexadecimal bytes, 2 characters
		 * per byte.  If connected in ANSI mode, these are treated as ASCII
		 * characters; if in 3270 mode, they are considered EBCDIC.
		 *
		 * Graphic Escapes are handled as \E.
		 */
		void hex_input(string s)
		{
			
			bool escaped;
			byte[] xbuf = null;
			int tbuf = 0;
			//unsigned char *xbuf = (unsigned char *)NULL;
			//unsigned char *tbuf = (unsigned char *)NULL;
			int nbytes = 0;

			/* Validate the string. */
			if ((s.Length % 2)!=0) 
			{
				telnet.events.popup_an_error("HexString_action: Odd number of characters in specification");
				return;
			}
			int index;
			escaped = false;
			index=0;
			while (index<s.Length) 
			{
				if (isxdigit(s[index]) && isxdigit(s[index+1])) 
				{
					escaped = false;
					nbytes++;
				} 
				else if (s.Substring(index, 2).ToLower()=="\\e")
				{
					if (escaped) 
					{
						telnet.events.popup_an_error("HexString_action: Double \\E");

						return;
					}
					if (!telnet.IN_3270) 
					{
						telnet.events.popup_an_error("HexString_action: \\E in ANSI mode");
						return;
					}
					escaped = true;
				} 
				else 
				{
					telnet.events.popup_an_error("HexString_action: Illegal character in specification");
					return;
				}
				index += 2;
			}
			if (escaped) 
			{
				telnet.events.popup_an_error("HexString_action: Nothing follows \\E");
				return;
			}

			/* Allocate a temporary buffer. */
			if (!telnet.IN_3270 && nbytes!=0)
			{
				xbuf = new byte[nbytes];
				tbuf = 0;
				//tbuf = xbuf = (unsigned char *)Malloc(nbytes);
			}

			/* Pump it in. */
			index = 0;
			escaped = false;
			while (index < s.Length) 
			{
				if (isxdigit(s[index]) && isxdigit(s[index+1])) 
				{
					byte c;

					c = (byte)((FROM_HEX(s[index]) * 16) + FROM_HEX(s[index+1]));
					if (telnet.IN_3270)
						key_Character(Tables.ebc2cg[c], escaped, true);
					else
						xbuf[tbuf++] = (byte)c;
					escaped = false;
				} 
				else if (s.Substring(index, 2).ToLower()=="\\e")
				{
					escaped = true;
				}
				index += 2;
			}
			if (!telnet.IN_3270 && nbytes!=0) 
			{
				telnet.net_hexansi_out(xbuf, nbytes);
			}
		}
 


		/*
		 * Translate a keysym name to a keysym, including APL and extended
		 * characters.
		 */
		int MyStringToKeysym(string s,  out enum_keytype keytypep)
		{
            throw new ApplicationException("MyStringToKeysym not implemented");
		}
		/*
		 * FieldExit for the 5250-like emulation.
		 * Erases from the current cursor position to the end of the field, and moves
		 * the cursor to the beginning of the next field.
		 *
		 * Derived from work (C) Minolta (Schweiz) AG, Beat Rubischon <bru@minolta.ch>
		 */
		public bool FieldExit_action(params object[] args)
		{
			int baddr;
			int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
	

	
			if (telnet.IN_ANSI) 
			{
				telnet.net_sendc('\n');
				return true;
			}
			if (kybdlock!=0) 
			{
				enq_ta(new ActionDelegate(FieldExit_action), args);
				return true;
			}
			baddr = telnet.tnctlr.cursor_addr;
			fa_index = telnet.tnctlr.get_field_attribute(baddr);
			if (fa_index!=-1)
				fa = telnet.tnctlr.screen_buf[fa_index];

			if (telnet.tnctlr.FA_IS_PROTECTED(fa) || telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr])) 
			{
				operator_error(baddr, KL_OERR_PROTECTED);
				return false;
			}
			if (telnet.tnctlr.formatted) 
			{        /* erase to next field attribute */
				do 
				{
					telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
					telnet.tnctlr.INC_BA(ref baddr);
				} while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]));
				telnet.tnctlr.mdt_set(telnet.tnctlr.screen_buf, fa_index);
				telnet.tnctlr.cursor_move(telnet.tnctlr.next_unprotected(telnet.tnctlr.cursor_addr));
			} 
			else 
			{        /* erase to end of screen */
				do 
				{
					telnet.tnctlr.ctlr_add(baddr, CG.CG_null, 0);
					telnet.tnctlr.INC_BA(ref baddr);
				} while (baddr != 0);
			}
			return true;
		}

		public bool Fields_action(params object[] args)
		{
			//int baddr;
			//int fa_index;
			byte fa = telnet.tnctlr.fake_fa;

			int fieldpos = 0;
			int index = 0;
			int end;
			do
			{
				int newfield = telnet.tnctlr.next_unprotected(fieldpos);
				if (newfield<=fieldpos) break;
				end = newfield;
				while (!telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[end])) 
				{
					telnet.tnctlr.INC_BA(ref end);
					if (end==0) 
					{
						end=(telnet.tnctlr.COLS*telnet.tnctlr.ROWS)-1;
						break;
					}
				}
				telnet.action.action_output("data: field["+index+"] at "+newfield+" to "+end+" (x="+telnet.tnctlr.BA_TO_COL(newfield)+", y="+telnet.tnctlr.BA_TO_ROW(newfield)+", len="+(end-newfield+1)+")\n");
				
				index++;
				fieldpos = newfield;
			}
			while (true);
			return true;
		}

		public bool FieldGet_action(params object[] args)
		{
			//int baddr;
			//int fa_index;
			//byte fa = telnet.tnctlr.fake_fa;

	
			if (!telnet.tnctlr.formatted)
			{
				telnet.events.popup_an_error("FieldGet: Screen is not formatted");
				return false;
			}
			int fieldnumber = (int)args[0];

			int fieldpos = 0;
			int index = 0;
			do
			{
				int newfield = telnet.tnctlr.next_unprotected(fieldpos);
				if (newfield<=fieldpos) break;

				if (fieldnumber==index)
				{
					byte fa = telnet.tnctlr.fake_fa;
					int fa_index;
					int start, baddr;
					int len = 0;

					fa_index = telnet.tnctlr.get_field_attribute(newfield);
					if (fa_index!=-1)
						fa = telnet.tnctlr.screen_buf[fa_index];
					start = fa_index;
					telnet.tnctlr.INC_BA(ref start);
					baddr = start;
					do 
					{
						if (telnet.tnctlr.IS_FA(telnet.tnctlr.screen_buf[baddr]))
							break;
						len++;
						telnet.tnctlr.INC_BA(ref baddr);
					} while (baddr != start);

					telnet.tnctlr.dump_range(start, len, true, telnet.tnctlr.screen_buf, telnet.tnctlr.ROWS, telnet.tnctlr.COLS);

					return true;
				}
				index++;
				fieldpos = newfield;
			}
			while (true);
			telnet.events.popup_an_error("FieldGet: Field %d not found", fieldnumber);
			return true;
		}

		public bool FieldSet_action(params object[] args)
		{
			//int baddr;
			//int fa_index;
			byte fa = telnet.tnctlr.fake_fa;
	
			if (!telnet.tnctlr.formatted) 
			{
				telnet.events.popup_an_error("FieldSet: Screen is not formatted");
				return false;
			}
			int fieldnumber = (int)args[0];
			string fielddata = (string)args[1];
			//
			int fieldpos = 0;
			int index = 0;
			do
			{
				int newfield = telnet.tnctlr.next_unprotected(fieldpos);
				if (newfield<=fieldpos) break;

				if (fieldnumber==index)
				{
					telnet.tnctlr.cursor_addr = newfield;
					DeleteField_action(null, null, null, 0);
					ps_set(fielddata, false);

					return true;
				}
				index++;
				fieldpos = newfield;
			}
			while (true);
			telnet.events.popup_an_error("FieldGet: Field %d not found", fieldnumber);
			return true;
		}
	}
}
