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
using System.Text;
using Open3270.TN3270;
using Open3270;
using System.Collections.Generic;


namespace Open3270.TN3270
{

	public class TN3270API : IDisposable
	{

		#region Events and Delegates
		public event RunScriptDelegate RunScriptRequested;
		public event OnDisconnectDelegate Disconnected;
		public event EventHandler CursorLocationChanged;
		#endregion Events



		#region Fields

		Telnet tn;

		bool debug = false;
		bool useSSL = false;
		bool isDisposed = false;

		string sourceIP = string.Empty;

		#endregion Fields



		#region Properties
        /// <summary>
        /// Gets or sets whether or not we are using SSL.
        /// </summary>
		public bool UseSSL
		{
			get { return useSSL; }
			set { useSSL = value; }
		}
        /// <summary>
        /// Returns whether or not the session is connected.
        /// </summary>
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
        /// <summary>
        /// Sets the value of debug.
        /// </summary>
        public bool Debug
        {
            set
            {
                debug = value;
            }
        }
        /// <summary>
        /// Returns the state of the keyboard.
        /// </summary>
        public int KeyboardLock
        {
            get
            {
                return tn.Keyboard.keyboardLock;
            }
        }
        /// <summary>
        /// Returns the cursor's current X position.
        /// </summary>
        public int CursorX
        {
            get
            {
                lock (tn)
                {
                    return tn.Controller.CursorX;
                }
            }
        }
        /// <summary>
        /// Returns the cursor's current Y positon.
        /// </summary>
        public int CursorY
        {
            get
            {
                lock (tn)
                {
                    return tn.Controller.CursorY;
                }
            }
        }
        /// <summary>
        /// Returns the text of the last exception thrown.
        /// </summary>
        public string LastException
        {
            get
            {
                return this.tn.Events.GetErrorAsText();
            }
        }

		internal TN3270API()
		{
			tn = null;
		}
		internal string DisconnectReason
		{
			get { if (this.tn != null) return this.tn.DisconnectReason; else return null; }
		}
		internal bool ShowParseError
		{
			set { if (tn != null) tn.ShowParseError = value; }
		}
		#endregion Properties



		#region Ctors, dtors, and clean-up

