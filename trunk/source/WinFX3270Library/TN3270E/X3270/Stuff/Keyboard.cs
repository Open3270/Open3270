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

	internal class Keyboard:IDisposable
	{
		Telnet telnet;
		TNTrace trace;
		Actions action;



		public int keyboardLock = KeyboardConstants.NotConnected;


		bool insertMode = false;
		bool reverseMode = false;
		bool flipped = false;

		int PF_SZ;
		int PA_SZ;

		Composing composing = Composing.None;

		Queue taQueue = new Queue();

		System.Threading.Timer unlock_id = null;




		#region Nested Classes

		internal class AKeySym 
		{
			public byte keysym;
			public KeyType keytype;
		};

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

		#endregion Nested Classes







#if N_COMPOSITES
		internal class composite 
		{
			public akeysym k1, k2;
			public  akeysym translation;
		};
		composite[] composites = null;
		int n_composites = 0;
#endif


		


		#region Ctors, dtors, and clean-up

		internal Keyboard(Telnet telnet)
		{

			this.action = this.telnet.Action;
			this.telnet = telnet;
			this.trace = this.telnet.Trace;
			PF_SZ = KeyboardConstants.PfTranslation.Length;
			PA_SZ = KeyboardConstants.PaTranslation.Length;
		}

		#endregion Ctors, dtors, and clean-up




		public bool AkEq(AKeySym k1, AKeySym k2)
		{
			return ((k1.keysym  == k2.keysym) && (k1.keytype == k2.keytype));
		}


		byte FromHex(char c)
		{
			const string dx1 = "0123456789abcdef";
			const string dx2 = "0123456789ABCDEF";

			int index = dx1.IndexOf(c);
			if (index == -1)
			{
				index = dx2.IndexOf(c);
			}
			if (index == -1)
			{
				throw new ApplicationException("sorry, '" + c + "' isn't a valid hex digit");
			}
			return (byte)index;
			
		}

		bool IsXDigit(char ch)
		{
			string ok = "0123456789ABCDEFabcdef";
			if (ok.IndexOf((char)ch) != -1)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		bool IsDigit(char ch)
		{
			if (ch >= '0' && ch <= '9')
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		

		

		/// <summary>
		/// Put an action on the typeahead queue.
		/// </summary>
		/// <param name="fn"></param>
		/// <param name="args"></param>
		void EnqueueTypeAheadAction(ActionDelegate fn, params object[] args)
		{
			// If no connection, forget it.
			if (!this.telnet.IsConnected) 
			{
				this.telnet.Trace.trace_event("  dropped (not connected)\n");
				return;
			}

			// If operator error, complain and drop it.
			if ((this.keyboardLock & KeyboardConstants.ErrorMask) != 0) 
			{
				//ring_bell();
				this.telnet.Trace.trace_event("  dropped (operator error)\n");
				return;
			}

			// If scroll lock, complain and drop it.
			if ((this.keyboardLock & KeyboardConstants.Scrolled) != 0)
			{
				//ring_bell();
				this.telnet.Trace.trace_event("  dropped (scrolled)\n");
				return;
			}

			// If typeahead disabled, complain and drop it.
			if (!telnet.Appres.typeahead) 
			{
				this.telnet.Trace.trace_event("  dropped (no typeahead)\n");
				return;
			}

			taQueue.Enqueue(new TAItem(fn, args));
			//	status_typeahead(true);

			this.telnet.Trace.trace_event("  action queued (kybdlock 0x"+keyboardLock+")\n");
		}


		/// <summary>
		/// Execute an action from the typeahead queue.
		/// </summary>
		/// <returns></returns>
		public bool RunTypeAhead()
		{
			bool success = false;
			if (this.keyboardLock == 0 && taQueue.Count != 0)
			{
				TAItem item = (TAItem)taQueue.Dequeue();

				if (taQueue.Count == 0)
				{
					//status_typeahead(false);
				}
				item.fn(item.args);
				success = true;
			}
			return success;
		}


		/// <summary>
		/// Flush the typeahead queue.  Returns whether or not anything was flushed.
		/// </summary>
		/// <returns></returns>
		bool FlushTypeAheadQueue()
		{
			bool any = false;
			if (taQueue.Count > 0)
			{
				any = true;
			}
			taQueue.Clear();
			//			status_typeahead(false);
			return any;
		}

		private void PsSet(string text, bool is_hex)
		{
			// Move forwards to first non protected
			// Hack for mfw/FDI USA
			bool skiptounprotected = this.telnet.Config.AlwaysSkipToUnprotected;

			int address = this.telnet.Controller.CursorAddress;
			if (skiptounprotected)
			{
				// Move cursor forwards to next unprotected field
				bool ok = true;
				int fa;
				do
				{
					ok = true;
					fa = this.telnet.Controller.GetFieldAttribute(address);
					if (fa == -1)
					{
						break;
					}
					if (FA.IsFA(this.telnet.Controller.ScreenBuffer[address]) || (fa >= 0 && FA.IsProtected(this.telnet.Controller.ScreenBuffer[fa]))) 
					{
						ok = false;
						this.telnet.Controller.IncrementAddress(ref address);
						if (address == this.telnet.Controller.CursorAddress)
						{
							Console.WriteLine("**BUGBUG** Screen has no unprotected field!");
							return;
						}
					}
				}while (!ok);

				if (address != this.telnet.Controller.CursorAddress)
				{
					Console.WriteLine("Moved cursor to "+address+" to skip protected fields");
					this.telnet.Controller.SetCursorAddress(address);
					Console.WriteLine("cursor position "+telnet.Controller.AddressToColumn(address)+", "+telnet.Controller.AddresstoRow(address));
					Console.WriteLine("text : "+text);
				}
			}

			//push_string(text, false, is_hex);
			this.emulate_input(text, false);
			
		}

		/// <summary>
		/// Set bits in the keyboard lock.
		/// </summary>
		/// <param name="bits"></param>
		/// <param name="cause"></param>
		public void KeyboardLockSet(int bits, string cause)
		{
			int n;

			n = keyboardLock | bits;
			if (n != keyboardLock) 
			{
				keyboardLock = n;
			}
			//Console.WriteLine("kybdlock_set "+bits+" "+cause);
		}

		/// <summary>
		/// Clear bits in the keyboard lock.
		/// </summary>
		/// <param name="bits"></param>
		/// <param name="debug"></param>
		public void KeyboardLockClear(int bits, string debug)
		{
			int n;
			//Console.WriteLine("kybdlock_clr "+bits+" "+debug);
			if (bits == -1)
			{
				bits = 0xFFFF;
			}

			n = keyboardLock & ~bits;
			if (n != keyboardLock) 
			{
				keyboardLock = n;
			}
		}



		/// <summary>
		/// Set or clear enter-inhibit mode.
		/// </summary>
		/// <param name="inhibit"></param>
		public void ToggleEnterInhibitMode(bool inhibit)
		{
			if (inhibit) 
			{
				this.KeyboardLockSet(KeyboardConstants.EnterInhibit, "kybd_inhibit");
			} 
			else 
			{
				this.KeyboardLockClear(KeyboardConstants.EnterInhibit, "kybd_inhibit");
			}
		}



		/// <summary>
		/// Called when a host connects or disconnects.
		/// </summary>
		/// <param name="connected"></param>
		public void ConnectedStateChanged(bool connected)
		{
			if ((this.keyboardLock & KeyboardConstants.DeferredUnlock) != 0)
			{
				this.telnet.Controller.RemoveTimeOut(unlock_id);
			}

			this.KeyboardLockClear(-1, "kybd_connect");

			if (connected) 
			{
				// Wait for any output or a WCC(restore) from the host
				this.KeyboardLockSet(KeyboardConstants.AwaitingFirst, "kybd_connect");
			} 
			else 
			{
				this.KeyboardLockSet(KeyboardConstants.NotConnected, "kybd_connect");
				this.FlushTypeAheadQueue();
			}
		}


		/// <summary>
		/// Called when we switch between 3270 and ANSI modes.
		/// </summary>
		/// <param name="in3270"></param>
		public void SwitchMode3270(bool in3270)
		{
			if ((this.keyboardLock & KeyboardConstants.DeferredUnlock) != 0)
			{
				this.telnet.Controller.RemoveTimeOut(unlock_id);
			}
			this.KeyboardLockClear(-1, "kybd_connect");
		}


		/// <summary>
		/// Called to initialize the keyboard logic.
		/// </summary>
		public void Initialize()
		{
			this.telnet.PrimaryConnectionChanged += telnet_PrimaryConnectionChanged;
			this.telnet.Connected3270 += telnet_Connected3270;
		}


		void telnet_Connected3270(object sender, Connected3270EventArgs e)
		{
			this.SwitchMode3270(e.Is3270);
		}


		void telnet_PrimaryConnectionChanged(object sender, PrimaryConnectionChangedArgs e)
		{
			this.ConnectedStateChanged(e.Success);
		}


		/// <summary>
		/// Lock the keyboard because of an operator error.
		/// </summary>
		/// <param name="address"></param>
		/// <param name="errorType"></param>
		public void HandleOperatorError(int address, int errorType)
		{
			Console.WriteLine("cursor@"+address+" - ROW="+telnet.Controller.AddresstoRow(address)+" COL="+telnet.Controller.AddressToColumn(address));
			this.telnet.Events.popup_an_error("Keyboard locked");
			Console.WriteLine("WARNING--operator_error error_type="+errorType);

			if (this.telnet.Config.LockScreenOnWriteToUnprotected)
			{
				this.KeyboardLockSet(errorType, "operator_error");
				this.FlushTypeAheadQueue();
			} 
			else 
			{
				//ring_bell();
			}
		}

 
		/// <summary>
		/// Handle an AID (Attention IDentifier) key.  This is the common stuff that gets executed for all AID keys (PFs, PAs, Clear and etc).
		/// </summary>
		/// <param name="aidCode"></param>
		public void HandleAttentionIdentifierKey(byte aidCode)
		{
			if (this.telnet.IsAnsi) 
			{
				int i;

				if (aidCode == AID.Enter) 
				{
					this.telnet.SendChar('\r');
					return;
				}
				for (i = 0; i < PF_SZ; i++)
				{
					if (aidCode == KeyboardConstants.PfTranslation[i]) 
					{
						this.telnet.Ansi.ansi_send_pf(i + 1);
						return;
					}
				}
				for (i = 0; i < PA_SZ; i++)
				{
					if (aidCode == KeyboardConstants.PaTranslation[i]) 
					{
						this.telnet.Ansi.ansi_send_pa(i + 1);
						return;
					}
				}
				return;
			}
			if (this.telnet.IsSscp) 
			{
				if ((this.keyboardLock & KeyboardConstants.OiaMinus) != 0)
					return;
				if (aidCode != AID.Enter && aidCode != AID.Clear) 
				{
					KeyboardLockSet(KeyboardConstants.OiaMinus, "key_AID");
					return;
				}
			}
			if (this.telnet.IsSscp && aidCode == AID.Enter) 
			{
				//Act as if the host had written our input.
				this.telnet.Controller.BufferAddress = this.telnet.Controller.CursorAddress;
			}
			if (!telnet.IsSscp || aidCode != AID.Clear) 
			{
				this.insertMode = false;
				//Console.WriteLine("**BUGBUG** KL_OIA_LOCKED REMOVED");
				KeyboardLockSet(KeyboardConstants.OiaTWait | KeyboardConstants.OiaLocked, "key_AID");
			}
			this.telnet.Idle.reset_idle_timer();
			this.telnet.Controller.AttentionID = aidCode;
			this.telnet.Controller.ProcessReadModifiedCommand(this.telnet.Controller.AttentionID, false);
		}



		public bool PFAction(params object[] args)
		{
			int k;

			k = (int)args[0];
			if (k < 1 || k > PF_SZ) 
			{
				this.telnet.Events.popup_an_error("PF_action: Invalid argument '" + args[0] + "'");
				return false;
			}
			if ((this.keyboardLock & KeyboardConstants.OiaMinus) != 0)
			{
				return false;
			}
			else if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(PFAction), args);
			}
			else
			{
				this.HandleAttentionIdentifierKey(KeyboardConstants.PfTranslation[k - 1]);
			}
			return true;
		}

		public bool PAAction(params object[] args)
		{
			int k;

			k = (int)args[0];
			if (k < 1 || k > PA_SZ) 
			{
				this.telnet.Events.popup_an_error("PA_action: Invalid argument '" + args[0] + "'");
				return false;
			}
			if ((this.keyboardLock & KeyboardConstants.OiaMinus) != 0)
			{
				return false;
			}
			else if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(PAAction), args);
			}
			else
			{
				this.HandleAttentionIdentifierKey(KeyboardConstants.PaTranslation[k - 1]);
			}
			return true;
		}


		/// <summary>
		/// ATTN key, per RFC 2355.  Sends IP, regardless.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool AttnAction(params object[] args)
		{
			if (this.telnet.Is3270)
			{
				this.telnet.Interrupt();
				return true;
			}
			return false;
		}


		/// <summary>
		/// IAC IP, which works for 5250 System Request and interrupts the program on an AS/400, even when the keyboard is locked.
		/// This is now the same as the Attn action.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool InterruptAction(params object[] args)
		{
			if (this.telnet.Is3270)
			{
				this.telnet.Interrupt();
				return true;
			}
			return false;
		}



		bool  WrapCharacter(int cgcode)
		{
			bool with_ge = false;
			bool  pasting = false;

			if ((cgcode & KeyboardConstants.WFlag) != 0) 
			{
				with_ge = true;
				cgcode &= ~KeyboardConstants.WFlag;
			}
			if ((cgcode & KeyboardConstants.PasteWFlag) != 0)
			{
				pasting = true;
				cgcode &= ~KeyboardConstants.PasteWFlag;
			}
			this.telnet.Trace.trace_event(" %s -> Key(%s\"%s\")\n",
				"nop",/*ia_name[(int) ia_cause],*/
				with_ge ? "GE " : "",
				Util.ctl_see((byte) Tables.Cg2Ascii[cgcode]));
			return HandleOrdinaryCharacter(cgcode, with_ge, pasting);
		}


		/// <summary>
		/// Handle an ordinary displayable character key.  Lots of stuff to handle: insert-mode, protected fields and etc.
		/// </summary>
		/// <param name="cgCode"></param>
		/// <param name="withGE"></param>
		/// <param name="pasting"></param>
		/// <returns></returns>
		public bool HandleOrdinaryCharacter(int cgCode, bool withGE, bool pasting)
		{
			int address;
			int endAddress;
			int fa;
			bool noRoom = false;

			if (this.keyboardLock!=0) 
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

			address = this.telnet.Controller.CursorAddress;
			fa = this.telnet.Controller.GetFieldAttribute(address);
			byte favalue = this.telnet.Controller.FakeFA;
			if (fa != -1)
			{
				favalue = this.telnet.Controller.ScreenBuffer[fa];
			}
			if (FA.IsFA(this.telnet.Controller.ScreenBuffer[address]) || FA.IsProtected(favalue)) 
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}
			if (this.telnet.Appres.numeric_lock && FA.IsNumeric(favalue) &&
				!((cgCode >= CG.CG_0 && cgCode <= CG.CG_9) ||
				cgCode == CG.CG_minus || cgCode == CG.CG_period)) 
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorNumeric);
				return false;
			}
			if (reverseMode || (insertMode && this.telnet.Controller.ScreenBuffer[address]!=0)) 
			{
				int last_blank = -1;

				//Find next null, next fa, or last blank
				endAddress = address;
				if (this.telnet.Controller.ScreenBuffer[endAddress] == CG.CG_space)
				{
					last_blank = endAddress;
				}
				do 
				{
					this.telnet.Controller.IncrementAddress(ref endAddress);
					if (this.telnet.Controller.ScreenBuffer[endAddress] == CG.CG_space)
					{
						last_blank = endAddress;
					}
					if (this.telnet.Controller.ScreenBuffer[endAddress] == CG.CG_null ||  FA.IsFA(this.telnet.Controller.ScreenBuffer[endAddress]))
					{
						break;
					}
				} while (endAddress != address);

				//Pretend a trailing blank is a null, if desired.
				if (this.telnet.Appres.toggled(Appres.BLANK_FILL) && last_blank != -1) 
				{
					this.telnet.Controller.IncrementAddress(ref last_blank);
					if (last_blank == endAddress) 
					{
						this.telnet.Controller.DecrementAddress(ref endAddress);
						this.telnet.Controller.AddCharacter(endAddress, CG.CG_null, 0);
					}
				}

				//Check for field overflow.
				if (this.telnet.Controller.ScreenBuffer[endAddress] != CG.CG_null) 
				{
					if (insertMode) 
					{
						this.HandleOperatorError(endAddress, KeyboardConstants.ErrorOverflow);
						return false;
					} 
					else 
					{	
						//Reverse
						noRoom = true;
					}
				} 
				else 
				{
					// Shift data over.
					if (endAddress > address) 
					{
						// At least one byte to copy, no wrap.
						this.telnet.Controller.CopyBlock(address, address + 1, endAddress - address, false);
					}
					else if (endAddress < address) 
					{
						// At least one byte to copy, wraps to top.
						this.telnet.Controller.CopyBlock(0, 1, endAddress, false);
						this.telnet.Controller.AddCharacter(0, this.telnet.Controller.ScreenBuffer[(this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount) - 1], 0);
						this.telnet.Controller.CopyBlock(address, address + 1, ((this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount) - 1) - address, false);
					}
				}

			}

			// Replace leading nulls with blanks, if desired.
			if (this.telnet.Controller.Formatted && this.telnet.Appres.toggled(Appres.BLANK_FILL)) 
			{
				int	addresSof = fa;//fa - this.telnet.tnctlr.screen_buf;
				int addressFill = address;

				this.telnet.Controller.DecrementAddress(ref addressFill);
				while (addressFill != addresSof) 
				{
					// Check for backward line wrap.
					if ((addressFill % this.telnet.Controller.ColumnCount) == this.telnet.Controller.ColumnCount - 1) 
					{
						bool aborted = true;
						int addressScan = addressFill;

						 // Check the field within the preceeding line for NULLs.
						while (addressScan != addresSof) 
						{
							if (this.telnet.Controller.ScreenBuffer[addressScan] != CG.CG_null) 
							{
								aborted = false;
								break;
							}
							if (0 == (addressScan % this.telnet.Controller.ColumnCount))
							{
								break;
							}
							this.telnet.Controller.DecrementAddress(ref addressScan);
						}
						if (aborted)
						{
							break;
						}
					}

					if (this.telnet.Controller.ScreenBuffer[addressFill] == CG.CG_null)
					{
						this.telnet.Controller.AddCharacter(addressFill, CG.CG_space, 0);
					}
					this.telnet.Controller.DecrementAddress(ref addressFill);
				}
			}

			// Add the character.
			if (noRoom) 
			{
				do 
				{
					this.telnet.Controller.IncrementAddress(ref address);
				} while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[address]));
			} 
			else 
			{
				this.telnet.Controller.AddCharacter(address, (byte)cgCode, (byte)(withGE ? ExtendedAttribute.CS_GE : (byte)0));
				this.telnet.Controller.SetForegroundColor(address, 0);
				this.telnet.Controller.ctlr_add_gr(address, 0);
				if (!reverseMode)
				{
					this.telnet.Controller.IncrementAddress(ref address);
				}
			}

			 //Implement auto-skip, and don't land on attribute bytes.
			 //This happens for all pasted data (even DUP), and for all keyboard-generated data except DUP.
			if (pasting || (cgCode != CG.CG_dup)) 
			{
				while (FA.IsFA(this.telnet.Controller.ScreenBuffer[address])) 
				{
					if (FA.IsSkip(this.telnet.Controller.ScreenBuffer[address]))
					{
						address = this.telnet.Controller.GetNextUnprotectedField(address);
					}
					else
					{
						this.telnet.Controller.IncrementAddress(ref address);
					}
				}
				this.telnet.Controller.SetCursorAddress(address);
			}

			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, fa);
			return true;
		}

		
		/// <summary>
		/// Handle an ordinary character key, given an ASCII code.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="keytype"></param>
		/// <param name="cause"></param>
		void HandleAsciiCharacter(byte character, KeyType keytype, iaction cause)
		{
			AKeySym keySymbol = new AKeySym();

			keySymbol.keysym = character;
			keySymbol.keytype = keytype;

			switch (composing) 
			{
				case Composing.None:
					{
						break;
					}
				case Composing.Compose:
					{
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
							composing = Composing.None;
						}
						return;
					}
				case Composing.First:
					{
						composing = Composing.None;
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
							return;
						}
					}
			}

			trace.trace_event(" %s -> Key(\"%s\")\n", this.telnet.Action.ia_name[(int) cause], Util.ctl_see(character));
			if (this.telnet.Is3270) 
			{
				if (character < ' ') 
				{
					trace.trace_event("  dropped (control char)\n");
					return;
				}
				this.HandleOrdinaryCharacter(Tables.Ascii2Cg[character], keytype == KeyType.GE, false);
			}
			else if (this.telnet.IsAnsi) 
			{
				this.telnet.SendChar((char)character);
			}
			else 
			{
				trace.trace_event("  dropped (not connected)\n");
			}
		}

 

		/// <summary>
		/// Simple toggles.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MonoCaseAction(params object[] args)
		{
			this.telnet.Appres.do_toggle(Appres.MONOCASE);
			return true;
		}


		/// <summary>
		/// Flip the display left-to-right
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool FlipAction(params object[] args)
		{
//			screen_flip();
			return true;
		}



		public bool TabForwardAction(params object[] args)
		{
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(TabForwardAction), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendChar('\t');
				return true;
			}
			this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(this.telnet.Controller.CursorAddress));
			return true;
		}

 
		/// <summary>
		/// Tab backward to previous field.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool BackTab_action(params object[] args)
		{
			int	baddr, nbaddr;
			int		sbaddr;

			if (!this.telnet.Is3270)
			{
				return false;
			}
	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(BackTab_action), args);
				return true;
			}
			baddr = this.telnet.Controller.CursorAddress;
			this.telnet.Controller.DecrementAddress(ref baddr);
			if (FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr]))	/* at bof */
			{
				this.telnet.Controller.DecrementAddress(ref baddr);
			}
			sbaddr = baddr;
			while (true) 
			{
				nbaddr = baddr;
				this.telnet.Controller.IncrementAddress(ref nbaddr);
				if (FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr])
					&& !FA.IsProtected(this.telnet.Controller.ScreenBuffer[baddr])
					&& !FA.IsFA(this.telnet.Controller.ScreenBuffer[nbaddr]))
				{
					break;
				}
				this.telnet.Controller.DecrementAddress(ref baddr);
				if (baddr == sbaddr) 
				{
					this.telnet.Controller.SetCursorAddress(0);
					return true;
				}
			}
			this.telnet.Controller.IncrementAddress(ref baddr);
			this.telnet.Controller.SetCursorAddress(baddr);
			return true;
		}



		/// <summary>
		/// Deferred keyboard unlock.
		/// </summary>
		/// <param name="state"></param>
		void DeferUnlock(object state)
		{
			lock (telnet)
			{
				// Only actually process the event if the keyboard is currently unlocked...
				if ((this.telnet.Keyboard.keyboardLock | KeyboardConstants.DeferredUnlock) == KeyboardConstants.DeferredUnlock)
				{

					this.telnet.Trace.WriteLine("--debug--defer_unlock");
					KeyboardLockClear(KeyboardConstants.DeferredUnlock, "defer_unlock");
					//status_reset();
					if (this.telnet.IsConnected)
					{
						this.telnet.Controller.ProcessPendingInput();
					}
				}
				else
				{
					this.telnet.Trace.WriteLine("--debug--defer_unlock ignored");
				}

			}
		}



		public void ResetKeyboardLock(bool explicitvalue)
		{
			 //If explicit (from the keyboard) and there is typeahead or a half-composed key, simply flush it.
			 
			if (explicitvalue)
			{
				bool halfReset = false;

				if (FlushTypeAheadQueue())
				{
					halfReset = true;
				}
				if (composing != Composing.None) 
				{
					composing = Composing.None;
					//	status_compose(false, 0, KT_STD);
					halfReset = true;
				}
				if (halfReset)
				{
					return;
				}
			}


			//Always clear insert mode.
			this.insertMode = false;

			// Otherwise, if not connect, reset is a no-op.
			if (this.telnet.IsConnected)
			{
				return;
			}

			//Remove any deferred keyboard unlock.  We will either unlock the keyboard now, or want to defer further into the future.

			if ((this.keyboardLock & KeyboardConstants.DeferredUnlock) != 0)
			{
				this.telnet.Controller.RemoveTimeOut(unlock_id);
			}

			
			//If explicit (from the keyboard), unlock the keyboard now.
			//Otherwise (from the host), schedule a deferred keyboard unlock.
			if (explicitvalue) 
			{
				this.KeyboardLockClear(-1, "ResetKeyboardLock");
			}
			else if ((this.keyboardLock & (KeyboardConstants.DeferredUnlock | KeyboardConstants.OiaTWait | KeyboardConstants.OiaLocked | KeyboardConstants.AwaitingFirst)) != 0) 
			{
				this.telnet.Trace.WriteLine("Clear lock in 1010/55");
				this.KeyboardLockClear(~KeyboardConstants.DeferredUnlock, "ResetKeyboardLock");
				this.KeyboardLockSet(KeyboardConstants.DeferredUnlock, "ResetKeyboardLock");
				lock (telnet)
				{
					unlock_id = this.telnet.Controller.AddTimeout(KeyboardConstants.UnlockMS, new TimerCallback(DeferUnlock));
				}
			}

			// Clean up other modes.
			composing = Composing.None;
		}


		public bool ResetAction(params object[] args)
		{
			ResetKeyboardLock(true);
			return true;
		}


		/// <summary>
		/// Move to first unprotected field on screen.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool HomeAction(params object[] args)
		{
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(HomeAction),args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.Ansi.ansi_send_home();
				return true;
			}
			if (!telnet.Controller.Formatted) 
			{
				this.telnet.Controller.SetCursorAddress(0);
				return true;
			}
			this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(this.telnet.Controller.RowCount*telnet.Controller.ColumnCount-1));
			return true;
		}


		
		
		/// <summary>
		/// Cursor left 1 position.
		/// </summary>
		void MoveLeft()
		{
			int	address = this.telnet.Controller.CursorAddress;
			this.telnet.Controller.DecrementAddress(ref address);
			this.telnet.Controller.SetCursorAddress(address);
		}


		public bool LeftAction(params object[] args)
		{
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(LeftAction), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.Ansi.ansi_send_left();
				return true;
			}
			if (!this.flipped)
				this.MoveLeft();
			else 
			{
				int	address = this.telnet.Controller.CursorAddress;
				this.telnet.Controller.IncrementAddress(ref address);
				this.telnet.Controller.SetCursorAddress(address);
			}
			return true;
		}

 
		/// <summary>
		/// Delete char key.
		/// </summary>
		/// <returns> Returns "true" if succeeds, "false" otherwise.</returns>
		bool DeleteCharacter()
		{
			int	address, andAddress;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);
			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}
			if (FA.IsProtected(fa) || FA.IsFA(this.telnet.Controller.ScreenBuffer[address])) 
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}
			//Find next FA
			if (this.telnet.Controller.Formatted) 
			{
				andAddress = address;
				do 
				{
					this.telnet.Controller.IncrementAddress(ref andAddress);
					if (FA.IsFA(this.telnet.Controller.ScreenBuffer[andAddress]))
						break;
				} while (andAddress != address);

				this.telnet.Controller.DecrementAddress(ref andAddress);
			} 
			else 
			{
				if ((address % this.telnet.Controller.ColumnCount) == this.telnet.Controller.ColumnCount - 1)
				{
					return true;
				}
				andAddress = address + (this.telnet.Controller.ColumnCount - (address % this.telnet.Controller.ColumnCount)) - 1;
			}

			if (andAddress > address) 
			{
				this.telnet.Controller.CopyBlock(address + 1, address, andAddress - address, false);
			} 
			else if (andAddress != address) 
			{
				this.telnet.Controller.CopyBlock(address + 1, address, ((this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount) - 1) - address, false);
				this.telnet.Controller.AddCharacter((this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount) - 1, this.telnet.Controller.ScreenBuffer[0], 0);
				this.telnet.Controller.CopyBlock(1, 0, andAddress, false);
			}

			this.telnet.Controller.AddCharacter(andAddress, CG.CG_null, 0);
			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			return true;
		}


		public bool DeleteAction(params object[] args)
		{
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(DeleteAction), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendByte(0x7f);
				return true;
			}
			if (!this.DeleteCharacter())
				return false;
			if (this.reverseMode) 
			{
				int address = this.telnet.Controller.CursorAddress;

				this.telnet.Controller.DecrementAddress(ref address);
				if (!FA.IsFA(this.telnet.Controller.ScreenBuffer[address]))
				{
					this.telnet.Controller.SetCursorAddress(address);
				}
			}
			return true;
		}

 
		public bool BackSpaceAction(params object[] args)
		{
			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(BackSpaceAction), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendErase();
				return true;
			}
			if (this.reverseMode)
			{
				this.DeleteCharacter();
			}
			else if (!this.flipped)
			{
				this.MoveLeft();
			}
			else
			{
				int address;

				address = this.telnet.Controller.CursorAddress;
				this.telnet.Controller.DecrementAddress(ref address);
				this.telnet.Controller.SetCursorAddress(address);
			}
			return true;
		}

 

		/// <summary>
		/// Destructive backspace, like Unix "erase".
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool EraseAction(params object[] args)
		{
			int	address;
			byte fa = this.telnet.Controller.FakeFA;
			int faIndex;

			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(EraseAction), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendErase();
				return true;
			}
			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);
			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}
			if (faIndex == address || FA.IsProtected(fa)) 
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}
			if (address != 0 && faIndex == address - 1)
			{
				return true;
			}
			this.MoveLeft();
			this.DeleteCharacter();
			return true;
		}


		/// <summary>
		/// Move cursor right
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveRight(params object[] args)
		{
			int address;

			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveRight), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.Ansi.ansi_send_right();
				return true;
			}
			if (!flipped)
			{
				address = this.telnet.Controller.CursorAddress;
				this.telnet.Controller.IncrementAddress(ref address);
				this.telnet.Controller.SetCursorAddress(address);
			}
			else
			{
				MoveLeft();
			}
			return true;
		}

 
		/// <summary>
		/// Move cursor left 2 positions.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorLeft2Positions(params object[] args)
		{
			int	address;

			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursorLeft2Positions), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			address = this.telnet.Controller.CursorAddress;
			this.telnet.Controller.DecrementAddress(ref address);
			this.telnet.Controller.DecrementAddress(ref address);
			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}

 
	
		/// <summary>
		/// Move cursor to previous word.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool PreviousWordAction(params object[] args)
		{
			int address;
			int address0;
			byte c;
			bool prot;


			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(PreviousWordAction), args);
				return true;
			}
			if (this.telnet.IsAnsi)
			{
				return false;
			}
			if (!this.telnet.Controller.Formatted)
			{
				return false;
			}

			address = this.telnet.Controller.CursorAddress;
			prot = FA.IsProtectedAt(this.telnet.Controller.ScreenBuffer, address);

			//Skip to before this word, if in one now.
			if (!prot) 
			{
				c = this.telnet.Controller.ScreenBuffer[address];
				while (!FA.IsFA(c) && c != CG.CG_space && c != CG.CG_null) 
				{
					this.telnet.Controller.DecrementAddress(ref address);
					if (address == this.telnet.Controller.CursorAddress)
						return true;
					c = this.telnet.Controller.ScreenBuffer[address];
				}
			}
			address0 = address;

			//Find the end of the preceding word.
			do 
			{
				c = this.telnet.Controller.ScreenBuffer[address];
				if (FA.IsFA(c)) 
				{
					this.telnet.Controller.DecrementAddress(ref address);
					prot = FA.IsProtectedAt(this.telnet.Controller.ScreenBuffer, address);
					continue;
				}
				if (!prot && c != CG.CG_space && c != CG.CG_null)
					break;
				this.telnet.Controller.DecrementAddress(ref address);
			} while (address != address0);

			if (address == address0)
			{
				return true;
			}

			// Go to the front.
			do
			{
				this.telnet.Controller.DecrementAddress(ref address);
				c = this.telnet.Controller.ScreenBuffer[address];
			}
			while (!FA.IsFA(c) && c != CG.CG_space && c != CG.CG_null);

			this.telnet.Controller.IncrementAddress(ref address);
			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}

 


		/// <summary>
		/// Move cursor right 2 positions.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorRight2Positions(params object[] args)
		{
			int	address;

			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursorRight2Positions), args);
				return true;
			}
			if (this.telnet.IsAnsi)
			{
				return false;
			}
			address = this.telnet.Controller.CursorAddress;
			this.telnet.Controller.IncrementAddress(ref address);
			this.telnet.Controller.IncrementAddress(ref address);
			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}

 

		/// <summary>
		/// Find the next unprotected word
		/// </summary>
		/// <param name="baseAddress"></param>
		/// <returns>-1 if unsuccessful</returns>
		int FindNextUnprotectedWord(int baseAddress)
		{
			int address0 = baseAddress;
			byte c;
			bool prot;

			prot = FA.IsProtectedAt(this.telnet.Controller.ScreenBuffer, baseAddress);

			do 
			{
				c = this.telnet.Controller.ScreenBuffer[baseAddress];
				if (FA.IsFA(c))
				{
					prot = FA.IsProtected(c);
				}
				else if (!prot && c != CG.CG_space && c != CG.CG_null)
				{
					return baseAddress;
				}
				this.telnet.Controller.IncrementAddress(ref baseAddress);

			} while (baseAddress != address0);

			return -1;
		}



		/// <summary>
		/// Find the next word in this field
		/// </summary>
		/// <param name="baseAddress"></param>
		/// <returns>-1 when unsuccessful</returns>
		int FindNextWordInField(int baseAddress)
		{
			int address0 = baseAddress;
			byte c;
			bool inWord = true;

			do 
			{
				c = this.telnet.Controller.ScreenBuffer[baseAddress];
				if (FA.IsFA(c))
				{
					return -1;
				}

				if (inWord) 
				{
					if (c == CG.CG_space || c == CG.CG_null)
					{
						inWord = false;
					}
				} 
				else 
				{
					if (c != CG.CG_space && c != CG.CG_null)
					{
						return baseAddress;
					}
				}

				this.telnet.Controller.IncrementAddress(ref baseAddress);
			} while (baseAddress != address0);

			return -1;
		}



		/// <summary>
		/// Cursor to next unprotected word.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorToNextUnprotectedWord(params object[] args)
		{
			int	address;
			byte c;

			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursorToNextUnprotectedWord), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}
			if (!telnet.Controller.Formatted)
			{
				return false;
			}

			// If not in an unprotected field, go to the next unprotected word.
			if (FA.IsFA(this.telnet.Controller.ScreenBuffer[telnet.Controller.CursorAddress]) ||
				FA.IsProtectedAt(this.telnet.Controller.ScreenBuffer, this.telnet.Controller.CursorAddress)) 
			{
				address = this.FindNextUnprotectedWord(this.telnet.Controller.CursorAddress);
				if (address != -1)
				{
					this.telnet.Controller.SetCursorAddress(address);
				}
				return true;
			}

			// If there's another word in this field, go to it.
			address = this.FindNextWordInField(this.telnet.Controller.CursorAddress);
			if (address != -1) 
			{
				this.telnet.Controller.SetCursorAddress(address);
				return true;
			}

			/* If in a word, go to just after its end. */
			c = this.telnet.Controller.ScreenBuffer[telnet.Controller.CursorAddress];
			if (c != CG.CG_space && c != CG.CG_null) 
			{
				address = this.telnet.Controller.CursorAddress;
				do 
				{
					c = this.telnet.Controller.ScreenBuffer[address];
					if (c == CG.CG_space || c == CG.CG_null) 
					{
						this.telnet.Controller.SetCursorAddress(address);
						return true;
					} 
					else if (FA.IsFA(c)) 
					{
						address = this.FindNextUnprotectedWord(address);
						if (address != -1)
						{
							this.telnet.Controller.SetCursorAddress(address);
						}
						return true;
					}
					this.telnet.Controller.IncrementAddress(ref address);
				} while (address != this.telnet.Controller.CursorAddress);
			}
				//Otherwise, go to the next unprotected word.
			else 
			{
				address = FindNextUnprotectedWord(this.telnet.Controller.CursorAddress);
				if (address != -1)
				{
					this.telnet.Controller.SetCursorAddress(address);
				}
			}
			return true;
		}

 

		/// <summary>
		/// Cursor up 1 position.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorUp(params object[] args)
		{
			int	address;

			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursorUp), args);
				return true;
			}

			if (this.telnet.IsAnsi) 
			{
				this.telnet.Ansi.ansi_send_up();
				return true;
			}

			address = this.telnet.Controller.CursorAddress - this.telnet.Controller.ColumnCount;

			if (address < 0)
			{
				address = (this.telnet.Controller.CursorAddress + (this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount)) - this.telnet.Controller.ColumnCount;
			}

			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}

 

		/// <summary>
		/// Cursor down 1 position.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorDown(params object[] args)
		{
			int	address;

			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursorDown), args);
				return true;
			}

			if (this.telnet.IsAnsi) 
			{
				this.telnet.Ansi.ansi_send_down();
				return true;
			}

			address = (this.telnet.Controller.CursorAddress + this.telnet.Controller.ColumnCount) % (this.telnet.Controller.ColumnCount * this.telnet.Controller.RowCount);
			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}

 

		/// <summary>
		/// Cursor to first field on next line or any lines after that.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorToNewLine(params object[] args)
		{
			int	address;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;


			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursorToNewLine), args);
				return true;
			}

			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendChar('\n');
				return false;
			}

			address = (this.telnet.Controller.CursorAddress + this.telnet.Controller.ColumnCount) % (this.telnet.Controller.ColumnCount * this.telnet.Controller.RowCount);	/* down */
			address = (address / this.telnet.Controller.ColumnCount) * this.telnet.Controller.ColumnCount;			/* 1st col */
			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}
			if (faIndex != address && !FA.IsProtected(fa))
			{
				this.telnet.Controller.SetCursorAddress(address);
			}
			else
			{
				this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(address));
			}
			return true;
		}

 

		/// <summary>
		/// DUP key
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool DupAction(params object[] args)
		{
			if (this.keyboardLock != 0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(DupAction), args);
				return true;
			}
			if (this.telnet.IsAnsi)
				return false;
			if (HandleOrdinaryCharacter(CG.CG_dup, false, false))
				this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(this.telnet.Controller.CursorAddress));
			return true;
		}

 
		/// <summary>
		/// FM key
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool FieldMarkAction(params object[] args)
		{
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(FieldMarkAction), args);
				return true;
			}
			if (this.telnet.IsAnsi)
			{
				return false;
			}
			this.HandleOrdinaryCharacter(CG.CG_fm, false, false);
			return true;
		}

 

		/// <summary>
		/// Vanilla AID keys
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool EnterAction(params object[] args)
		{
			if ((this.keyboardLock & KeyboardConstants.OiaMinus) != 0)
			{
				return false;
			}
			else if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(EnterAction), args);
			}
			else
			{
				this.HandleAttentionIdentifierKey(AID.Enter);
			}
			return true;
		}


		public bool SystemRequestAction(params object[] args)
		{
			if (this.telnet.IsAnsi)
			{
				return false;
			}
			if (this.telnet.IsE) 
			{
				this.telnet.Abort();
			} 
			else
			{
				if ((this.keyboardLock & KeyboardConstants.OiaMinus) != 0)
				{
					return false;
				}
				else if (this.keyboardLock != 0)
				{
					this.EnqueueTypeAheadAction(new ActionDelegate(SystemRequestAction), args);
				}
				else
				{
					this.HandleAttentionIdentifierKey(AID.SysReq);
				}
			}
			return true;
		}

 

		/// <summary>
		/// Clear AID key
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool ClearAction(params object[] args)
		{
			if ((this.keyboardLock & KeyboardConstants.OiaMinus) != 0)
			{
				return false;
			}

			if (this.keyboardLock!=0 && this.telnet.IsConnected) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(ClearAction), args);
				return true;
			}

			if (this.telnet.IsAnsi) 
			{
				this.telnet.Ansi.ansi_send_clear();
				return true;
			}

			this.telnet.Controller.BufferAddress = 0;
			this.telnet.Controller.Clear(true);
			this.telnet.Controller.SetCursorAddress(0);

			if (this.telnet.IsConnected)
			{
				this.HandleAttentionIdentifierKey(AID.Clear);
			}
			return true;
		}

 

		/// <summary>
		/// Cursor Select key (light pen simulator).
		/// </summary>
		/// <param name="address"></param>
		void LightPenSelect(int address)
		{
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;
			byte sel;
			int designator;

			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}

			if (!FA.IsSelectable(fa)) 
			{
				//ring_bell();
				return;
			}

			sel = this.telnet.Controller.ScreenBuffer[faIndex+1];

			designator = faIndex+1;

			switch (sel) 
			{
				case CG.CG_greater:		/* > */
					this.telnet.Controller.AddCharacter(designator, CG.CG_question, 0); /* change to ? */
					this.telnet.Controller.MDTClear(this.telnet.Controller.ScreenBuffer, faIndex);
					break;
				case CG.CG_question:		/* ? */
					this.telnet.Controller.AddCharacter(designator, CG.CG_greater, 0);	/* change to > */
					this.telnet.Controller.MDTClear(this.telnet.Controller.ScreenBuffer, faIndex);
					break;
				case CG.CG_space:		/* space */
				case CG.CG_null:		/* null */
					this.HandleAttentionIdentifierKey(AID.SELECT);
					break;
				case CG.CG_ampersand:		/* & */
					this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
					this.HandleAttentionIdentifierKey(AID.Enter);
					break;
				default:
					//ring_bell();
					break;
			}
			return;
		}


		/// <summary>
		/// Cursor Select key (light pen simulator) -- at the current cursor location.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool CursorSelectAction(params object[] args)
		{
	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(CursorSelectAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}
			this.LightPenSelect(this.telnet.Controller.CursorAddress);
			return true;
		}


 
		/// <summary>
		/// Erase End Of Field Key.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool EraseEndOfFieldAaction(params object[] args)
		{
			int	address;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;

	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(EraseEndOfFieldAaction), args);
				return false;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}

			if (FA.IsProtected(fa) || FA.IsFA(this.telnet.Controller.ScreenBuffer[address])) 
			{
				HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			if (this.telnet.Controller.Formatted) 
			{	
				//Erase to next field attribute
				do 
				{
					this.telnet.Controller.AddCharacter(address, CG.CG_null, 0);
					this.telnet.Controller.IncrementAddress(ref address);
				} while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[address]));

				this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			} 
			else 
			{	
				//Erase to end of screen
				do 
				{
					this.telnet.Controller.AddCharacter(address, CG.CG_null, 0);
					this.telnet.Controller.IncrementAddress(ref address);
				} while (address != 0);
			}
			return true;
		}

 

		/// <summary>
		/// Erase all Input Key.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool EraseInputAction(params object[] args)
		{
			int address, sbAddress;
			byte fa = this.telnet.Controller.FakeFA;
			Boolean f;

	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(EraseInputAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			if (this.telnet.Controller.Formatted) 
			{
				/* find first field attribute */
				address = 0;
				do 
				{
					if (FA.IsFA(this.telnet.Controller.ScreenBuffer[address]))
					{
						break;
					}
					this.telnet.Controller.IncrementAddress(ref address);
				} while (address != 0);

				sbAddress = address;
				f = false;

				do 
				{
					fa = this.telnet.Controller.ScreenBuffer[address];
					if (!FA.IsProtected(fa)) 
					{
						this.telnet.Controller.MDTClear(this.telnet.Controller.ScreenBuffer, address);
						do 
						{
							this.telnet.Controller.IncrementAddress(ref address);
							if (!f) 
							{
								this.telnet.Controller.SetCursorAddress(address);
								f = true;
							}

							if (!FA.IsFA(this.telnet.Controller.ScreenBuffer[address])) 
							{
								this.telnet.Controller.AddCharacter(address, CG.CG_null, 0);
							}
						}	while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[address]));
					} 
					else 
					{	/* skip protected */
						do 
						{
							this.telnet.Controller.IncrementAddress(ref address);
						} while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[address]));

					}
				} while (address != sbAddress);

				if (!f)
				{
					this.telnet.Controller.SetCursorAddress(0);
				}
			} 
			else 
			{
				this.telnet.Controller.Clear(true);
				this.telnet.Controller.SetCursorAddress(0);
			}
			return true;
		}


 
	
		/// <summary>
		/// Delete word key.  Backspaces the cursor until it hits the front of a word, deletes characters until it hits a blank or null, 
		///and deletes all of these but the last. Which is to say, does a ^W.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool DeleteWordAction(params object[] args)
		{
			int	address, address2, frontAddress, backAddress, endAddress;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;
	

	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(DeleteWordAction), args);
				return false;
			}

			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendWErase();
				return true;
			}

			if (!telnet.Controller.Formatted)
			{
				return true;
			}

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);
			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}

			// Make sure we're on a modifiable field.
			if (FA.IsProtected(fa) || FA.IsFA(this.telnet.Controller.ScreenBuffer[address])) 
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			//Search backwards for a non-blank character.
			frontAddress = address;
			while (this.telnet.Controller.ScreenBuffer[frontAddress] == CG.CG_space ||
				this.telnet.Controller.ScreenBuffer[frontAddress] == CG.CG_null)
			{
				this.telnet.Controller.DecrementAddress(ref frontAddress);
			}

			//If we ran into the edge of the field without seeing any non-blanks,
			//there isn't any word to delete; just move the cursor. 
			if (FA.IsFA(this.telnet.Controller.ScreenBuffer[frontAddress])) 
			{
				this.telnet.Controller.SetCursorAddress(frontAddress+1);
				return true;
			}

			//FrontAddress is now pointing at a non-blank character.  Now search for the first blank to the left of that
			//(or the edge of the field), leaving frontAddress pointing at the the beginning of the word.
			while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[frontAddress]) &&
				this.telnet.Controller.ScreenBuffer[frontAddress] != CG.CG_space &&
				this.telnet.Controller.ScreenBuffer[frontAddress] != CG.CG_null)
			{
				this.telnet.Controller.DecrementAddress(ref frontAddress);
			}

			this.telnet.Controller.IncrementAddress(ref frontAddress);

			// Find the end of the word, searching forward for the edge of the field or a non-blank.
			backAddress = frontAddress;
			while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[backAddress]) &&
				this.telnet.Controller.ScreenBuffer[backAddress] != CG.CG_space &&
				this.telnet.Controller.ScreenBuffer[backAddress] != CG.CG_null)
			{
				this.telnet.Controller.IncrementAddress(ref backAddress);
			}

			//Find the start of the next word, leaving back_baddr pointing at it or at the end of the field.
			while (this.telnet.Controller.ScreenBuffer[backAddress] == CG.CG_space ||
				this.telnet.Controller.ScreenBuffer[backAddress] == CG.CG_null)
			{
				this.telnet.Controller.IncrementAddress(ref backAddress);
			}

			// Find the end of the field, leaving end_baddr pointing at the field attribute of the start of the next field.
			endAddress = backAddress;
			while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[endAddress]))
			{
				this.telnet.Controller.IncrementAddress(ref endAddress);
			}

			//Copy any text to the right of the word we are deleting.
			address = frontAddress;
			address2 = backAddress;
			while (address2 != endAddress) 
			{
				this.telnet.Controller.AddCharacter(address, this.telnet.Controller.ScreenBuffer[address2], 0);
				this.telnet.Controller.IncrementAddress(ref address);
				this.telnet.Controller.IncrementAddress(ref address2);
			}

			// Insert nulls to pad out the end of the field.
			while (address != endAddress) 
			{
				this.telnet.Controller.AddCharacter(address, CG.CG_null, 0);
				this.telnet.Controller.IncrementAddress(ref address);
			}

			// Set the MDT and move the cursor.
			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			this.telnet.Controller.SetCursorAddress(frontAddress);
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
			byte fa = this.telnet.Controller.FakeFA;
			int fa_index;

	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(DeleteField_action), args);
				return true;
			}
			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendKill();
				return true;
			}
			if (!telnet.Controller.Formatted)
				return false;

			baddr = this.telnet.Controller.CursorAddress;
			fa_index = this.telnet.Controller.GetFieldAttribute(baddr);
			if (fa_index != -1)
				fa = this.telnet.Controller.ScreenBuffer[fa_index];
			if (FA.IsProtected(fa) || FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr])) 
			{
				HandleOperatorError(baddr, KeyboardConstants.ErrorProtected);
				return false;
			}
			while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr]))
				this.telnet.Controller.DecrementAddress(ref baddr);
			this.telnet.Controller.IncrementAddress(ref baddr);
			this.telnet.Controller.SetCursorAddress(baddr);
			while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr])) 
			{
				this.telnet.Controller.AddCharacter(baddr, CG.CG_null, 0);
				this.telnet.Controller.IncrementAddress(ref baddr);
			}
			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, fa_index);
			return true;
		}


 
		/*
		 * Set insert mode key.
		 */
		public bool Insert_action(params object[] args)
		{
	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(Insert_action), args);
				return true;
			}
			if (this.telnet.IsAnsi)
				return false;
			this.insertMode = true;
			return true;
		}

 
		/*
		 * Toggle insert mode key.
		 */
		public bool ToggleInsert_action(params object[] args)
		{
	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(ToggleInsert_action), args);
				return true;
			}
			if (this.telnet.IsAnsi)
				return false;
			if (insertMode)
				this.insertMode = false;
			else
				this.insertMode = true;
			return true;
		}

 
		/*
		 * Toggle reverse mode key.
		 */
		public bool ToggleReverse_action(params object[] args)
		{
	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(ToggleReverse_action), args);
				return true;
			}
			if (this.telnet.IsAnsi)
				return false;
			this.reverseMode = !this.reverseMode;
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
			byte fa = this.telnet.Controller.FakeFA;
			byte c;
			int	last_nonblank = -1;

	
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(FieldEnd_action), args);
				return true;
			}
			if (this.telnet.IsAnsi)
				return false;
			if (!telnet.Controller.Formatted)
				return false;
			baddr = this.telnet.Controller.CursorAddress;
			fa_index = this.telnet.Controller.GetFieldAttribute(baddr);
			if (fa_index != -1)
				fa = this.telnet.Controller.ScreenBuffer[fa_index];
			//
			if (fa_index == this.telnet.Controller.ScreenBuffer[baddr] || FA.IsProtected(fa))
				return false;

			baddr = fa_index;
			while (true) 
			{
				this.telnet.Controller.IncrementAddress(ref baddr);
				c = this.telnet.Controller.ScreenBuffer[baddr];
				if (FA.IsFA(c))
					break;
				if (c != CG.CG_null && c != CG.CG_space)
					last_nonblank = baddr;
			}

			if (last_nonblank == -1) 
			{
				baddr = fa_index;// - this.telnet.tnctlr.screen_buf;
				this.telnet.Controller.IncrementAddress(ref baddr);
			} 
			else 
			{
				baddr = last_nonblank;
				this.telnet.Controller.IncrementAddress(ref baddr);
				if (FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr]))
					baddr = last_nonblank;
			}
			this.telnet.Controller.SetCursorAddress(baddr);
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

	
			if (this.keyboardLock!=0) 
			{
				if (args.Length == 2)
					this.EnqueueTypeAheadAction(new ActionDelegate(MoveCursor_action), args);
				return true;
			}

			switch (args.Length) 
			{
				case 2:		/* probably a macro call */
					row = (int)args[0];
					col = (int)args[1];
					if (!telnet.Is3270) 
					{
						row--;
						col--;
					}
					if (row < 0)
						row = 0;
					if (col < 0)
						col = 0;
					baddr = ((row * this.telnet.Controller.ColumnCount) + col) % (this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount);
//					printf("--MoveCursor baddr=%d\n", baddr);
					this.telnet.Controller.SetCursorAddress(baddr);
					break;
				default:		/* couln't say */
					this.telnet.Events.popup_an_error("MoveCursor_action requires 0 or 2 arguments");
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
			KeyType keytype;

	
			for (i = 0; i < args.Length; i++) 
			{
				string s = args[i] as String;
		
				k = MyStringToKeysym(s, out keytype);
				if (k == KeyboardConstants.NoSymbol) 
				{
					this.telnet.Events.popup_an_error("Key_action: Nonexistent or invalid KeySym: "+s);
					continue;
				}
				if ((k & ~0xff) !=0)
				{
					this.telnet.Events.popup_an_error("Key_action: Invalid KeySym: "+s);
					continue;
				}
				HandleAsciiCharacter((byte)(k & 0xff), keytype, iaction.IA_KEY);
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
			PsSet(s, false);
			bool ok = !telnet.Events.IsError();
			if (!ok && this.telnet.Config.ThrowExceptionOnLockedScreen)
				throw new ApplicationException(this.telnet.Events.GetErrorAsText());
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
			PsSet(s, true);
			return true;
		}

		/*
		 * Dual-mode action for the "asciicircum" ("^") key:
		 *  If in ANSI mode, pass through untranslated.
		 *  If in 3270 mode, translate to "notsign".
		 */
		public bool CircumNot_action(params object[] args)
		{
			if (this.telnet.Is3270 && composing == Composing.None)
				HandleAsciiCharacter(0xac, KeyType.Standard, iaction.IA_KEY);
			else
				HandleAsciiCharacter((byte)'^', KeyType.Standard, iaction.IA_KEY);
			return true;
		}

		/* PA key action for String actions */
		void do_pa(int n)
		{
			if (n < 1 || n > PA_SZ) 
			{
				this.telnet.Events.popup_an_error("Unknown PA key %d", n);
				return;
			}
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(PAAction), n.ToString());
				return;
			}
			this.HandleAttentionIdentifierKey(KeyboardConstants.PaTranslation[n - 1]);
		}

		/* PF key action for String actions */
		void do_pf(int n)
		{
			if (n < 1 || n > PF_SZ) 
			{
				this.telnet.Events.popup_an_error("Unknown PF key %d", n);
				return;
			}
			if (this.keyboardLock!=0) 
			{

				this.EnqueueTypeAheadAction(new ActionDelegate(PFAction), n.ToString());
				return;
			}
			this.HandleAttentionIdentifierKey(KeyboardConstants.PfTranslation[n - 1]);
		}

		/*
		 * Set or clear the keyboard scroll lock.
		 */
		void kybd_scroll_lock(bool lockflag)
		{
			if (!telnet.Is3270)
				return;
			if (lockflag)
				KeyboardLockSet(KeyboardConstants.Scrolled, "kybd_scroll_lock");
			else
				KeyboardLockClear(KeyboardConstants.Scrolled, "kybd_scroll_lock");
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
			byte fa = this.telnet.Controller.FakeFA;
	

			baddr = this.telnet.Controller.CursorAddress;
			while (this.telnet.Controller.AddressToColumn(baddr) < lmargin) 
			{
				baddr = this.telnet.Controller.RowColumnToByteAddress(this.telnet.Controller.AddresstoRow(baddr), lmargin);
				if (!ever) 
				{
					b0 = baddr;
					ever = true;
				}
				fa_index = this.telnet.Controller.GetFieldAttribute(baddr);
				if (fa_index != -1)
					fa = this.telnet.Controller.ScreenBuffer[fa_index];

				if (fa_index == baddr || FA.IsProtected(fa)) 
				{
					baddr = this.telnet.Controller.GetNextUnprotectedField(baddr);
					if (baddr <= b0)
						return false;
				}
			}

			this.telnet.Controller.SetCursorAddress(baddr);
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
			int orig_addr = this.telnet.Controller.CursorAddress;
			int orig_col = this.telnet.Controller.AddressToColumn(this.telnet.Controller.CursorAddress);
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
				if (this.keyboardLock!=0) 
				{
					this.telnet.Trace.trace_event("  keyboard locked, string dropped. kybdlock="+keyboardLock+"\n");
					if (this.telnet.Config.ThrowExceptionOnLockedScreen)
						throw new ApplicationException("Keyboard locked typing data onto screen - data was lost.  Turn of configuration option 'ThrowExceptionOnLockedScreen' to ignore this exception.");
					return 0;
				}

				if (pasting && this.telnet.Is3270) 
				{

					/* Check for cursor wrap to top of screen. */
					if (this.telnet.Controller.CursorAddress < orig_addr)
						return len-1;		/* wrapped */

					/* Jump cursor over left margin. */
					if (this.telnet.Appres.toggled(Appres.MARGINED_PASTE) &&
						this.telnet.Controller.AddressToColumn(this.telnet.Controller.CursorAddress) < orig_col) 
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
							action.action_internal(new ActionDelegate(LeftAction), ia);
							continue;
						case '\f':
							if (pasting) 
							{
								HandleAsciiCharacter((byte) ' ', KeyType.Standard, ia);
							} 
							else 
							{
								action.action_internal(new ActionDelegate(ClearAction), ia);
								if (this.telnet.Is3270)
									return len-1;
								else
									break;
							}
							break; // mfw - added BUGBUG
						case '\n':
							if (pasting)
								action.action_internal(new ActionDelegate(MoveCursorToNewLine), ia);
							else 
							{
								action.action_internal(new ActionDelegate(EnterAction), ia);
								if (this.telnet.Is3270)
									return len-1;
							}
							break;
						case '\r':	/* ignored */
							break;
						case '\t':
							action.action_internal(new ActionDelegate(TabForwardAction), ia);
							break;
						case '\\':	/* backslashes are NOT special when pasting */
							if (!pasting)
								state = EIState.BACKSLASH;
							else
								HandleAsciiCharacter((byte) c, KeyType.Standard, ia);
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
							HandleAsciiCharacter((byte) c, KeyType.Standard, ia);
							break;
						case ']':	/* APL right bracket */
							/*MFW if (pasting && appres.apl_mode)
								key_ACharacter(
									(byte) XK_diaeresis,
									KT_GE, ia);
							else*/
							HandleAsciiCharacter((byte) c, KeyType.Standard, ia);
							break;
						default:
							HandleAsciiCharacter((byte) c, KeyType.Standard, ia);
							break;
					}
						break;
					case EIState.BACKSLASH:	/* last character was a backslash */
					switch ((char)c) 
					{
						case 'a':
							this.telnet.Events.popup_an_error("String_action: Bell not supported");
							state = EIState.BASE;
							break;
						case 'b':
							action.action_internal(new ActionDelegate(LeftAction), ia);
							state = EIState.BASE;
							break;
						case 'f':
							action.action_internal(new ActionDelegate(ClearAction), ia);
							state = EIState.BASE;
							if (this.telnet.Is3270)
								return len-1;
							else
								break;
						case 'n':
							action.action_internal(new ActionDelegate(EnterAction), ia);
							state = EIState.BASE;
							if (this.telnet.Is3270)
								return len-1;
							else
								break;
						case 'p':
							state = EIState.BACKP;
							break;
						case 'r':
							action.action_internal(new ActionDelegate(MoveCursorToNewLine), ia);
							state = EIState.BASE;
							break;
						case 't':
							action.action_internal(new ActionDelegate(TabForwardAction), ia);
							state = EIState.BASE;
							break;
						case 'T':
							action.action_internal(new ActionDelegate(BackTab_action), ia);
							state = EIState.BASE;
							break;
						case 'v':
							this.telnet.Events.popup_an_error("String_action: Vertical tab not supported");
							state = EIState.BASE;
							break;
						case 'x':
							state = EIState.BACKX;
							break;
						case '\\':
							HandleAsciiCharacter((byte) c, KeyType.Standard, ia);
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
							this.telnet.Events.popup_an_error("String_action: Unknown character after \\p");
							state = EIState.BASE;
							break;
					}
						break;
					case EIState.BACKPF: /* last three characters were "\pf" */
						if (nc < 2 && IsDigit(c)) 
						{
							literal = (literal * 10) + (c - '0');
							nc++;
						} 
						else if (nc==0) 
						{
							this.telnet.Events.popup_an_error("String_action: Unknown character after \\pf");
							state = EIState.BASE;
						} 
						else 
						{
							do_pf(literal);
							if (this.telnet.Is3270)
								return len-1;
							state = EIState.BASE;
							continue;
						}
						break;
					case EIState.BACKPA: /* last three characters were "\pa" */
						if (nc < 1 && IsDigit(c)) 
						{
							literal = (literal * 10) + (c - '0');
							nc++;
						} 
						else if (nc==0) 
						{
							this.telnet.Events.popup_an_error("String_action: Unknown character after \\pa");
							state = EIState.BASE;
						} 
						else 
						{
							do_pa(literal);
							if (this.telnet.Is3270)
								return len-1;
							state = EIState.BASE;
							continue;
						}
						break;
					case EIState.BACKX:	/* last two characters were "\x" */
						if (IsXDigit(c)) 
						{
							state = EIState.HEX;
							literal = 0;
							nc = 0;
							continue;
						} 
						else 
						{
							this.telnet.Events.popup_an_error("String_action: Missing hex digits after \\x");
							state = EIState.BASE;
							continue;
						}
					case EIState.OCTAL:	/* have seen \ and one or more octal digits */
						if (nc < 3 && IsDigit(c) && c < '8') 
						{
							literal = (literal * 8) + FromHex(c);
							nc++;
							break;
						} 
						else 
						{
							HandleAsciiCharacter((byte) literal, KeyType.Standard, ia);
							state = EIState.BASE;
							continue;
						}
					case EIState.HEX:	/* have seen \ and one or more hex digits */
						if (nc < 2 && IsXDigit(c)) 
						{
							literal = (literal * 16) + FromHex(c);
							nc++;
							break;
						} 
						else 
						{
							HandleAsciiCharacter((byte) literal, KeyType.Standard,
								ia);
							state = EIState.BASE;
							continue;
						}
					case EIState.XGE:	/* have seen ESC */
					switch ((char)c) 
					{
						case ';':	/* FM */
							this.HandleOrdinaryCharacter(CG.CG_fm, false, true);
							break;
						case '*':	/* DUP */
							this.HandleOrdinaryCharacter(CG.CG_dup, false, true);
							break;
						default:
							HandleAsciiCharacter((byte) c, KeyType.GE, ia);
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
					HandleAsciiCharacter((byte) literal, KeyType.Standard, ia);
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
				this.telnet.Events.popup_an_error("String_action: Missing data after \\");

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
				this.telnet.Events.popup_an_error("HexString_action: Odd number of characters in specification");
				return;
			}
			int index;
			escaped = false;
			index=0;
			while (index<s.Length) 
			{
				if (IsXDigit(s[index]) && IsXDigit(s[index+1])) 
				{
					escaped = false;
					nbytes++;
				} 
				else if (s.Substring(index, 2).ToLower()=="\\e")
				{
					if (escaped) 
					{
						this.telnet.Events.popup_an_error("HexString_action: Double \\E");

						return;
					}
					if (!telnet.Is3270) 
					{
						this.telnet.Events.popup_an_error("HexString_action: \\E in ANSI mode");
						return;
					}
					escaped = true;
				} 
				else 
				{
					this.telnet.Events.popup_an_error("HexString_action: Illegal character in specification");
					return;
				}
				index += 2;
			}
			if (escaped) 
			{
				this.telnet.Events.popup_an_error("HexString_action: Nothing follows \\E");
				return;
			}

			/* Allocate a temporary buffer. */
			if (!telnet.Is3270 && nbytes!=0)
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
				if (IsXDigit(s[index]) && IsXDigit(s[index+1])) 
				{
					byte c;

					c = (byte)((FromHex(s[index]) * 16) + FromHex(s[index+1]));
					if (this.telnet.Is3270)
						this.HandleOrdinaryCharacter(Tables.Ebc2Cg[c], escaped, true);
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
			if (!telnet.Is3270 && nbytes!=0) 
			{
				this.telnet.SendHexAnsiOut(xbuf, nbytes);
			}
		}
 


		/*
		 * Translate a keysym name to a keysym, including APL and extended
		 * characters.
		 */
		int MyStringToKeysym(string s,  out KeyType keytypep)
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
			byte fa = this.telnet.Controller.FakeFA;
	

	
			if (this.telnet.IsAnsi) 
			{
				this.telnet.SendChar('\n');
				return true;
			}
			if (this.keyboardLock!=0) 
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(FieldExit_action), args);
				return true;
			}
			baddr = this.telnet.Controller.CursorAddress;
			fa_index = this.telnet.Controller.GetFieldAttribute(baddr);
			if (fa_index!=-1)
				fa = this.telnet.Controller.ScreenBuffer[fa_index];

			if (FA.IsProtected(fa) || FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr])) 
			{
				HandleOperatorError(baddr, KeyboardConstants.ErrorProtected);
				return false;
			}
			if (this.telnet.Controller.Formatted) 
			{        /* erase to next field attribute */
				do 
				{
					this.telnet.Controller.AddCharacter(baddr, CG.CG_null, 0);
					this.telnet.Controller.IncrementAddress(ref baddr);
				} while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr]));
				this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, fa_index);
				this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(this.telnet.Controller.CursorAddress));
			} 
			else 
			{        /* erase to end of screen */
				do 
				{
					this.telnet.Controller.AddCharacter(baddr, CG.CG_null, 0);
					this.telnet.Controller.IncrementAddress(ref baddr);
				} while (baddr != 0);
			}
			return true;
		}

		public bool Fields_action(params object[] args)
		{
			//int baddr;
			//int fa_index;
			byte fa = this.telnet.Controller.FakeFA;

			int fieldpos = 0;
			int index = 0;
			int end;
			do
			{
				int newfield = this.telnet.Controller.GetNextUnprotectedField(fieldpos);
				if (newfield<=fieldpos) break;
				end = newfield;
				while (!FA.IsFA(this.telnet.Controller.ScreenBuffer[end])) 
				{
					this.telnet.Controller.IncrementAddress(ref end);
					if (end==0) 
					{
						end=(this.telnet.Controller.ColumnCount*telnet.Controller.RowCount)-1;
						break;
					}
				}
				this.telnet.Action.action_output("data: field["+index+"] at "+newfield+" to "+end+" (x="+telnet.Controller.AddressToColumn(newfield)+", y="+telnet.Controller.AddresstoRow(newfield)+", len="+(end-newfield+1)+")\n");
				
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
			//byte fa = this.telnet.tnctlr.fake_fa;

	
			if (!telnet.Controller.Formatted)
			{
				this.telnet.Events.popup_an_error("FieldGet: Screen is not formatted");
				return false;
			}
			int fieldnumber = (int)args[0];

			int fieldpos = 0;
			int index = 0;
			do
			{
				int newfield = this.telnet.Controller.GetNextUnprotectedField(fieldpos);
				if (newfield<=fieldpos) break;

				if (fieldnumber==index)
				{
					byte fa = this.telnet.Controller.FakeFA;
					int fa_index;
					int start, baddr;
					int len = 0;

					fa_index = this.telnet.Controller.GetFieldAttribute(newfield);
					if (fa_index!=-1)
						fa = this.telnet.Controller.ScreenBuffer[fa_index];
					start = fa_index;
					this.telnet.Controller.IncrementAddress(ref start);
					baddr = start;
					do 
					{
						if (FA.IsFA(this.telnet.Controller.ScreenBuffer[baddr]))
							break;
						len++;
						this.telnet.Controller.IncrementAddress(ref baddr);
					} while (baddr != start);

					this.telnet.Controller.DumpRange(start, len, true, this.telnet.Controller.ScreenBuffer, this.telnet.Controller.RowCount, this.telnet.Controller.ColumnCount);

					return true;
				}
				index++;
				fieldpos = newfield;
			}
			while (true);
			this.telnet.Events.popup_an_error("FieldGet: Field %d not found", fieldnumber);
			return true;
		}

		public bool FieldSet_action(params object[] args)
		{
			//int baddr;
			//int fa_index;
			byte fa = this.telnet.Controller.FakeFA;
	
			if (!telnet.Controller.Formatted) 
			{
				this.telnet.Events.popup_an_error("FieldSet: Screen is not formatted");
				return false;
			}
			int fieldnumber = (int)args[0];
			string fielddata = (string)args[1];
			//
			int fieldpos = 0;
			int index = 0;
			do
			{
				int newfield = this.telnet.Controller.GetNextUnprotectedField(fieldpos);
				if (newfield<=fieldpos) break;

				if (fieldnumber==index)
				{
					this.telnet.Controller.CursorAddress = newfield;
					DeleteField_action(null, null, null, 0);
					PsSet(fielddata, false);

					return true;
				}
				index++;
				fieldpos = newfield;
			}
			while (true);
			this.telnet.Events.popup_an_error("FieldGet: Field %d not found", fieldnumber);
			return true;
		}

		public Actions Actions
		{
			set
			{
				this.action = value;
			}
		}

		public void Dispose()
		{
			this.telnet.PrimaryConnectionChanged -= telnet_PrimaryConnectionChanged;
			this.telnet.Connected3270 -= telnet_Connected3270;
		}
	}
}
