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
using Open3270.TN3270;
using Open3270;


namespace Open3270.TN3270
{
	internal delegate void RunScriptDelegate(string where);

	internal enum KeyboardOp
	{
		Reset,
		AID,
		ATTN,
		Home

	}
	internal class TN3270API
	{
		public event RunScriptDelegate RunScriptEvent;
		public event OnDisconnectDelegate OnDisconnect;
		Telnet tn;
		bool mDebug = false;
        bool mUseSSL = false;

        public bool UseSSL { get { return mUseSSL; } set { mUseSSL = value; } }
		internal TN3270API()
		{
			tn = null;
		}
		public bool Connect(IAudit audit, string host, int port, string lu, ConnectionConfig config)
		{
			tn = new Telnet(this, audit, config);
            tn.UseSSL = mUseSSL;
			tn.trace.optionTraceAnsi = mDebug;
			tn.trace.optionTraceDS = mDebug;
			tn.trace.optionTraceDSN = mDebug;
			tn.trace.optionTraceEvent = mDebug;
			tn.trace.optionTraceNetworkData = mDebug;
			tn.telnetDataEvent += new TelnetDataDelegate(tn_telnetDataEvent);
			if (lu==null || lu.Length==0)
				tn.lus = null;
			else
			{
				tn.lus = new System.Collections.ArrayList();
				tn.lus.Add(lu);
			}
			
			tn.Connect(this, host, port);
			if (!tn.WaitForConnect())
			{
				tn.Disconnect();
				string text = tn.DisconnectReason;
				tn = null;
				throw new TNHostException("connect to "+host+" on port "+port+" failed", text, null);
				//return false;
			}
			tn.trace.WriteLine("--connected");
			return true;
		}
		public void Disconnect()
		{
			if (tn != null)
			{
				tn.Disconnect();
				tn = null;
			}
		}
		internal string DisconnectReason
		{
			get { if (this.tn != null) return this.tn.DisconnectReason; else return null;}
		}
		public bool IsConnected
		{
			get
			{
				if (tn != null && tn.IsSocketConnected)
					return true;
				else
					return false;
			}
		}
		internal bool ShowParseError
		{
			set { if (tn != null) tn.mShowParseError = value; }
		}
		public bool Debug
		{
			set 
			{
				mDebug = value;
			}
		}
		public bool ExecuteAction(bool submit, string name, params object[] args)
		{
			lock (tn)
			{
				return tn.action.Execute(submit, name, args);
			}
		}

		public bool KeyboardCommandCausesSubmit(string name, params object[] args)
		{
			return tn.action.KeyboardCommandCausesSubmit(name, args);
		}

		public bool WaitForConnect(int timeout)
		{
			bool ok = tn.WaitFor(sms_state.SS_CONNECT_WAIT, timeout);
			if (ok)
			{
				// check we actually connected
				if (!tn.CONNECTED)
					return false;
			}
			return ok;
		}
		public bool Wait(int timeout)
		{
			return tn.WaitFor(sms_state.SS_KBWAIT, timeout);
		}
		public string GetStringData(int index)
		{
			lock (tn)
			{

				return tn.action.GetStringData(index);
			}
		}
		public string GetAllStringData(bool crlf)
		{
			lock (tn)
			{

				StringBuilder builder = new StringBuilder();
				int index = 0;
				string temp;
				while ((temp=tn.action.GetStringData(index))!=null)
				{
					builder.Append(temp);
					if (crlf)
						builder.Append("\n");
					index++;
				}
				return builder.ToString();
			}
		}
		public bool SendKeyOp(KeyboardOp op, string key)
		{
			lock (tn)
			{

				// these can go to screen that is locked			
				switch (op)
				{
					case KeyboardOp.Reset:
						//tn.tnctlr.do_reset();
						return true;
					default:
						break;
				}

				if ((tn.keyboard.kybdlock & Keyboard.KL_OIA_MINUS)!=0)
				{
					return false;
				}
				else if (tn.keyboard.kybdlock !=0)
				{
					return false;
					//enq_ta(Enter_action, CN, CN);
				}
				else
				{
					// these need unlocked screen
					switch (op)
					{
						case KeyboardOp.AID:
							byte v = (byte)typeof(AID).GetField(key).GetValue(null);
							tn.keyboard.key_AID(v);
							return true;
						case KeyboardOp.Home:

							if (tn.IN_ANSI) 
							{
								Console.WriteLine("IN_ANSI Home key not supported");
								//ansi_send_home();
								return false;
							}

							if (!tn.tnctlr.formatted) 
							{
								tn.tnctlr.cursor_move(0);
								return true;
							}
							tn.tnctlr.cursor_move(tn.tnctlr.next_unprotected(tn.tnctlr.ROWS*tn.tnctlr.COLS-1));
							return true;
						case KeyboardOp.ATTN:
							tn.net_interrupt();
							return true;
					}
					throw new ApplicationException("Sorry, key '"+key+"'not known");
				}
			}
		}
		public bool SendText(string text, bool paste)
		{
			lock (tn)
			{

				bool ok = true;
				int i;
				if (text==null)
					return ok;
				//
				for (i=0; i<text.Length; i++)
				{
					ok = tn.keyboard.key_Character(text[i], false, paste);
					if (!ok)
						break;
				}
				return ok;
			}
		}
		public string GetText(int x, int y, int length)
		{
			return null;
		}
		public bool MoveCursor(CursorOp op, int x, int y)
		{
			lock (tn)
			{

				return tn.tnctlr.MoveCursor(op,x,y);
			}
		}
		public int keyboardLock
		{
			get 
			{
				return tn.keyboard.kybdlock;
			}

		}
		
		public int cursorX
		{
			get
			{
				lock(tn)
				{
					return tn.tnctlr.cursorX;
				}
			}
		}
		public int cursorY
		{
			get
			{
				lock(tn)
				{
					return tn.tnctlr.cursorY;
				}
			}
		}

		private void tn_telnetDataEvent(object parentData, TNEvent eventType, string text)
		{
			
			Console.WriteLine("event = "+eventType+" text='"+text+"'");
			if (eventType==TNEvent.Disconnect)
			{
				if (OnDisconnect != null)
					OnDisconnect(null, "Client disconnected session");
			}
			if (eventType==TNEvent.DisconnectUnexpected)
			{
				if (OnDisconnect != null)
					OnDisconnect(null, "Host disconnected session");
			}
		}
		public void RunScript(string where)
		{
			if (this.RunScriptEvent!=null)
				this.RunScriptEvent(where);
		}

		public string GetLastError()
		{
			return this.tn.events.GetErrorAsText();
		}

	}

}
