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
using System.Collections.Generic;
using System.Text;

namespace Open3270
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


	public enum KeyboardOp
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

	public enum KeyType 
	{ 
		Standard, 
		GE 
	};

	public enum Composing 
	{ 
		None, 
		Compose, 
		First 
	};


	public enum EIState 
	{ 
		Base, 
		Backslash, 
		BackX, 
		BackP, 
		BackPA, 
		BackPF, 
		Octal, 
		Hex, 
		XGE 
	};

	internal enum EIAction
	{
		String,
		Paste,
		Redraw,
		Keypad,
		Default,
		Key,
		Macro,
		Script,
		Peek,
		TypeAhead,
		FT,
		Command,
		KeyMap
	};

	internal enum DataType3270
	{
		Data3270,
		DataScs,
		Response,
		BindImage,
		Unbind,
		NvtData,
		Request,
		SscpLuData,
		PrintEoj
	}

	public enum TnKey
	{
		F1,
		F2,
		F3,
		F4,
		F5,
		F6,
		F7,
		F8,
		F9,
		F10,
		F11,
		F12,
		F13,
		F14,
		F15,
		F16,
		F17,
		F18,
		F19,
		F20,
		F21,
		F22,
		F23,
		F24,
		Tab,
		BackTab,
		Enter,
		Backspace,
		Clear,
		Delete,
		DeleteField,
		DeleteWord,
		Left,
		Left2,
		Up,
		Right,
		Right2,
		Down,
		Attn,
		CircumNot,
		CursorSelect,
		Dup,
		Erase,
		EraseEOF,
		EraseInput,
		FieldEnd,
		FieldMark,
		FieldExit,
		Home,
		Insert,
		Interrupt,
		Key,
		Newline,
		NextWord,
		PAnn,
		PreviousWord,
		Reset,
		SysReq,
		Toggle,
		ToggleInsert,
		ToggleReverse,
		PA1,
		PA2,
		PA3,
		PA4,
		PA5,
		PA6,
		PA7,
		PA8,
		PA9,
		PA10,
		PA11,
		PA12,
	}
}