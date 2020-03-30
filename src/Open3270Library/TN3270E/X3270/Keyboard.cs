#region License
/* 
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
using System.Text;
using System.Collections;
using System.Threading;

namespace Open3270.TN3270
{

	internal class Keyboard : IDisposable
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

		internal Keyboard(Telnet telnetObject)
		{
			this.telnet = telnetObject;
			this.action = this.telnet.Action;

			this.trace = this.telnet.Trace;
			this.PF_SZ = KeyboardConstants.PfTranslation.Length;
			this.PA_SZ = KeyboardConstants.PaTranslation.Length;
		}

		public void Dispose()
		{
			this.telnet.PrimaryConnectionChanged -= telnet_PrimaryConnectionChanged;
			this.telnet.Connected3270 -= telnet_Connected3270;
		}

		#endregion Ctors, dtors, and clean-up


		public Actions Actions
		{
			set
			{
				this.action = value;
			}
		}



		public bool AkEq(AKeySym k1, AKeySym k2)
		{
			return ((k1.keysym == k2.keysym) && (k1.keytype == k2.keytype));
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

			this.telnet.Trace.trace_event("  action queued (kybdlock 0x" + keyboardLock + ")\n");
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
					if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]) || (fa >= 0 && FieldAttribute.IsProtected(this.telnet.Controller.ScreenBuffer[fa])))
					{
						ok = false;
						this.telnet.Controller.IncrementAddress(ref address);
						if (address == this.telnet.Controller.CursorAddress)
						{
							Console.WriteLine("**BUGBUG** Screen has no unprotected field!");
							return;
						}
					}
				} while (!ok);

				if (address != this.telnet.Controller.CursorAddress)
				{
					Console.WriteLine("Moved cursor to " + address + " to skip protected fields");
					this.telnet.Controller.SetCursorAddress(address);
					Console.WriteLine("cursor position " + telnet.Controller.AddressToColumn(address) + ", " + telnet.Controller.AddresstoRow(address));
					Console.WriteLine("text : " + text);
				}
			}

			//push_string(text, false, is_hex);
			this.EmulateInput(text, false);

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
			Console.WriteLine("cursor@" + address + " - ROW=" + telnet.Controller.AddresstoRow(address) + " COL=" + telnet.Controller.AddressToColumn(address));
			this.telnet.Events.ShowError("Keyboard locked");
			Console.WriteLine("WARNING--operator_error error_type=" + errorType);

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
			this.telnet.Idle.ResetIdleTimer();
			this.telnet.Controller.AttentionID = aidCode;
			this.telnet.Controller.ProcessReadModifiedCommand(this.telnet.Controller.AttentionID, false);
		}



		public bool PFAction(params object[] args)
		{
			int k;

			k = (int)args[0];
			if (k < 1 || k > PF_SZ)
			{
				this.telnet.Events.ShowError("PF_action: Invalid argument '" + args[0] + "'");
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
				this.telnet.Events.ShowError("PA_action: Invalid argument '" + args[0] + "'");
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



		bool WrapCharacter(int cgcode)
		{
			bool with_ge = false;
			bool pasting = false;

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
				Util.ControlSee((byte)Tables.Cg2Ascii[cgcode]));
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

			if (this.keyboardLock != 0)
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
			if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]) || FieldAttribute.IsProtected(favalue))
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}
			if (this.telnet.Appres.numeric_lock && FieldAttribute.IsNumeric(favalue) &&
				!((cgCode >= CharacterGenerator.Numeral0 && cgCode <= CharacterGenerator.Numeral9) ||
				cgCode == CharacterGenerator.Minus || cgCode == CharacterGenerator.Period))
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorNumeric);
				return false;
			}
			if (reverseMode || (insertMode && this.telnet.Controller.ScreenBuffer[address] != 0))
			{
				int last_blank = -1;

				//Find next null, next fa, or last blank
				endAddress = address;
				if (this.telnet.Controller.ScreenBuffer[endAddress] == CharacterGenerator.Space)
				{
					last_blank = endAddress;
				}
				do
				{
					this.telnet.Controller.IncrementAddress(ref endAddress);
					if (this.telnet.Controller.ScreenBuffer[endAddress] == CharacterGenerator.Space)
					{
						last_blank = endAddress;
					}
					if (this.telnet.Controller.ScreenBuffer[endAddress] == CharacterGenerator.Null || FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[endAddress]))
					{
						break;
					}
				} while (endAddress != address);

				//Pretend a trailing blank is a null, if desired.
				if (this.telnet.Appres.Toggled(Appres.BlankFill) && last_blank != -1)
				{
					this.telnet.Controller.IncrementAddress(ref last_blank);
					if (last_blank == endAddress)
					{
						this.telnet.Controller.DecrementAddress(ref endAddress);
						this.telnet.Controller.AddCharacter(endAddress, CharacterGenerator.Null, 0);
					}
				}

				//Check for field overflow.
				if (this.telnet.Controller.ScreenBuffer[endAddress] != CharacterGenerator.Null)
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
			if (this.telnet.Controller.Formatted && this.telnet.Appres.Toggled(Appres.BlankFill))
			{
				int addresSof = fa;//fa - this.telnet.tnctlr.screen_buf;
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
							if (this.telnet.Controller.ScreenBuffer[addressScan] != CharacterGenerator.Null)
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

					if (this.telnet.Controller.ScreenBuffer[addressFill] == CharacterGenerator.Null)
					{
						this.telnet.Controller.AddCharacter(addressFill, CharacterGenerator.Space, 0);
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
				} while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]));
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
			if (pasting || (cgCode != CharacterGenerator.dup))
			{
				while (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
				{
					if (FieldAttribute.IsSkip(this.telnet.Controller.ScreenBuffer[address]))
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
		void HandleAsciiCharacter(byte character, KeyType keytype, EIAction cause)
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

			trace.trace_event(" %s -> Key(\"%s\")\n", this.telnet.Action.ia_name[(int)cause], Util.ControlSee(character));
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
			this.telnet.Appres.ToggleTheValue(Appres.MonoCase);
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
			if (this.keyboardLock != 0)
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
			int baddr, nbaddr;
			int sbaddr;

			if (!this.telnet.Is3270)
			{
				return false;
			}

			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(BackTab_action), args);
				return true;
			}
			baddr = this.telnet.Controller.CursorAddress;
			this.telnet.Controller.DecrementAddress(ref baddr);
			if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[baddr]))	/* at bof */
			{
				this.telnet.Controller.DecrementAddress(ref baddr);
			}
			sbaddr = baddr;
			while (true)
			{
				nbaddr = baddr;
				this.telnet.Controller.IncrementAddress(ref nbaddr);
				if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[baddr])
					&& !FieldAttribute.IsProtected(this.telnet.Controller.ScreenBuffer[baddr])
					&& !FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[nbaddr]))
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
			if (!this.telnet.IsConnected)
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
			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(HomeAction), args);
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
			this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(this.telnet.Controller.RowCount * telnet.Controller.ColumnCount - 1));
			return true;
		}




		/// <summary>
		/// Cursor left 1 position.
		/// </summary>
		void MoveLeft()
		{
			int address = this.telnet.Controller.CursorAddress;
			this.telnet.Controller.DecrementAddress(ref address);
			this.telnet.Controller.SetCursorAddress(address);
		}


		public bool LeftAction(params object[] args)
		{
			if (this.keyboardLock != 0)
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
				int address = this.telnet.Controller.CursorAddress;
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
			int address;
			int endAddress;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];

				if (!FieldAttribute.IsProtected(fa))
				{
					//We're in an unprotected field, so it's okay to delete.
					this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
				}
			}


			if (FieldAttribute.IsProtected(fa) || FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			//Find next FA
			if (this.telnet.Controller.Formatted)
			{
				endAddress = address;
				do
				{
					this.telnet.Controller.IncrementAddress(ref endAddress);
					if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[endAddress]))
						break;
				} while (endAddress != address);

				this.telnet.Controller.DecrementAddress(ref endAddress);
			}
			else
			{
				if ((address % this.telnet.Controller.ColumnCount) == this.telnet.Controller.ColumnCount - 1)
				{
					return true;
				}
				endAddress = address + (this.telnet.Controller.ColumnCount - (address % this.telnet.Controller.ColumnCount)) - 1;
			}

			if (endAddress > address)
			{
				this.telnet.Controller.CopyBlock(address + 1, address, endAddress - address, false);
			}
			else if (endAddress != address)
			{
				this.telnet.Controller.CopyBlock(address + 1, address, ((this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount) - 1) - address, false);
				this.telnet.Controller.AddCharacter((this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount) - 1, this.telnet.Controller.ScreenBuffer[0], 0);
				this.telnet.Controller.CopyBlock(1, 0, endAddress, false);
			}

			this.telnet.Controller.AddCharacter(endAddress, CharacterGenerator.Null, 0);
			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			return false;
		}


		public bool DeleteAction(params object[] args)
		{
			if (this.keyboardLock != 0)
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
			{
				return false;
			}

			if (this.reverseMode)
			{
				int address = this.telnet.Controller.CursorAddress;

				this.telnet.Controller.DecrementAddress(ref address);
				if (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
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
			int address;
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
			if (faIndex == address || FieldAttribute.IsProtected(fa))
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

			if (this.keyboardLock != 0)
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
			int address;

			if (this.keyboardLock != 0)
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
			prot = FieldAttribute.IsProtectedAt(this.telnet.Controller.ScreenBuffer, address);

			//Skip to before this word, if in one now.
			if (!prot)
			{
				c = this.telnet.Controller.ScreenBuffer[address];
				while (!FieldAttribute.IsFA(c) && c != CharacterGenerator.Space && c != CharacterGenerator.Null)
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
				if (FieldAttribute.IsFA(c))
				{
					this.telnet.Controller.DecrementAddress(ref address);
					prot = FieldAttribute.IsProtectedAt(this.telnet.Controller.ScreenBuffer, address);
					continue;
				}
				if (!prot && c != CharacterGenerator.Space && c != CharacterGenerator.Null)
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
			while (!FieldAttribute.IsFA(c) && c != CharacterGenerator.Space && c != CharacterGenerator.Null);

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
			int address;

			if (this.keyboardLock != 0)
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

			prot = FieldAttribute.IsProtectedAt(this.telnet.Controller.ScreenBuffer, baseAddress);

			do
			{
				c = this.telnet.Controller.ScreenBuffer[baseAddress];
				if (FieldAttribute.IsFA(c))
				{
					prot = FieldAttribute.IsProtected(c);
				}
				else if (!prot && c != CharacterGenerator.Space && c != CharacterGenerator.Null)
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
				if (FieldAttribute.IsFA(c))
				{
					return -1;
				}

				if (inWord)
				{
					if (c == CharacterGenerator.Space || c == CharacterGenerator.Null)
					{
						inWord = false;
					}
				}
				else
				{
					if (c != CharacterGenerator.Space && c != CharacterGenerator.Null)
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
			int address;
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
			if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[telnet.Controller.CursorAddress]) ||
				FieldAttribute.IsProtectedAt(this.telnet.Controller.ScreenBuffer, this.telnet.Controller.CursorAddress))
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
			if (c != CharacterGenerator.Space && c != CharacterGenerator.Null)
			{
				address = this.telnet.Controller.CursorAddress;
				do
				{
					c = this.telnet.Controller.ScreenBuffer[address];
					if (c == CharacterGenerator.Space || c == CharacterGenerator.Null)
					{
						this.telnet.Controller.SetCursorAddress(address);
						return true;
					}
					else if (FieldAttribute.IsFA(c))
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
			int address;

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
			int address;

			if (this.keyboardLock != 0)
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
			int address;
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
			if (faIndex != address && !FieldAttribute.IsProtected(fa))
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
			if (HandleOrdinaryCharacter(CharacterGenerator.dup, false, false))
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
			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(FieldMarkAction), args);
				return true;
			}
			if (this.telnet.IsAnsi)
			{
				return false;
			}
			this.HandleOrdinaryCharacter(CharacterGenerator.fm, false, false);
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

			if (this.keyboardLock != 0 && this.telnet.IsConnected)
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

			if (!FieldAttribute.IsSelectable(fa))
			{
				//ring_bell();
				return;
			}

			sel = this.telnet.Controller.ScreenBuffer[faIndex + 1];

			designator = faIndex + 1;

			switch (sel)
			{
				case CharacterGenerator.GreaterThan:		/* > */
					this.telnet.Controller.AddCharacter(designator, CharacterGenerator.QuestionMark, 0); /* change to ? */
					this.telnet.Controller.MDTClear(this.telnet.Controller.ScreenBuffer, faIndex);
					break;
				case CharacterGenerator.QuestionMark:		/* ? */
					this.telnet.Controller.AddCharacter(designator, CharacterGenerator.GreaterThan, 0);	/* change to > */
					this.telnet.Controller.MDTClear(this.telnet.Controller.ScreenBuffer, faIndex);
					break;
				case CharacterGenerator.Space:		/* space */
				case CharacterGenerator.Null:		/* null */
					this.HandleAttentionIdentifierKey(AID.SELECT);
					break;
				case CharacterGenerator.Ampersand:		/* & */
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

			if (this.keyboardLock != 0)
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
		public bool EraseEndOfFieldAction(params object[] args)
		{
			int address;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;


			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(EraseEndOfFieldAction), args);
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

			if (FieldAttribute.IsProtected(fa) || FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			if (this.telnet.Controller.Formatted)
			{
				//Erase to next field attribute
				do
				{
					this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
					this.telnet.Controller.IncrementAddress(ref address);
				} while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]));

				this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			}
			else
			{
				//Erase to end of screen
				do
				{
					this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
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


			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(this.EraseInputAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			if (this.telnet.Controller.Formatted)
			{
				//Find first field attribute
				address = 0;
				do
				{
					if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
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
					if (!FieldAttribute.IsProtected(fa))
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

							if (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
							{
								this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
							}
						} while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]));
					}
					else
					{	/* skip protected */
						do
						{
							this.telnet.Controller.IncrementAddress(ref address);
						} while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]));

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
			int address, address2, frontAddress, backAddress, endAddress;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;



			if (this.keyboardLock != 0)
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
			if (FieldAttribute.IsProtected(fa) || FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			//Search backwards for a non-blank character.
			frontAddress = address;
			while (this.telnet.Controller.ScreenBuffer[frontAddress] == CharacterGenerator.Space ||
				this.telnet.Controller.ScreenBuffer[frontAddress] == CharacterGenerator.Null)
			{
				this.telnet.Controller.DecrementAddress(ref frontAddress);
			}

			//If we ran into the edge of the field without seeing any non-blanks,
			//there isn't any word to delete; just move the cursor. 
			if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[frontAddress]))
			{
				this.telnet.Controller.SetCursorAddress(frontAddress + 1);
				return true;
			}

			//FrontAddress is now pointing at a non-blank character.  Now search for the first blank to the left of that
			//(or the edge of the field), leaving frontAddress pointing at the the beginning of the word.
			while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[frontAddress]) &&
				this.telnet.Controller.ScreenBuffer[frontAddress] != CharacterGenerator.Space &&
				this.telnet.Controller.ScreenBuffer[frontAddress] != CharacterGenerator.Null)
			{
				this.telnet.Controller.DecrementAddress(ref frontAddress);
			}

			this.telnet.Controller.IncrementAddress(ref frontAddress);

			//Find the end of the word, searching forward for the edge of the field or a non-blank.
			backAddress = frontAddress;
			while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[backAddress]) &&
				this.telnet.Controller.ScreenBuffer[backAddress] != CharacterGenerator.Space &&
				this.telnet.Controller.ScreenBuffer[backAddress] != CharacterGenerator.Null)
			{
				this.telnet.Controller.IncrementAddress(ref backAddress);
			}

			//Find the start of the next word, leaving back_baddr pointing at it or at the end of the field.
			while (this.telnet.Controller.ScreenBuffer[backAddress] == CharacterGenerator.Space ||
				this.telnet.Controller.ScreenBuffer[backAddress] == CharacterGenerator.Null)
			{
				this.telnet.Controller.IncrementAddress(ref backAddress);
			}

			// Find the end of the field, leaving end_baddr pointing at the field attribute of the start of the next field.
			endAddress = backAddress;
			while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[endAddress]))
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
				this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
				this.telnet.Controller.IncrementAddress(ref address);
			}

			// Set the MDT and move the cursor.
			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			this.telnet.Controller.SetCursorAddress(frontAddress);
			return true;
		}



		/// <summary>
		/// Delete field key.  Similar to EraseEOF, but it wipes out the entire field rather than just 
		/// to the right of the cursor, and it leaves the cursor at the front of the field.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool DeleteFieldAction(params object[] args)
		{
			int address;
			byte fa = this.telnet.Controller.FakeFA;
			int faIndex;


			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(DeleteFieldAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				this.telnet.SendKill();
				return true;
			}

			if (!telnet.Controller.Formatted)
			{
				return false;
			}

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}

			if (FieldAttribute.IsProtected(fa) || FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				this.telnet.Controller.DecrementAddress(ref address);
			}

			this.telnet.Controller.IncrementAddress(ref address);
			this.telnet.Controller.SetCursorAddress(address);

			while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
				this.telnet.Controller.IncrementAddress(ref address);
			}

			this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
			return true;
		}



		/// <summary>
		/// Set insert mode key.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool InsertAction(params object[] args)
		{

			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(this.InsertAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			this.insertMode = true;
			return true;
		}



		/// <summary>
		/// Toggle insert mode key.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool ToggleInsertAction(params object[] args)
		{

			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(this.ToggleInsertAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			if (insertMode)
			{
				this.insertMode = false;
			}
			else
			{
				this.insertMode = true;
			}

			return true;
		}



		/// <summary>
		/// Toggle reverse mode key.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool ToggleReverseAction(params object[] args)
		{

			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(this.ToggleReverseAction), args);
				return true;
			}

			if (this.telnet.IsAnsi)
			{
				return false;
			}

			this.reverseMode = !this.reverseMode;
			return true;
		}



		/// <summary>
		/// Move the cursor to the first blank after the last nonblank in the field, or if the field is full, to the last character in the field.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool FieldEndAction(params object[] args)
		{
			int address;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;
			byte c;
			int lastNonBlank = -1;


			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(FieldEndAction), args);
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

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}
			//
			if (faIndex == this.telnet.Controller.ScreenBuffer[address] || FieldAttribute.IsProtected(fa))
			{
				return false;
			}

			address = faIndex;
			while (true)
			{
				this.telnet.Controller.IncrementAddress(ref address);
				c = this.telnet.Controller.ScreenBuffer[address];
				if (FieldAttribute.IsFA(c))
				{
					break;
				}
				if (c != CharacterGenerator.Null && c != CharacterGenerator.Space)
				{
					lastNonBlank = address;
				}
			}

			if (lastNonBlank == -1)
			{
				address = faIndex;// - this.telnet.tnctlr.screen_buf;
				this.telnet.Controller.IncrementAddress(ref address);
			}
			else
			{
				address = lastNonBlank;
				this.telnet.Controller.IncrementAddress(ref address);
				if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
				{
					address = lastNonBlank;
				}
			}
			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}


		/// <summary>
		/// MoveCursor action.  Depending on arguments, this is either a move to the mouse cursor position, or to an absolute location.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool MoveCursorAction(params object[] args)
		{
			int address;
			int row, col;


			if (this.keyboardLock != 0)
			{
				if (args.Length == 2)
				{
					this.EnqueueTypeAheadAction(new ActionDelegate(this.MoveCursorAction), args);
				}
				return true;
			}

			if (args.Length == 2)
			{
				//Probably a macro call
				row = (int)args[0];
				col = (int)args[1];

				if (!telnet.Is3270)
				{
					row--;
					col--;
				}

				if (row < 0)
				{
					row = 0;
				}

				if (col < 0)
				{
					col = 0;
				}

				address = ((row * this.telnet.Controller.ColumnCount) + col) % (this.telnet.Controller.RowCount * this.telnet.Controller.ColumnCount);
				this.telnet.Controller.SetCursorAddress(address);
			}
			else
			{
				//Couldn't say
				this.telnet.Events.ShowError("MoveCursor_action requires 0 or 2 arguments");
			}

			return true;
		}



		/// <summary>
		/// Key action.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool SendKeyAction(params object[] args)
		{
			int i;
			int k;
			KeyType keytype;


			for (i = 0; i < args.Length; i++)
			{
				string s = args[i] as String;

				k = StringToKeySymbol(s, out keytype);
				if (k == KeyboardConstants.NoSymbol)
				{
					this.telnet.Events.ShowError("SendKey action: Nonexistent or invalid KeySym: " + s);
					continue;
				}
				if ((k & ~0xff) != 0)
				{
					this.telnet.Events.ShowError("SendKey action: Invalid KeySym: " + s);
					continue;
				}
				this.HandleAsciiCharacter((byte)(k & 0xff), keytype, EIAction.Key);
			}
			return true;
		}


		/// <summary>
		/// Translate a keysym name to a keysym, including APL and extended characters.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="keytypep"></param>
		/// <returns></returns>
		int StringToKeySymbol(string s, out KeyType keytypep)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// String action.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool SendStringAction(params object[] args)
		{
			int i;
			string s = "";
			for (i = 0; i < args.Length; i++)
			{
				s += (string)args[i];
			}

			// Set a pending string.
			PsSet(s, false);
			bool ok = !telnet.Events.IsError();

			if (!ok && this.telnet.Config.ThrowExceptionOnLockedScreen)
			{
				throw new ApplicationException(this.telnet.Events.GetErrorAsText());
			}

			return ok;
		}



		public bool HexStringAction(params object[] args)
		{
			int i;
			string s = "";
			string t;

			for (i = 0; i < args.Length; i++)
			{
				t = (string)args[i];
				if (t.Length > 2 && (t.Substring(0, 2) == "0x" || t.Substring(0, 2) == "0X"))
				{
					t = t.Substring(2);
				}
				s += t;
			}
			if (s.Length == 0)
			{
				return false;
			}

			// Set a pending string.
			PsSet(s, true);
			return true;
		}


		/// <summary>
		/// Dual-mode action for the "asciicircum" ("^") key:
		/// If in ANSI mode, pass through untranslated.
		/// If in 3270 mode, translate to "notsign".
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool CircumNotAction(params object[] args)
		{
			if (this.telnet.Is3270 && composing == Composing.None)
			{
				HandleAsciiCharacter(0xac, KeyType.Standard, EIAction.Key);
			}
			else
			{
				HandleAsciiCharacter((byte)'^', KeyType.Standard, EIAction.Key);
			}
			return true;
		}

		/// <summary>
		/// PA key action for String actions
		/// </summary>
		/// <param name="n"></param>
		void DoPA(int n)
		{
			if (n < 1 || n > PA_SZ)
			{
				this.telnet.Events.ShowError("Unknown PA key %d", n);
				return;
			}
			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(PAAction), n.ToString());
				return;
			}
			this.HandleAttentionIdentifierKey(KeyboardConstants.PaTranslation[n - 1]);
		}

		/// <summary>
		/// PF key action for String actions
		/// </summary>
		/// <param name="n"></param>
		void DoFunctionKey(int n)
		{
			if (n < 1 || n > PF_SZ)
			{
				this.telnet.Events.ShowError("Unknown PF key %d", n);
				return;
			}
			if (this.keyboardLock != 0)
			{

				this.EnqueueTypeAheadAction(new ActionDelegate(PFAction), n.ToString());
				return;
			}
			this.HandleAttentionIdentifierKey(KeyboardConstants.PfTranslation[n - 1]);
		}


		/// <summary>
		///  Set or clear the keyboard scroll lock.
		/// </summary>
		/// <param name="lockflag"></param>
		void ToggleScrollLock(bool lockflag)
		{
			if (telnet.Is3270)
			{
				if (lockflag)
				{
					this.KeyboardLockSet(KeyboardConstants.Scrolled, "ToggleScrollLock");
				}
				else
				{
					this.KeyboardLockClear(KeyboardConstants.Scrolled, "ToggleScrollLock");
				}
			}
		}


		/// <summary>
		/// Move the cursor back within the legal paste area.
		/// </summary>
		/// <param name="lMargin"></param>
		/// <returns>Returns a bool indicating success.</returns>
		bool RemarginCursor(int lMargin)
		{
			bool ever = false;
			int address = 0;
			int b0 = 0;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;


			address = this.telnet.Controller.CursorAddress;
			while (this.telnet.Controller.AddressToColumn(address) < lMargin)
			{
				address = this.telnet.Controller.RowColumnToByteAddress(this.telnet.Controller.AddresstoRow(address), lMargin);
				if (!ever)
				{
					b0 = address;
					ever = true;
				}
				faIndex = this.telnet.Controller.GetFieldAttribute(address);
				if (faIndex != -1)
					fa = this.telnet.Controller.ScreenBuffer[faIndex];

				if (faIndex == address || FieldAttribute.IsProtected(fa))
				{
					address = this.telnet.Controller.GetNextUnprotectedField(address);
					if (address <= b0)
						return false;
				}
			}

			this.telnet.Controller.SetCursorAddress(address);
			return true;
		}



		/// <summary>
		/// Pretend that a sequence of keys was entered at the keyboard.
		/// "Pasting" means that the sequence came from the X clipboard.  Returns are ignored; newlines mean
		/// "move to beginning of next line"; tabs and formfeeds become spaces.  Backslashes are not special, 
		/// but ASCII ESC characters are used to signify 3270 Graphic Escapes.
		/// "Not pasting" means that the sequence is a login string specified in the hosts file, or a parameter
		/// to the String action.  Returns are "move to beginning of next line"; newlines mean "Enter AID" and 
		/// the termination of processing the string.  Backslashes are processed as in C.
		/// </summary>
		/// <param name="args"></param>
		/// <returns>Returns the number of unprocessed characters.</returns>
		public bool EmulateInputAction(params object[] args)
		{
			StringBuilder sb = new StringBuilder();
			int i;
			for (i = 0; i < args.Length; i++)
			{
				sb.Append(args[i].ToString());
			}
			EmulateInput(sb.ToString(), false);
			return true;
		}



		int EmulateInput(string s, bool pasting)
		{
			char c;

			EIState state = EIState.Base;
			int literal = 0;
			int nc = 0;
			EIAction ia = pasting ? EIAction.Paste : EIAction.String;
			int originalAddress = this.telnet.Controller.CursorAddress;
			int originalColumn = this.telnet.Controller.AddressToColumn(this.telnet.Controller.CursorAddress);
			int length = s.Length;


			//In the switch statements below, "break" generally means "consume this character," while "continue" means "rescan this character."
			while (s.Length > 0)
			{

				//It isn't possible to unlock the keyboard from a string, so if the keyboard is locked, it's fatal
				if (this.keyboardLock != 0)
				{
					this.telnet.Trace.trace_event("  keyboard locked, string dropped. kybdlock=" + keyboardLock + "\n");
					if (this.telnet.Config.ThrowExceptionOnLockedScreen)
					{
						throw new ApplicationException("Keyboard locked typing data onto screen - data was lost.  Turn of configuration option 'ThrowExceptionOnLockedScreen' to ignore this exception.");
					}
					return 0;
				}

				if (pasting && this.telnet.Is3270)
				{

					// Check for cursor wrap to top of screen
					if (this.telnet.Controller.CursorAddress < originalAddress)
					{
						// Wrapped
						return length - 1;
					}

					// Jump cursor over left margin.
					if (this.telnet.Appres.Toggled(Appres.MarginedPaste) &&
						this.telnet.Controller.AddressToColumn(this.telnet.Controller.CursorAddress) < originalColumn)
					{
						if (!RemarginCursor(originalColumn))
						{
							return length - 1;
						}
					}
				}

				c = s[0];

				switch (state)
				{
					case EIState.Base:
						switch ((char)c)
						{
							case '\b':
								{
									this.action.action_internal(new ActionDelegate(LeftAction), ia);
									continue;
								}
							case '\f':
								{
									if (pasting)
									{
										this.HandleAsciiCharacter((byte)' ', KeyType.Standard, ia);
									}
									else
									{
										this.action.action_internal(new ActionDelegate(ClearAction), ia);
										if (this.telnet.Is3270)
										{
											return length - 1;
										}
										else
										{
											break;
										}
									}
									break; // mfw - added BUGBUG
								}
							case '\n':
								{
									if (pasting)
									{
										this.action.action_internal(new ActionDelegate(MoveCursorToNewLine), ia);
									}
									else
									{
										this.action.action_internal(new ActionDelegate(EnterAction), ia);
										if (this.telnet.Is3270)
											return length - 1;
									}
									break;
								}
							case '\r':
								{
									// Ignored
									break;
								}
							case '\t':
								{
									this.action.action_internal(new ActionDelegate(TabForwardAction), ia);
									break;
								}
							case '\\':
								{
									// Backslashes are NOT special when pasting
									if (!pasting)
									{
										state = EIState.Backslash;
									}
									else
									{
										this.HandleAsciiCharacter((byte)c, KeyType.Standard, ia);
									}
									break;
								}
							case (char)0x1b: /* ESC is special only when pasting */
								{
									if (pasting)
									{
										state = EIState.XGE;
									}
									break;
								}
							case '[':
								{
									// APL left bracket

									//MFW 
									/* if (pasting && appres.apl_mode)
									   key_ACharacter((byte) XK_Yacute,KT_GE, ia);
							   else*/
									this.HandleAsciiCharacter((byte)c, KeyType.Standard, ia);
									break;
								}
							case ']':
								{
									// APL right bracket

									//MFW 
									/* if (pasting && appres.apl_mode)
									   key_ACharacter((byte) XK_diaeresis, KT_GE, ia);
							   else*/
									this.HandleAsciiCharacter((byte)c, KeyType.Standard, ia);
									break;
								}
							default:
								{
									this.HandleAsciiCharacter((byte)c, KeyType.Standard, ia);
									break;
								}
						}
						break;
					case EIState.Backslash:
						{
							//Last character was a backslash */
							switch ((char)c)
							{
								case 'a':
									{
										this.telnet.Events.ShowError("String_action: Bell not supported");
										state = EIState.Base;
										break;
									}
								case 'b':
									{
										this.action.action_internal(new ActionDelegate(LeftAction), ia);
										state = EIState.Base;
										break;
									}
								case 'f':
									{
										this.action.action_internal(new ActionDelegate(ClearAction), ia);
										state = EIState.Base;
										if (this.telnet.Is3270)
										{
											return length - 1;
										}
										else
										{
											break;
										}
									}
								case 'n':
									{
										this.action.action_internal(new ActionDelegate(EnterAction), ia);
										state = EIState.Base;
										if (this.telnet.Is3270)
											return length - 1;
										else
											break;
									}
								case 'p':
									{
										state = EIState.BackP;
										break;
									}
								case 'r':
									{
										this.action.action_internal(new ActionDelegate(MoveCursorToNewLine), ia);
										state = EIState.Base;
										break;
									}
								case 't':
									{
										this.action.action_internal(new ActionDelegate(TabForwardAction), ia);
										state = EIState.Base;
										break;
									}
								case 'T':
									{
										this.action.action_internal(new ActionDelegate(BackTab_action), ia);
										state = EIState.Base;
									}
									break;
								case 'v':
									{
										this.telnet.Events.ShowError("String_action: Vertical tab not supported");
										state = EIState.Base;
										break;
									}
								case 'x':
									{
										state = EIState.BackX;
										break;
									}
								case '\\':
									{
										this.HandleAsciiCharacter((byte)c, KeyType.Standard, ia);
										state = EIState.Base;
										break;
									}
								case '0':
								case '1':
								case '2':
								case '3':
								case '4':
								case '5':
								case '6':
								case '7':
									{
										state = EIState.Octal;
										literal = 0;
										nc = 0;
										continue;
									}
								default:
									{
										state = EIState.Base;
										continue;
									}
							}
							break;
						}
					case EIState.BackP:
						{
							// Last two characters were "\p"
							switch ((char)c)
							{
								case 'a':
									{
										literal = 0;
										nc = 0;
										state = EIState.BackPA;
										break;
									}
								case 'f':
									{
										literal = 0;
										nc = 0;
										state = EIState.BackPF;
										break;
									}
								default:
									{
										this.telnet.Events.ShowError("StringAction: Unknown character after \\p");
										state = EIState.Base;
										break;
									}
							}
							break;
						}
					case EIState.BackPF:
						{
							// Last three characters were "\pf"
							if (nc < 2 && IsDigit(c))
							{
								literal = (literal * 10) + (c - '0');
								nc++;
							}
							else if (nc == 0)
							{
								this.telnet.Events.ShowError("StringAction: Unknown character after \\pf");
								state = EIState.Base;
							}
							else
							{
								this.DoFunctionKey(literal);
								if (this.telnet.Is3270)
								{
									return length - 1;
								}
								state = EIState.Base;
								continue;
							}
							break;
						}
					case EIState.BackPA:
						{
							// Last three characters were "\pa"
							if (nc < 1 && IsDigit(c))
							{
								literal = (literal * 10) + (c - '0');
								nc++;
							}
							else if (nc == 0)
							{
								this.telnet.Events.ShowError("String_action: Unknown character after \\pa");
								state = EIState.Base;
							}
							else
							{
								this.DoPA(literal);
								if (this.telnet.Is3270)
								{
									return length - 1;
								}
								state = EIState.Base;
								continue;
							}
							break;
						}
					case EIState.BackX:
						{
							// Last two characters were "\x"
							if (IsXDigit(c))
							{
								state = EIState.Hex;
								literal = 0;
								nc = 0;
								continue;
							}
							else
							{
								this.telnet.Events.ShowError("String_action: Missing hex digits after \\x");
								state = EIState.Base;
								continue;
							}
						}
					case EIState.Octal:
						{
							// Have seen \ and one or more octal digits
							if (nc < 3 && IsDigit(c) && c < '8')
							{
								literal = (literal * 8) + FromHex(c);
								nc++;
								break;
							}
							else
							{
								this.HandleAsciiCharacter((byte)literal, KeyType.Standard, ia);
								state = EIState.Base;
								continue;
							}
						}
					case EIState.Hex:
						{
							// Have seen \ and one or more hex digits
							if (nc < 2 && IsXDigit(c))
							{
								literal = (literal * 16) + FromHex(c);
								nc++;
								break;
							}
							else
							{
								this.HandleAsciiCharacter((byte)literal, KeyType.Standard, ia);
								state = EIState.Base;
								continue;
							}
						}
					case EIState.XGE:
						{
							//Have seen ESC
							switch ((char)c)
							{
								case ';':
									{
										// FM
										this.HandleOrdinaryCharacter(CharacterGenerator.fm, false, true);
										break;
									}
								case '*':
									{
										// DUP
										this.HandleOrdinaryCharacter(CharacterGenerator.dup, false, true);
										break;
									}
								default:
									{
										this.HandleAsciiCharacter((byte)c, KeyType.GE, ia);
										break;
									}
							}
							state = EIState.Base;
							break;
						}
				}
				s = s.Substring(1);
				//s++;
				length--;
			}

			switch (state)
			{
				case EIState.Octal:
				case EIState.Hex:
					{
						this.HandleAsciiCharacter((byte)literal, KeyType.Standard, ia);
						state = EIState.Base;
						break;
					}
				case EIState.BackPF:
					if (nc > 0)
					{
						this.DoFunctionKey(literal);
						state = EIState.Base;
					}
					break;
				case EIState.BackPA:
					if (nc > 0)
					{
						this.DoPA(literal);
						state = EIState.Base;
					}
					break;
				default:
					break;
			}

			if (state != EIState.Base)
			{
				this.telnet.Events.ShowError("String_action: Missing data after \\");
			}

			return length;
		}




		/// <summary>
		/// Pretend that a sequence of hexadecimal characters was entered at the keyboard.  The input is a sequence
		///	of hexadecimal bytes, 2 characters per byte.  If connected in ANSI mode, these are treated as ASCII 
		/// characters; if in 3270 mode, they are considered EBCDIC.
		/// 
		/// Graphic Escapes are handled as \E.
		/// </summary>
		/// <param name="s"></param>
		void HexInput(string s)
		{

			bool escaped;
			byte[] xBuffer = null;
			int bufferIndex = 0;
			int byteCount = 0;
			int index = 0;
			escaped = false;

			// Validate the string.
			if ((s.Length % 2) != 0)
			{
				this.telnet.Events.ShowError("HexStringAction: Odd number of characters in specification");
				return;
			}

			while (index < s.Length)
			{
				if (this.IsXDigit(s[index]) && this.IsXDigit(s[index + 1]))
				{
					escaped = false;
					byteCount++;
				}
				else if (s.Substring(index, 2).ToLower() == "\\e")
				{
					if (escaped)
					{
						this.telnet.Events.ShowError("HexString_action: Double \\E");
						return;
					}
					if (!telnet.Is3270)
					{
						this.telnet.Events.ShowError("HexString_action: \\E in ANSI mode");
						return;
					}
					escaped = true;
				}
				else
				{
					this.telnet.Events.ShowError("HexString_action: Illegal character in specification");
					return;
				}
				index += 2;
			}
			if (escaped)
			{
				this.telnet.Events.ShowError("HexString_action: Nothing follows \\E");
				return;
			}

			// Allocate a temporary buffer.
			if (!telnet.Is3270 && byteCount != 0)
			{
				xBuffer = new byte[byteCount];
				bufferIndex = 0;
			}

			// Fill it
			index = 0;
			escaped = false;
			while (index < s.Length)
			{
				if (this.IsXDigit(s[index]) && this.IsXDigit(s[index + 1]))
				{
					byte c;

					c = (byte)((this.FromHex(s[index]) * 16) + this.FromHex(s[index + 1]));
					if (this.telnet.Is3270)
					{
						this.HandleOrdinaryCharacter(Tables.Ebc2Cg[c], escaped, true);
					}
					else
					{
						xBuffer[bufferIndex++] = (byte)c;
					}
					escaped = false;
				}
				else if (s.Substring(index, 2).ToLower() == "\\e")
				{
					escaped = true;
				}
				index += 2;
			}
			if (!telnet.Is3270 && byteCount != 0)
			{
				this.telnet.SendHexAnsiOut(xBuffer, byteCount);
			}
		}






		/// <summary>
		/// FieldExit for the 5250-like emulation.
		/// Erases from the current cursor position to the end of the field, and moves the cursor to the beginning of the next field.
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public bool FieldExitAction(params object[] args)
		{
			int address;
			int faIndex;
			byte fa = this.telnet.Controller.FakeFA;


			if (this.telnet.IsAnsi)
			{
				this.telnet.SendChar('\n');
				return true;
			}

			if (this.keyboardLock != 0)
			{
				this.EnqueueTypeAheadAction(new ActionDelegate(FieldExitAction), args);
				return true;
			}

			address = this.telnet.Controller.CursorAddress;
			faIndex = this.telnet.Controller.GetFieldAttribute(address);

			if (faIndex != -1)
			{
				fa = this.telnet.Controller.ScreenBuffer[faIndex];
			}

			if (FieldAttribute.IsProtected(fa) || FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
			{
				this.HandleOperatorError(address, KeyboardConstants.ErrorProtected);
				return false;
			}

			if (this.telnet.Controller.Formatted)
			{
				//Erase to next field attribute
				do
				{
					this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
					this.telnet.Controller.IncrementAddress(ref address);
				} while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]));

				this.telnet.Controller.SetMDT(this.telnet.Controller.ScreenBuffer, faIndex);
				this.telnet.Controller.SetCursorAddress(this.telnet.Controller.GetNextUnprotectedField(this.telnet.Controller.CursorAddress));
			}
			else
			{
				// Erase to end of screen
				do
				{
					this.telnet.Controller.AddCharacter(address, CharacterGenerator.Null, 0);
					this.telnet.Controller.IncrementAddress(ref address);
				} while (address != 0);
			}
			return true;
		}



		public bool FieldsAction(params object[] args)
		{
			byte fa = this.telnet.Controller.FakeFA;

			int fieldpos = 0;
			int index = 0;
			int end;

			do
			{
				int newfield = this.telnet.Controller.GetNextUnprotectedField(fieldpos);

				if (newfield <= fieldpos)
				{
					break;
				}

				end = newfield;
				while (!FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[end]))
				{
					this.telnet.Controller.IncrementAddress(ref end);
					if (end == 0)
					{
						end = (this.telnet.Controller.ColumnCount * telnet.Controller.RowCount) - 1;
						break;
					}
				}

				this.telnet.Action.action_output("data: field[" + index + "] at " + newfield + " to " + end + " (x=" + telnet.Controller.AddressToColumn(newfield) + ", y=" + telnet.Controller.AddresstoRow(newfield) + ", len=" + (end - newfield + 1) + ")\n");

				index++;
				fieldpos = newfield;

			} while (true);

			return true;
		}



		public bool FieldGetAction(params object[] args)
		{
			int fieldnumber = (int)args[0];
			int fieldpos = 0;
			int index = 0;

			if (!telnet.Controller.Formatted)
			{
				this.telnet.Events.ShowError("FieldGet: Screen is not formatted");
				return false;
			}

			do
			{
				int newfield = this.telnet.Controller.GetNextUnprotectedField(fieldpos);
				if (newfield <= fieldpos)
				{
					break;
				}

				if (fieldnumber == index)
				{
					byte fa = this.telnet.Controller.FakeFA;
					int faIndex;
					int start;
					int address;
					int length = 0;

					faIndex = this.telnet.Controller.GetFieldAttribute(newfield);
					if (faIndex != -1)
					{
						fa = this.telnet.Controller.ScreenBuffer[faIndex];
					}

					start = faIndex;
					this.telnet.Controller.IncrementAddress(ref start);
					address = start;

					do
					{
						if (FieldAttribute.IsFA(this.telnet.Controller.ScreenBuffer[address]))
						{
							break;
						}

						length++;
						this.telnet.Controller.IncrementAddress(ref address);

					} while (address != start);

					this.telnet.Controller.DumpRange(start, length, true, this.telnet.Controller.ScreenBuffer, this.telnet.Controller.RowCount, this.telnet.Controller.ColumnCount);

					return true;
				}

				index++;
				fieldpos = newfield;

			} while (true);

			this.telnet.Events.ShowError("FieldGet: Field %d not found", fieldnumber);
			return true;
		}



		public bool FieldSetAction(params object[] args)
		{

			int fieldnumber = (int)args[0];
			string fielddata = (string)args[1];
			int fieldpos = 0;
			int index = 0;

			byte fa = this.telnet.Controller.FakeFA;

			if (!telnet.Controller.Formatted)
			{
				this.telnet.Events.ShowError("FieldSet: Screen is not formatted");
				return false;
			}

			do
			{
				int newfield = this.telnet.Controller.GetNextUnprotectedField(fieldpos);
				if (newfield <= fieldpos)
				{
					break;
				}

				if (fieldnumber == index)
				{
					this.telnet.Controller.CursorAddress = newfield;
					this.DeleteFieldAction(null, null, null, 0);
					this.PsSet(fielddata, false);

					return true;
				}

				index++;
				fieldpos = newfield;

			} while (true);

			this.telnet.Events.ShowError("FieldGet: Field %d not found", fieldnumber);
			return true;
		}












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




	}
}
