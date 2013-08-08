
using System;
using System.Text;
using Open3270.TN3270;
using Open3270;
using System.Collections.Generic;


namespace Open3270.TN3270
{

	internal class TN3270API : IDisposable
	{

		#region Events and Delegates

		public event RunScriptDelegate RunScriptRequested;
		public event OnDisconnectDelegate Disconnected;

		#endregion Events




		#region Fields

		Telnet tn;

		bool debug = false;
		bool useSSL = false;
		bool isDisposed = false;

		string sourceIP = string.Empty;

		#endregion Fields




		#region Properties

		public bool UseSSL
		{
			get { return useSSL; }
			set { useSSL = value; }
		}

		internal TN3270API()
		{
			tn = null;
		}

		internal string DisconnectReason
		{
			get { if (this.tn != null) return this.tn.DisconnectReason; else return null; }
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
			set { if (tn != null) tn.ShowParseError = value; }
		}

		public bool Debug
		{
			set
			{
				debug = value;
			}
		}


		public int KeyboardLock
		{
			get
			{
				return tn.Keyboard.kybdlock;
			}

		}

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
			this.tn = new Telnet(this, audit, config);

			this.tn.Trace.optionTraceAnsi = debug;
			this.tn.Trace.optionTraceDS = debug;
			this.tn.Trace.optionTraceDSN = debug;
			this.tn.Trace.optionTraceEvent = debug;
			this.tn.Trace.optionTraceNetworkData = debug;

			this.tn.telnetDataEventOccurred += new TelnetDataDelegate(tn_DataEventReceived);

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
				this.tn = null;
			}
		}


		public bool ExecuteAction(bool submit, string name, params object[] args)
		{
			lock (this.tn)
			{
				return this.tn.Action.Execute(submit, name, args);
			}
		}


		public bool KeyboardCommandCausesSubmit(string name, params object[] args)
		{
			return this.tn.Action.KeyboardCommandCausesSubmit(name, args);
		}


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


		public bool Wait(int timeout)
		{
			return this.tn.WaitFor(SmsState.KBWait, timeout);
		}


		public string GetStringData(int index)
		{
			lock (tn)
			{
				return this.tn.Action.GetStringData(index);
			}
		}


		public string GetAllStringData(bool crlf)
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


		public bool SendKeyOp(KeyboardOp op, string key)
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

					if ((tn.Keyboard.kybdlock & Keyboard.KL_OIA_MINUS) != 0 ||
						tn.Keyboard.kybdlock != 0)
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
									byte v = (byte)typeof(AID).GetField(key).GetValue(null);
									this.tn.Keyboard.key_AID(v);
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
									throw new ApplicationException("Sorry, key '" + key + "'not known");
								}
						}

					}
				}
			}
			return success;
		}


		public bool SendText(string text, bool paste)
		{
			lock (tn)
			{
				bool success = true;
				int i;
				if (text != null)
				{
					for (i = 0; i < text.Length; i++)
					{
						success = tn.Keyboard.key_Character(text[i], false, paste);
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
		/// This method has not been implemented yet
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public string GetText(int x, int y, int length)
		{
			throw new NotSupportedException();
		}

		public bool MoveCursor(CursorOp op, int x, int y)
		{
			lock (this.tn)
			{
				return this.tn.Controller.MoveCursor(op, x, y);
			}
		}


		public void RunScript(string where)
		{
			if (this.RunScriptRequested != null)
			{
				this.RunScriptRequested(where);
			}
		}


		public string GetLastError()
		{
			return this.tn.Events.GetErrorAsText();
		}

		#endregion Public Methods




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

		#endregion Eventhandlers and such

	}

}
