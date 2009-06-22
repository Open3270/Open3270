//#define SupportSSL true
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
using System.Collections;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
#if !FX1_1
using System.Net.Security;
#endif
using System.Security.Cryptography.X509Certificates;
using Open3270;
using Open3270.Library;

namespace Open3270.TN3270
{

	internal delegate void SChangeDelegate(bool option);
	internal enum sms_state
	{

		SS_IDLE,	/* no command active (scripts only) */
		SS_INCOMPLETE,	/* command(s) buffered and ready to run */
		SS_RUNNING,	/* command executing */
		SS_KBWAIT,	/* command awaiting keyboard unlock */
		SS_CONNECT_WAIT,/* command awaiting connection to complete */
		SS_PAUSED,	/* stopped in PauseScript action */
		SS_WAIT_ANSI,	/* awaiting completion of Wait(ansi) */
		SS_WAIT_3270,	/* awaiting completion of Wait(3270) */
		SS_WAIT_OUTPUT,	/* awaiting completion of Wait(Output) */
		SS_SWAIT_OUTPUT,/* awaiting completion of Snap(Wait) */
		SS_WAIT_DISC,	/* awaiting completion of Wait(Disconnect) */
		SS_WAIT,	/* awaiting completion of Wait() */
		SS_EXPECTING,	/* awaiting completion of Expect() */
		SS_CLOSING	/* awaiting completion of Close() */
	};


	internal enum TNEvent
	{
		Connect,
		Data,
		Error,
		Disconnect,
		DisconnectUnexpected
	}
	internal enum TN3270E_SUBMODE
	{ 
		E_NONE, E_3270, E_NVT, E_SSCP 
	}
	internal enum STCALLBACK
	{
		ST_HALF_CONNECT,
		ST_CONNECT,
		ST_3270_MODE,
		ST_LINE_MODE
	}
	internal delegate void TelnetDataDelegate(object parentData, TNEvent eventType, string text);

	/// <summary>
	/// Summary description for Form1.
	/// </summary>
    internal class Telnet
    {
        public bool mParseLogFileOnly = false;
        public TN3270API mAPI = null;
        public enum TN3270State
        {
            IN_NEITHER,
            ANSI,
            TN3270
        }
        private enum TNSTATE
        {
            /* telnet states */
            TNS_DATA,//	0	/* receiving data */
            TNS_IAC,//	1	/* got an IAC */
            TNS_WILL,//	2	/* got an IAC WILL */
            TNS_WONT,//	3	/* got an IAC WONT */
            TNS_DO,//	4	/* got an IAC DO */
            TNS_DONT,//	5	/* got an IAC DONT */
            TNS_SB,//	6	/* got an IAC SB */
            TNS_SB_IAC//	7	/* got an IAC after an IAC SB */

        }
        public const int TELQUAL_IS = 0;	/* option is... */
        public const int TELQUAL_SEND = 1;	/* send option */
        private ConnectionConfig mConnectionConfig = null;

        public ConnectionConfig Config
        {
            get { return mConnectionConfig; }
        }

        internal bool mShowParseError;



        const byte IAC = 255;//Convert.ToChar(255);
        const byte DO = 253;//Convert.ToChar(253);
        const byte DONT = 254;//Convert.ToChar(254);
        const byte WILL = 251;//Convert.ToChar(251);
        const byte WONT = 252;//Convert.ToChar(252);
        //const byte SB					= 250;//Convert.ToChar(250);
        //const byte SE					= 240;//Convert.ToChar(240);
        //const byte EOR					= 239;//Convert.ToChar(240);
        const byte SB = 250;		/* interpret as subnegotiation */
        const byte GA = 249;		/* you may reverse the line */
        const byte EL = 248;		/* erase the current line */
        const byte EC = 247;		/* erase the current character */
        const byte AYT = 246;		/* are you there */
        const byte AO = 245;		/* abort output--but let prog finish */
        const byte IP = 244;		/* interrupt process--permanently */
        const byte BREAK = 243;		/* break */
        const byte DM = 242;		/* data mark--for connect. cleaning */
        const byte NOP = 241;		/* nop */
        const byte SE = 240;		/* end sub negotiation */
        const byte EOR = 239;            /* end of record (transparent mode) */
        const byte SUSP = 237;		/* suspend process */
        const byte xEOF = 236;		/* end of file */


        const byte SYNCH = 242;		/* for telfunc calls */

        const Char IS = '0';
        const Char SEND = '1';
        const Char INFO = '2';
        const Char VAR = '0';
        const Char VALUE = '1';
        const Char ESC = '2';
        const Char USERVAR = '3';
        //
        /* telnet predefined messages */
        byte[] do_opt = new byte[] { IAC, DO, 0 };
        byte[] dont_opt = new byte[] { IAC, DONT, 0 };
        byte[] will_opt = new byte[] { IAC, WILL, 0 };
        byte[] wont_opt = new byte[] { IAC, WONT, 0 };
        //static unsigned char	functions_req[] = {
        //	IAC, SB, TELOPT_TN3270E, TN3270E_OP_FUNCTIONS };

        string[] telquals = new string[] { "IS", "SEND" };
        //

        // Wait functions

        public sms_state WaitState;
        public System.Threading.ManualResetEvent WaitEvent = new ManualResetEvent(false);

        public bool KBWAIT
        {
            get { return 0 != (keyboard.kybdlock & (Keyboard.KL_OIA_LOCKED | Keyboard.KL_OIA_TWAIT | Keyboard.KL_DEFERRED_UNLOCK)); }
        }

        /* Macro that defines when it's safe to continue a Wait()ing sms. */
        public bool CAN_PROCEED
        {
            get
            {
                return (
                    IN_SSCP ||
                    (IN_3270 && tnctlr.formatted && tnctlr.cursor_addr != 0 && !KBWAIT) ||
                    (IN_ANSI && 0 == (keyboard.kybdlock & Keyboard.KL_AWAITING_FIRST))
                    );
            }
        }

        //

        const int LU_MAX = 32;
        //
        //string m_strResp = "";

        private ArrayList m_ListOptions = new ArrayList();
        private IPEndPoint iep;
        private AsyncCallback callbackProc;
        private string address;
        private int port;
        private Socket mSocketBase;
        private Stream mSocketStream;
        private bool mUseSSL = false;
        Byte[] m_byBuff = new Byte[32767];
        int e_xmit_seq = 0;

        public bool UseSSL { get { return mUseSSL; } set { mUseSSL = value; } }

        public bool IsSocketConnected
        {
            get
            {
                if (mConnectionConfig.LogFile != null)
                    return true;
                //
                if (mSocketBase != null && mSocketBase.Connected)
                    return true;
                else
                {
                    if (this.mDisconnectReason == null)
                        this.mDisconnectReason = "Server disconnected socket";
                    return false;
                }
            }
        }

        // events
        public event TelnetDataDelegate telnetDataEvent;
        private object mParentData = null;
        public Ctlr tnctlr = null;
        public Print print = null;
        public Actions action = null;
        public Idle idle = null;
        public Ansi ansi = null;
        public Appres appres;
        //
        public string termtype = null;

        //
        // state
        //
        TN3270State tnState = TN3270State.IN_NEITHER;
        //
        bool non_tn3270e_host = false;
        public Events events = null;
        public Keyboard keyboard = null;
        // ansi stuff
        bool t_valid = false;
        //int      ansi_data;// = 0;
        //unsigned char *lbuf;// = (unsigned char *)NULL; 	/* line-mode input buffer */
        //unsigned char *lbptr;
        //int      lnext;// = 0;
        byte vintr;
        byte vquit;
        byte verase;
        byte vkill;
        byte veof;
        byte vwerase;
        byte vrprnt;
        byte vlnext;

        // end ansi
        NetBuffer sbptr = null;


        DateTime ns_time;
        int ns_brcvd;
        //int             ns_rrcvd;
        //int             ns_bsent;
        int ns_rsent;



        public ArrayList lus = null;
        int currentLUIndex = 0;
        string connected_type = null;
        string reported_type = null;
        string connected_lu = null;
        string reported_lu = null;


        public Telnet(TN3270API api, IAudit audit, ConnectionConfig config)
        {
            this.mConnectionConfig = config;
            mAPI = api;
            if (config.IgnoreSequenceCount)
            {
                e_funcs = E_OPT(TN3270E_FUNC_BIND_IMAGE) |
                    E_OPT(TN3270E_FUNC_SYSREQ);
            }
            else
            {
                e_funcs = E_OPT(TN3270E_FUNC_BIND_IMAGE) |
                    E_OPT(TN3270E_FUNC_RESPONSES) |
                    E_OPT(TN3270E_FUNC_SYSREQ);
            }

            // MFW - Removed this for Ryan's mainframe. check!!
            //Console.WriteLine("-- TN3270E_FUNC_RESPONSES option disabled");

            this.mDisconnectReason = null;

            trace = new TNTrace(this, audit);
            appres = new Appres();
            events = new Events(this);
            ansi = new Ansi(this);
            print = new Print(this);
            tnctlr = new Ctlr(this, appres);
            keyboard = new Keyboard(this);
            action = new Actions(this);
            idle = new Idle(this);
            //
            if (!t_valid)
            {
                vintr = parse_ctlchar(appres.intr);
                vquit = parse_ctlchar(appres.quit);
                verase = parse_ctlchar(appres.erase);
                vkill = parse_ctlchar(appres.kill);
                veof = parse_ctlchar(appres.eof);
                vwerase = parse_ctlchar(appres.werase);
                vrprnt = parse_ctlchar(appres.rprnt);
                vlnext = parse_ctlchar(appres.lnext);
                t_valid = true;
            }

            int i;
            hisopts = new int[256];
            for (i = 0; i < 256; i++)
                hisopts[i] = 0;

        }
        ~Telnet()
        {
            Disconnect();
            this.mDisconnectReason = null;
        }
        void main_connect(bool ignored)
        {
            if (CONNECTED || appres.disconnect_clear)
                tnctlr.ctlr_erase(true);
        }

        private System.Threading.Thread mLogFileProcessorThread = null;
        private bool mLogFileProcessorThread_Quit = false;
        private MySemaphore mLogFileSemaphore = null;
        private Queue mLogClientData = null;
        private Thread mMainThread = null;
        private string mDisconnectReason = null;


