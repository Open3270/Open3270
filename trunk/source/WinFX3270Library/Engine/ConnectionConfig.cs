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
			hostName = null;
			hostPort = 23;
			hostLU = null;
			termType = null;
		}
		private bool fastScreenMode = false;
		private StreamReader logFile = null;
		private bool ignoreSequenceCount = false;
		private bool identificationEngineOn = false;
		private bool alwaysSkipToUnprotected = true;
		private bool lockScreenOnWriteToUnprotected = false;
		private bool throwExceptionOnLockedScreen = true;
		private int defaultTimeout = 40000;
		private string hostName;
		private int hostPort;
		private string hostLU;
		private string termType;
		private bool alwaysRefreshWhenWaiting = false;
		private bool submitAllKeyboardCommands = false;
		private bool refuseTN3270E = false;
		private bool useSSL = false;


		internal void Dump(IAudit sout)
		{
			if (sout == null) return;
			sout.WriteLine("Config.FastScreenMode " + fastScreenMode);
			sout.WriteLine("Config.IgnoreSequenceCount " + ignoreSequenceCount);
			sout.WriteLine("Config.IdentificationEngineOn " + identificationEngineOn);
			sout.WriteLine("Config.AlwaysSkipToUnprotected " + alwaysSkipToUnprotected);
			sout.WriteLine("Config.LockScreenOnWriteToUnprotected " + lockScreenOnWriteToUnprotected);
			sout.WriteLine("Config.ThrowExceptionOnLockedScreen " + throwExceptionOnLockedScreen);
			sout.WriteLine("Config.DefaultTimeout " + defaultTimeout);
			sout.WriteLine("Config.hostName " + hostName);
			sout.WriteLine("Config.hostPort " + hostPort);
			sout.WriteLine("Config.hostLU " + hostLU);
			sout.WriteLine("Config.termType " + termType);
			sout.WriteLine("Config.AlwaysRefreshWhenWaiting " + alwaysRefreshWhenWaiting);
			sout.WriteLine("Config.SubmitAllKeyboardCommands " + submitAllKeyboardCommands);
			sout.WriteLine("Config.RefuseTN3270E " + refuseTN3270E);
		}

		/// <summary>
		/// The host name to connect to
		/// </summary>
		public string HostName
		{
			get { return hostName; }
			set { hostName = value; }
		}
		/// <summary>
		/// Host Port
		/// </summary>
		public int HostPort
		{
			get { return hostPort; }
			set { hostPort = value; }
		}
		/// <summary>
		/// Host LU, null for none
		/// </summary>
		public string HostLU
		{
			get { return hostLU; }
			set { hostLU = value; }
		}
		/// <summary>
		/// Terminal type for host
		/// </summary>
		public string TermType
		{
			get { return termType; }
			set { termType = value; }

		}

		public bool UseSSL
		{
			get { return useSSL; }
			set { useSSL = value; }
		}

		/// <summary>
		/// Is the internal screen identification engine turned on? Default false.
		/// </summary>
		public bool IdentificationEngineOn
		{
			get { return identificationEngineOn; }
			set { identificationEngineOn = value; }
		}

		/// <summary>
		/// Should we skip to the next unprotected field if SendText is called
		/// on an protected field? Default true.
		/// </summary>
		public bool AlwaysSkipToUnprotected
		{
			get { return alwaysSkipToUnprotected; }
			set { alwaysSkipToUnprotected = value; }
		}

		/// <summary>
		/// Lock the screen if user tries to write to a protected field. Default false.
		/// </summary>
		public bool LockScreenOnWriteToUnprotected
		{
			get { return lockScreenOnWriteToUnprotected; }
			set { lockScreenOnWriteToUnprotected = value; }
		}

		/// <summary>
		/// Default timeout for operations such as SendKeyFromText. Default value is 40000 (40 seconds).
		/// </summary>
		public int DefaultTimeout
		{
			get { return defaultTimeout; }
			set { defaultTimeout = value; }
		}

		/// <summary>
		/// Flag to set whether an exception should be thrown if a screen write met
		/// a locked screen. Default is now true.
		/// </summary>
		public bool ThrowExceptionOnLockedScreen
		{
			get { return throwExceptionOnLockedScreen; }
			set { throwExceptionOnLockedScreen = value; }
		}

		/// <summary>
		/// Whether to ignore host request for sequence counting
		/// </summary>
		public bool IgnoreSequenceCount
		{
			get { return ignoreSequenceCount; }
			set { ignoreSequenceCount = value; }
		}

		/// <summary>
		/// Allows connection to be connected to a proxy log file rather than directly to a host
		/// for debugging.
		/// </summary>
		public StreamReader LogFile
		{
			get { return logFile; }
			set { logFile = value; }
		}

		/// <summary>
		/// Whether to ignore keyboard inhibit when moving between screens. Significantly speeds up operations, 
		/// but can result in locked screens and data loss if you try to key data onto a screen that is still locked.
		/// </summary>
		public bool FastScreenMode
		{
			get { return fastScreenMode; }
			set { fastScreenMode = value; }
		}


		/// <summary>
		/// Whether the screen should always be refreshed when waiting for an update. Default is false.
		/// </summary>
		public bool AlwaysRefreshWhenWaiting
		{
			get { return alwaysRefreshWhenWaiting; }
			set { alwaysRefreshWhenWaiting = value; }
		}


		/// <summary>
		/// Whether to refresh the screen for keys like TAB, BACKSPACE etc should refresh the host. Default is now false.
		/// </summary>
		public bool SubmitAllKeyboardCommands
		{
			get { return submitAllKeyboardCommands; }
			set { submitAllKeyboardCommands = value; }
		}

		/// <summary>
		/// Whether to refuse a TN3270E request from the host, despite the terminal type
		/// </summary>
		public bool RefuseTN3270E
		{
			get { return refuseTN3270E; }
			set { refuseTN3270E = value; }
		}


	}
}