		~TN3270API()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				isDisposed = true;
				if (disposing)
				{
					this.Disconnect();
					this.Disconnected = null;
					this.RunScriptRequested = null;
					if (tn != null)
					{
						tn.telnetDataEventOccurred -= this.tn_DataEventReceived;
						tn.Dispose();
					}
				}
			}
		}

		#endregion Ctors, dtors, and clean-up



        #region Private Methods
        private void tn_CursorLocationChanged(object sender, EventArgs e)
        {
            this.OnCursorLocationChanged(e);
        }
        #endregion



        #region Public Methods
        /// <summary>
		/// Connects to host using a local IP
		/// If a source IP is given then use it for the local IP
		/// </summary>
		/// <param name="audit">IAudit interface to post debug/tracing to</param>
		/// <param name="localIP">ip to use for local end point</param>
		/// <param name="host">host ip/name</param>
		/// <param name="port">port to use</param>
		/// <param name="config">configuration parameters</param>
		/// <returns></returns>
		public bool Connect(IAudit audit, string localIP, string host, int port, ConnectionConfig config)
		{
			this.sourceIP = localIP;
			return Connect(audit, host, port, string.Empty, config);
		}
		/// <summary>
		/// Connects a Telnet object to the host using the parameters provided
		/// </summary>
		/// <param name="audit">IAudit interface to post debug/tracing to</param>
		/// <param name="host">host ip/name</param>
		/// <param name="port">port to use</param>
		/// <param name="lu">lu to use or empty string for host negotiated</param>
		/// <param name="config">configuration parameters</param>
		/// <returns></returns>
		/// 
		public bool Connect(IAudit audit, string host, int port, string lu, ConnectionConfig config)
		{
			if (this.tn != null)
			{
				this.tn.CursorLocationChanged -= tn_CursorLocationChanged;
			}

			this.tn = new Telnet(this, audit, config);

			this.tn.Trace.optionTraceAnsi = debug;
			this.tn.Trace.optionTraceDS = debug;
			this.tn.Trace.optionTraceDSN = debug;
			this.tn.Trace.optionTraceEvent = debug;
			this.tn.Trace.optionTraceNetworkData = debug;

			this.tn.telnetDataEventOccurred += new TelnetDataDelegate(tn_DataEventReceived);
			this.tn.CursorLocationChanged += tn_CursorLocationChanged;

			if (lu == null || lu.Length == 0)
			{
				this.tn.Lus = null;
			}
			else
			{
				this.tn.Lus = new List<string>();
				this.tn.Lus.Add(lu);
			}

			if (!string.IsNullOrEmpty(sourceIP))
			{
				this.tn.Connect(this, host, port, sourceIP);
			}
			else
			{
				this.tn.Connect(this, host, port);
			}

			if (!tn.WaitForConnect())
			{
				this.tn.Disconnect();
				string text = tn.DisconnectReason;
				this.tn = null;
				throw new TNHostException("connect to " + host + " on port " + port + " failed", text, null);
			}
			this.tn.Trace.WriteLine("--connected");

			return true;
		}
		/// <summary>
		/// Disconnects the connected telnet object from the host
		/// </summary>
		public void Disconnect()
		{
			if (this.tn != null)
			{
				this.tn.Disconnect();
				this.tn.CursorLocationChanged -= tn_CursorLocationChanged;
				this.tn = null;
			}
		}
        /// <summary>
        /// Waits for the connection to complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
		public bool WaitForConnect(int timeout)
		{
			bool success = this.tn.WaitFor(SmsState.ConnectWait, timeout);
			if (success)
			{
				if (!tn.IsConnected)
				{
					success = false;
				}
			}
			return success;
		}
        /// <summary>
        /// Gets the entire contents of the screen.
        /// </summary>
        /// <param name="crlf"></param>
        /// <returns></returns>
		public string GetAllStringData(bool crlf = false)
		{
			lock (tn)
			{

				StringBuilder builder = new StringBuilder();
				int index = 0;
				string temp;
				while ((temp = tn.Action.GetStringData(index)) != null)
				{
					builder.Append(temp);
					if (crlf)
					{
						builder.Append("\n");
					}
					index++;
				}
				return builder.ToString();
			}
		}
        /// <summary>
        /// Sends an operator key to the mainframe.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
		public bool SendKeyOp(KeyboardOp op)
		{
			bool success = false;
			lock (tn)
			{
				// These can go to a locked screen		
				if (op == KeyboardOp.Reset)
				{
					success = true;
				}
				else
				{

					if ((tn.Keyboard.keyboardLock & KeyboardConstants.OiaMinus) != 0 ||
						tn.Keyboard.keyboardLock != 0)
					{
						success = false;
					}
					else
					{
						// These need unlocked screen
						switch (op)
						{
							case KeyboardOp.AID:
								{
									byte v = (byte)typeof(AID).GetField(op.ToString()).GetValue(null);
									this.tn.Keyboard.HandleAttentionIdentifierKey(v);
									success = true;
									break;
								}
							case KeyboardOp.Home:
								{
									if (this.tn.IsAnsi)
									{
										Console.WriteLine("IN_ANSI Home key not supported");
										//ansi_send_home();
										return false;
									}

									if (!this.tn.Controller.Formatted)
									{
										this.tn.Controller.SetCursorAddress(0);
										return true;
									}
									this.tn.Controller.SetCursorAddress(tn.Controller.GetNextUnprotectedField(tn.Controller.RowCount * tn.Controller.ColumnCount - 1));
									success = true;
									break;
								}
							case KeyboardOp.ATTN:
								{
									this.tn.Interrupt();
									success = true;
									break;
								}
							default:
								{
									throw new ApplicationException("Sorry, key '" + op.ToString() + "'not known");
								}
						}

					}
				}
			}
			return success;
		}
        /// <summary>
        /// Gets the text at the specified cursor position.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="length"></param>
        /// <returns></returns>
		public string GetText(int x, int y, int length)
		{
            MoveCursor(CursorOp.Exact, x, y);
            lock (tn)
			{
                this.tn.Controller.MoveCursor(CursorOp.Exact, x, y);
				return this.tn.Action.GetStringData(length);
			}
		}
        /// <summary>
        /// Sets the text to the specified value at the specified position.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="paste"></param>
        public void SetText(string text, int x, int y, bool paste = true)
        {
            MoveCursor(CursorOp.Exact, x, y);
            lock (tn)
            {
                SetText(text, paste);
            }
        }
        /// <summary>
        /// Sets the text value at the current cursor position.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="paste"></param>
        /// <returns></returns>
        public bool SetText(string text, bool paste = true)
        {
            lock (tn)
            {
                bool success = true;
                int i;
                if (text != null)
                {
                    for (i = 0; i < text.Length; i++)
                    {
                        success = tn.Keyboard.HandleOrdinaryCharacter(text[i], false, paste);
                        if (!success)
                        {
                            break;
                        }
                    }
                }
                return success;
            }
        }
        /// <summary>
        /// Gets the field attributes of the field at the specified coordinates.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public FieldAttributes GetFieldAttribute(int x, int y)
        {
            byte b = 0;
            lock (tn)
            {
                b = (byte)tn.Controller.GetFieldAttribute(tn.Controller.CursorAddress);
            }

            FieldAttributes fa = new FieldAttributes();
            fa.IsHigh = FieldAttribute.IsHigh(b);
            fa.IsIntense = FieldAttribute.IsIntense(b);
            fa.IsModified = FieldAttribute.IsModified(b);
            fa.IsNormal = FieldAttribute.IsNormal(b);
            fa.IsNumeric = FieldAttribute.IsNumeric(b);
            fa.IsProtected = FieldAttribute.IsProtected(b);
            fa.IsSelectable = FieldAttribute.IsSelectable(b);
            fa.IsSkip = FieldAttribute.IsSkip(b);
            fa.IsZero = FieldAttribute.IsZero(b);
            return fa;
        }
        /// <summary>
        /// Moves the cursor to the specified position.
        /// </summary>
        /// <param name="op"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public bool MoveCursor(CursorOp op, int x, int y)
		{
			lock (this.tn)
			{
				return this.tn.Controller.MoveCursor(op, x, y);
			}
		}
        /// <summary>
        /// Returns the text of the last error thrown.
        /// </summary>
        /// <returns></returns>
        public bool ExecuteAction(bool submit, string name, params object[] args)
        {
            lock (this.tn)
            {
                return this.tn.Action.Execute(submit, name, args);
            }
        }
        public bool KeyboardCommandCausesSubmit(string name)
        {
            return this.tn.Action.KeyboardCommandCausesSubmit(name);
        }
        public bool Wait(int timeout)
        {
            return this.tn.WaitFor(SmsState.KBWait, timeout);
        }
        public void RunScript(string where)
        {
            if (this.RunScriptRequested != null)
            {
                this.RunScriptRequested(where);
            }
        }
        #endregion Public Methods



        #region Depricated Methods
        [Obsolete("This method has been deprecated.  Please use SendKeyOp(KeyboardOp op) instead. This method is only included for backwards compatibiity and might not exist in future releases.")]
        public bool SendKeyOp(KeyboardOp op, string key)
        {
            return SendKeyOp(op);
        }
        [Obsolete("This method has been deprecated.  Please use SetText instead. This method is only included for backwards compatibiity and might not exist in future releases.")]
        public bool SendText(string text, bool paste)
        {
            return SetText(text, paste);
        }
        [Obsolete("This method has been deprecated.  Please use GetText instead. This method is only included for backwards compatibiity and might not exist in future releases.")]
        public string GetStringData(int index)
        {
            lock (tn)
            {
                return this.tn.Action.GetStringData(index);
            }
        }
        [Obsolete("This method has been deprecated.  Please use LastException instead. This method is only included for backwards compatibiity and might not exist in future releases.")]
        public string GetLastError()
        {
            return LastException;
        }
        #endregion



        #region Eventhandlers and such

        private void tn_DataEventReceived(object parentData, TNEvent eventType, string text)
		{

			//Console.WriteLine("event = "+eventType+" text='"+text+"'");
			if (eventType == TNEvent.Disconnect)
			{
				if (Disconnected != null)
				{
					Disconnected(null, "Client disconnected session");
				}
			}
			if (eventType == TNEvent.DisconnectUnexpected)
			{
				if (Disconnected != null)
				{
					Disconnected(null, "Host disconnected session");
				}
			}
		}


		protected virtual void OnCursorLocationChanged(EventArgs args)
		{
			if (this.CursorLocationChanged != null)
			{
				this.CursorLocationChanged(this, args);
			}
		}

		#endregion Eventhandlers and such

	}
}