        public void Connect(object _ParentData, string _address, int _port)
        {
            this.mParentData = _ParentData;
            this.address = _address;
            this.port = _port;
            this.mDisconnectReason = null;
            this.mCloseRequested = false;
            //
            //junk
            //
            if (mConnectionConfig.TermType == null)
                this.termtype = "IBM-3278-2";
            else
                this.termtype = mConnectionConfig.TermType;


            tnctlr.ctlr_init(-1);
            tnctlr.ctlr_reinit(-1);
            keyboard.kybd_init();
            ansi.ansi_init();
            //sms_init();
            register_schange(STCALLBACK.ST_CONNECT, new SChangeDelegate(main_connect));
            register_schange(STCALLBACK.ST_3270_MODE, new SChangeDelegate(main_connect));

            /* Make sure we don't fall over any SIGPIPEs. */
            //	(void) signal(SIGPIPE, SIG_IGN);

            // setup colour screen
            appres.mono = false;
            appres.m3279 = true;
            // setup trace options
            appres.debug_tracing = true;


            /* Handle initial toggle settings. */
            if (!appres.debug_tracing)
            {
                appres.settoggle(Appres.DS_TRACE, false);
                appres.settoggle(Appres.EVENT_TRACE, false);
            }
            //	initialize_toggles();

            appres.settoggle(Appres.DS_TRACE, true);


            if (mConnectionConfig.LogFile != null)
            {
                mParseLogFileOnly = true;
                // simulate a connect
                //
                mLogFileSemaphore = new MySemaphore(0, 9999);
                mLogClientData = new Queue();
                mLogFileProcessorThread_Quit = false;
                mMainThread = Thread.CurrentThread;
                mLogFileProcessorThread = new Thread(new System.Threading.ThreadStart(LogFileProcessorThreadHandler));

                mLogFileProcessorThread.Start();



            }
            else
            {
                //
                // Actually connect
                //
                // count .s, numeric and 3 .s =ipaddress
                bool ipaddress = false;
                bool text = false;
                int count = 0;
                int i;
                for (i = 0; i < address.Length; i++)
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
                    ipaddress = true;

                if (!ipaddress)
                {
                    try
                    {
                        IPHostEntry IPHost = Dns.GetHostEntry(address);
                        string[] aliases = IPHost.Aliases;
                        IPAddress[] addr = IPHost.AddressList;
                        iep = new IPEndPoint(addr[0], port);
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
                        iep = new IPEndPoint(IPAddress.Parse(address), port);
                    }
                    catch (System.FormatException se)
                    {
                        throw new TNHostException("Invalid TCP/IP address '" + address + "'", se.Message, null);
                    }
                }

                this.mDisconnectReason = null;

                try
                {
                    // Create New Socket 
                    mSocketBase = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    // Create New EndPoint
                    // Assign Callback function to read from Asyncronous Socket
                    callbackProc = new AsyncCallback(ConnectCallback);
                    // Begin Asyncronous Connection
                    cstate = CSTATE.RESOLVING;

                    mSocketBase.BeginConnect(iep, callbackProc, mSocketBase);

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
        private bool mCloseRequested = false;
        public void Disconnect()
        {
            if (!mParseLogFileOnly)
            {
                lock (this)
                {
                    if (mSocketStream != null)
                    {
                        //Console.WriteLine("Disconnect TN3270 socket");
                        mCloseRequested = true;
                        mSocketStream.Close();
                        mSocketStream = null;
                    }
                    //
                    if (mSocketBase != null)
                    {
                        mCloseRequested = true;

                        try
                        {
                            mSocketBase.Close();
                        }
                        catch (System.ObjectDisposedException)
                        {
                            // ignore this
                        }
                        mSocketBase = null;
                    }
                }

            }
            if (mLogFileProcessorThread != null)
            {
                mLogFileProcessorThread_Quit = true;
                Console.WriteLine("closing log processor thread");
                mLogFileProcessorThread.Join();
                mLogFileProcessorThread = null;
            }
        }
        public string DisconnectReason
        {
            get { return mDisconnectReason; }
        }




        private void LogFileProcessorThreadHandler()
        {
            try
            {
                //
                // Simulate notification of parent
                //
                // notify parent
                if (telnetDataEvent != null)
                    telnetDataEvent(mParentData, TNEvent.Connect, null);
                //
                // Notify TN
                //
                cstate = CSTATE.CONNECTED_INITIAL;
                st_changed(STCALLBACK.ST_CONNECT, true);

                //
                net_connected();
                host_in3270(CSTATE.CONNECTED_ANSI);
                //
                // Now simulate TCP/IP data coming in from the file
                //
                while (!mLogFileProcessorThread_Quit)
                {
                    string text = this.mConnectionConfig.LogFile.ReadLine();
                    if (text == null)
                    {
                        // simulate disconnect
                        trace.trace_dsn("RCVD disconnect\n");
                        //host_disconnect(false);
                        // If no data was recieved then the connection is probably dead

                        Console.WriteLine("Disconnected from log file");
                        // (We are this thread!) Disconnect();
                        if (telnetDataEvent != null)
                            telnetDataEvent(mParentData, TNEvent.Disconnect, null);
                        break; // close thread.
                    }
                    else if (text.Length >= 11)
                    {
                        int time = System.Convert.ToInt32(text.Substring(0, 6));
                        //
                        trace.WriteLine("\n" + text.Substring(7));
                        if (text.Substring(9, 2) == "H ")
                        {
                            text = text.Substring(18);
                            if (HALF_CONNECTED)
                            {
                                host_connected();
                                net_connected();
                            }

                            lock (this)
                            {
                                while (text.Length > 1)
                                {
                                    byte v = System.Convert.ToByte(text.Substring(0, 2), 16);
                                    //
                                    if (this.tnState == TN3270State.IN_NEITHER)
                                    {
                                        keyboard.kybdlock_clr(Keyboard.KL_AWAITING_FIRST, "telnet_fsm");
                                        //status_reset();
                                        this.tnState = TN3270State.ANSI;
                                    }
                                    if (telnet_fsm(ref sbptr, v) != 0)
                                    {
                                        host_disconnect(true);
                                        Disconnect();
                                        return;
                                    }
                                    //
                                    //
                                    text = text.Substring(2).Trim();
                                }

                            }
                        }
                        else if (text.Substring(9, 2) == "C ")
                        {
                            trace.WriteLine("--client data - should wait for netout before moving to next row. CC=" + mLogFileSemaphore.Count);
                            int length = 0;
                            text = text.Substring(18);
                            while (text.Length > 1)
                            {
                                byte v = System.Convert.ToByte(text.Substring(0, 2), 16);
                                length++;
                                byte netoutbyte = 0;
                                try
                                {
                                    netoutbyte = (byte)mLogClientData.Dequeue();
                                }
                                catch (System.InvalidOperationException)
                                {
                                    Console.WriteLine("Queue empty - increment empty queue flag");
                                }
                                if (v != netoutbyte)
                                {
                                    Console.WriteLine("**BUGBUG** " + String.Format("oops - byte is not the same as client buffer. Read {0:x2}'{2}' netout {1:x2}'{3}'", v, netoutbyte, System.Convert.ToChar(Tables.ebc2asc[v]), System.Convert.ToChar(Tables.ebc2asc[netoutbyte])));
                                }
                                while (!mLogFileProcessorThread_Quit)
                                {
                                    if (mLogFileSemaphore.Acquire(1000))
                                        break;
                                    if (!mMainThread.IsAlive ||
                                        (mMainThread.ThreadState & ThreadState.Stopped) != 0 ||
                                        (mMainThread.ThreadState & ThreadState.StopRequested) != 0
                                    )
                                    {
                                        mLogFileProcessorThread_Quit = true;
                                        break;
                                    }
                                }
                                if (mLogFileProcessorThread_Quit)
                                    break;

                                text = text.Substring(2).Trim();
                            }
                            trace.WriteLine("--client data - acquired " + length + " bytes ok. CC=" + mLogFileSemaphore.Count);
                            //
                            //
                            // 
                        }
                    }
                }
                // done
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
                Socket sock1 = (Socket)ar.AsyncState;
                if (sock1.Connected)
                {
                    // notify parent
                    if (telnetDataEvent != null)
                        telnetDataEvent(mParentData, TNEvent.Connect, null);
                    //
                    // Notify TN
                    //
                    cstate = CSTATE.CONNECTED_INITIAL;
                    st_changed(STCALLBACK.ST_CONNECT, true);

                    //
                    net_connected();
                    host_in3270(CSTATE.CONNECTED_ANSI);

                    // Define a new Callback to read the data 
                    AsyncCallback recieveData = new AsyncCallback(OnRecievedData);
                    //
                    if (mUseSSL)
                    {
#if !FX1_1
                        NetworkStream mNetworkStream = new NetworkStream(mSocketBase, false);
                        SslStream ssl = new SslStream(mNetworkStream, false, new RemoteCertificateValidationCallback(cryptocallback));
                        ssl.AuthenticateAsClient(this.address);
                        trace.WriteLine("SSL Connection made. Encryption is '" + ssl.IsEncrypted + "'");

//                        Console.WriteLine("ssl auth = " + ssl.IsAuthenticated);
                        //Console.WriteLine("ssl enc = " + ssl.IsEncrypted);
                        //
                        this.mSocketStream = ssl;
#else
	throw new ApplicationException("SSL is only supported under .NET 2.0");
#endif
                    }
                    else
                    {
                        this.mSocketStream = new NetworkStream(mSocketBase, false);
                    }
                    //
                    // Begin reading data asyncronously
                    //
                    mSocketStream.BeginRead(m_byBuff, 0, m_byBuff.Length, recieveData, mSocketStream);
                }
                else
                {
                    this.mDisconnectReason = "Unable to connect to host - timeout on connect";
                    if (telnetDataEvent != null)
                        telnetDataEvent(mParentData, TNEvent.Error, "Connect callback returned 'not connected'");
                    // spurious, but to meet spec
                    cstate = CSTATE.NOT_CONNECTED;
                    st_changed(STCALLBACK.ST_CONNECT, false);

                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine("Setup Receive callback failed " + ex);
                trace.trace_event("%s", "Exception occured connecting to host. Disconnecting\n\n" + ex);
                Disconnect();
            }
        }

        public string hex(int n)
        {
            return String.Format("{0:x2}", n);
        }

        public bool ParseByte(byte b)
        {
            lock (this)
            {
                if (this.tnState == TN3270State.IN_NEITHER)
                {
                    keyboard.kybdlock_clr(Keyboard.KL_AWAITING_FIRST, "telnet_fsm");
                    //status_reset();
                    this.tnState = TN3270State.ANSI;
                }
                if (telnet_fsm(ref sbptr, b) != 0)
                {
                    host_disconnect(true);
                    Disconnect();
                    return false;
                }
            }
            return true;
        }

        private void OnRecievedData(IAsyncResult ar)
        {
            // Get The connection socket from the callback
            Stream streamsock = (Stream)ar.AsyncState;
            bool disconnectme = false;

            // is socket closing
            mDisconnectReason = null;

            if (mSocketBase==null || mSocketBase.Connected==false)
            {
                disconnectme = true;
                mDisconnectReason = "Host dropped connection";

            }
            else
            {
                try
                {

                    // Get The data , if any
                    int nBytesRec = 0;
                    nBytesRec = streamsock.EndRead(ar);

                    ansi_data = 0;
                    if (nBytesRec > 0)
                    {
                        if (HALF_CONNECTED)
                        {
                            host_connected();
                            net_connected();
                        }
                        trace.trace_netdata('<', m_byBuff, nBytesRec);
                        ns_brcvd += nBytesRec;

                        int i;

                        if (mShowParseError)
                        {
                            trace.trace_dsn("ShowParseError called - throw exception");
                            throw new ApplicationException("ShowParseError exception test requested");
                        }

                        byte[] data = new byte[nBytesRec];
#if false
				Console.Write("--data ");
				for (i=0; i<nBytesRec; i++)
				{
					Console.Write("0x"+hex(m_byBuff[i]));
				}
				Console.WriteLine();
#endif
                        lock (this)
                        {
                            for (i = 0; i < nBytesRec; i++)
                            {

                                if (this.tnState == TN3270State.IN_NEITHER)
                                {
                                    keyboard.kybdlock_clr(Keyboard.KL_AWAITING_FIRST, "telnet_fsm");
                                    //status_reset();
                                    this.tnState = TN3270State.ANSI;
                                }
                                if (telnet_fsm(ref sbptr, m_byBuff[i]) != 0)
                                {
                                    host_disconnect(true);
                                    Disconnect();
                                    return;
                                }
                            }
                        }
                        //
                        // Define a new Callback to read the data 
                        AsyncCallback recieveData = new AsyncCallback(OnRecievedData);
                        // Begin reading data asyncronously
                        mSocketStream.BeginRead(m_byBuff, 0, m_byBuff.Length,recieveData , mSocketStream);

                        //
                    }
                    else
                    {
                        disconnectme = true;
                    }
                }
                catch (System.ObjectDisposedException)
                {
                    disconnectme = true;
                    mDisconnectReason = "Client dropped connection";
                }
                catch (Exception e)
                {

                    trace.trace_event("%s", "Exception occured processing Telnet buffer. Disconnecting\n\n" + e);
                    disconnectme = true;
                    mDisconnectReason = "Exception in data stream (" + e.Message + "). Connection dropped.";
                }
            }

            if (disconnectme)
            {
                bool tempCloseRequested = mCloseRequested;
                trace.trace_dsn("RCVD disconnect\n");
                host_disconnect(false);
                // If no data was recieved then the connection is probably dead
                //Console.WriteLine( "Socket disconnected");//Disconnected", sock.RemoteEndPoint );

                Disconnect();
                if (telnetDataEvent != null)
                {
                    if (tempCloseRequested)
                        telnetDataEvent(mParentData, TNEvent.Disconnect, null);
                    else
                        telnetDataEvent(mParentData, TNEvent.DisconnectUnexpected, null);
                    mCloseRequested = false;
                }
            }
        }


        void net_rawout(NetBuffer smk)
        {
            byte[] bytes = smk.Data;
            net_rawout(bytes);
        }
        string Dump(byte[] data, int len)
        {
            string buf = " ";
            for (int i = 0; i < len; i++)
            {
                buf += String.Format("{0:x2}", data[i]) + " ";
            }
            return buf;
        }
        void net_rawout(byte[] smk)
        {
            //
            if (this.mParseLogFileOnly)
            {

                trace.WriteLine("\nnet_rawout [" + smk.Length + "] " + Dump(smk, smk.Length) + "\n");
                //
                // If we're reading the log file, allow the next bit of data to flow
                //
                if (mLogFileSemaphore != null)
                {
                    //trace.WriteLine("\nnet_rawout ["+"+mLogFileSemaphore.Count+"\n");
                    for (int i = 0; i < smk.Length; i++)
                    {
                        mLogClientData.Enqueue(smk[i]);
                    }
                    mLogFileSemaphore.Release(smk.Length);
                }
            }
            else
            {
                trace.trace_netdata('>', smk, smk.Length);
                mSocketStream.Write(smk, 0, smk.Length);
            }
        }
        void net_rawout(byte[] smk, int len)
        {
            if (this.mParseLogFileOnly)
            {
                trace.WriteLine("\nnet_rawout2 [" + len + "]" + Dump(smk, len) + "\n");
                //
                // If we're reading the log file, allow the next bit of data to flow
                //
                if (mLogFileSemaphore != null)
                {
                    //trace.WriteLine("net_rawout2 - CC="+mLogFileSemaphore.Count);
                    for (int i = 0; i < len; i++)
                    {
                        mLogClientData.Enqueue(smk[i]);
                    }
                    mLogFileSemaphore.Release(len);
                }
            }
            else
            {
                trace.trace_netdata('>', smk, len);
                mSocketStream.Write(smk, 0, len);
            }
        }


        //
        // TELNET_FSM
        //
        enum CSTATE
        {
            NOT_CONNECTED = 0,		/* no socket, unknown mode */
            RESOLVING,		/* resolving hostname */
            PENDING,		/* connection pending */
            CONNECTED_INITIAL,	/* connected, no mode yet */
            CONNECTED_ANSI,		/* connected in NVT ANSI mode */
            CONNECTED_3270,		/* connected in old-style 3270 mode */
            CONNECTED_INITIAL_E,	/* connected in TN3270E mode, unnegotiated */
            CONNECTED_NVT,		/* connected in TN3270E mode, NVT mode */
            CONNECTED_SSCP,		/* connected in TN3270E mode, SSCP-LU mode */
            CONNECTED_TN3270E	/* connected in TN3270E mode, 3270 mode */
        };
        /* telnet options */
        private const int TELOPT_BINARY = 0;	/* 8-bit data path */
        private const int TELOPT_ECHO = 1;	/* echo */
        private const int TELOPT_RCP = 2;	/* prepare to reconnect */
        private const int TELOPT_SGA = 3;	/* suppress go ahead */
        private const int TELOPT_NAMS = 4;	/* approximate message size */
        private const int TELOPT_STATUS = 5;	/* give status */
        private const int TELOPT_TM = 6;	/* timing mark */
        private const int TELOPT_RCTE = 7;	/* remote controlled transmission and echo */
        private const int TELOPT_NAOL = 8;	/* negotiate about output line width */
        private const int TELOPT_NAOP = 9;	/* negotiate about output page size */
        private const int TELOPT_NAOCRD = 10;	/* negotiate about CR disposition */
        private const int TELOPT_NAOHTS = 11;	/* negotiate about horizontal tabstops */
        private const int TELOPT_NAOHTD = 12;	/* negotiate about horizontal tab disposition */
        private const int TELOPT_NAOFFD = 13;	/* negotiate about formfeed disposition */
        private const int TELOPT_NAOVTS = 14;	/* negotiate about vertical tab stops */
        private const int TELOPT_NAOVTD = 15;	/* negotiate about vertical tab disposition */
        private const int TELOPT_NAOLFD = 16;	/* negotiate about output LF disposition */
        private const int TELOPT_XASCII = 17;	/* extended ascic character set */
        private const int TELOPT_LOGOUT = 18;	/* force logout */
        private const int TELOPT_BM = 19;	/* byte macro */
        private const int TELOPT_DET = 20;	/* data entry terminal */
        private const int TELOPT_SUPDUP = 21;	/* supdup protocol */
        private const int TELOPT_SUPDUPOUTPUT = 22;	/* supdup output */
        private const int TELOPT_SNDLOC = 23;	/* send location */
        private const int TELOPT_TTYPE = 24;	/* terminal type */
        private const int TELOPT_EOR = 25;	/* end or record */
        private const int TELOPT_TUID = 26;      /* TACACS user identification */
        private const int TELOPT_OUTMRK = 27;      /* output marking */
        private const int TELOPT_TTYLOC = 28;      /* terminal location number */
        private const int TELOPT_3270REGIME = 29;    /* 3270 regime */
        private const int TELOPT_X3PAD = 30;      /* X.3 PAD */
        private const int TELOPT_NAWS = 31;      /* window size */
        private const int TELOPT_TSPEED = 32;      /* terminal speed */
        private const int TELOPT_LFLOW = 33;      /* remote flow control */
        private const int TELOPT_LINEMODE = 34;      /* linemode option */
        private const int TELOPT_XDISPLOC = 35;      /* X Display Location */
        private const int TELOPT_OLD_ENVIRON = 36;   /* old - Environment variables */
        private const int TELOPT_AUTHENTICATION = 37;/* authenticate */
        private const int TELOPT_ENCRYPT = 38;      /* encryption option */
        private const int TELOPT_NEW_ENVIRON = 39;   /* new - environment variables */
        private const int TELOPT_TN3270E = 40;	/* extended 3270 regime */
        private const int TELOPT_EXOPL = 255;	/* extended-options-list */


        /* Negotiation function Names. */
        private const int TN3270E_FUNC_BIND_IMAGE = 0;
        private const int TN3270E_FUNC_DATA_STREAM_CTL = 1;
        private const int TN3270E_FUNC_RESPONSES = 2;
        private const int TN3270E_FUNC_SCS_CTL_CODES = 3;
        private const int TN3270E_FUNC_SYSREQ = 4;

        int[] myopts = new int[256];
        int[] hisopts = null;//bugnew int[256];
        //int[] doopts = null;//bug
        int ansi_data = 0;
        //bool ever_3270 = false;
        CSTATE cstate = CSTATE.NOT_CONNECTED;
        bool tn3270e_negotiated = false;
        TN3270E_SUBMODE tn3270e_submode = TN3270E_SUBMODE.E_NONE;
        bool tn3270e_bound = false;
        bool linemode = false;
        bool syncing = false;
        int e_funcs;//
        int response_required = TN3270E_HEADER.TN3270E_RSF_NO_RESPONSE;

        TNSTATE telnet_state = TNSTATE.TNS_DATA;
        public TNTrace trace = null;

        //
        byte[] ibuf = null;
        int ibptr = 0;


        void store3270in(byte c)
        {
            if (ibptr >= ibuf.Length)
            {
                byte[] temp = new byte[ibuf.Length + 256];
                ibuf.CopyTo(temp, 0);
                ibuf = temp;
            }
            ibuf[ibptr++] = c;
        }


        private void host_in3270(CSTATE new_cstate)
        {
            bool now3270 = (new_cstate == CSTATE.CONNECTED_3270 ||
                new_cstate == CSTATE.CONNECTED_SSCP ||
                new_cstate == CSTATE.CONNECTED_TN3270E);

            cstate = new_cstate;
            tnctlr.ever_3270 = now3270;
            st_changed(STCALLBACK.ST_3270_MODE, now3270);
        }

        void check_in3270()
        {
            CSTATE new_cstate = CSTATE.NOT_CONNECTED;
            /*			static const char *state_name[] = 
                    {
                        "unconnected",
                        "resolving",
                        "pending",
                        "connected initial",
                        "TN3270 NVT",
                        "TN3270 3270",
                        "TN3270E",
                        "TN3270E NVT",
                        "TN3270E SSCP-LU",
                        "TN3270E 3270"
                    };
                    */

            if (myopts[TELOPT_TN3270E] != 0)
            {
                if (!tn3270e_negotiated)
                    new_cstate = CSTATE.CONNECTED_INITIAL_E;
                else
                {
                    switch (tn3270e_submode)
                    {
                        case TN3270E_SUBMODE.E_NONE:
                            new_cstate = CSTATE.CONNECTED_INITIAL_E;
                            break;
                        case TN3270E_SUBMODE.E_NVT:
                            new_cstate = CSTATE.CONNECTED_NVT;
                            break;
                        case TN3270E_SUBMODE.E_3270:
                            new_cstate = CSTATE.CONNECTED_TN3270E;
                            break;
                        case TN3270E_SUBMODE.E_SSCP:
                            new_cstate = CSTATE.CONNECTED_SSCP;
                            break;
                    }
                }
            }
            else
                if (myopts[TELOPT_BINARY] != 0 &&
                myopts[TELOPT_EOR] != 0 &&
                myopts[TELOPT_TTYPE] != 0 &&
                hisopts[TELOPT_BINARY] != 0 &&
                hisopts[TELOPT_EOR] != 0)
                {
                    new_cstate = CSTATE.CONNECTED_3270;
                }
                else if (cstate == CSTATE.CONNECTED_INITIAL)
                {
                    /* Nothing has happened, yet. */
                    return;
                }
                else
                {
                    new_cstate = CSTATE.CONNECTED_ANSI;
                }

            if (new_cstate != cstate)
            {
                bool was_in_e = (cstate >= CSTATE.CONNECTED_INITIAL_E);//IN_E;

                trace.trace_dsn("Now operating in " + new_cstate + " mode\n");
                host_in3270(new_cstate);

                /*
                * If we've now switched between non-TN3270E mode and
                * TN3270E mode, reset the LU list so we can try again
                * in the new mode.
                */
                if (lus != null && was_in_e != IN_E)
                {
                    currentLUIndex = 0;
                }
                /* Allocate the initial 3270 input buffer. */
                if (new_cstate >= CSTATE.CONNECTED_INITIAL && ibuf == null)
                {
                    ibuf = new byte[256];
                    ibptr = 0;
                }

                /* Reinitialize line mode. */
                if ((new_cstate == CSTATE.CONNECTED_ANSI && linemode) ||
                    new_cstate == CSTATE.CONNECTED_NVT)
                {
                    Console.WriteLine("cooked_init-bad");
                    //cooked_init();
                }

                /* If we fell out of TN3270E, remove the state. */
                if (myopts[TELOPT_TN3270E] == 0)
                {
                    tn3270e_negotiated = false;
                    tn3270e_submode = TN3270E_SUBMODE.E_NONE;
                    tn3270e_bound = false;
                }
                // notify script
                tnctlr.sms_continue();
            }
        }

        //

        public int E_OPT(int n)
        {
            return (1 << (n));
        }

        bool process_eor()
        {
            int i;
            if (syncing || ibptr == 0)
                return false;

            if (cstate >= CSTATE.CONNECTED_INITIAL_E)
            {
                TN3270E_HEADER h = new TN3270E_HEADER(ibuf);//*h = (tn3270e_header *)ibuf;
                //unsigned char *s;
                pds rv;

                trace.trace_dsn("RCVD TN3270E(datatype: " + h.data_type + ", request: " + h.request_flag + ", response: " + h.response_flag + ", seq: " + (h.seq_number[0] << 8 | h.seq_number[1]) + ")\n");

                switch (h.data_type)
                {
                    case TN3270E_DT.TN3270_DATA:
                        if ((e_funcs & E_OPT(TN3270E_FUNC_BIND_IMAGE)) != 0 &&
                            !tn3270e_bound)
                            return false;
                        tn3270e_submode = TN3270E_SUBMODE.E_3270;
                        check_in3270();
                        response_required = h.response_flag;
                        rv = tnctlr.process_ds(ibuf, TN3270E_HEADER.EH_SIZE, ibptr - TN3270E_HEADER.EH_SIZE);
                        //Console.WriteLine("*** RV = "+rv);
                        //Console.WriteLine("*** response_required = "+response_required);						
                        if (rv < 0 &&
                            response_required != TN3270E_HEADER.TN3270E_RSF_NO_RESPONSE)
                            tn3270e_nak(rv);
                        else if (rv == pds.PDS_OKAY_NO_OUTPUT &&
                            response_required == TN3270E_HEADER.TN3270E_RSF_ALWAYS_RESPONSE)
                            tn3270e_ack();
                        response_required = TN3270E_HEADER.TN3270E_RSF_NO_RESPONSE;
                        return false;
                    case TN3270E_DT.BIND_IMAGE:
                        if ((e_funcs & E_OPT(TN3270E_FUNC_BIND_IMAGE)) == 0)
                            return false;
                        tn3270e_bound = true;
                        check_in3270();
                        return false;
                    case TN3270E_DT.UNBIND:
                        if ((e_funcs & E_OPT(TN3270E_FUNC_BIND_IMAGE)) == 0)
                            return false;
                        tn3270e_bound = false;
                        if (tn3270e_submode == TN3270E_SUBMODE.E_3270)
                            tn3270e_submode = TN3270E_SUBMODE.E_NONE;
                        check_in3270();
                        return false;
                    case TN3270E_DT.NVT_DATA:
                        /* In tn3270e NVT mode */
                        tn3270e_submode = TN3270E_SUBMODE.E_NVT;
                        check_in3270();
                        for (i = 0; i < ibptr; i++)
                        {
                            ansi.ansi_process(ibuf[i]);
                        }
                        return false;
                    case TN3270E_DT.SSCP_LU_DATA:
                        if ((e_funcs & E_OPT(TN3270E_FUNC_BIND_IMAGE)) == 0)
                            return false;
                        tn3270e_submode = TN3270E_SUBMODE.E_SSCP;
                        check_in3270();
                        tnctlr.ctlr_write_sscp_lu(ibuf, TN3270E_HEADER.EH_SIZE, ibptr - TN3270E_HEADER.EH_SIZE);
                        return false;
                    default:
                        /* Should do something more extraordinary here. */
                        return false;
                }
            }
            else
            {
                tnctlr.process_ds(ibuf, 0, ibptr);
            }
            return false;
        }

        public int telnet_fsm(ref NetBuffer sbptr, byte c)
        {
            int i;
            //char	*see_chr;
            int sl = 0;
            if (sbptr == null)
                sbptr = new NetBuffer();
            //Console.WriteLine(""+telnet_state+"-0x"+(int)c);

            switch (telnet_state)
            {
                case TNSTATE.TNS_DATA:	/* normal data processing */
                    if (c == IAC)
                    {
                        /* got a telnet command */
                        telnet_state = TNSTATE.TNS_IAC;
                        if (ansi_data != 0)
                        {
                            trace.trace_dsn("\n");
                            ansi_data = 0;
                        }
                        break;
                    }
                    if (cstate == CSTATE.CONNECTED_INITIAL)
                    {
                        /* now can assume ANSI mode */
                        host_in3270(CSTATE.CONNECTED_ANSI);
                        /*if (linemode)
                                cooked_init();*/
                        keyboard.kybdlock_clr(Keyboard.KL_AWAITING_FIRST, "telnet_fsm");
                        tnctlr.ps_process();
                    }
                    if (IN_ANSI && !IN_E)
                    {
                        if (ansi_data == 0)
                        {
                            trace.trace_dsn("<.. ");
                            ansi_data = 4;
                        }
                        string see_chr = Util.ctl_see((byte)c);
                        ansi_data += (sl = see_chr.Length);
                        if (ansi_data >= TNTrace.TRACELINE)
                        {
                            trace.trace_dsn(" ...\n... ");
                            ansi_data = 4 + sl;
                        }
                        trace.trace_dsn(see_chr);
                        if (!syncing)
                        {
                            if (linemode && appres.onlcr && c == '\n')
                                ansi.ansi_process((byte)'\r');
                            ansi.ansi_process(c);
                            //Console.WriteLine("--BUGBUG--sms_store");
                            //sms_store(c);
                        }
                    }
                    else
                    {
                        store3270in(c);
                    }
                    break;
                case TNSTATE.TNS_IAC:	/* process a telnet command */
                    if (c != EOR && c != IAC)
                    {
                        trace.trace_dsn("RCVD " + cmd(c) + " ");
                    }
                    switch (c)
                    {
                        case IAC:	/* escaped IAC, insert it */
                            if (IN_ANSI && !IN_E)
                            {
                                if (ansi_data == 0)
                                {
                                    trace.trace_dsn("<.. ");
                                    ansi_data = 4;
                                }
                                string see_chr = Util.ctl_see(c);
                                ansi_data += (sl = see_chr.Length);
                                if (ansi_data >= TNTrace.TRACELINE)
                                {
                                    trace.trace_dsn(" ...\n ...");
                                    ansi_data = 4 + sl;
                                }
                                trace.trace_dsn(see_chr);
                                ansi.ansi_process(c);
                                //Console.WriteLine("--BUGBUG--sms_store");
                                //sms_store(c);
                            }
                            else
                                store3270in(c);
                            telnet_state = TNSTATE.TNS_DATA;
                            break;
                        case EOR:	/* eor, process accumulated input */
                            if (IN_3270 || (IN_E && tn3270e_negotiated))
                            {
                                // (can't see this being used) --> ns_rrcvd++;
                                if (process_eor())
                                    return -1;
                            }
                            else
                                events.Warning("EOR received when not in 3270 mode, ignored.");
                            //
                            trace.trace_dsn("RCVD EOR\n");
                            ibptr = 0;
                            telnet_state = TNSTATE.TNS_DATA;
                            break;
                        case WILL:
                            telnet_state = TNSTATE.TNS_WILL;
                            break;
                        case WONT:
                            telnet_state = TNSTATE.TNS_WONT;
                            break;
                        case DO:
                            telnet_state = TNSTATE.TNS_DO;
                            break;
                        case DONT:
                            telnet_state = TNSTATE.TNS_DONT;
                            break;
                        case SB:
                            telnet_state = TNSTATE.TNS_SB;
                            sbptr = new NetBuffer();
                            //if (sbbuf == null)
                            //	sbbuf = (int)Malloc(1024); //bug
                            //sbptr = sbbuf;
                            break;
                        case DM:
                            trace.trace_dsn("\n");
                            if (syncing)
                            {
                                syncing = false;
                                //							x_except_on(sock);
                            }
                            telnet_state = TNSTATE.TNS_DATA;
                            break;
                        case GA:
                        case NOP:
                            trace.trace_dsn("\n");
                            telnet_state = TNSTATE.TNS_DATA;
                            break;
                        default:
                            trace.trace_dsn("???\n");
                            telnet_state = TNSTATE.TNS_DATA;
                            break;
                    }
                    break;
                case TNSTATE.TNS_WILL:	/* telnet WILL DO OPTION command */
                    trace.trace_dsn("" + opt(c) + "\n");
                    if (c == TELOPT_SGA ||
                        c == TELOPT_BINARY ||
                        c == TELOPT_EOR ||
                        c == TELOPT_TTYPE ||
                        c == TELOPT_ECHO ||
                        c == TELOPT_TN3270E)
                    {
                        if (c != TELOPT_TN3270E || !non_tn3270e_host)
                        {
                            if (hisopts[c] == 0)
                            {
                                hisopts[c] = 1;
                                do_opt[2] = c;
                                net_rawout(do_opt);
                                trace.trace_dsn("SENT DO " + opt(c) + "\n");

                                /*
                                     * For UTS, volunteer to do EOR when
                                     * they do.
                                     */
                                if (c == TELOPT_EOR && myopts[c] == 0)
                                {
                                    myopts[c] = 1;
                                    will_opt[2] = c;
                                    net_rawout(will_opt);
                                    trace.trace_dsn("SENT WILL " + opt(c) + "\n");
                                }

                                check_in3270();
                                check_linemode(false);
                            }
                        }
                    }
                    else
                    {
                        dont_opt[2] = c;
                        net_rawout(dont_opt);
                        trace.trace_dsn("SENT DONT " + opt(c) + "\n");
                    }
                    telnet_state = TNSTATE.TNS_DATA;
                    break;
                case TNSTATE.TNS_WONT:	/* telnet WONT DO OPTION command */
                    trace.trace_dsn("" + opt(c) + "\n");
                    if (hisopts[c] != 0)
                    {
                        hisopts[c] = 0;
                        dont_opt[2] = c;
                        net_rawout(dont_opt);
                        trace.trace_dsn("SENT DONT " + opt(c) + "\n");
                        check_in3270();
                        check_linemode(false);
                    }
                    telnet_state = TNSTATE.TNS_DATA;
                    break;
                case TNSTATE.TNS_DO:	/* telnet PLEASE DO OPTION command */
                    trace.trace_dsn("" + opt(c) + "\n");
                    if (c == TELOPT_BINARY ||
                        c == TELOPT_BINARY ||
                        c == TELOPT_EOR ||
                        c == TELOPT_TTYPE ||
                        c == TELOPT_SGA ||
                        c == TELOPT_NAWS ||
                        c == TELOPT_TM ||
                        (c == TELOPT_TN3270E && !Config.RefuseTN3270E))
                    {
                        bool fallthrough = true;
                        if (c != TELOPT_TN3270E || !non_tn3270e_host)
                        {
                            if (myopts[c] == 0)
                            {
                                if (c != TELOPT_TM)
                                    myopts[c] = 1;
                                will_opt[2] = c;
                                net_rawout(will_opt);
                                trace.trace_dsn("SENT WILL " + opt(c) + "\n");
                                check_in3270();
                                check_linemode(false);
                            }
                            if (c == TELOPT_NAWS)
                                send_naws();
                            fallthrough = false;
                        }
                        if (fallthrough)
                        {
                            //fallthrough
                            wont_opt[2] = c;
                            net_rawout(wont_opt);
                            trace.trace_dsn("SENT WONT " + opt(c) + "\n");
                        }
                    }
                    else
                    {
                        wont_opt[2] = c;
                        net_rawout(wont_opt);
                        trace.trace_dsn("SENT WONT " + opt(c) + "\n");
                    }

                    telnet_state = TNSTATE.TNS_DATA;
                    break;
                case TNSTATE.TNS_DONT:	/* telnet PLEASE DON'T DO OPTION command */
                    trace.trace_dsn("" + opt(c) + "\n");
                    if (myopts[c] != 0)
                    {
                        myopts[c] = 0;
                        wont_opt[2] = c;
                        net_rawout(wont_opt);
                        trace.trace_dsn("SENT WONT " + opt(c) + "\n");
                        check_in3270();
                        check_linemode(false);
                    }
                    telnet_state = TNSTATE.TNS_DATA;
                    break;
                case TNSTATE.TNS_SB:	/* telnet sub-option string command */
                    if (c == IAC)
                        telnet_state = TNSTATE.TNS_SB_IAC;
                    else
                        sbptr.Add(c);
                    break;
                case TNSTATE.TNS_SB_IAC:	/* telnet sub-option string command */
                    sbptr.Add(c);
                    if (c == SE)
                    {
                        telnet_state = TNSTATE.TNS_DATA;
                        if (sbptr.Data[0] == TELOPT_TTYPE &&
                            sbptr.Data[1] == TELQUAL_SEND)
                        {
                            int tt_len;//, tb_len;



                            trace.trace_dsn("" + opt(sbptr.Data[0]) + " " + telquals[sbptr.Data[1]] + "\n");
                            if (lus != null && this.currentLUIndex >= lus.Count)
                            {
                                //Console.WriteLine("BUGBUG-resending LUs, rather than forcing error");

                                //this.currentLUIndex=0;
                                /* None of the LUs worked. */
                                events.popup_an_error("Cannot connect to specified LU");
                                return -1;

                            }

                            tt_len = termtype.Length;//(termtype);
                            if (lus != null)
                            {
                                tt_len += ((string)lus[currentLUIndex]).Length + 1;
                                //tt_len += strlen(try_lu) + 1;
                                connected_lu = (string)lus[currentLUIndex];
                            }
                            else
                                connected_lu = null;
                            //							status_lu(connected_lu);

                            NetBuffer tt_out = new NetBuffer();
                            tt_out.Add(IAC);
                            tt_out.Add(SB);
                            tt_out.Add(TELOPT_TTYPE);
                            tt_out.Add(TELQUAL_IS);
                            tt_out.Add(termtype);

                            if (lus != null)
                            {
                                tt_out.Add((byte)'@');

                                byte[] b_try_lu = Encoding.ASCII.GetBytes((string)lus[this.currentLUIndex]);
                                for (i = 0; i < b_try_lu.Length; i++)
                                {
                                    tt_out.Add(b_try_lu[i]);
                                }
                                tt_out.Add(IAC);
                                tt_out.Add(SE);
                                Console.WriteLine("Attempt LU='" + lus[this.currentLUIndex] + "'");
                            }
                            else
                            {
                                tt_out.Add(IAC);
                                tt_out.Add(SE);
                            }
                            net_rawout(tt_out);

                            trace.trace_dsn("SENT SB " + opt(TELOPT_TTYPE) + " " + tt_out.Data.Length + " " + termtype + " " + cmd(SE));

                            /* Advance to the next LU name. */
                            this.currentLUIndex++;

                        }
                        else if (myopts[TELOPT_TN3270E] != 0 &&
                            sbptr.Data[0] == TELOPT_TN3270E)
                        {
                            if (tn3270e_negotiate(sbptr) != 0)
                                return -1;
                        }
                    }
                    else
                    {
                        telnet_state = TNSTATE.TNS_SB;
                    }
                    break;
            }
            return 0;
        }
        void tn3270e_ack()
        {
            byte[] rsp_buf = new byte[9];
            TN3270E_HEADER h = new TN3270E_HEADER();
            TN3270E_HEADER h_in = new TN3270E_HEADER(ibuf);
            int rsp_len = TN3270E_HEADER.EH_SIZE;

            h.data_type = TN3270E_DT.RESPONSE;
            h.request_flag = 0;
            h.response_flag = TN3270E_HEADER.TN3270E_RSF_POSITIVE_RESPONSE;
            h.seq_number[0] = h_in.seq_number[0];
            h.seq_number[1] = h_in.seq_number[1];
            h.OnToByte(rsp_buf);
            //
            if (h.seq_number[1] == IAC)
                rsp_buf[rsp_len++] = IAC;
            rsp_buf[rsp_len++] = TN3270E_HEADER.TN3270E_POS_DEVICE_END;
            rsp_buf[rsp_len++] = IAC;
            rsp_buf[rsp_len++] = EOR;
            trace.trace_dsn("SENT TN3270E(RESPONSE POSITIVE-RESPONSE " +
                (h_in.seq_number[0] << 8 | h_in.seq_number[1]) +
                ") DEVICE-END\n");
            net_rawout(rsp_buf, rsp_len);
        }

        /* Send a TN3270E negative response to the server. */
        void tn3270e_nak(pds rv)
        {
            byte[] rsp_buf = new byte[9];
            TN3270E_HEADER h = new TN3270E_HEADER();
            TN3270E_HEADER h_in = new TN3270E_HEADER(ibuf);
            int rsp_len = TN3270E_HEADER.EH_SIZE;


            h.data_type = TN3270E_DT.RESPONSE;
            h.request_flag = 0;
            h.response_flag = TN3270E_HEADER.TN3270E_RSF_NEGATIVE_RESPONSE;
            h.seq_number[0] = h_in.seq_number[0];
            h.seq_number[1] = h_in.seq_number[1];
            h.OnToByte(rsp_buf);
            //
            if (h.seq_number[1] == IAC)
                rsp_buf[rsp_len++] = IAC;
            rsp_buf[rsp_len++] = TN3270E_HEADER.TN3270E_NEG_COMMAND_REJECT;
            rsp_buf[rsp_len++] = IAC;
            rsp_buf[rsp_len++] = EOR;
            trace.trace_dsn("SENT TN3270E(RESPONSE NEGATIVE-RESPONSE " +
                (h_in.seq_number[0] << 8 | h_in.seq_number[1]) +
                ") COMMAND-REJECT\n");
            net_rawout(rsp_buf, rsp_len);
        }
        /*
 * check_linemode
 *	Set the global variable 'linemode', which says whether we are in
 *	character-by-character mode or line mode.
 */
        void check_linemode(bool init)
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
            linemode = (hisopts[TELOPT_ECHO] == 0) /* && !hisopts[TELOPT_SGA] */;

            if (init || linemode != wasline)
            {
                //Console.WriteLine("--ST_LINE_MODE changed");
                st_changed(STCALLBACK.ST_LINE_MODE, linemode);
                if (!init)
                {
                    trace.trace_dsn("Operating in %s mode.\n",
                        linemode ? "line" : "character-at-a-time");
                }
                if (IN_ANSI && linemode)
                    cooked_init();
            }
        }
        private string cmd(int index)
        {
            string[] telcmds = new string[]
								{
									"EOF", "SUSP", "ABORT", "EOR", "SE", "NOP", "DMARK", "BRK", "IP",
									"AO", "AYT", "EC", "EL", "GA", "SB", "WILL", "WONT", "DO", "DONT",
									"IAC"
								};
            const int TELCMD_FIRST = xEOF;
            //const int TELCMD_LAST	=IAC;

            return telcmds[index - TELCMD_FIRST];
        }
        private string opt(int index)
        {
            string[] telopts = new string[] 
										  {
											  "BINARY", "ECHO", "RCP", "SUPPRESS GO AHEAD", "NAME",
											  "STATUS", "TIMING MARK", "RCTE", "NAOL", "NAOP",
											  "NAOCRD", "NAOHTS", "NAOHTD", "NAOFFD", "NAOVTS",
											  "NAOVTD", "NAOLFD", "EXTEND ASCII", "LOGOUT", "BYTE MACRO",
											  "DATA ENTRY TERMINAL", "SUPDUP", "SUPDUP OUTPUT",
											  "SEND LOCATION", "TERMINAL TYPE", "END OF RECORD",
											  "TACACS UID", "OUTPUT MARKING", "TTYLOC",
											  "3270 REGIME", "X.3 PAD", "NAWS", "TSPEED", "LFLOW",
											  "LINEMODE", "XDISPLOC", "OLD-ENVIRON", "AUTHENTICATION",
											  "ENCRYPT", "NEW-ENVIRON", "TN3270E"
											  
										  };

            if (index <= TELOPT_TN3270E)
                return telopts[index];
            else
                if (index == TELOPT_TN3270E)
                    return "TN3270E";
                else
                    return "" + index;
        }
        public bool PCONNECTED { get { return ((int)cstate >= (int)CSTATE.RESOLVING); } }
        public bool HALF_CONNECTED { get { return (cstate == CSTATE.RESOLVING || cstate == CSTATE.PENDING); } }
        public bool CONNECTED { get { return ((int)cstate >= (int)CSTATE.CONNECTED_INITIAL); } }
        public bool IN_NEITHER { get { return (cstate == CSTATE.CONNECTED_INITIAL); } }
        public bool IN_ANSI { get { return (cstate == CSTATE.CONNECTED_ANSI || cstate == CSTATE.CONNECTED_NVT); } }
        public bool IN_3270 { get { return (cstate == CSTATE.CONNECTED_3270 || cstate == CSTATE.CONNECTED_TN3270E || cstate == CSTATE.CONNECTED_SSCP); } }
        public bool IN_SSCP { get { return (cstate == CSTATE.CONNECTED_SSCP); } }
        public bool IN_TN3270E { get { return (cstate == CSTATE.CONNECTED_TN3270E); } }
        public bool IN_E { get { return (cstate >= CSTATE.CONNECTED_INITIAL_E); } }


        //private bool backslashed = false;
        byte parse_ctlchar(string s)
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

        public void net_sends(string s)
        {
            int i;
            for (i = 0; i < s.Length; i++)
                net_sendc(s[i]);
            //net_cookout(s, s.Length);
        }

        public void net_sendc(char c)
        {
            net_sendc((byte)c);
        }
        public void net_sendc(byte c)
        {
            byte[] buf = new byte[2];
            if (c == '\r' && !linemode)
            {
                /* CR must be quoted */
                buf[0] = (byte)'\r';
                buf[1] = 0;

                net_cookout(buf, 2);
            }
            else
            {
                buf[0] = c;
                net_cookout(buf, 1);
            }
        }
        public void net_abort()
        {
            byte[] buf = new byte[] { IAC, AO };

            if ((e_funcs & E_OPT(TN3270E_FUNC_SYSREQ)) != 0)
            {
                /*
                     * I'm not sure yet what to do here.  Should the host respond
                     * to the AO by sending us SSCP-LU data (and putting us into
                     * SSCP-LU mode), or should we put ourselves in it?
                     * Time, and testers, will tell.
                     */
                switch (tn3270e_submode)
                {
                    case TN3270E_SUBMODE.E_NONE:
                    case TN3270E_SUBMODE.E_NVT:
                        break;
                    case TN3270E_SUBMODE.E_SSCP:
                        net_rawout(buf, buf.Length);
                        trace.trace_dsn("SENT AO\n");
                        if (tn3270e_bound ||
                            0 == (e_funcs & E_OPT(TN3270E_FUNC_BIND_IMAGE)))
                        {
                            tn3270e_submode = TN3270E_SUBMODE.E_3270;
                            check_in3270();
                        }
                        break;
                    case TN3270E_SUBMODE.E_3270:
                        net_rawout(buf, buf.Length);
                        trace.trace_dsn("SENT AO\n");
                        tn3270e_submode = TN3270E_SUBMODE.E_SSCP;
                        check_in3270();
                        break;
                }
            }
        }
        /*
 * net_send_erase
 *	Sends the KILL character in ANSI mode.
 */
        public void net_send_erase()
        {
            byte[] data = new byte[1];
            data[0] = verase;
            net_cookout(data, 1);
        }


        /*
         * net_send_kill
         *	Sends the KILL character in ANSI mode.
         */
        public void net_send_kill()
        {
            byte[] data = new byte[1];
            data[0] = vkill;
            net_cookout(data, 1);
        }

        /*
         * net_send_werase
         *	Sends the WERASE character in ANSI mode.
         */
        public void net_send_werase()
        {
            byte[] data = new byte[1];
            data[0] = vwerase;
            net_cookout(data, 1);
        }


        /*
 * net_hexansi_out
 *	Send uncontrolled user data to the host in ANSI mode, performing IAC
 *	and CR quoting as necessary.
 */
        public void net_hexansi_out(byte[] buf, int len)
        {
            byte[] tbuf;
            int tindex;

            if (len == 0)
                return;

            /* Trace the data. */
            if (appres.toggled(Appres.DS_TRACE))
            {
                int i;

                trace.trace_dsn(">");
                for (i = 0; i < len; i++)
                    trace.trace_dsn(" " + Util.ctl_see(buf[i]));
                trace.trace_dsn("\n");
            }


            /* Expand it. */
            tbuf = new byte[2 * len];
            tindex = 0;
            int bindex = 0;
            while (len > 0)
            {
                byte c = buf[bindex++];

                tbuf[tindex++] = c;
                len--;
                if (c == IAC)
                {
                    tbuf[tindex++] = IAC;
                }
                else if (c == (byte)'\r' && (len == 0 || buf[bindex] != (byte)'\n'))
                {
                    tbuf[tindex++] = 0;
                }
            }

            /* Send it to the host. */
            net_rawout(tbuf, tindex);
        }




        /*
         * net_cookout
         *	Send output in ANSI mode, including cooked-mode processing if
         *	appropriate.
         */

        void net_cookout(byte[] buf, int len)
        {
            if (!IN_ANSI || (keyboard.kybdlock & Keyboard.KL_AWAITING_FIRST) != 0)
                return;
            if (linemode)
            {
                trace.WriteLine("**BUGBUG** net_cookedout not implemented for line mode");
                return;

            }
            else
                net_cookedout(buf, len);

        }

        void ansi_process_s(string data)
        {
            int i;
            for (i = 0; i < data.Length; i++)
            {
                ansi.ansi_process((byte)data[i]);
            }
        }

        /*
		 * Cooked mode input processing.
		 */

        void cooked_init()
        {
            Console.WriteLine("--bugbug--cooked-init())");
        }
        public void net_output(NetBuffer obptr)
        {

            NetBuffer outputBuffer = new NetBuffer();
            /* Set the TN3720E header. */
            if (IN_TN3270E || IN_SSCP)
            {
                TN3270E_HEADER h = new TN3270E_HEADER();

                /* Check for sending a TN3270E response. */
                if (response_required == TN3270E_HEADER.TN3270E_RSF_ALWAYS_RESPONSE)
                {
                    tn3270e_ack();
                    response_required = TN3270E_HEADER.TN3270E_RSF_NO_RESPONSE;
                }

                /* Set the outbound TN3270E header. */
                h.data_type = IN_TN3270E ?
                TN3270E_DT.TN3270_DATA : TN3270E_DT.SSCP_LU_DATA;
                h.request_flag = 0;
                h.response_flag = 0;
                h.seq_number[0] = (byte)((e_xmit_seq >> 8) & 0xff);
                h.seq_number[1] = (byte)(e_xmit_seq & 0xff);

                trace.trace_dsn("SENT TN3270E(%s NO-RESPONSE %u)\n", IN_TN3270E ? "3270-DATA" : "SSCP-LU-DATA", e_xmit_seq);

                if (this.mConnectionConfig.IgnoreSequenceCount == false &&
                    (e_funcs & E_OPT(TN3270E_FUNC_RESPONSES)) != 0)
                    e_xmit_seq = (e_xmit_seq + 1) & 0x7fff;
                h.AddToNetBuffer(outputBuffer);
            }

            int i;
            byte[] data = obptr.Data;
            /* Copy and expand IACs. */
            for (i = 0; i < data.Length; i++)
            {
                outputBuffer.Add(data[i]);
                if (data[i] == IAC)
                    outputBuffer.Add(IAC);
            }
            /* Append the IAC EOR and transmit. */
            outputBuffer.Add(IAC);
            outputBuffer.Add(EOR);
            net_rawout(outputBuffer);

            trace.trace_dsn("SENT EOR\n");
            ns_rsent++;
        }
        public void net_break()
        {
            byte[] buf = new byte[] { IAC, BREAK };

            /* I don't know if we should first send TELNET synch ? */
            net_rawout(buf, buf.Length);
            trace.trace_dsn("SENT BREAK\n");
        }
        public void net_interrupt()
        {
            byte[] buf = new byte[] { IAC, IP };

            /* I don't know if we should first send TELNET synch ? */
            net_rawout(buf, buf.Length);
            trace.trace_dsn("SENT IP\n");
        }

        /*
         * net_cookedout
         *	Send user data out in ANSI mode, without cooked-mode processing.
         */
        void net_cookedout(byte[] buf, int len)
        {
            if (appres.toggled(Appres.DS_TRACE))
            {
                int i;

                trace.trace_dsn(">");
                for (i = 0; i < len; i++)
                    trace.trace_dsn(" %s", Util.ctl_see(buf[i]));
                trace.trace_dsn("\n");
            }
            net_rawout(buf, len);
        }

        private string rsn(int i)
        {
            switch (i)
            {
                case 0: return "CONN-PARTNER";
                case 1: return "DEVICE-IN-USE";
                case 2: return "INV-ASSOCIATE";
                case 3: return "INV-NAME";
                case 4: return "INV-DEVICE-TYPE";
                case 5: return "TYPE-NAME-ERROR";
                case 6: return "UNKNOWN-ERROR";
                case 7: return "UNSUPPORTED-REQ";
                default: return "??";
            }
        }
        /*
         * send_naws
         *	Send a Telnet window size sub-option negotation.
         */
        void send_naws()
        {
            NetBuffer buffer = new NetBuffer();
            //NetBuffer sb2;

            buffer.Add(IAC);
            buffer.Add(SB);
            buffer.Add(TELOPT_NAWS);
            buffer.Add16(tnctlr.maxCOLS); // screen ?
            buffer.Add16(tnctlr.maxROWS); // screen ?
            buffer.Add(IAC);
            buffer.Add(SE);
            net_rawout(buffer);
            trace.trace_dsn("SENT %s NAWS %d %d %s\n", cmd(SB), tnctlr.maxCOLS,
                tnctlr.maxROWS, cmd(SE));
        }
        /*
         * Negotiation of TN3270E options.
         * Returns 0 if okay, -1 if we have to give up altogether.
         */
        int tn3270e_negotiate(NetBuffer sbbuf)
        {
            int sblen;
            int e_rcvd;
            NetBuffer sb2 = null;

            /* Find out how long the subnegotiation buffer is. */
            for (sblen = 0; ; sblen++)
            {
                if (sbbuf.Data[sblen] == SE)
                    break;
            }

            trace.trace_dsn("TN3270E ");

            switch (sbbuf.Data[1])
            {

                case TN3270E_HEADER.TN3270E_OP_SEND:

                    if (sbbuf.Data[2] == TN3270E_HEADER.TN3270E_OP_DEVICE_TYPE)
                    {

                        /* Host wants us to send our device type. */
                        trace.trace_dsn("SEND DEVICE-TYPE SE\n");

                        tn3270e_request();
                    }
                    else
                    {
                        trace.trace_dsn("SEND ??%u SE\n", sbbuf.Data[2]);
                    }
                    break;

                case TN3270E_HEADER.TN3270E_OP_DEVICE_TYPE:

                    /* Device type negotiation. */
                    trace.trace_dsn("DEVICE-TYPE ");

                    switch (sbbuf.Data[2])
                    {
                        case TN3270E_HEADER.TN3270E_OP_IS:
                            {
                                int tnlen, snlen;

                                /* Device type success. */

                                /* Isolate the terminal type and session. */
                                tnlen = 0;
                                while (sbbuf.Data[3 + tnlen] != SE &&
                                    sbbuf.Data[3 + tnlen] != TN3270E_HEADER.TN3270E_OP_CONNECT)
                                    tnlen++;
                                snlen = 0;
                                if (sbbuf.Data[3 + tnlen] == TN3270E_HEADER.TN3270E_OP_CONNECT)
                                {
                                    while (sbbuf.Data[3 + tnlen + 1 + snlen] != SE)
                                        snlen++;
                                }
                                trace.trace_dsn("IS " + sbbuf.AsString(3, tnlen) + " CONNECT " + sbbuf.AsString(3 + tnlen + 1, snlen) + " SE\n");
                                //trace.trace_dsn("IS %s CONNECT %s SE\n",
                                //tnlen, &sbbuf[3],
                                //snlen, &sbbuf[3+tnlen+1]);

                                /* Remember the LU. */
                                if (tnlen != 0)
                                {
                                    if (tnlen > LU_MAX)
                                        tnlen = LU_MAX;
                                    reported_type = sbbuf.AsString(3, tnlen);
                                    //(void)strncpy(reported_type,
                                    //	(char *)&sbbuf[3], tnlen);
                                    //reported_type[tnlen] = '\0';
                                    connected_type = reported_type;
                                }
                                if (snlen != 0)
                                {
                                    if (snlen > LU_MAX)
                                        snlen = LU_MAX;
                                    reported_lu = sbbuf.AsString(3 + tnlen + 1, snlen);
                                    //strncpy(reported_lu,
                                    //	(char *)&sbbuf[3+tnlen+1], snlen);
                                    //reported_lu[snlen] = '\0';
                                    connected_lu = reported_lu;
                                    //							status_lu(connected_lu);
                                }

                                /* Tell them what we can do. */
                                tn3270e_subneg_send(TN3270E_HEADER.TN3270E_OP_REQUEST, e_funcs);
                                break;
                            }
                        case TN3270E_HEADER.TN3270E_OP_REJECT:

                            /* Device type failure. */

                            trace.trace_dsn("REJECT REASON %s SE\n", rsn(sbbuf.Data[4]));
                            if (sbbuf.Data[4] == TN3270E_HEADER.TN3270E_REASON_INV_DEVICE_TYPE ||
                                sbbuf.Data[4] == TN3270E_HEADER.TN3270E_REASON_UNSUPPORTED_REQ)
                            {
                                backoff_tn3270e("Host rejected device type or request type");
                                break;
                            }

                            this.currentLUIndex++;
                            //next_lu();
                            if (this.currentLUIndex < this.lus.Count)//this.lus[this.cutry_lu != CN) 
                            {
                                /* Try the next LU. */
                                tn3270e_request();
                            }
                            else if (lus != null)
                            {
                                /* No more LUs to try.  Give up. */
                                backoff_tn3270e("Host rejected resource(s)");
                            }
                            else
                            {
                                backoff_tn3270e("Device type rejected");
                            }

                            break;
                        default:
                            trace.trace_dsn("??%u SE\n", sbbuf.Data[2]);
                            break;
                    }
                    break;

                case TN3270E_HEADER.TN3270E_OP_FUNCTIONS:

                    /* Functions negotiation. */
                    trace.trace_dsn("FUNCTIONS ");

                    switch (sbbuf.Data[2])
                    {

                        case TN3270E_HEADER.TN3270E_OP_REQUEST:

                            /* Host is telling us what functions they want. */
                            //trace.trace_dsn("BUGBUG**1358");
                            sb2 = sbbuf.CopyFrom(3, sblen - 3);
                            trace.trace_dsn("REQUEST %s SE\n",
                                tn3270e_function_names(sb2));//sbbuf+3, sblen-3));

                            e_rcvd = tn3270e_fdecode(sb2);
                            if ((e_rcvd == e_funcs) || (e_funcs & ~e_rcvd) != 0)
                            {
                                /* They want what we want, or less.  Done. */
                                e_funcs = e_rcvd;
                                tn3270e_subneg_send(TN3270E_HEADER.TN3270E_OP_IS, e_funcs);
                                tn3270e_negotiated = true;
                                trace.trace_dsn("TN3270E option negotiation complete.\n");
                                check_in3270();
                            }
                            else
                            {
                                /*
                                                   * They want us to do something we can't.
                                                   * Request the common subset.
                                                   */
                                e_funcs &= e_rcvd;
                                tn3270e_subneg_send(TN3270E_HEADER.TN3270E_OP_REQUEST,
                                    e_funcs);
                            }
                            break;

                        case TN3270E_HEADER.TN3270E_OP_IS:

                            /* They accept our last request, or a subset thereof. */
                            sb2 = sbbuf.CopyFrom(3, sblen - 3);
                            trace.trace_dsn("IS %s SE\n",
                                tn3270e_function_names(sb2));
                            e_rcvd = tn3270e_fdecode(sb2);
                            if (e_rcvd != e_funcs)
                            {
                                if ((e_funcs & ~e_rcvd) != 0)
                                {
                                    /*
                                    * They've removed something.  This is
                                    * technically illegal, but we can
                                    * live with it.
                                    */
                                    e_funcs = e_rcvd;
                                }
                                else
                                {
                                    /*
                                                       * They've added something.  Abandon
                                                       * TN3270E, they're brain dead.
                                                       */
                                    backoff_tn3270e("Host illegally added function(s)");
                                    break;
                                }
                            }
                            tn3270e_negotiated = true;
                            trace.trace_dsn("TN3270E option negotiation complete.\n");
                            check_in3270();
                            break;

                        default:
                            trace.trace_dsn("??%u SE\n", sbbuf.Data[2]);
                            break;
                    }
                    break;

                default:
                    trace.trace_dsn("??%u SE\n", sbbuf.Data[1]);
                    break;
            }

            /* Good enough for now. */
            return 0;

        }

        /* Send a TN3270E terminal type request. */
        public void tn3270e_request()
        {
            //			int tt_len, tb_len;
            NetBuffer buf = new NetBuffer();
            string try_lu = null;

            //tt_len = termtype.Length;
            if (lus != null)
            {
                try_lu = lus[this.currentLUIndex] as String;
                //	tt_len += try_lu.Length + 1;
            }

            //tb_len = 5 + tt_len + 2;
            //tt_out = (char*)Malloc(tb_len + 1);
            //t = tt_out;
            buf.Add(IAC);
            buf.Add(SB);
            buf.Add(TELOPT_TN3270E);
            buf.Add(TN3270E_HEADER.TN3270E_OP_DEVICE_TYPE);
            buf.Add(TN3270E_HEADER.TN3270E_OP_REQUEST);
            string temp = termtype;
            temp = temp.Replace("3279", "3278"); // replace 3279 with 3278 as per the RFC
            buf.Add(temp);
            if (try_lu != null)
            {
                buf.Add(TN3270E_HEADER.TN3270E_OP_CONNECT);
                buf.Add(try_lu);
            }
            buf.Add(IAC);
            buf.Add(SE);
            net_rawout(buf);

            trace.trace_dsn("SENT %s %s DEVICE-TYPE REQUEST %s %s%s%s\n",
                          cmd(SB),
                opt(TELOPT_TN3270E),
                termtype,
                (try_lu != null) ? " CONNECT " : "",
                (try_lu != null) ? try_lu : "",
                cmd(SE));

        }

        private string fnn(int i)
        {

            string[] function_name = new string[] {
					"BIND-IMAGE", "DATA-STREAM-CTL",
					"RESPONSES", "SCS-CTL-CODES", "SYSREQ" 
												  };
            if (i >= 0 && i < function_name.Length)
            {
                return function_name[i];
            }
            else
                return "?[function_name=" + i + "]?";
        }
        /* Expand a string of TN3270E function codes into text. */
        public string tn3270e_function_names(NetBuffer netbuf)
        {
            int i;
            string temp = "";
            byte[] buf = netbuf.Data;

            if (buf.Length == 0)
                return ("(null)");
            for (i = 0; i < buf.Length; i++)
            {
                if (temp != null)
                    temp += " ";
                temp += fnn(buf[i]);
            }
            return temp;
        }


        /* Expand the current TN3270E function codes into text. */
        string tn3270e_current_opts()
        {
            int i;
            string temp = "";

            if (e_funcs == 0 || !IN_E)
                return null;
            for (i = 0; i < 32; i++)
            {
                if ((e_funcs & E_OPT(i)) != 0)
                {
                    if (temp != null)
                        temp += " ";
                    temp += fnn(i);
                }
            }
            return temp;
        }

        private byte[] functions_req = new byte[]
						{
							IAC, SB, TELOPT_TN3270E,TN3270E_HEADER.TN3270E_OP_FUNCTIONS };

        /* Transmit a TN3270E FUNCTIONS REQUEST or FUNCTIONS IS message. */
        void tn3270e_subneg_send(byte op, int funcs)
        {
            byte[] proto_buf = new byte[7 + 32];
            int proto_len;
            int i;

            /* Construct the buffers. */
            proto_buf[0] = functions_req[0];
            proto_buf[1] = functions_req[1];
            proto_buf[2] = functions_req[2];
            proto_buf[3] = functions_req[3];
            proto_buf[4] = op;
            proto_len = 5;
            for (i = 0; i < 32; i++)
            {
                if ((funcs & E_OPT(i)) != 0)
                    proto_buf[proto_len++] = (byte)i;
            }

            /* Complete and send out the protocol message. */
            proto_buf[proto_len++] = IAC;
            proto_buf[proto_len++] = SE;
            net_rawout(proto_buf, proto_len);

            /* Complete and send out the trace text. */
            trace.trace_dsn("SENT %s %s FUNCTIONS %s %s %s\n",
                cmd(SB), opt(TELOPT_TN3270E),
                (op == TN3270E_HEADER.TN3270E_OP_REQUEST) ? "REQUEST" : "IS",
                tn3270e_function_names(new NetBuffer(proto_buf, 5, proto_len - 7)),
                cmd(SE));
        }

        /* Translate a string of TN3270E functions into a bit-map. */
        int tn3270e_fdecode(NetBuffer netbuf)
        {
            int r = 0;
            int i;
            byte[] buf = netbuf.Data;

            /* Note that this code silently ignores options >= 32. */
            for (i = 0; i < buf.Length; i++)
            {
                if (buf[i] < 32)
                    r |= E_OPT(buf[i]);
            }
            return r;
        }
        /*
         * Back off of TN3270E.
         */
        void backoff_tn3270e(string why)
        {
            trace.trace_dsn("Aborting TN3270E: %s\n", why);

            /* Tell the host 'no'. */
            wont_opt[2] = TELOPT_TN3270E;
            net_rawout(wont_opt, wont_opt.Length);
            trace.trace_dsn("SENT %s %s\n", cmd(WONT), opt(TELOPT_TN3270E));

            /* Restore the LU list; we may need to run it again in TN3270 mode. */
            this.currentLUIndex = 0;


            /* Reset our internal state. */
            myopts[TELOPT_TN3270E] = 0;
            check_in3270();
        }

        public class SC_Item
        {
            public STCALLBACK id;
            public SChangeDelegate lib;
            public SC_Item(STCALLBACK id, SChangeDelegate lib)
            {
                this.id = id;
                this.lib = lib;
            }
        }
        private ArrayList SC_List = new ArrayList();
        public void register_schange(STCALLBACK id, SChangeDelegate lib)
        {
            //Console.WriteLine("register_schange "+id);
            SC_List.Add(new SC_Item(id, lib));
        }
        public void st_changed(STCALLBACK id, bool v)
        {
            int i;
            for (i = 0; i < SC_List.Count; i++)
            {
                SC_Item item = (SC_Item)SC_List[i];
                if (item.id == id)
                    item.lib(v);
            }
        }
        void host_connected()
        {
            cstate = CSTATE.CONNECTED_INITIAL;
            st_changed(STCALLBACK.ST_CONNECT, true);

        }

        void net_connected()
        {
            trace.trace_dsn("NETCONNECTED Connected to %s, port %u.\n", this.address, this.port);

            /* set up telnet options */
            int i;
            for (i = 0; i < myopts.Length; i++)
                myopts[i] = 0;
            for (i = 0; i < hisopts.Length; i++)
                hisopts[i] = 0;
            if (this.mConnectionConfig.IgnoreSequenceCount)
            {
                e_funcs = E_OPT(TN3270E_FUNC_BIND_IMAGE) |
                    E_OPT(TN3270E_FUNC_SYSREQ);
            }
            else
            {
                e_funcs = E_OPT(TN3270E_FUNC_BIND_IMAGE) |
                    E_OPT(TN3270E_FUNC_RESPONSES) |
                    E_OPT(TN3270E_FUNC_SYSREQ);
            }
            e_xmit_seq = 0;
            response_required = TN3270E_HEADER.TN3270E_RSF_NO_RESPONSE;
            telnet_state = TNSTATE.TNS_DATA;
            //ibptr = ibuf;

            /* clear statistics and flags */
            ns_time = DateTime.Now;
            ns_brcvd = 0;
            //ns_rrcvd = 0;
            //ns_bsent = 0;
            ns_rsent = 0;
            syncing = false;
            tn3270e_negotiated = false;
            tn3270e_submode = TN3270E_SUBMODE.E_NONE;
            tn3270e_bound = false;

            //mfwConsole.WriteLine("bug-setup_lus() not called");
            //setup_lus();

            check_linemode(true);

        }
        void host_disconnect(bool failed)
        {
            if (CONNECTED || HALF_CONNECTED)
            {
                //x_remove_input();
                Disconnect();

                /*
                * Remember a disconnect from ANSI mode, to keep screen tracing
                * in sync.
                */
                trace.Stop(IN_ANSI);

                cstate = CSTATE.NOT_CONNECTED;

                /* Propagate the news to everyone else. */
                st_changed(STCALLBACK.ST_CONNECT, false);
            }
        }

        //
        public void RestartReceive()
        {
            // Define a new Callback to read the data 
            AsyncCallback recieveData = new AsyncCallback(OnRecievedData);
            // Begin reading data asyncronously
            mSocketStream.BeginRead(m_byBuff, 0, m_byBuff.Length, recieveData, mSocketStream);
        }

        //
        public bool WaitForConnect()
        {
            //System.Threading.Thread.Sleep(5000);
            //RestartReceive();
            while (!IN_ANSI && !IN_3270)
            {
                //Console.Write("."+cstate);
                System.Threading.Thread.Sleep(100);
                //(void) process_events(True);
                if (!PCONNECTED)
                {
                    this.mDisconnectReason = "Timeout waiting for connection";
                    return false;
                }
            }
            return true;
        }
        // tests
        public void test_enter()
        {
            Console.WriteLine("state = " + cstate);
            /*
            while (!IN_ANSI && !IN_3270) 
            {
                //(void) process_events(True);
                //
                Console.Write(".");
                System.Threading.Thread.Sleep(1000);
                //
                if (!PCONNECTED)
                {
                    Console.WriteLine("not connected");
                    return;
                }
            }
*/
            if ((keyboard.kybdlock & Keyboard.KL_OIA_MINUS) != 0)
            {
                Console.WriteLine("--KL_OIA_MINUS");
                return;
            }
            else if ((keyboard.kybdlock) != 0)
            {
                Console.WriteLine("queue key - " + keyboard.kybdlock);
                throw new ApplicationException("Sorry, queue key is not implemented, please contact mikewarriner@gmail.com for assistance");
                //enq_ta(Enter_action, CN, CN);
            }
            else
            {
                Console.WriteLine("do key");
                keyboard.key_AID(AID.AID_ENTER);
            }

        }

        public bool WaitFor(sms_state what, int timeout)
        {
            lock (this)
            {
                WaitState = what;
                WaitEvent.Reset();
                tnctlr.sms_continue(); // are we already there?
            }
            if (WaitEvent.WaitOne(timeout, false))
            {
                return true;
            }
            else
            {
                lock (this)
                {
                    WaitState = sms_state.SS_IDLE;
                }
                return false;
            }

        }

        /*
        private void trace_TraceEvent(TraceType type, string text)
        {
            Console.Write(text);
        }
        */
#if !FX1_1

        bool cryptocallback(
             Object sender,
             X509Certificate certificate,
             X509Chain chain,
             SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
#endif
    }
 }


