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
using System.Collections;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Open3270;
using Open3270.Library;
using System.Net.Security;
using System.Collections.Generic;

namespace Open3270.TN3270
{

	internal class Telnet : IDisposable
	{

		#region Fields

		public event TelnetDataDelegate telnetDataEventOccurred;
		public event EventHandler<Connected3270EventArgs> Connected3270;
		public event EventHandler ConnectedLineMode;
		public event EventHandler ConnectionPending;
		public event EventHandler<PrimaryConnectionChangedArgs> PrimaryConnectionChanged;

		SmsState waitState;
		TN3270State tnState = TN3270State.InNeither;
		ConnectionState connectionState = ConnectionState.NotConnected;
		TN3270ESubmode tn3270eSubmode = TN3270ESubmode.None;
		TelnetState telnetState = TelnetState.Data;

		TN3270API telnetApi = null;
		ConnectionConfig connectionConfig = null;


		#region Services

		Controller controller = null;
		Print print = null;
		Idle idle = null;
		Actions action = null;
		Ansi ansi = null;
		Appres appres;
		Events events = null;
		Keyboard keyboard = null;
		private TNTrace trace = null;

		#endregion Services


		bool nonTn3270eHost = false;
		bool parseLogFileOnly = false;
		bool showParseError;
		bool isValid = false;
		bool tn3270eBound = false;
		bool linemode = false;
		bool syncing = false;
		bool tn3270e_negotiated = false;
		bool logFileProcessorThread_Quit = false;
		bool closeRequested = false;
		bool isDisposed = false;

		// ANSI stuff
		byte vintr;
		byte vquit;
		byte verase;
		byte vkill;
		byte veof;
		byte vwerase;
		byte vrprnt;
		byte vlnext;


		int port;
		int bytesReceived;
		int bytesSent;
		int currentLUIndex = 0;
		int eTransmitSequence = 0;
		int ansiData = 0;
		int currentOptionMask;
		int responseRequired = TnHeader.HeaderReponseFlags.NoResponse;
		int inputBufferIndex = 0;
		int startedReceivingCount = 0;

		int[] clientOptions = new int[256];
		int[] hostOptions = null;

		List<string> lus = null;



		string termType = null;
		string connectedType = null;
		string reportedType = null;
		string connectedLu = null;
		string reportedLu = null;
		string sourceIP = string.Empty;
		string address;
		string disconnectReason = null;


		//Buffers
		NetBuffer sbBuffer = null;
		byte[] byteBuffer = new Byte[32767];
		byte[] inputBuffer = null;

		//Telnet predefined messages
		byte[] doOption = new byte[] { TelnetConstants.IAC, TelnetConstants.DO, 0 };
		byte[] dontOption = new byte[] { TelnetConstants.IAC, TelnetConstants.DONT, 0 };
		byte[] willDoOption = new byte[] { TelnetConstants.IAC, TelnetConstants.WILL, 0 };
		byte[] wontDoOption = new byte[] { TelnetConstants.IAC, TelnetConstants.WONT, 0 };
		
		
		//Sockets
		 IPEndPoint remoteEndpoint;
		 IPEndPoint localEndpoint;
		 AsyncCallback callbackProc;
		 Socket socketBase;
		 Stream socketStream;


		//Threading and synchronization fields
		object receivingPadlock = new object();
		MySemaphore logFileSemaphore = null;
		Thread logFileProcessorThread = null;
		Thread mainThread = null;
		ManualResetEvent WaitEvent = new ManualResetEvent(false);
		Queue logClientData = null;

		object parentData;

		#endregion Fields




		#region Simple Properties

		public TNTrace Trace
		{
			get { return trace; }
			set { trace = value; }
		}

		public Controller Controller
		{
			get { return controller; }
			private set { controller = value; }
		}

		public Print Print
		{
			get { return print; }
			set { print = value; }
		}

		public Actions Action
		{
			get { return action; }
			set { action = value; }
		}

		public Idle Idle
		{
			get { return idle; }
			set { idle = value; }
		}

		public Ansi Ansi
		{
			get { return ansi; }
			set { ansi = value; }
		}

		public Events Events
		{
			get { return events; }
			set { events = value; }
		}

		public Keyboard Keyboard
		{
			get { return keyboard; }
			set { keyboard = value; }
		}
		public Appres Appres
		{
			get { return appres; }
			private set { appres = value; }
		}

		public ManualResetEvent WaitEvent1
		{
			get { return WaitEvent; }
			set { WaitEvent = value; }
		}

		public bool ParseLogFileOnly
		{
			get { return parseLogFileOnly; }
			set { parseLogFileOnly = value; }
		}
		public TN3270API TelnetApi
		{
			get { return telnetApi; }
			set { telnetApi = value; }
		}

		public ConnectionConfig Config
		{
			get { return connectionConfig; }
		}

		public SmsState WaitState
		{
			get { return waitState; }
			set { waitState = value; }
		}

		internal bool ShowParseError
		{
			get { return showParseError; }
			set { showParseError = value; }
		}

		public List<string> Lus
		{
			get { return lus; }
			set { lus = value; }
		}
		public string TermType
		{
			get { return termType; }
			set { termType = value; }
		}

		public string DisconnectReason
		{
			get { return disconnectReason; }
		}
		
		public int StartedReceivingCount
		{
			get
			{
				lock (receivingPadlock)
				{
					return startedReceivingCount;
				}
			}
		}

		#endregion Simple Properties



		#region Macro-like Properties

		public bool IsKeyboardInWait
		{
			get { return 0 != (keyboard.keyboardLock & (KeyboardConstants.OiaLocked | KeyboardConstants.OiaTWait | KeyboardConstants.DeferredUnlock)); }
		}

		//Macro that defines when it's safe to continue a Wait()ing sms.
		public bool CanProceed
		{
			get
			{
				return (
					IsSscp ||
					(Is3270 && controller.Formatted && controller.CursorAddress != 0 && !IsKeyboardInWait) ||
					(IsAnsi && 0 == (keyboard.keyboardLock & KeyboardConstants.AwaitingFirst))
					);
			}
		}

		public bool IsSocketConnected
		{
			get
			{
				if (this.connectionConfig.LogFile != null)
				{
					return true;
				}

				if (this.socketBase != null && socketBase.Connected)
				{
					return true;
				}
				else
				{
					if (this.disconnectReason == null)
						this.disconnectReason = "Server disconnected socket";
					return false;
				}
			}
		}


		public bool IsResolving
		{
			get { return ((int)connectionState >= (int)ConnectionState.Resolving); }
		}

		public bool IsPending
		{
			get { return (connectionState == ConnectionState.Resolving || connectionState == ConnectionState.Pending); }
		}

		public bool IsConnected
		{
			get { return ((int)connectionState >= (int)ConnectionState.ConnectedInitial); }
		}

		public bool IsAnsi
		{
			get { return (connectionState == ConnectionState.ConnectedANSI || connectionState == ConnectionState.ConnectedNVT); }
		}

		public bool Is3270
		{
			get { return (connectionState == ConnectionState.Connected3270 || connectionState == ConnectionState.Connected3270E || connectionState == ConnectionState.ConnectedSSCP); }
		}

		public bool IsSscp
		{
			get { return (connectionState == ConnectionState.ConnectedSSCP); }
		}

		public bool IsTn3270E
		{
			get { return (connectionState == ConnectionState.Connected3270E); }
		}

		public bool IsE
		{
			get { return (connectionState >= ConnectionState.ConnectedInitial3270E); }
		}

		#endregion Macro-like Properties




		#region Ctors, Dtors, clean-up

		public Telnet(TN3270API api, IAudit audit, ConnectionConfig config)
		{
			this.connectionConfig = config;
			this.telnetApi = api;
			if (config.IgnoreSequenceCount)
			{
				this.currentOptionMask = Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE) |
					Shift(TelnetConstants.TN3270E_FUNC_SYSREQ);
			}
			else
			{
				this.currentOptionMask = Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE) |
					Shift(TelnetConstants.TN3270E_FUNC_RESPONSES) |
					Shift(TelnetConstants.TN3270E_FUNC_SYSREQ);
			}


			this.disconnectReason = null;

			this.trace = new TNTrace(this, audit);
			this.appres = new Appres();
			this.events = new Events(this);
			this.ansi = new Ansi(this);
			this.print = new Print(this);
			this.controller = new Controller(this, appres);
			this.keyboard = new Keyboard(this);
			this.action = new Actions(this);
			this.keyboard.Actions = action;
			this.idle = new Idle(this);

			this.controller.CursorLocationChanged += controller_CursorLocationChanged;

			if (!isValid)
			{
				this.vintr = ParseControlCharacter(appres.intr);
				this.vquit = ParseControlCharacter(appres.quit);
				this.verase = ParseControlCharacter(appres.erase);
				this.vkill = ParseControlCharacter(appres.kill);
				this.veof = ParseControlCharacter(appres.eof);
				this.vwerase = ParseControlCharacter(appres.werase);
				this.vrprnt = ParseControlCharacter(appres.rprnt);
				this.vlnext = ParseControlCharacter(appres.lnext);
				this.isValid = true;
			}

