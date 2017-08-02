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
using System.Threading;
using System.Collections;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Reflection;

namespace Open3270
{
	/// <summary>
	/// Connection configuration class holds the configuration options for a connection
	/// </summary>
	public class ConnectionConfig
	{
		public ConnectionConfig()
		{
			_hostName = null;
			_hostPort = 23;
			_hostLU   = null;
			_termType = null;
		}
		private bool _FastScreenMode = false;
		private StreamReader _LogFile = null;
		private bool _IgnoreSequenceCount = false;
		private bool _IdentificationEngineOn = false;
		private bool _AlwaysSkipToUnprotected = true;
		private bool _LockScreenOnWriteToUnprotected = false;
		private bool _ThrowExceptionOnLockedScreen = true;
		private int _DefaultTimeout = 40000;
		private string _hostName;
		private int    _hostPort;
		private string _hostLU;
		private string _termType;
		private bool   _AlwaysRefreshWhenWaiting = false;
		private bool   _SubmitAllKeyboardCommands = false;
		private bool   _RefuseTN3270E = false;

		internal void Dump(IAudit sout)
		{
			if (sout==null) return;
			sout.WriteLine("Config.FastScreenMode "+_FastScreenMode);
			sout.WriteLine("Config.IgnoreSequenceCount "+_IgnoreSequenceCount);
			sout.WriteLine("Config.IdentificationEngineOn "+_IdentificationEngineOn);
			sout.WriteLine("Config.AlwaysSkipToUnprotected "+_AlwaysSkipToUnprotected);
			sout.WriteLine("Config.LockScreenOnWriteToUnprotected "+_LockScreenOnWriteToUnprotected);
			sout.WriteLine("Config.ThrowExceptionOnLockedScreen "+_ThrowExceptionOnLockedScreen);
			sout.WriteLine("Config.DefaultTimeout "+_DefaultTimeout);
			sout.WriteLine("Config.hostName "+_hostName);
			sout.WriteLine("Config.hostPort "+_hostPort);
			sout.WriteLine("Config.hostLU "+_hostLU);
			sout.WriteLine("Config.termType "+_termType);
			sout.WriteLine("Config.AlwaysRefreshWhenWaiting "+_AlwaysRefreshWhenWaiting);
			sout.WriteLine("Config.SubmitAllKeyboardCommands "+_SubmitAllKeyboardCommands);
			sout.WriteLine("Config.RefuseTN3270E "+_RefuseTN3270E);
		}

		/// <summary>
		/// The host name to connect to
		/// </summary>
		public string HostName
		{
			get { return _hostName;  }
			set { _hostName = value; }
		}
		/// <summary>
		/// Host Port
		/// </summary>
		public int HostPort
		{
			get { return _hostPort;  }
			set { _hostPort = value; }
		}
		/// <summary>
		/// Host LU, null for none
		/// </summary>
		public string HostLU
		{
			get { return _hostLU;  }
			set { _hostLU = value; }
		}
		/// <summary>
		/// Terminal type for host
		/// </summary>
		public string TermType
		{
			get { return _termType;  }
			set { _termType = value; }

		}
		/// <summary>
		/// Is the internal screen identification engine turned on? Default false.
		/// </summary>
		public bool IdentificationEngineOn 
		{ 
			get { return _IdentificationEngineOn; } 
			set { _IdentificationEngineOn = value; }
		}

		/// <summary>
		/// Should we skip to the next unprotected field if SendText is called
		/// on an protected field? Default true.
		/// </summary>
		public bool AlwaysSkipToUnprotected
		{
			get { return _AlwaysSkipToUnprotected; }
			set { _AlwaysSkipToUnprotected = value; }
		}

		/// <summary>
		/// Lock the screen if user tries to write to a protected field. Default false.
		/// </summary>
		public bool LockScreenOnWriteToUnprotected
		{
			get { return _LockScreenOnWriteToUnprotected; }
			set { _LockScreenOnWriteToUnprotected = value; }
		}

		/// <summary>
		/// Default timeout for operations such as SendKeyFromText. Default value is 40000 (40 seconds).
		/// </summary>
		public int DefaultTimeout
		{
			get { return _DefaultTimeout; }
			set { _DefaultTimeout = value; }
		}

		/// <summary>
		/// Flag to set whether an exception should be thrown if a screen write met
		/// a locked screen. Default is now true.
		/// </summary>
		public bool ThrowExceptionOnLockedScreen
		{
			get { return _ThrowExceptionOnLockedScreen; }
			set { _ThrowExceptionOnLockedScreen = value; }
		}

		/// <summary>
		/// Whether to ignore host request for sequence counting
		/// </summary>
		public bool IgnoreSequenceCount
		{
			get { return _IgnoreSequenceCount; }
			set { _IgnoreSequenceCount = value; }
		}

		/// <summary>
		/// Allows connection to be connected to a proxy log file rather than directly to a host
		/// for debugging.
		/// </summary>
		public StreamReader LogFile
		{
			get { return _LogFile; }
			set { _LogFile = value;}
		}

		/// <summary>
		/// Whether to ignore keyboard inhibit when moving between screens. Significantly speeds up operations, 
		/// but can result in locked screens and data loss if you try to key data onto a screen that is still locked.
		/// </summary>
		public bool FastScreenMode
		{
			get { return _FastScreenMode; }
			set { _FastScreenMode = value; }
		}


		/// <summary>
		/// Whether the screen should always be refreshed when waiting for an update. Default is false.
		/// </summary>
		public bool AlwaysRefreshWhenWaiting
		{
			get { return _AlwaysRefreshWhenWaiting; }
			set { _AlwaysRefreshWhenWaiting = value; }
		}


		/// <summary>
		/// Whether to refresh the screen for keys like TAB, BACKSPACE etc should refresh the host. Default is now false.
		/// </summary>
		public bool SubmitAllKeyboardCommands
		{
			get { return _SubmitAllKeyboardCommands; }
			set { _SubmitAllKeyboardCommands = value;}
		}

		/// <summary>
		/// Whether to refuse a TN3270E request from the host, despite the terminal type
		/// </summary>
		public bool RefuseTN3270E
		{
			get { return _RefuseTN3270E; }
			set { _RefuseTN3270E = value;}
		}
		//


		
	}
}
