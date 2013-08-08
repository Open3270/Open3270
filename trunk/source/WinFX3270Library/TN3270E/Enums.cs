using System;
using System.Collections.Generic;
using System.Text;

namespace Open3270.TN3270
{

	internal enum SmsState
	{
		/// <summary>
		/// no command active (scripts only) 
		/// </summary>
		Idle,
		/// <summary>
		/// command(s) buffered and ready to run
		/// </summary>
		Incomplete,
		/// <summary>
		/// command executing
		/// </summary>
		Running,	
		/// <summary>
		/// command awaiting keyboard unlock
		/// </summary>
		KBWait,	
		/// <summary>
		/// command awaiting connection to complete
		/// </summary>
		ConnectWait,
		/// <summary>
		/// stopped in PauseScript action
		/// </summary>
		Paused,	
		/// <summary>
		/// awaiting completion of Wait(ansi)
		/// </summary>
		WaitAnsi,	
		/// <summary>
		/// awaiting completion of Wait(3270)
		/// </summary>
		Wait3270,	
		/// <summary>
		/// awaiting completion of Wait(Output)
		/// </summary>
		WaitOutput,	
		/// <summary>
		/// awaiting completion of Snap(Wait)
		/// </summary>
		SnapWaitOutput,
		/// <summary>
		/// awaiting completion of Wait(Disconnect)
		/// </summary>
		WaitDisconnect,	
		/// <summary>
		/// awaiting completion of Wait()
		/// </summary>
		Wait,	
		/// <summary>
		/// awaiting completion of Expect()
		/// </summary>
		Expecting,	
		/// <summary>
		/// awaiting completion of Close()
		/// </summary>
		Closing	
	};


	enum ConnectionState
	{
		/// <summary>
		/// no socket, unknown mode
		/// </summary>
		NotConnected = 0,
		/// <summary>
		/// resolving hostname
		/// </summary>
		Resolving,
		/// <summary>
		/// connection pending
		/// </summary>
		Pending,
		/// <summary>
		/// connected, no mode yet
		/// </summary>
		ConnectedInitial,
		/// <summary>
		/// connected in NVT ANSI mode
		/// </summary>
		ConnectedANSI,
		/// <summary>
		/// connected in old-style 3270 mode
		/// </summary>
		Connected3270,
		/// <summary>
		/// connected in TN3270E mode, unnegotiated
		/// </summary>
		ConnectedInitial3270E,
		/// <summary>
		/// connected in TN3270E mode, NVT mode
		/// </summary>
		ConnectedNVT,
		/// <summary>
		/// connected in TN3270E mode, SSCP-LU mode
		/// </summary>
		ConnectedSSCP,
		/// <summary>
		/// connected in TN3270E mode, 3270 mode
		/// </summary>
		Connected3270E
	};

	internal enum TNEvent
	{
		Connect,
		Data,
		Error,
		Disconnect,
		DisconnectUnexpected
	}

	internal enum TN3270ESubmode
	{
		None,
		Mode3270,
		NVT,
		SSCP
	}


	internal enum KeyboardOp
	{
		Reset,
		AID,
		ATTN,
		Home
	}

	public enum CursorOp
	{
		Tab,
		BackTab,
		Exact,
		NearestUnprotectedField
	}

	internal enum PDS
	{
		
		/// <summary>
		/// Command accepted, produced no output
		/// </summary>
		OkayNoOutput = 0,

		/// <summary>
		/// Command accepted, produced output
		/// </summary>
		OkayOutput = 1,
	
		/// <summary>
		/// Command rejected
		/// </summary>
		BadCommand = -1,

		/// <summary>
		/// Command contained a bad address
		/// </summary>
		BadAddress = -2	
	}

	enum ControllerState
	{
		Data = 0, 
		Esc = 1, 
		CSDES = 2,
		N1 = 3, 
		DECP = 4, 
		Text = 5, 
		Text2 = 6
	}

	public enum PreviousEnum 
	{ 
		None,
		Order,
		SBA,
		Text,
		NullCharacter 
	};

	public enum TN3270State
	{
		InNeither,
		ANSI,
		TN3270
	}
	internal enum TelnetState
	{
		/// <summary>
		/// receiving data
		/// </summary>
		Data,
		/// <summary>
		/// got an IAC
		/// </summary>
		IAC,
		/// <summary>
		/// got an IAC WILL
		/// </summary>
		Will,
		/// <summary>
		/// got an IAC WONT
		/// </summary>
		Wont,
		/// <summary>
		/// got an IAC DO
		/// </summary>
		Do,
		/// <summary>
		/// got an IAC DONT
		/// </summary>
		Dont,
		/// <summary>
		/// got an IAC SB
		/// </summary>
		SB,
		/// <summary>
		/// got an IAC after an IAC SB
		/// </summary>
		SbIac

	}


	internal delegate void TelnetDataDelegate(object parentData, TNEvent eventType, string text);

	internal delegate void SChangeDelegate(bool option);

	internal delegate void RunScriptDelegate(string where);
}