			int i;
			this.hostOptions = new int[256];
			for (i = 0; i < 256; i++)
			{
				this.hostOptions[i] = 0;
			}

		}


		~Telnet()
		{
			Dispose(false);
		}


		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}


		protected virtual void Dispose(bool disposing)
		{
			if (this.isDisposed)
			{
				return;
			}
			this.isDisposed = true;

			if (disposing)
			{
				this.Disconnect();
				if (this.controller != null)
				{
					this.controller.CursorLocationChanged-=controller_CursorLocationChanged;
					this.controller.Dispose();
				}
				if (this.Idle != null)
				{
					this.Idle.Dispose();
				}

				if (this.keyboard != null)
				{
					this.keyboard.Dispose();
				}

				if (this.ansi != null)
				{
					this.ansi.Dispose();
				}

			}
		}

		#endregion Ctors




		#region Eventhandlers and similar

		void ReactToConnectionChange(bool success)
		{
			if (this.IsConnected || this.appres.disconnect_clear)
			{
				this.controller.Erase(true);
			}
		}

		#endregion Eventhandlers and similar






		#region Public Methods

		/// <summary>
		/// Connects to host using a sourceIP for VPN's that require
		/// source IP to determine LU
		/// </summary>
		/// <param name="parameterObjectToSendCallbacks">object to send to callbacks/events</param>
		/// <param name="hostAddress">host ip address or name</param>
		/// <param name="hostPort">host port</param>
		/// <param name="sourceIP">IP to use as local IP</param>
		public void Connect(object parameterObjectToSendCallbacks, string hostAddress, int hostPort, string sourceIP)
		{
			
			this.sourceIP = sourceIP;
			this.Connect(parameterObjectToSendCallbacks, hostAddress, hostPort);
		}


		/// <summary>
		/// Connects to host at address/port
		/// </summary>
		/// <param name="parameterObjectToSendCallbacks">object to send to callbacks/events</param>
		/// <param name="hostAddress">host ip address or name</param>
		/// <param name="hostPort">host port</param>
		public void Connect(object parameterObjectToSendCallbacks, string hostAddress, int hostPort)
		{
			this.parentData = parameterObjectToSendCallbacks;
			this.address = hostAddress;
			this.port = hostPort;
			this.disconnectReason = null;
			this.closeRequested = false;

			//Junk
			if (connectionConfig.TermType == null)
			{
				this.termType = "IBM-3278-2";
			}
			else
			{
				this.termType = connectionConfig.TermType;
			}

			this.controller.Initialize(-1);
			this.controller.Reinitialize(-1);
			this.keyboard.Initialize();
			ansi.ansi_init();


			//Set up colour screen
			appres.mono = false;
			appres.m3279 = true;
			//Set up trace options
			appres.debug_tracing = true;


			//Handle initial toggle settings.
			if (!appres.debug_tracing)
			{
				this.appres.SetToggle(Appres.DSTrace, false);
				this.appres.SetToggle(Appres.EventTrace, false);
			}

			this.appres.SetToggle(Appres.DSTrace, true);

			if (connectionConfig.LogFile != null)
			{
				this.parseLogFileOnly = true;
				// Simulate a connect
				this.logFileSemaphore = new MySemaphore(0, 9999);
				this.logClientData = new Queue();
				this.logFileProcessorThread_Quit = false;
				this.mainThread = Thread.CurrentThread;
				this.logFileProcessorThread = new Thread(new System.Threading.ThreadStart(LogFileProcessorThreadHandler));

				this.logFileProcessorThread.Start();
			}
			else
			{
				// Actually connect

				//TODO: Replace IP address analysis with a regex
				bool ipaddress = false;
				bool text = false;
				int count = 0;

				for (int i = 0; i < address.Length; i++)
				{
					if (address[i] == '.')
						count++;
					else
					{
						if (address[i] < '0' || address[i] > '9')
							text = true;
					}
				}
				if (count == 3 && text == false)
				{
					ipaddress = true;
				}

				if (!ipaddress)
				{
					try
					{
						IPHostEntry hostEntry = Dns.GetHostEntry(address);
                        IPAddress ipAddress = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

                        //string[] aliases = hostEntry.Aliases;
						//IPAddress[] addr = hostEntry.AddressList;
                        
						this.remoteEndpoint = new IPEndPoint(/*addr[0]*/ipAddress, port);
					}
					catch (System.Net.Sockets.SocketException se)
					{
						throw new TNHostException("Unable to resolve host '" + address + "'", se.Message, null);
					}
				}
				else
				{
					try
					{
						this.remoteEndpoint = new IPEndPoint(IPAddress.Parse(address), port);
					}
					catch (System.FormatException se)
					{
						throw new TNHostException("Invalid Host TCP/IP address '" + address + "'", se.Message, null);
					}
				}


				// If a source IP is given then use it for the local IP
				if (!string.IsNullOrEmpty(sourceIP))
				{
					try
					{
						this.localEndpoint = new IPEndPoint(IPAddress.Parse(sourceIP), port);
					}
					catch (System.FormatException se)
					{
						throw new TNHostException("Invalid Source TCP/IP address '" + address + "'", se.Message, null);
					}
				}
				else
				{
					this.localEndpoint = new IPEndPoint(IPAddress.Any, 0);
				}

				this.disconnectReason = null;

				try
				{
					// Create New Socket 
					socketBase = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					// Create New EndPoint
					// Assign Callback function to read from Asyncronous Socket
					callbackProc = new AsyncCallback(ConnectCallback);
					// Begin Asyncronous Connection
					this.connectionState = ConnectionState.Resolving;
					this.socketBase.Bind(localEndpoint);
					this.socketBase.BeginConnect(remoteEndpoint, callbackProc, socketBase);

				}
				catch (System.Net.Sockets.SocketException se)
				{
					throw new TNHostException("An error occured connecting to the host '" + address + "' on port " + port, se.Message, null);
				}
				catch (Exception eeeee)
				{
					Console.WriteLine("e=" + eeeee);
					throw;
				}
			}
		}



		public void Disconnect()
		{
			if (!parseLogFileOnly)
			{
				lock (this)
				{
					if (socketStream != null)
					{
						//Console.WriteLine("Disconnect TN3270 socket");
						closeRequested = true;
						socketStream.Close();
						socketStream = null;

						if (string.IsNullOrEmpty(disconnectReason))
							this.disconnectReason = "telnet.disconnect socket-stream requested";
					}
					//
					if (socketBase != null)
					{
						closeRequested = true;

						try
						{
							socketBase.Close();

							if (string.IsNullOrEmpty(disconnectReason))
								this.disconnectReason = "telnet.disconnect socket-base requested";
						}
						catch (System.ObjectDisposedException)
						{
							// Ignore this
						}
						socketBase = null;
					}
				}
			}
			if (logFileProcessorThread != null)
			{
				logFileProcessorThread_Quit = true;
				Console.WriteLine("closing log processor thread");
				logFileProcessorThread.Join();
				logFileProcessorThread = null;
			}
		}


		public bool ParseByte(byte b)
		{
			lock (this)
			{
				if (this.tnState == TN3270State.InNeither)
				{
					keyboard.KeyboardLockClear(KeyboardConstants.AwaitingFirst, "telnet_fsm");
					//status_reset();
					this.tnState = TN3270State.ANSI;
				}
				if (this.TelnetProcessFiniteStateMachine(ref sbBuffer, b) != 0)
				{
					this.Host_Disconnect(true);
					this.Disconnect();
					this.disconnectReason = "Telnet state machine error during ParseByte";
					return false;
				}
			}
			return true;
		}

		public int TelnetProcessFiniteStateMachine(ref NetBuffer sbptr, byte currentByte)
		{
			int i;
			int sl = 0;
			if (sbptr == null)
			{
				sbptr = new NetBuffer();
			}

			//Console.WriteLine(""+telnet_state+"-0x"+(int)c);

			switch (this.telnetState)
			{
				case TelnetState.Data:
					{
						//Normal data processing
						if (currentByte == TelnetConstants.IAC)
						{
							//Got a telnet command
							telnetState = TelnetState.IAC;
							if (ansiData != 0)
							{
								trace.trace_dsn("\n");
								ansiData = 0;
							}
							break;
						}
						if (connectionState == ConnectionState.ConnectedInitial)
						{
							//Now can assume ANSI mode 
							SetHostState(ConnectionState.ConnectedANSI);
							keyboard.KeyboardLockClear(KeyboardConstants.AwaitingFirst, "telnet_fsm");
							controller.ProcessPendingInput();
						}
						if (IsAnsi && !IsE)
						{
							if (ansiData == 0)
							{
								trace.trace_dsn("<.. ");
								ansiData = 4;
							}
							string see_chr = Util.ControlSee((byte)currentByte);
							ansiData += (sl = see_chr.Length);
							if (ansiData >= TNTrace.TRACELINE)
							{
								trace.trace_dsn(" ...\n... ");
								ansiData = 4 + sl;
							}
							trace.trace_dsn(see_chr);
							if (!syncing)
							{
								if (linemode && appres.onlcr && currentByte == '\n')
								{
									ansi.ansi_process((byte)'\r');
								}
								ansi.ansi_process(currentByte);
							}
						}
						else
						{
							this.Store3270Input(currentByte);
						}
						break;
					}
				case TelnetState.IAC:
					{
						//Process a telnet command
						if (currentByte != TelnetConstants.EOR && currentByte != TelnetConstants.IAC)
						{
							trace.trace_dsn("RCVD " + GetCommand(currentByte) + " ");
						}

						switch (currentByte)
						{
							case TelnetConstants.IAC:
								{
									//Ecaped IAC, insert it
									if (IsAnsi && !IsE)
									{
										if (ansiData == 0)
										{
											trace.trace_dsn("<.. ");
											ansiData = 4;
										}
										string see_chr = Util.ControlSee(currentByte);
										ansiData += (sl = see_chr.Length);
										if (ansiData >= TNTrace.TRACELINE)
										{
											trace.trace_dsn(" ...\n ...");
											ansiData = 4 + sl;
										}
										trace.trace_dsn(see_chr);
										ansi.ansi_process(currentByte);
										//Console.WriteLine("--BUGBUG--sms_store");
										//sms_store(c);
									}
									else
									{
										Store3270Input(currentByte);
									}

									this.telnetState = TelnetState.Data;
									break;
								}
							case TelnetConstants.EOR:
								{
									//EOR, process accumulated input
									trace.trace_dsn("RCVD EOR\n");
									if (Is3270 || (IsE && tn3270e_negotiated))
									{
										//Can't see this being used. --> ns_rrcvd++;
										if (ProcessEOR())
										{
											return -1;
										}
									}
									else
									{
										events.Warning("EOR received when not in 3270 mode, ignored.");
									}

									inputBufferIndex = 0;
									this.telnetState = TelnetState.Data;
									break;
								}
							case TelnetConstants.WILL:
								{
									this.telnetState = TelnetState.Will;
									break;
								}
							case TelnetConstants.WONT:
								{
									this.telnetState = TelnetState.Wont;
									break;
								}
							case TelnetConstants.DO:
								{
									this.telnetState = TelnetState.Do;
									break;
								}
							case TelnetConstants.DONT:
								{
									this.telnetState = TelnetState.Dont;
									break;
								}
							case TelnetConstants.SB:
								{
									this.telnetState = TelnetState.SB;
									this.sbBuffer = new NetBuffer();
									//if (sbbuf == null)
									//	sbbuf = (int)Malloc(1024); //bug
									//sbptr = sbbuf;
									break;
								}
							case TelnetConstants.DM:
								{
									trace.trace_dsn("\n");
									if (syncing)
									{
										syncing = false;
										//x_except_on(sock);
									}
									this.telnetState = TelnetState.Data;
									break;
								}
							case TelnetConstants.GA:
							case TelnetConstants.NOP:
								{
									trace.trace_dsn("\n");
									this.telnetState = TelnetState.Data;
									break;
								}
							default:
								{
									trace.trace_dsn("???\n");
									this.telnetState = TelnetState.Data;
									break;
								}
						}
						break;
					}
				case TelnetState.Will:
					{
						//Telnet WILL DO OPTION command
						trace.trace_dsn("" + GetOption(currentByte) + "\n");
						if (currentByte == TelnetConstants.TELOPT_SGA ||
							currentByte == TelnetConstants.TELOPT_BINARY ||
							currentByte == TelnetConstants.TELOPT_EOR ||
							currentByte == TelnetConstants.TELOPT_TTYPE ||
							currentByte == TelnetConstants.TELOPT_ECHO ||
							currentByte == TelnetConstants.TELOPT_TN3270E)
						{
							if (currentByte != TelnetConstants.TELOPT_TN3270E || !nonTn3270eHost)
							{
								if (hostOptions[currentByte] == 0)
								{
									hostOptions[currentByte] = 1;
									doOption[2] = currentByte;
									this.SendRawOutput(doOption);
									trace.trace_dsn("SENT DO " + GetOption(currentByte) + "\n");

									//For UTS, volunteer to do EOR when they do.
									if (currentByte == TelnetConstants.TELOPT_EOR && clientOptions[currentByte] == 0)
									{
										clientOptions[currentByte] = 1;
										willDoOption[2] = currentByte;
										this.SendRawOutput(willDoOption);
										trace.trace_dsn("SENT WILL " + GetOption(currentByte) + "\n");
									}

									this.CheckIn3270();
									this.CheckLineMode(false);
								}
							}
						}
						else
						{
							dontOption[2] = currentByte;
							this.SendRawOutput(dontOption);
							trace.trace_dsn("SENT DONT " + GetOption(currentByte) + "\n");
						}
						telnetState = TelnetState.Data;
						break;
					}
				case TelnetState.Wont:
					{
						//Telnet WONT DO OPTION command
						trace.trace_dsn("" + GetOption(currentByte) + "\n");
						if (hostOptions[currentByte] != 0)
						{
							hostOptions[currentByte] = 0;
							dontOption[2] = currentByte;
							this.SendRawOutput(dontOption);
							trace.trace_dsn("SENT DONT " + GetOption(currentByte) + "\n");
							this.CheckIn3270();
							this.CheckLineMode(false);
						}
						this.telnetState = TelnetState.Data;
						break;
					}
				case TelnetState.Do:
					{
						//Telnet PLEASE DO OPTION command
						trace.trace_dsn("" + GetOption(currentByte) + "\n");
						if (currentByte == TelnetConstants.TELOPT_BINARY ||
							currentByte == TelnetConstants.TELOPT_BINARY ||
							currentByte == TelnetConstants.TELOPT_EOR ||
							currentByte == TelnetConstants.TELOPT_TTYPE ||
							currentByte == TelnetConstants.TELOPT_SGA ||
							currentByte == TelnetConstants.TELOPT_NAWS ||
							currentByte == TelnetConstants.TELOPT_TM ||
							(currentByte == TelnetConstants.TELOPT_TN3270E && !Config.RefuseTN3270E))
						{
							bool fallthrough = true;
							if (currentByte != TelnetConstants.TELOPT_TN3270E || !nonTn3270eHost)
							{
								if (clientOptions[currentByte] == 0)
								{
									if (currentByte != TelnetConstants.TELOPT_TM)
									{
										clientOptions[currentByte] = 1;
									}
									willDoOption[2] = currentByte;

									this.SendRawOutput(willDoOption);
									trace.trace_dsn("SENT WILL " + GetOption(currentByte) + "\n");
									this.CheckIn3270();
									this.CheckLineMode(false);
								}
								if (currentByte == TelnetConstants.TELOPT_NAWS)
								{
									this.SendNaws();
								}
								fallthrough = false;
							}
							if (fallthrough)
							{
								wontDoOption[2] = currentByte;
								this.SendRawOutput(wontDoOption);
								trace.trace_dsn("SENT WONT " + GetOption(currentByte) + "\n");
							}
						}
						else
						{
							wontDoOption[2] = currentByte;
							this.SendRawOutput(wontDoOption);
							trace.trace_dsn("SENT WONT " + GetOption(currentByte) + "\n");
						}

						telnetState = TelnetState.Data;
						break;
					}
				case TelnetState.Dont:
					{
						//Telnet PLEASE DON'T DO OPTION command
						trace.trace_dsn("" + GetOption(currentByte) + "\n");
						if (clientOptions[currentByte] != 0)
						{
							clientOptions[currentByte] = 0;
							wontDoOption[2] = currentByte;
							this.SendRawOutput(wontDoOption);
							trace.trace_dsn("SENT WONT " + GetOption(currentByte) + "\n");
							this.CheckIn3270();
							this.CheckLineMode(false);
						}
						telnetState = TelnetState.Data;
						break;
					}
				case TelnetState.SB:
					{
						//Telnet sub-option string command
						if (currentByte == TelnetConstants.IAC)
						{
							this.telnetState = TelnetState.SbIac;
						}
						else
						{
							this.sbBuffer.Add(currentByte);
						}
						break;
					}
				case TelnetState.SbIac:
					{
						//Telnet sub-option string command
						this.sbBuffer.Add(currentByte);
						if (currentByte == TelnetConstants.SE)
						{
							this.telnetState = TelnetState.Data;
							if (sbptr.Data[0] == TelnetConstants.TELOPT_TTYPE &&
								sbptr.Data[1] == TelnetConstants.TELQUAL_SEND)
							{
								int tt_len;
								trace.trace_dsn("" + GetOption(sbptr.Data[0]) + " " + TelnetConstants.TelQuals[sbptr.Data[1]] + "\n");
								if (lus != null && this.currentLUIndex >= lus.Count)
								{
									//Console.WriteLine("BUGBUG-resending LUs, rather than forcing error");

									//this.currentLUIndex=0;
									/* None of the LUs worked. */
									events.ShowError("Cannot connect to specified LU");
									return -1;
								}

								tt_len = termType.Length;
								if (lus != null)
								{
									tt_len += ((string)lus[currentLUIndex]).Length + 1;
									//tt_len += strlen(try_lu) + 1;
									connectedLu = (string)lus[currentLUIndex];
								}
								else
								{
									connectedLu = null;
									//status_lu(connected_lu);
								}

								NetBuffer tt_out = new NetBuffer();
								tt_out.Add(TelnetConstants.IAC);
								tt_out.Add(TelnetConstants.SB);
								tt_out.Add(TelnetConstants.TELOPT_TTYPE);
								tt_out.Add(TelnetConstants.TELQUAL_IS);
								tt_out.Add(termType);

								if (lus != null)
								{
									tt_out.Add((byte)'@');

									byte[] b_try_lu = Encoding.ASCII.GetBytes((string)lus[this.currentLUIndex]);
									for (i = 0; i < b_try_lu.Length; i++)
									{
										tt_out.Add(b_try_lu[i]);
									}
									tt_out.Add(TelnetConstants.IAC);
									tt_out.Add(TelnetConstants.SE);
									Console.WriteLine("Attempt LU='" + lus[this.currentLUIndex] + "'");
								}
								else
								{
									tt_out.Add(TelnetConstants.IAC);
									tt_out.Add(TelnetConstants.SE);
								}
								this.SendRawOutput(tt_out);

								trace.trace_dsn("SENT SB " + GetOption(TelnetConstants.TELOPT_TTYPE) + " " + tt_out.Data.Length + " " + termType + " " + GetCommand(TelnetConstants.SE));

								/* Advance to the next LU name. */
								this.currentLUIndex++;

							}
							else if (clientOptions[TelnetConstants.TELOPT_TN3270E] != 0 && this.sbBuffer.Data[0] == TelnetConstants.TELOPT_TN3270E)
							{
								if (Tn3270e_Negotiate(sbptr) != 0)
								{
									return -1;
								}
							}
						}
						else
						{
							this.telnetState = TelnetState.SB;
						}
						break;
					}
			}
			return 0;
		}


		public void SendString(string s)
		{
			int i;
			for (i = 0; i < s.Length; i++)
				SendChar(s[i]);
		}


		public void SendChar(char c)
		{
			SendByte((byte)c);
		}


		public void SendByte(byte c)
		{
			byte[] buf = new byte[2];
			if (c == '\r' && !linemode)
			{
				/* CR must be quoted */
				buf[0] = (byte)'\r';
				buf[1] = 0;

				Cook(buf, 2);
			}
			else
			{
				buf[0] = c;
				Cook(buf, 1);
			}
		}


		public void Abort()
		{
			byte[] buf = new byte[] { TelnetConstants.IAC, TelnetConstants.AO };

			if ((currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_SYSREQ)) != 0)
			{
				/* I'm not sure yet what to do here.  Should the host respond
				 * to the AO by sending us SSCP-LU data (and putting us into
				 * SSCP-LU mode), or should we put ourselves in it?
				 * Time, and testers, will tell.
				 */
				switch (tn3270eSubmode)
				{
					case TN3270ESubmode.None:
					case TN3270ESubmode.NVT:
						break;
					case TN3270ESubmode.SSCP:
						SendRawOutput(buf, buf.Length);
						trace.trace_dsn("SENT AO\n");
						if (tn3270eBound ||
							0 == (currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE)))
						{
							tn3270eSubmode = TN3270ESubmode.Mode3270;
							CheckIn3270();
						}
						break;
					case TN3270ESubmode.Mode3270:
						SendRawOutput(buf, buf.Length);
						trace.trace_dsn("SENT AO\n");
						tn3270eSubmode = TN3270ESubmode.SSCP;
						CheckIn3270();
						break;
				}
			}
		}

		/// <summary>
		/// Sends erase character in ANSI mode
		/// </summary>
		public void SendErase()
		{
			byte[] data = new byte[1];
			data[0] = verase;
			Cook(data, 1);
		}


		/// <summary>
		/// Sends the KILL character in ANSI mode
		/// </summary>
		public void SendKill()
		{
			byte[] data = new byte[1];
			data[0] = vkill;
			Cook(data, 1);
		}

		/// <summary>
		/// Sends WERASE character
		/// </summary>
		public void SendWErase()
		{
			byte[] data = new byte[1];
			data[0] = vwerase;
			Cook(data, 1);
		}


		/// <summary>
		/// Send uncontrolled user data to the host in ANSI mode, performing IAC and CR quoting as necessary.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="length"></param>
		public void SendHexAnsiOut(byte[] buffer, int length)
		{
			byte[] tempBuffer;
			int index;

			if (length > 0)
			{
				//Trace the data.
				if (appres.Toggled(Appres.DSTrace))
				{
					int i;

					trace.trace_dsn(">");
					for (i = 0; i < length; i++)
					{
						trace.trace_dsn(" " + Util.ControlSee(buffer[i]));
					}
					trace.trace_dsn("\n");
				}


				//Expand it.
				tempBuffer = new byte[2 * length];
				index = 0;
				int bindex = 0;
				while (length > 0)
				{
					byte c = buffer[bindex++];

					tempBuffer[index++] = c;
					length--;
					if (c == TelnetConstants.IAC)
					{
						tempBuffer[index++] = TelnetConstants.IAC;
					}
					else if (c == (byte)'\r' && (length == 0 || buffer[bindex] != (byte)'\n'))
					{
						tempBuffer[index++] = 0;
					}
				}

				//Send it to the host.
				this.SendRawOutput(tempBuffer, index);
			}
		}


		#endregion Public Methods



		#region Private Methods

		private void LogFileProcessorThreadHandler()
		{
			try
			{
				// Simulate notification of parent
				this.OnTelnetData(parentData, TNEvent.Connect, null);

				// Notify TN
				connectionState = ConnectionState.ConnectedInitial;
				this.OnPrimaryConnectionChanged(true);

				Net_Connected();
				SetHostState(ConnectionState.ConnectedANSI);

				// Now simulate TCP/IP data coming in from the file
				while (!logFileProcessorThread_Quit)
				{
					string text = this.connectionConfig.LogFile.ReadLine();
					if (text == null)
					{
						// Simulate disconnect
						trace.trace_dsn("RCVD disconnect\n");
						//host_disconnect(false);
						// If no data was received then the connection is probably dead

						Console.WriteLine("Disconnected from log file");
						// (We are this thread!)
						this.OnTelnetData(parentData, TNEvent.Disconnect, null);
						// Close thread.
					}
					else if (text.Length >= 11)
					{
						int time = System.Convert.ToInt32(text.Substring(0, 6));
						trace.WriteLine("\n" + text.Substring(7));
						if (text.Substring(9, 2) == "H ")
						{
							text = text.Substring(18);
							if (this.IsPending)
							{
								this.Host_Connected();
								this.Net_Connected();
							}

							lock (this)
							{
								while (text.Length > 1)
								{
									byte v = System.Convert.ToByte(text.Substring(0, 2), 16);
									if (this.tnState == TN3270State.InNeither)
									{
										keyboard.KeyboardLockClear(KeyboardConstants.AwaitingFirst, "telnet_fsm");
										//status_reset();
										this.tnState = TN3270State.ANSI;
									}
									if (TelnetProcessFiniteStateMachine(ref sbBuffer, v) != 0)
									{
										Host_Disconnect(true);
										Disconnect();
										this.disconnectReason = "open3270.LogfileProcessorThreadHandler telnet_fsm error : disconnected";
										return;
									}

									text = text.Substring(2).Trim();
								}

							}
						}
						else if (text.Substring(9, 2) == "C ")
						{
							trace.WriteLine("--client data - should wait for netout before moving to next row. CC=" + logFileSemaphore.Count);
							int length = 0;
							text = text.Substring(18);
							while (text.Length > 1)
							{
								byte v = System.Convert.ToByte(text.Substring(0, 2), 16);
								length++;
								byte netoutbyte = 0;
								try
								{
									netoutbyte = (byte)logClientData.Dequeue();
								}
								catch (System.InvalidOperationException)
								{
									Console.WriteLine("Queue empty - increment empty queue flag");
								}
								if (v != netoutbyte)
								{
									Console.WriteLine("**BUGBUG** " + String.Format("oops - byte is not the same as client buffer. Read {0:x2}'{2}' netout {1:x2}'{3}'", v, netoutbyte, System.Convert.ToChar(Tables.Ebc2Ascii[v]), System.Convert.ToChar(Tables.Ebc2Ascii[netoutbyte])));
								}
								while (!logFileProcessorThread_Quit)
								{
									if (logFileSemaphore.Acquire(1000))
									{
										break;
									}

									if (!mainThread.IsAlive ||
										(mainThread.ThreadState & ThreadState.Stopped) != 0 ||
										(mainThread.ThreadState & ThreadState.StopRequested) != 0
									)
									{
										logFileProcessorThread_Quit = true;
										break;
									}
								}
								if (logFileProcessorThread_Quit)
								{
									break;
								}

								text = text.Substring(2).Trim();
							}
							trace.WriteLine("--client data - acquired " + length + " bytes ok. CC=" + logFileSemaphore.Count);
						}
					}
				}
				// Done
			}
			catch (Exception e)
			{
				Console.WriteLine("Telnet logfile parser exception " + e);
				throw;
			}
			finally
			{
				Console.WriteLine("LogFileProcessor Thread stopped");
			}
		}


		private void ConnectCallback(IAsyncResult ar)
		{
			try
			{
				// Get The connection socket from the callback
				Socket socket1 = (Socket)ar.AsyncState;
				if (socket1.Connected)
				{
					//Notify parent
					this.OnTelnetData(parentData, TNEvent.Connect, null);

					//Notify TN
					this.connectionState = ConnectionState.ConnectedInitial;
					this.OnPrimaryConnectionChanged(true);

					this.Net_Connected();
					this.SetHostState(ConnectionState.ConnectedANSI);

					//Define a new callback to read the data 
					AsyncCallback receiveData = new AsyncCallback(OnReceivedData);

					if (this.Config.UseSSL)
					{
						NetworkStream mNetworkStream = new NetworkStream(socketBase, false);
						SslStream ssl = new SslStream(mNetworkStream, false, new RemoteCertificateValidationCallback(cryptocallback));
						ssl.AuthenticateAsClient(this.address);
						trace.WriteLine("SSL Connection made. Encryption is '" + ssl.IsEncrypted + "'");

						this.socketStream = ssl;
					}
					else
					{
						this.socketStream = new NetworkStream(socketBase, false);
					}

					// Begin reading data asyncronously
					this.socketStream.BeginRead(byteBuffer, 0, byteBuffer.Length, receiveData, socketStream);
					trace.trace_dsn("\nConnectCallback : SocketStream.BeginRead called to read asyncronously\n");
				}
				else
				{
					this.disconnectReason = "Unable to connect to host - timeout on connect";
					this.OnTelnetData(parentData, TNEvent.Error, "Connect callback returned 'not connected'");
					// spurious, but to meet spec
					this.connectionState = ConnectionState.NotConnected;
					
					this.OnPrimaryConnectionChanged(false);

				}
			}
			catch (Exception ex)
			{
				//Console.WriteLine("Setup Receive callback failed " + ex);
				trace.trace_event("%s", "Exception occured connecting to host. Disconnecting\n\n" + ex);
				this.Disconnect();
				this.disconnectReason = "exception during telnet connect callback";
			}
		}


		/// <summary>
		/// This section is for screen syncronization with the user of this library
		/// StartedReceivingCount gets incremented each time OnReceiveData is invoked by the socket.
		/// (CFC,Jr, 2008/06/26)
		/// </summary>
		private void NotifyStartedReceiving()
		{
			lock (receivingPadlock)
			{
				startedReceivingCount++;
			}
			trace.trace_dsn("NotifyStartedReceiving : startedReceivingCount = " + StartedReceivingCount.ToString() + Environment.NewLine);
		}


		/// <summary>
		/// Called from the socket when data is available
		/// </summary>
		/// <param name="ar"></param>
		private void OnReceivedData(IAsyncResult ar)
		{
			// (CFC, added for screen syncronization)
			try
			{
				this.NotifyStartedReceiving();
			}
			catch
			{
			}

			// Get The connection socket from the callback
			Stream streamSocket = (Stream)ar.AsyncState;
			bool disconnectme = false;

			// Is socket closing
			disconnectReason = null;

			if (socketBase == null || socketBase.Connected == false)
			{
				disconnectme = true;
				if (string.IsNullOrEmpty(disconnectReason))
				{
					disconnectReason = "Host dropped connection or not connected in telnet.OnReceivedData";
				}
			}
			else
			{
				try
				{
					// Get The data , if any
					int nBytesRec = 0;
					nBytesRec = streamSocket.EndRead(ar);

					ansiData = 0;
					if (nBytesRec > 0)
					{
						if (this.IsPending)
						{
							this.Host_Connected();
							this.Net_Connected();
						}

						trace.trace_netdata('<', byteBuffer, nBytesRec);
						bytesReceived += nBytesRec;

						int i;

						if (showParseError)
						{
							trace.trace_dsn("ShowParseError called - throw exception");
							throw new ApplicationException("ShowParseError exception test requested");
						}

						byte[] data = new byte[nBytesRec];

						lock (this)
						{
							//CFCJR sync up sequence number
							if (nBytesRec >= 5 && (currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_RESPONSES)) != 0)  // CFCJR
							{
								eTransmitSequence = (byteBuffer[3] << 8) | byteBuffer[4];
								eTransmitSequence = (eTransmitSequence + 1) & 0x7FFF;

								trace.trace_dsn("\nxmit sequence set to " + eTransmitSequence.ToString() + "\n");
							}

							for (i = 0; i < nBytesRec; i++)
							{

								if (this.tnState == TN3270State.InNeither)
								{
									this.keyboard.KeyboardLockClear(KeyboardConstants.AwaitingFirst, "telnet_fsm");
									//status_reset();
									this.tnState = TN3270State.ANSI;
								}
								if (TelnetProcessFiniteStateMachine(ref sbBuffer, byteBuffer[i]) != 0)
								{
									this.Host_Disconnect(true);
									this.Disconnect();
									this.disconnectReason = "telnet_fsm error in OnReceiveData"; //CFC,Jr. 7/8/2008
									return;
								}
							}
						}

						// Define a new Callback to read the data 
						AsyncCallback receiveData = new AsyncCallback(this.OnReceivedData);
						// Begin reading data asyncronously
						this.socketStream.BeginRead(byteBuffer, 0, byteBuffer.Length, receiveData, socketStream);
						trace.trace_dsn("\nOnReceiveData : SocketStream.BeginRead called to read asyncronously\n");
					}
					else
					{
						disconnectme = true;
						this.disconnectReason = "No data received in telnet.OnReceivedData, disconnecting";
					}
				}
				catch (System.ObjectDisposedException)
				{
					disconnectme = true;
					disconnectReason = "Client dropped connection : Using Disposed Object Exception";
				}
				catch (Exception e)
				{

					trace.trace_event("%s", "Exception occured processing Telnet buffer. Disconnecting\n\n" + e);
					disconnectme = true;
					disconnectReason = "Exception in data stream (" + e.Message + "). Connection dropped.";
				}
			}

			if (disconnectme)
			{
				bool closeWasRequested = closeRequested;
				trace.trace_dsn("RCVD disconnect\n");
				this.Host_Disconnect(false);
				// If no data was received then the connection is probably dead

				this.Disconnect();

				if (closeWasRequested)
				{
					this.OnTelnetData(parentData, TNEvent.Disconnect, null);
				}
				else
				{
					this.OnTelnetData(parentData, TNEvent.DisconnectUnexpected, null);
				}

				this.closeRequested = false;

			}
		}


		protected void OnTelnetData(object parentData, TNEvent eventType, string text)
		{
			if (this.telnetDataEventOccurred != null)
			{
				this.telnetDataEventOccurred(parentData, eventType, text);
			}
		}


		private string DumpToString(byte[] data, int length)
		{
			string output = " ";
			for (int i = 0; i < length; i++)
			{
				output += String.Format("{0:x2}", data[i]) + " ";
			}
			return output;
		}


		private string ToHex(int n)
		{
			return String.Format("{0:x2}", n);
		}


		private int Shift(int n)
		{
			return (1 << (n));
		}
		

		private void SendRawOutput(NetBuffer smk)
		{
			byte[] bytes = smk.Data;
			this.SendRawOutput(bytes);
		}


		private void SendRawOutput(byte[] smkBuffer)
		{
			this.SendRawOutput(smkBuffer, smkBuffer.Length);
		}


		private void SendRawOutput(byte[] smkBuffer, int length)
		{
			if (this.parseLogFileOnly)
			{
				trace.WriteLine("\nnet_rawout2 [" + length + "]" + DumpToString(smkBuffer, length) + "\n");

				// If we're reading the log file, allow the next bit of data to flow
				if (this.logFileSemaphore != null)
				{
					//trace.WriteLine("net_rawout2 - CC="+mLogFileSemaphore.Count);
					for (int i = 0; i < length; i++)
					{
						this.logClientData.Enqueue(smkBuffer[i]);
					}
					this.logFileSemaphore.Release(length);
				}
			}
			else
			{
				trace.trace_netdata('>', smkBuffer, length);
				this.socketStream.Write(smkBuffer, 0, length);
			}
		}


		private void Store3270Input(byte c)
		{
			if (this.inputBufferIndex >= this.inputBuffer.Length)
			{
				byte[] temp = new byte[this.inputBuffer.Length + 256];
				this.inputBuffer.CopyTo(temp, 0);
				this.inputBuffer = temp;
			}
			this.inputBuffer[inputBufferIndex++] = c;
		}


		private void SetHostState(ConnectionState new_cstate)
		{
			bool now3270 = (new_cstate == ConnectionState.Connected3270 ||
				new_cstate == ConnectionState.ConnectedSSCP ||
				new_cstate == ConnectionState.Connected3270E);

			this.connectionState = new_cstate;
			this.controller.Is3270 = now3270;

			this.OnConnected3270(now3270);
		}

		void CheckIn3270()
		{
			ConnectionState newConnectionState = ConnectionState.NotConnected;


			if (clientOptions[TelnetConstants.TELOPT_TN3270E] != 0)
			{
				if (!tn3270e_negotiated)
					newConnectionState = ConnectionState.ConnectedInitial3270E;
				else
				{
					switch (tn3270eSubmode)
					{
						case TN3270ESubmode.None:
							newConnectionState = ConnectionState.ConnectedInitial3270E;
							break;
						case TN3270ESubmode.NVT:
							newConnectionState = ConnectionState.ConnectedNVT;
							break;
						case TN3270ESubmode.Mode3270:
							newConnectionState = ConnectionState.Connected3270E;
							break;
						case TN3270ESubmode.SSCP:
							newConnectionState = ConnectionState.ConnectedSSCP;
							break;
					}
				}
			}
			else
				if (clientOptions[TelnetConstants.TELOPT_BINARY] != 0 &&
				clientOptions[TelnetConstants.TELOPT_EOR] != 0 &&
				clientOptions[TelnetConstants.TELOPT_TTYPE] != 0 &&
				hostOptions[TelnetConstants.TELOPT_BINARY] != 0 &&
				hostOptions[TelnetConstants.TELOPT_EOR] != 0)
				{
					newConnectionState = ConnectionState.Connected3270;
				}
				else if (connectionState == ConnectionState.ConnectedInitial)
				{
					//Nothing has happened, yet.
					return;
				}
				else
				{
					newConnectionState = ConnectionState.ConnectedANSI;
				}

			if (newConnectionState != connectionState)
			{
				bool wasInE = (connectionState >= ConnectionState.ConnectedInitial3270E);

				trace.trace_dsn("Now operating in " + newConnectionState + " mode\n");
				this.SetHostState(newConnectionState);


				//If we've now switched between non-TN3270E mode and TN3270E mode, reset the LU list so we can try again in the new mode.
				if (lus != null && wasInE != IsE)
				{
					currentLUIndex = 0;
				}

				//Allocate the initial 3270 input buffer.
				if (newConnectionState >= ConnectionState.ConnectedInitial && this.inputBuffer == null)
				{
					this.inputBuffer = new byte[256];
					this.inputBufferIndex = 0;
				}

				//Reinitialize line mode.
				if ((newConnectionState == ConnectionState.ConnectedANSI && linemode) || newConnectionState == ConnectionState.ConnectedNVT)
				{
					Console.WriteLine("cooked_init-bad");
					//cooked_init();
				}

				//If we fell out of TN3270E, remove the state.
				if (clientOptions[TelnetConstants.TELOPT_TN3270E] == 0)
				{
					this.tn3270e_negotiated = false;
					this.tn3270eSubmode = TN3270ESubmode.None;
					this.tn3270eBound = false;
				}
				// Notify script
				this.controller.Continue();
			}
		}


		private bool ProcessEOR()
		{
			int i;
			bool result = false;

			if (!syncing && inputBufferIndex != 0)
			{

				if (connectionState >= ConnectionState.ConnectedInitial3270E)
				{
					TnHeader h = new TnHeader(inputBuffer);
					PDS rv;

					trace.trace_dsn("RCVD TN3270E(datatype: " + h.DataType + ", request: " + h.RequestFlag + ", response: " + h.ResponseFlag + ", seq: " + (h.SequenceNumber[0] << 8 | h.SequenceNumber[1]) + ")\n");

					switch (h.DataType)
					{
						case DataType3270.Data3270:
							{
								if ((currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE)) == 0 || tn3270eBound)
								{
									this.tn3270eSubmode = TN3270ESubmode.Mode3270;
									this.CheckIn3270();
									this.responseRequired = h.ResponseFlag;
									rv = controller.ProcessDS(inputBuffer, TnHeader.EhSize, inputBufferIndex - TnHeader.EhSize);
									//Console.WriteLine("*** RV = "+rv);
									//Console.WriteLine("*** response_required = "+response_required);						
									if (rv < 0 && responseRequired != TnHeader.HeaderReponseFlags.NoResponse)
									{
										this.SendNak();
									}
									else if (rv == PDS.OkayNoOutput && responseRequired == TnHeader.HeaderReponseFlags.AlwaysResponse)
									{
										SendAck();
									}
									this.responseRequired = TnHeader.HeaderReponseFlags.NoResponse;
								}
								result = false;
								break;
							}
						case DataType3270.BindImage:
							{
								if ((currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE)) != 0)
								{
									tn3270eBound = true;
									this.CheckIn3270();
								}

								result = false;
								break;
							}
						case DataType3270.Unbind:
							{
								if ((currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE)) != 0)
								{
									tn3270eBound = false;
									if (tn3270eSubmode == TN3270ESubmode.Mode3270)
									{
										tn3270eSubmode = TN3270ESubmode.None;
									}
									this.CheckIn3270();
								}
								result = false;
								break;
							}
						case DataType3270.NvtData:
							{
								//In tn3270e NVT mode
								tn3270eSubmode = TN3270ESubmode.NVT;
								this.CheckIn3270();
								for (i = 0; i < inputBufferIndex; i++)
								{
									ansi.ansi_process(inputBuffer[i]);
								}
								result = false;
								break;
							}
						case DataType3270.SscpLuData:
							{
								if ((currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE)) != 0)
								{
									tn3270eSubmode = TN3270ESubmode.SSCP;
									this.CheckIn3270();
									this.controller.WriteSspcLuData(inputBuffer, TnHeader.EhSize, inputBufferIndex - TnHeader.EhSize);
								}

								result = false;
								break;
							}
						default:
							{
								//Should do something more extraordinary here.
								result = false;
								break;
							}

					}
				}
				else
				{
					this.controller.ProcessDS(inputBuffer, 0, inputBufferIndex);
				}
			}
			return result;
		}



		/// <summary>
		/// Send acknowledgment
		/// </summary>
		void SendAck()
		{
			this.Ack(true);
		}


		/// <summary>
		/// Send a TN3270E negative response to the server
		/// </summary>
		/// <param name="rv"></param>
		void SendNak()
		{
			this.Ack(false);
		}


		/// <summary>
		/// Sends an ACK or NAK
		/// </summary>
		/// <param name="positive">True to send ACK (positive acknowledgment), otherwise it NAK will be sent</param>
		private void Ack(bool positive)
		{
			byte[] responseBuffer = new byte[9];
			TnHeader header = new TnHeader();
			TnHeader header_in = new TnHeader(inputBuffer);
			int responseLength = TnHeader.EhSize;

			header.DataType = DataType3270.Response;
			header.RequestFlag = 0;
			header.ResponseFlag = positive ? TnHeader.HeaderReponseFlags.PositiveResponse : TnHeader.HeaderReponseFlags.NegativeResponse;

			header.SequenceNumber[0] = header_in.SequenceNumber[0];
			header.SequenceNumber[1] = header_in.SequenceNumber[1];
			header.OnToByte(responseBuffer);

			if (header.SequenceNumber[1] == TelnetConstants.IAC)
			{
				responseBuffer[responseLength++] = TelnetConstants.IAC;
			}

			responseBuffer[responseLength++] = positive ? TnHeader.HeaderReponseData.PosDeviceEnd : TnHeader.HeaderReponseData.NegCommandReject;
			responseBuffer[responseLength++] = TelnetConstants.IAC;
			responseBuffer[responseLength++] = TelnetConstants.EOR;

			trace.trace_dsn("SENT TN3270E(RESPONSE " + (positive ? "POSITIVE" : "NEGATIVE") + "-RESPONSE: " +
				(header_in.SequenceNumber[0] << 8 | header_in.SequenceNumber[1]) + ")\n");

			SendRawOutput(responseBuffer, responseLength);
		}

		/// <summary>
		/// Set the global variable 'linemode', which indicates whether we are in character-by-character mode or line mode.
		/// </summary>
		/// <param name="init"></param>
		private void CheckLineMode(bool init)
		{
			bool wasline = linemode;

			/*
				* The next line is a deliberate kluge to effectively ignore the SGA
				* option.  If the host will echo for us, we assume
				* character-at-a-time; otherwise we assume fully cooked by us.
				*
				* This allows certain IBM hosts which volunteer SGA but refuse
				* ECHO to operate more-or-less normally, at the expense of
				* implementing the (hopefully useless) "character-at-a-time, local
				* echo" mode.
				*
				* We still implement "switch to line mode" and "switch to character
				* mode" properly by asking for both SGA and ECHO to be off or on, but
				* we basically ignore the reply for SGA.
				*/
			linemode = (hostOptions[TelnetConstants.TELOPT_ECHO] == 0);

			if (init || linemode != wasline)
			{
				this.OnConnectedLineMode();
				if (!init)
				{
					trace.trace_dsn("Operating in %s mode.\n",
						linemode ? "line" : "character-at-a-time");
				}
				if (IsAnsi && linemode)
					CookedInitialized();
			}
		}


		private string GetCommand(int index)
		{
			return TelnetConstants.TelnetCommands[index - TelnetConstants.TELCMD_FIRST];
		}


		private string GetOption(int index)
		{
			string option;

			if (index <= TelnetConstants.TELOPT_TN3270E)
			{
				option = TelnetConstants.TelnetOptions[index];
			}
			else
			{
				option = index == TelnetConstants.TELOPT_TN3270E ? "TN3270E" : index.ToString();
			}
			return option;
		}


		private byte ParseControlCharacter(string s)
		{
			if (s == null || s.Length == 0)
				return 0;

			if (s.Length > 1)
			{
				if (s[0] != '^')
					return 0;
				else if (s[1] == '?')
					return (byte)0x7f;
				else
					return (byte)(s[1] - '@');
			}
			else
				return (byte)s[0];
		}


		/// <summary>
		/// Send output in ANSI mode, including cooked-mode processing if appropriate.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="length"></param>
		void Cook(byte[] buffer, int length)
		{
			if (!IsAnsi || (keyboard.keyboardLock & KeyboardConstants.AwaitingFirst) != 0)
			{
				return;
			}
			if (linemode)
			{
				trace.WriteLine("**BUGBUG** net_cookedout not implemented for line mode");
				return;
			}
			else
			{
				this.SendCookedOut(buffer, length);
			}

		}


		void AnsiProcessString(string data)
		{
			int i;
			for (i = 0; i < data.Length; i++)
			{
				ansi.ansi_process((byte)data[i]);
			}
		}


		void CookedInitialized()
		{
			Console.WriteLine("--bugbug--cooked-init())");
		}

		#endregion Private Methods


		public void Output(NetBuffer obptr)
		{
			NetBuffer outputBuffer = new NetBuffer();

			//Set the TN3720E header.
			if (IsTn3270E || IsSscp)
			{
				TnHeader header = new TnHeader();

				//Check for sending a TN3270E response.
				if (this.responseRequired == TnHeader.HeaderReponseFlags.AlwaysResponse)
				{
					this.SendAck();
					this.responseRequired = TnHeader.HeaderReponseFlags.NoResponse;
				}

				//Set the outbound TN3270E header.
				header.DataType = IsTn3270E ? DataType3270.Data3270 : DataType3270.SscpLuData;
				header.RequestFlag = 0;

				// CFCJR:
				// Request a response if negotiated to do so

				//JNU: THIS is the code that broke everything and caused the Sense 00004002 failure
				//if ((e_funcs & E_OPT(TN3270E_FUNC_RESPONSES)) != 0)
				//	h.response_flag = TN3270E_HEADER.TN3270E_RSF_ALWAYS_RESPONSE;
				//else
				header.ResponseFlag = 0;

				header.SequenceNumber[0] = (byte)((eTransmitSequence >> 8) & 0xff);
				header.SequenceNumber[1] = (byte)(eTransmitSequence & 0xff);

				trace.trace_dsn("SENT TN3270E(%s %s %u)\n",
					IsTn3270E ? "3270-DATA" : "SSCP-LU-DATA",
					(header.ResponseFlag == TnHeader.HeaderReponseFlags.ErrorResponse) ? "ERROR-RESPONSE" : ((header.ResponseFlag == TnHeader.HeaderReponseFlags.AlwaysResponse) ? "ALWAYS-RESPONSE" : "NO-RESPONSE"),
					eTransmitSequence);

				if (this.connectionConfig.IgnoreSequenceCount == false &&
					(currentOptionMask & Shift(TelnetConstants.TN3270E_FUNC_RESPONSES)) != 0)
				{
					eTransmitSequence = (eTransmitSequence + 1) & 0x7fff;
				}

				header.AddToNetBuffer(outputBuffer);
			}

			int i;
			byte[] data = obptr.Data;
			/* Copy and expand IACs. */
			for (i = 0; i < data.Length; i++)
			{
				outputBuffer.Add(data[i]);
				if (data[i] == TelnetConstants.IAC)
					outputBuffer.Add(TelnetConstants.IAC);
			}
			/* Append the IAC EOR and transmit. */
			outputBuffer.Add(TelnetConstants.IAC);
			outputBuffer.Add(TelnetConstants.EOR);
			SendRawOutput(outputBuffer);

			trace.trace_dsn("SENT EOR\n");
			bytesSent++;
		}


		public void Break()
		{
			byte[] buf = new byte[] { TelnetConstants.IAC, TelnetConstants.BREAK };

			//Should we first send TELNET synch?
			SendRawOutput(buf, buf.Length);
			trace.trace_dsn("SENT BREAK\n");
		}


		public void Interrupt()
		{
			byte[] buf = new byte[] { TelnetConstants.IAC, TelnetConstants.IP };

			//Should we first send TELNET synch?
			this.SendRawOutput(buf, buf.Length);
			trace.trace_dsn("SENT IP\n");
		}


		/// <summary>
		/// Send user data out in ANSI mode, without cooked-mode processing.
		/// </summary>
		/// <param name="buf"></param>
		/// <param name="len"></param>
		void SendCookedOut(byte[] buf, int len)
		{
			if (appres.Toggled(Appres.DSTrace))
			{
				int i;

				trace.trace_dsn(">");
				for (i = 0; i < len; i++)
				{
					trace.trace_dsn(" %s", Util.ControlSee(buf[i]));
				}
				trace.trace_dsn("\n");
			}
			this.SendRawOutput(buf, len);
		}



		/// <summary>
		/// Send a Telnet window size sub-option negotation.
		/// </summary>
		void SendNaws()
		{
			NetBuffer buffer = new NetBuffer();

			buffer.Add(TelnetConstants.IAC);
			buffer.Add(TelnetConstants.SB);
			buffer.Add(TelnetConstants.TELOPT_NAWS);
			buffer.Add16(controller.MaxColumns);
			buffer.Add16(controller.MaxRows);
			buffer.Add(TelnetConstants.IAC);
			buffer.Add(TelnetConstants.SE);
			this.SendRawOutput(buffer);
			trace.trace_dsn("SENT %s NAWS %d %d %s\n", GetCommand(TelnetConstants.SB), controller.MaxColumns, controller.MaxRows, GetCommand(TelnetConstants.SE));
		}


		/// <summary>
		/// Negotiation of TN3270E options.
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns>Returns 0 if okay, -1 if we have to give up altogether.</returns>
		int Tn3270e_Negotiate(NetBuffer buffer)
		{
			int bufferLength;
			int capabilitiesRequested;
			NetBuffer hostWants = null;

			//Find out how long the subnegotiation buffer is.
			for (bufferLength = 0; ; bufferLength++)
			{
				if (buffer.Data[bufferLength] == TelnetConstants.SE)
					break;
			}

			trace.trace_dsn("TN3270E ");

			switch (buffer.Data[1])
			{
				case TnHeader.Ops.Send:
					{
						if (buffer.Data[2] == TnHeader.Ops.DeviceType)
						{
							//Host wants us to send our device type.
							trace.trace_dsn("SEND DEVICE-TYPE SE\n");
							Tn3270e_SendRequest();
						}
						else
						{
							trace.trace_dsn("SEND ??%u SE\n", buffer.Data[2]);
						}
						break;
					}
				case TnHeader.Ops.DeviceType:
					{
						//Device type negotiation
						trace.trace_dsn("DEVICE-TYPE ");

						switch (buffer.Data[2])
						{
							case TnHeader.Ops.Is:
								{
									//Device type success.
									int tnLength, snLength;

									//Isolate the terminal type and session.
									tnLength = 0;
									while (buffer.Data[3 + tnLength] != TelnetConstants.SE && buffer.Data[3 + tnLength] != TnHeader.Ops.Connect)
									{
										tnLength++;
									}

									snLength = 0;
									if (buffer.Data[3 + tnLength] == TnHeader.Ops.Connect)
									{
										while (buffer.Data[3 + tnLength + 1 + snLength] != TelnetConstants.SE)
										{
											snLength++;
										}
									}

									trace.trace_dsn("IS " + buffer.AsString(3, tnLength) + " CONNECT " + buffer.AsString(3 + tnLength + 1, snLength) + " SE\n");


									//Remember the LU
									if (tnLength != 0)
									{
										if (tnLength > TelnetConstants.LU_MAX)
										{
											tnLength = TelnetConstants.LU_MAX;
										}

										reportedType = buffer.AsString(3, tnLength);
										connectedType = reportedType;
									}
									if (snLength != 0)
									{
										if (snLength > TelnetConstants.LU_MAX)
										{
											snLength = TelnetConstants.LU_MAX;
										}

										reportedLu = buffer.AsString(3 + tnLength + 1, snLength);
										connectedLu = reportedLu;
									}

									// Tell them what we can do.
									this.Tn3270e_Subneg_Send(TnHeader.Ops.Request, currentOptionMask);
									break;
								}
							case TnHeader.Ops.Reject:
								{
									//Device type failure.
									trace.trace_dsn("REJECT REASON %s SE\n", TelnetConstants.GetReason(buffer.Data[4]));

									if (buffer.Data[4] == TnHeader.NegotiationReasonCodes.InvDeviceType ||
										buffer.Data[4] == TnHeader.NegotiationReasonCodes.UnsupportedReq)
									{
										this.Backoff_TN3270e("Host rejected device type or request type");
										break;
									}

									this.currentLUIndex++;

									if (this.currentLUIndex < this.lus.Count)
									{
										//Try the next LU.
										this.Tn3270e_SendRequest();
									}
									else if (lus != null)
									{
										//No more LUs to try.  Give up.
										this.Backoff_TN3270e("Host rejected resource(s)");
									}
									else
									{
										this.Backoff_TN3270e("Device type rejected");
									}

									break;
								}
							default:
								{
									trace.trace_dsn("??%u SE\n", buffer.Data[2]);
									break;
								}
						}
						break;
					}
				case TnHeader.Ops.Functions:
					{
						//Functions negotiation.
						trace.trace_dsn("FUNCTIONS ");

						switch (buffer.Data[2])
						{
							case TnHeader.Ops.Request:
								{
									//Host is telling us what functions it wants
									hostWants = buffer.CopyFrom(3, bufferLength - 3);
									trace.trace_dsn("REQUEST %s SE\n", GetFunctionCodesAsText(hostWants));

									capabilitiesRequested = tn3270e_fdecode(hostWants);
									if ((capabilitiesRequested == currentOptionMask) || (currentOptionMask & ~capabilitiesRequested) != 0)
									{
										//They want what we want, or less.  Done.
										currentOptionMask = capabilitiesRequested;
										this.Tn3270e_Subneg_Send(TnHeader.Ops.Is, currentOptionMask);
										tn3270e_negotiated = true;
										trace.trace_dsn("TN3270E option negotiation complete.\n");
										this.CheckIn3270();
									}
									else
									{
										// They want us to do something we can't.
										//Request the common subset.
										currentOptionMask &= capabilitiesRequested;
										this.Tn3270e_Subneg_Send(TnHeader.Ops.Request, currentOptionMask);
									}
									break;
								}
							case TnHeader.Ops.Is:
								{
									//They accept our last request, or a subset thereof.
									hostWants = buffer.CopyFrom(3, bufferLength - 3);
									trace.trace_dsn("IS %s SE\n", this.GetFunctionCodesAsText(hostWants));
									capabilitiesRequested = this.tn3270e_fdecode(hostWants);
									if (capabilitiesRequested != currentOptionMask)
									{
										if ((currentOptionMask & ~capabilitiesRequested) != 0)
										{
											//They've removed something.  This is technically illegal, but we can live with it.
											currentOptionMask = capabilitiesRequested;
										}
										else
										{
											//They've added something.  Abandon TN3270E.  They're brain dead.
											this.Backoff_TN3270e("Host illegally added function(s)");
											break;
										}
									}

									tn3270e_negotiated = true;
									trace.trace_dsn("TN3270E option negotiation complete.\n");
									this.CheckIn3270();
									break;
								}
							default:
								{
									trace.trace_dsn("??%u SE\n", buffer.Data[2]);
									break;
								}
						}
						break;
					}
				default:
					trace.trace_dsn("??%u SE\n", buffer.Data[1]);
					break;
			}

			//Good enough for now.
			return 0;

		}


		/// <summary>
		/// Send a TN3270E terminal type request.
		/// </summary>
		public void Tn3270e_SendRequest()
		{
			NetBuffer buffer = new NetBuffer();
			string try_lu = null;

			if (this.lus != null)
			{
				try_lu = this.lus[this.currentLUIndex] as String;
			}

			buffer.Add(TelnetConstants.IAC);
			buffer.Add(TelnetConstants.SB);
			buffer.Add(TelnetConstants.TELOPT_TN3270E);
			buffer.Add(TnHeader.Ops.DeviceType);
			buffer.Add(TnHeader.Ops.Request);
			string temp = termType;

			// Replace 3279 with 3278 as per the RFC
			temp = temp.Replace("3279", "3278");
			buffer.Add(temp);
			if (try_lu != null)
			{
				buffer.Add(TnHeader.Ops.Connect);
				buffer.Add(try_lu);
			}
			buffer.Add(TelnetConstants.IAC);
			buffer.Add(TelnetConstants.SE);
			SendRawOutput(buffer);

			trace.trace_dsn("SENT %s %s DEVICE-TYPE REQUEST %s %s%s%s\n",
						  GetCommand(TelnetConstants.SB),
				GetOption(TelnetConstants.TELOPT_TN3270E),
				termType,
				(try_lu != null) ? " CONNECT " : "",
				(try_lu != null) ? try_lu : "",
				GetCommand(TelnetConstants.SE));

		}


		private string GetFunctionName(int i)
		{
			string functionName = string.Empty;

			if (i >= 0 && i < TelnetConstants.FunctionNames.Length)
			{
				functionName = TelnetConstants.FunctionNames[i];
			}
			else
			{
				functionName = "?[function_name=" + i + "]?";
			}

			return functionName;
		}


		/// <summary>
		/// Expand a string of TN3270E function codes into text.
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public string GetFunctionCodesAsText(NetBuffer buffer)
		{
			int i;
			string temp = "";
			byte[] bufferData = buffer.Data;

			if (bufferData.Length == 0)
			{
				return ("(null)");
			}
			for (i = 0; i < bufferData.Length; i++)
			{
				if (temp != null)
				{
					temp += " ";
				}
				temp += GetFunctionName(bufferData[i]);
			}
			return temp;
		}


		/// <summary>
		/// Expand the current TN3270E function codes into text.
		/// </summary>
		/// <returns></returns>
		string GetCurrentOptionsAsText()
		{
			int i;
			string temp = "";

			if (currentOptionMask == 0 || !IsE)
				return null;
			for (i = 0; i < 32; i++)
			{
				if ((currentOptionMask & Shift(i)) != 0)
				{
					if (temp != null)
					{
						temp += " ";
					}
					temp += GetFunctionName(i);
				}
			}
			return temp;
		}



		/// <summary>
		/// Transmit a TN3270E FUNCTIONS REQUEST or FUNCTIONS IS message.
		/// </summary>
		/// <param name="op"></param>
		/// <param name="funcs"></param>
		void Tn3270e_Subneg_Send(byte op, int funcs)
		{
			byte[] protoBuffer = new byte[7 + 32];
			int length;
			int i;

			//Construct the buffers.
			protoBuffer[0] = TelnetConstants.FunctionsReq[0];
			protoBuffer[1] = TelnetConstants.FunctionsReq[1];
			protoBuffer[2] = TelnetConstants.FunctionsReq[2];
			protoBuffer[3] = TelnetConstants.FunctionsReq[3];
			protoBuffer[4] = op;
			length = 5;

			for (i = 0; i < 32; i++)
			{
				if ((funcs & Shift(i)) != 0)
				{
					protoBuffer[length++] = (byte)i;
				}
			}

			//Complete and send out the protocol message.
			protoBuffer[length++] = TelnetConstants.IAC;
			protoBuffer[length++] = TelnetConstants.SE;
			SendRawOutput(protoBuffer, length);

			//Complete and send out the trace text.
			trace.trace_dsn("SENT %s %s FUNCTIONS %s %s %s\n",
				GetCommand(TelnetConstants.SB), GetOption(TelnetConstants.TELOPT_TN3270E),
				(op == TnHeader.Ops.Request) ? "REQUEST" : "IS",
				GetFunctionCodesAsText(new NetBuffer(protoBuffer, 5, length - 7)),
				GetCommand(TelnetConstants.SE));
		}


		//Translate a string of TN3270E functions into a bit-map.
		int tn3270e_fdecode(NetBuffer netbuf)
		{
			int r = 0;
			int i;
			byte[] buf = netbuf.Data;

			//Note that this code silently ignores options >= 32.
			for (i = 0; i < buf.Length; i++)
			{
				if (buf[i] < 32)
				{
					r |= Shift(buf[i]);
				}
			}
			return r;
		}


		/// <summary>
		/// Back off of TN3270E.
		/// </summary>
		/// <param name="why"></param>
		void Backoff_TN3270e(string why)
		{
			trace.trace_dsn("Aborting TN3270E: %s\n", why);

			//Tell the host 'no'
			wontDoOption[2] = TelnetConstants.TELOPT_TN3270E;
			SendRawOutput(wontDoOption, wontDoOption.Length);
			trace.trace_dsn("SENT %s %s\n", GetCommand(TelnetConstants.WONT), GetOption(TelnetConstants.TELOPT_TN3270E));

			//Restore the LU list; we may need to run it again in TN3270 mode.
			this.currentLUIndex = 0;

			//Reset our internal state.
			clientOptions[TelnetConstants.TELOPT_TN3270E] = 0;
			this.CheckIn3270();
		}


		protected virtual void OnPrimaryConnectionChanged(bool success)
		{
			this.ReactToConnectionChange(success);

			if (this.PrimaryConnectionChanged != null)
			{
				this.PrimaryConnectionChanged(this, new PrimaryConnectionChangedArgs(success));
			}
		}


		
		

		
		protected virtual void OnConnectionPending()
		{
			if (this.ConnectionPending != null)
			{
				this.ConnectionPending(this, EventArgs.Empty);
			}
		}

		
		protected virtual void OnConnected3270(bool is3270)
		{
			this.ReactToConnectionChange(is3270);

			if (this.Connected3270 != null)
			{
				this.Connected3270(this, new Connected3270EventArgs(is3270));
			}
		}


		

		
		protected virtual void OnConnectedLineMode()
		{
			if (this.ConnectedLineMode != null)
			{
				this.ConnectedLineMode(this, EventArgs.Empty);
			}
		}
		
		

		//public void Status_Changed(StCallback id, bool v)
		//{
		//	int i;
		//	//var b = StCallback.Mode3270;

		//	for (i = 0; i < this.statusChangeList.Count; i++)
		//	{
		//		StatusChangeItem item = (StatusChangeItem)statusChangeList[i];
		//		if (item.id == id)
		//		{
		//			item.proc(v);
		//		}
		//	}
		//}


		void Host_Connected()
		{
			connectionState = ConnectionState.ConnectedInitial;
			this.OnPrimaryConnectionChanged(true);
		}


		void Net_Connected()
		{
			trace.trace_dsn("NETCONNECTED Connected to %s, port %u.\n", this.address, this.port);

			//Set up telnet options 
			int i;
			for (i = 0; i < clientOptions.Length; i++)
			{
				clientOptions[i] = 0;
			}
			for (i = 0; i < hostOptions.Length; i++)
			{
				hostOptions[i] = 0;
			}
			if (this.connectionConfig.IgnoreSequenceCount)
			{
				currentOptionMask = Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE) |
					Shift(TelnetConstants.TN3270E_FUNC_SYSREQ);
			}
			else
			{
				currentOptionMask = Shift(TelnetConstants.TN3270E_FUNC_BIND_IMAGE) |
					Shift(TelnetConstants.TN3270E_FUNC_RESPONSES) |
					Shift(TelnetConstants.TN3270E_FUNC_SYSREQ);
			}
			eTransmitSequence = 0;
			responseRequired = TnHeader.HeaderReponseFlags.NoResponse;
			telnetState = TelnetState.Data;

			//Clear statistics and flags
			bytesReceived = 0;
			bytesSent = 0;
			syncing = false;
			tn3270e_negotiated = false;
			tn3270eSubmode = TN3270ESubmode.None;
			tn3270eBound = false;

			CheckLineMode(true);

		}


		void Host_Disconnect(bool failed)
		{
			if (IsConnected || IsPending)
			{
				this.Disconnect();

				//Remember a disconnect from ANSI mode, to keep screen tracing in sync.
				trace.Stop(IsAnsi);
				connectionState = ConnectionState.NotConnected;

				//Propagate the news to everyone else.
				this.OnPrimaryConnectionChanged(false);
			}
		}


		public void RestartReceive()
		{
			// Define a new Callback to read the data 
			AsyncCallback receiveData = new AsyncCallback(OnReceivedData);
			// Begin reading data asyncronously
			socketStream.BeginRead(byteBuffer, 0, byteBuffer.Length, receiveData, socketStream);
			trace.trace_dsn("\nRestartReceive : SocketStream.BeginRead called to read asyncronously\n");
		}


		public bool WaitForConnect()
		{
			while (!IsAnsi && !Is3270)
			{
				System.Threading.Thread.Sleep(100);
				if (!IsResolving)
				{
					this.disconnectReason = "Timeout waiting for connection";
					return false;
				}
			}
			return true;
		}


		public void test_enter()
		{
			Console.WriteLine("state = " + connectionState);

			if ((keyboard.keyboardLock & KeyboardConstants.OiaMinus) != 0)
			{
				Console.WriteLine("--KL_OIA_MINUS");
				return;
			}
			else if ((keyboard.keyboardLock) != 0)
			{
				Console.WriteLine("queue key - " + keyboard.keyboardLock);
				throw new ApplicationException("Sorry, queue key is not implemented, please contact mikewarriner@gmail.com for assistance");
			}
			else
			{
				Console.WriteLine("do key");
				keyboard.HandleAttentionIdentifierKey(AID.Enter);
			}

		}


		public bool WaitFor(SmsState what, int timeout)
		{
			lock (this)
			{
				WaitState = what;
				WaitEvent.Reset();
				// Are we already there?
				controller.Continue();
			}
			if (WaitEvent.WaitOne(timeout, false))
			{
				return true;
			}
			else
			{
				lock (this)
				{
					WaitState = SmsState.Idle;
				}
				return false;
			}

		}



		bool cryptocallback(
			 Object sender,
			 X509Certificate certificate,
			 X509Chain chain,
			 SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}

		public event EventHandler CursorLocationChanged;
		protected virtual void OnCursorLocationChanged(EventArgs args)
		{
			if (this.CursorLocationChanged != null)
			{
				this.CursorLocationChanged(this, args);
			}
		}
		
		void controller_CursorLocationChanged(object sender, EventArgs e)
		{
			this.OnCursorLocationChanged(e);
		}


	}
}


