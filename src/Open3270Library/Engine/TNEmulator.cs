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
using System.Threading;
using System.Collections;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Reflection;
using Open3270.TN3270;
using Open3270.Library;
using System.Linq;

namespace Open3270
{

	/// <summary>
	/// Summary description for TNEmulator.
	/// </summary>
	public class TNEmulator : IDisposable
	{
		#region Private Variables and Objects
		static bool firstTime = true;

		bool debug = false;
		bool isDisposed = false;
		MySemaphore semaphore = new MySemaphore(0, 9999);
		private object objectState;

		IXMLScreen currentScreenXML; // don't access me directly, use helper
		string mScreenName = null;
		IAudit sout = null;
		bool mUseSSL = false;
		private string _localIP = string.Empty;
		ConnectionConfig mConnectionConfiguration = null;
		TN3270API currentConnection = null;
		#endregion

		#region Event Handlers
		/// <summary>
		/// Event fired when the host disconnects. Note - this must be set before you connect to the host.
		/// </summary>
		public event OnDisconnectDelegate Disconnected;
		public event EventHandler CursorLocationChanged;
		OnDisconnectDelegate apiOnDisconnectDelegate = null;
		#endregion

		#region Constructors / Destructors
		public TNEmulator()
		{
			currentScreenXML = null;
			currentConnection = null;
			this.mConnectionConfiguration = new ConnectionConfig();
		}
		~TNEmulator()
		{
			Dispose(false);
		}
		#endregion

		#region Properties
		/// <summary>
		/// Returns whether or not this session is connected to the mainframe.
		/// </summary>
		public bool IsConnected
		{
			get
			{
				if (this.currentConnection == null)
					return false;
				return this.currentConnection.IsConnected;
			}

		}
		/// <summary>
		/// Gets or sets the ojbect state.
		/// </summary>
		public object ObjectState
		{
			get { return objectState; }
			set { objectState = value; }
		}
		/// <summary>
		/// Returns whether or not the disposed action has been performed on this object.
		/// </summary>
		public bool IsDisposed
		{
			get { return isDisposed; }
		}
		/// <summary>
		/// Returns the reason why the session has been disconnected.
		/// </summary>
		public string DisconnectReason
		{
			get
			{
				lock (this)
				{
					if (this.currentConnection != null)
						return this.currentConnection.DisconnectReason;
				}
				return string.Empty;
			}
		}
		/// <summary>
		/// Returns zero if the keyboard is currently locked (inhibited)
		/// non-zero otherwise
		/// </summary>
		public int KeyboardLocked
		{
			get
			{
				if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
				return this.currentConnection.KeyboardLock;
			}
		}
		/// <summary>
		/// Returns the zero based X coordinate of the cursor
		/// </summary>
		public int CursorX
		{
			get
			{
				if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
				return this.currentConnection.CursorX;
			}
		}
		/// <summary>
		/// Returns the zero based Y coordinate of the cursor
		/// </summary>
		public int CursorY
		{
			get
			{
				if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
				return this.currentConnection.CursorY;
			}
		}
		/// <summary>
		/// Returns the IP address of the mainframe.
		/// </summary>
		public string LocalIP
		{
			get { return _localIP; }
		}
		/// <summary>
		/// Returns the internal configuration object for this connection
		/// </summary>
		public ConnectionConfig Config
		{
			get { return this.mConnectionConfiguration; }
		}
		/// <summary>
		/// Debug flag - setting this to true turns on much more debugging output on the
		/// Audit output
		/// </summary>
		public bool Debug
		{
			get { return debug; }
			set { debug = value; }

		}
		/// <summary>
		/// Set this flag to true to enable SSL connections. False otherwise
		/// </summary>
		public bool UseSSL
		{
			get { return mUseSSL; }
			set { mUseSSL = value; }
		}
		/// <summary>
		/// Returns the current screen XML
		/// </summary>
		public IXMLScreen CurrentScreenXML
		{
			get
			{

				if (this.currentScreenXML == null)
				{
					if (sout != null && Debug == true)
					{
						sout.WriteLine("CurrentScreenXML reloading by calling GetScreenAsXML()");
						currentScreenXML = GetScreenAsXML();
						currentScreenXML.Dump(sout);
					}
					else
					{
						//
						currentScreenXML = GetScreenAsXML();
					}
				}
				//
				return this.currentScreenXML;
			}
		}
		/// <summary>
		/// Auditing interface
		/// </summary>
		public IAudit Audit
		{
			get { return sout; }
			set { sout = value; }
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Disposes of this emulator object.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		/// <summary>
		/// Sends the specified key stroke to the emulator.
		/// </summary>
		/// <param name="waitForScreenToUpdate"></param>
		/// <param name="key"></param>
		/// <param name="timeout"></param>
		/// <returns></returns>
		public bool SendKey(bool waitForScreenToUpdate, TnKey key, int timeout)
		{

			bool triggerSubmit = false;
			bool success = false;
			string command;

			//This is only used as a parameter for other methods when we're using function keys.
			//e.g. F1 yields a command of "PF" and a functionInteger of 1.
			int functionInteger = -1;


			if (sout != null && Debug == true)
			{
				sout.WriteLine("SendKeyFromText(" + waitForScreenToUpdate + ", \"" + key.ToString() + "\", " + timeout + ")");
			}

			if (currentConnection == null)
			{
				throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			}


			//Get the command name and accompanying int parameter, if applicable
			if (Constants.FunctionKeys.Contains(key))
			{
				command = "PF";
				functionInteger = Constants.FunctionKeyIntLUT[key];
			}
			else if (Constants.AKeys.Contains(key))
			{
				command = "PA";
				functionInteger = Constants.FunctionKeyIntLUT[key];
			}
			else
			{
				command = key.ToString();
			}

			//Should this action be followed by a submit?
			triggerSubmit = this.Config.SubmitAllKeyboardCommands || this.currentConnection.KeyboardCommandCausesSubmit(command);

			if (triggerSubmit)
			{
				lock (this)
				{
					this.DisposeOfCurrentScreenXML();
					currentScreenXML = null;

					if (sout != null && Debug)
					{
						sout.WriteLine("mre.Reset. Count was " + semaphore.Count);
					}

					// Clear to initial count (0)
					semaphore.Reset();
				}
			}

			success = this.currentConnection.ExecuteAction(triggerSubmit, command, functionInteger);


			if (sout != null && Debug)
			{
				sout.WriteLine("SendKeyFromText - submit = " + triggerSubmit + " ok=" + success);
			}

			if (triggerSubmit && success)
			{
				// Wait for a valid screen to appear
				if (waitForScreenToUpdate)
				{
					success = this.Refresh(true, timeout);
				}
				else
				{
					success = true;
				}
			}

			return success;

		}
		/// <summary>
		/// Waits until the keyboard state becomes unlocked.
		/// </summary>
		/// <param name="timeoutms"></param>
		public void WaitTillKeyboardUnlocked(int timeoutms)
		{
			DateTime dttm = DateTime.Now.AddMilliseconds(timeoutms);

			while (KeyboardLocked != 0 && DateTime.Now < dttm)
			{
				Thread.Sleep(10); // Wait 1/100th of a second
			}
		}
		/// <summary>
		/// Refresh the current screen.  If timeout > 0, it will wait for 
		/// this number of milliseconds.
		/// If waitForValidScreen is true, it will wait for a valid screen, otherwise it
		/// will return immediately that any screen data is visible
		/// </summary>
		/// <param name="waitForValidScreen"></param>
		/// <param name="timeoutMS">The time to wait in ms</param>
		/// <returns></returns>
		public bool Refresh(bool waitForValidScreen, int timeoutMS)
		{
			long start = DateTime.Now.Ticks / (10 * 1000);
			long end = start + timeoutMS;

			if (currentConnection == null)
			{
				throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			}

			if (sout != null && Debug == true)
			{
				sout.WriteLine("Refresh::Refresh(" + waitForValidScreen + ", " + timeoutMS + "). FastScreenMode=" + this.mConnectionConfiguration.FastScreenMode);
			}

			do
			{
				if (waitForValidScreen)
				{
					bool run = false;
					int timeout = 0;
					do
					{
						timeout = (int)(end - (DateTime.Now.Ticks / 10000));
						if (timeout > 0)
						{
							if (sout != null && this.Debug)
							{
								sout.WriteLine("Refresh::Acquire(" + timeout + " milliseconds). unsafe Count is currently " + semaphore.Count);
							}

							run = semaphore.Acquire(Math.Min(timeout, 1000));

							if (!IsConnected)
							{
								throw new TNHostException("The TN3270 connection was lost", this.currentConnection.DisconnectReason, null);
							}

							if (run)
							{
								if (sout != null && this.Debug)
								{
									sout.WriteLine("Refresh::return true at line 279");
								}
								return true;
							}
							else
							{

							}
						}

					}
					while (!run && timeout > 0);
					if (sout != null && this.Debug) sout.WriteLine("Refresh::Timeout or acquire failed. run= " + run + " timeout=" + timeout);

				}

				if (this.mConnectionConfiguration.FastScreenMode || this.KeyboardLocked == 0)
				{
					// Store screen in screen database and identify it
					this.DisposeOfCurrentScreenXML();

					// Force a refresh
					currentScreenXML = null;
					if (sout != null && this.Debug)
					{
						sout.WriteLine("Refresh::Timeout, but since keyboard is not locked or fastmode=true, return true anyway");
					}

					return true;

				}
				else
					System.Threading.Thread.Sleep(10);

			}
			while (DateTime.Now.Ticks / 10000 < end);

			if (sout != null)
			{
				sout.WriteLine("Refresh::Timed out (2) waiting for a valid screen. Timeout was " + timeoutMS);
			}

			if (Config.FastScreenMode == false && Config.ThrowExceptionOnLockedScreen && this.KeyboardLocked != 0)
			{
				throw new ApplicationException("Timeout waiting for new screen with keyboard inhibit false - screen present with keyboard inhibit. Turn off the configuration option 'ThrowExceptionOnLockedScreen' to turn off this exception. Timeout was " + timeoutMS + " and keyboard inhibit is " + this.KeyboardLocked);
			}

			if (Config.IdentificationEngineOn)
			{
				throw new TNIdentificationException(mScreenName, GetScreenAsXML());
			}
			else
			{
				return false;
			}

		}
		/// <summary>
		/// Dump fields to the current audit output
		/// </summary>
		public void ShowFields()
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);

			if (sout != null)
			{
				sout.WriteLine("-------------------dump screen data -----------------");
				currentConnection.ExecuteAction(false, "Fields");
				sout.WriteLine("" + currentConnection.GetAllStringData(false));
				this.CurrentScreenXML.Dump(sout);
				sout.WriteLine("-------------------dump screen end -----------------");
			}
			else
				throw new ApplicationException("ShowFields requires an active 'Audit' connection on the emulator");
		}
		/// <summary>
		/// Retrieves text at the specified location on the screen
		/// </summary>
		/// <param name="x">Column</param>
		/// <param name="y">Row</param>
		/// <param name="length">Length of the text to be returned</param>
		/// <returns></returns>
		public string GetText(int x, int y, int length)
		{
			return this.CurrentScreenXML.GetText(x, y, length);
		}
		/// <summary>
		/// Sends a string starting at the indicated screen position
		/// </summary>
		/// <param name="text">The text to send</param>
		/// <param name="x">Column</param>
		/// <param name="y">Row</param>
		/// <returns>True for success</returns>
		public bool SetText(string text, int x, int y)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);

			this.SetCursor(x, y);

			return SetText(text);
		}
		/// <summary>
		/// Sents the specified string to the emulator at it's current position.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public bool SetText(string text)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);

			lock (this)
			{
				DisposeOfCurrentScreenXML();
				currentScreenXML = null;
			}
			return currentConnection.ExecuteAction(false, "String", text);
		}
		/// <summary>
		/// Returns after new screen data has stopped flowing from the host for screenCheckInterval time.
		/// </summary>
		/// <param name="screenCheckInterval">The amount of time between screen data comparisons in milliseconds.  
		/// It's probably impractical for this to be much less than 100 ms.</param>
		/// <param name="finalTimeout">The absolute longest time we should wait before the method should time out</param>
		/// <returns>True if data ceased, and false if the operation timed out. </returns>
		public bool WaitForHostSettle(int screenCheckInterval, int finalTimeout)
		{
			bool success = true;
			//Accumulator for total poll time.  This is less accurate than using an interrupt or DateTime deltas, but it's light weight.
			int elapsed = 0;

			//This is low tech and slow, but simple to implement right now.
			while (!this.Refresh(true, screenCheckInterval))
			{
				if (elapsed > finalTimeout)
				{
					success = false;
					break;
				}
				elapsed += screenCheckInterval;
			}

			return success;
		}
		/// <summary>
		/// Returns the last asynchronous error that occured internally
		/// </summary>
		/// <returns></returns>
		public string GetLastError()
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			return currentConnection.GetLastError();
		}
		/// <summary>
		/// Set field value.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="text"></param>
		public void SetField(int index, string text)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			if (index == -1001)
			{
				switch (text)
				{
					case "showparseerror":
						currentConnection.ShowParseError = true;
						return;
					default:
						return;
				}
			}
			currentConnection.ExecuteAction(false, "FieldSet", index, text);
			DisposeOfCurrentScreenXML();
			currentScreenXML = null;
		}
		public void SetField(FieldInfo field, string text)
		{
			//this.currentConnection.ExecuteAction()
			throw new NotImplementedException();
		}
		/// <summary>
		/// Set the cursor position on the screen
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void SetCursor(int x, int y)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			//currentConnection.ExecuteAction("MoveCursor", x,y);
			this.currentConnection.MoveCursor(CursorOp.Exact, x, y);
		}
		/// <summary>
		/// Connects to the mainframe.
		/// </summary>
		public void Connect()
		{
			Connect(this.mConnectionConfiguration.HostName,
				 this.mConnectionConfiguration.HostPort,
				 this.mConnectionConfiguration.HostLU);
		}
		/// <summary>
		/// Connects to host using a local IP endpoint
		/// <remarks>
		/// Added by CFCJR on Feb/29/2008
		/// if a source IP is given then use it for the local IP
		/// </remarks>
		/// </summary>
		/// <param name="localIP"></param>
		/// <param name="host"></param>
		/// <param name="port"></param>
		public void Connect(string localIP, string host, int port)
		{
			_localIP = localIP;
			Connect(host, port, string.Empty);
		}
		/// <summary>
		/// Connect to TN3270 server using the connection details specified.
		/// </summary>
		/// <remarks>
		/// You should set the Audit property to an instance of an object that implements
		/// the IAudit interface if you want to see any debugging information from this function
		/// call.
		/// </remarks>
		/// <param name="host">Host name or IP address. Mandatory</param>
		/// <param name="port">TCP/IP port to connect to (default TN3270 port is 23)</param>
		/// <param name="lu">TN3270E LU to connect to. Specify null for no LU.</param>
		public void Connect(string host, int port, string lu)
		{
			if (this.currentConnection != null)
			{
				this.currentConnection.Disconnect();
				this.currentConnection.CursorLocationChanged -= currentConnection_CursorLocationChanged;
			}

			try
			{

				semaphore.Reset();

				this.currentConnection = null;
				this.currentConnection = new TN3270API();
				this.currentConnection.Debug = debug;
				this.currentConnection.RunScriptRequested += new RunScriptDelegate(currentConnection_RunScriptEvent);
				this.currentConnection.CursorLocationChanged += currentConnection_CursorLocationChanged;
				this.currentConnection.Disconnected += apiOnDisconnectDelegate;

				this.apiOnDisconnectDelegate = new OnDisconnectDelegate(currentConnection_OnDisconnect);

				//
				// Debug out our current state
				//
				if (sout != null)
				{
					sout.WriteLine("Open3270 emulator version " + Assembly.GetAssembly(typeof(Open3270.TNEmulator)).GetName().Version+" (c) 2004-2017 Mike Warriner and many others");
#if false
					sout.WriteLine("(c) 2004-2006 Mike Warriner (mikewarriner@gmail.com). All rights reserved");
					sout.WriteLine("");
					sout.WriteLine("This is free software; you can redistribute it and/or modify it");
					sout.WriteLine("under the terms of the GNU Lesser General Public License as");
					sout.WriteLine("published by the Free Software Foundation; either version 2.1 of");
					sout.WriteLine("the License, or (at your option) any later version.");
					sout.WriteLine("");
					sout.WriteLine("This software is distributed in the hope that it will be useful,");
					sout.WriteLine("but WITHOUT ANY WARRANTY; without even the implied warranty of");
					sout.WriteLine("MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU");
					sout.WriteLine("Lesser General Public License for more details.");
					sout.WriteLine("");
					sout.WriteLine("You should have received a copy of the GNU Lesser General Public");
					sout.WriteLine("License along with this software; if not, write to the Free");
					sout.WriteLine("Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA");
					sout.WriteLine("02110-1301 USA, or see the FSF site: http://www.fsf.org.");
					sout.WriteLine("");
#endif

					if (firstTime)
					{
						firstTime = false;
					}
					if (Debug)
					{
						Config.Dump(sout);
						sout.WriteLine("Connect to host \"" + host + "\"");
						sout.WriteLine("           port \"" + port + "\"");
						sout.WriteLine("           LU   \"" + lu + "\"");
						sout.WriteLine("     Local IP   \"" + _localIP + "\"");
					}
				}
				else
				{
				}

				currentConnection.UseSSL = this.mUseSSL;


				/// Modified CFCJR Feb/29/2008 to support local IP endpoint
				if (!string.IsNullOrEmpty(_localIP))
				{
					currentConnection.Connect(this.sout, _localIP, host, port, this.mConnectionConfiguration);
				}
				else
				{
					currentConnection.Connect(this.sout, host, port, lu, this.mConnectionConfiguration);
				}

				currentConnection.WaitForConnect(-1);
				DisposeOfCurrentScreenXML();
				currentScreenXML = null;
				// Force refresh 
				// GetScreenAsXML();
			}
			catch (Exception)
			{
				currentConnection = null;
				throw;
			}

			// These don't close the connection
			try
			{
				this.mScreenName = "Start";
				Refresh(true, 10000);
				if (sout != null && Debug == true) sout.WriteLine("Debug::Connected");
				//mScreenProcessor.Update_Screen(currentScreenXML, true);

			}
			catch (Exception)
			{
				throw;
			}
			return;
		}
		/// <summary>
		/// Closes the current connection to the mainframe.
		/// </summary>
		public void Close()
		{
			if (currentConnection != null)
			{
				currentConnection.Disconnect();
				currentConnection = null;
			}
		}
		/// <summary>
		/// Waits for the specified text to appear at the specified location.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="text"></param>
		/// <param name="timeoutMS"></param>
		/// <returns></returns>
		public bool WaitForText(int x, int y, string text, int timeoutMS)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			long start = DateTime.Now.Ticks;
			//bool ok = false;
			if (Config.AlwaysRefreshWhenWaiting)
			{
				lock (this)
				{
					DisposeOfCurrentScreenXML();
					this.currentScreenXML = null;
				}
			}
			do
			{
				if (CurrentScreenXML != null)
				{
					string screenText = CurrentScreenXML.GetText(x, y, text.Length);
					if (screenText == text)
					{
						if (Audit != null)
							Audit.WriteLine("WaitForText('" + text + "') Found!");
						return true;
					}
				}
				//
				if (timeoutMS == 0)
				{
					if (Audit != null)
						Audit.WriteLine("WaitForText('" + text + "') Not found");
					return false;
				}
				//
				System.Threading.Thread.Sleep(100);
				if (Config.AlwaysRefreshWhenWaiting)
				{
					lock (this)
					{
						DisposeOfCurrentScreenXML();
						this.currentScreenXML = null;
					}
				}
				Refresh(true, 1000);
			}
			while (((DateTime.Now.Ticks - start) / 10000) < timeoutMS);
			//
			if (Audit != null)
				Audit.WriteLine("WaitForText('" + text + "') Timed out");
			return false;
		}
		/// <summary>
		/// Waits for the specified text to appear at the specified location.
		/// </summary>
		/// <param name="timeoutMS"></param>
		/// <param name="text"></param>
		/// <returns>StreingPosition structure</returns>
		public StringPosition WaitForTextOnScreen2(int timeoutMS, params string[] text)
		{
			if (WaitForTextOnScreen(timeoutMS, text) != -1)
				return CurrentScreenXML.LookForTextStrings2(text);
			else
				return null;
		}


		/// <summary>
		/// Waits for the specified text to appear at the specified location.
		/// </summary>
		/// <param name="timeoutMS"></param>
		/// <param name="text"></param>
		/// <returns>Index of text on screen, -1 if not found or timeout occurs</returns>
		public int WaitForTextOnScreen(int timeoutMS, params string[] text)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			long start = DateTime.Now.Ticks;
			//bool ok = false;
			if (Config.AlwaysRefreshWhenWaiting)
			{
				lock (this)
				{
					DisposeOfCurrentScreenXML();
					this.currentScreenXML = null;
				}
			}
			do
			{
				lock (this)
				{
					if (CurrentScreenXML != null)
					{
						int index = CurrentScreenXML.LookForTextStrings(text);
						if (index != -1)
						{
							if (Audit != null)
								Audit.WriteLine("WaitForText('" + text[index] + "') Found!");
							return index;
						}
					}
				}
				//
				if (timeoutMS > 0)
				{

					//
					System.Threading.Thread.Sleep(100);
					if (Config.AlwaysRefreshWhenWaiting)
					{
						lock (this)
						{
							DisposeOfCurrentScreenXML();
							this.currentScreenXML = null;
						}
					}
					Refresh(true, 1000);
				}
			}
			while (timeoutMS > 0 && ((DateTime.Now.Ticks - start) / 10000) < timeoutMS);
			//
			if (Audit != null)
			{
				string temp = ""; foreach (string t in text) temp += "t" + "//";
			
				Audit.WriteLine("WaitForText('" + temp + "') Timed out");
			}
			return -1;
		}
		/// <summary>
		/// Dump current screen to the current audit output
		/// </summary>
		public void Dump()
		{
			lock (this)
			{
				if (sout != null)
					CurrentScreenXML.Dump(sout);
			}
		}
		/// <summary>
		/// Refreshes the connection to the mainframe.
		/// </summary>
		public void Refresh()
		{
			lock (this)
			{
				DisposeOfCurrentScreenXML();
				currentScreenXML = null;
			}
		}
		#endregion

		#region Protected / Internal Methods
		protected void DisposeOfCurrentScreenXML()
		{
			if (currentScreenXML != null)
			{
				IDisposable disposeXML = currentScreenXML as IDisposable;
				if (disposeXML != null)
					disposeXML.Dispose();
				currentScreenXML = null;
			}
		}
		protected virtual void Dispose(bool disposing)
		{
			lock (this)
			{
				if (IsDisposed)
					return;
				isDisposed = true;

				if (sout != null && Debug)
					sout.WriteLine("TNEmulator.Dispose(" + IsDisposed.ToString() + ")");

				if (disposing)
				{
					//----------------------------
					// release managed resources

					if (currentConnection != null)
					{
						if (sout != null && Debug)
							sout.WriteLine("TNEmulator.Dispose() Disposing of currentConnection");
						try
						{
							currentConnection.Disconnect();
							this.currentConnection.CursorLocationChanged -= currentConnection_CursorLocationChanged;

							if (apiOnDisconnectDelegate != null)
								currentConnection.Disconnected -= apiOnDisconnectDelegate;

							currentConnection.Dispose();
						}
						catch
						{
							if (sout != null && Debug)
								sout.WriteLine("TNEmulator.Dispose() Exception during currentConnection.Dispose");
						}
						currentConnection = null;
					}

					Disconnected = null;

					if (sout != null && Debug)
						sout.WriteLine("TNEmulator.Dispose() Disposing of currentScreenXML");

					DisposeOfCurrentScreenXML();

					if (objectState != null)
					{
						objectState = null;
					}
					if (mConnectionConfiguration != null)
					{
						mConnectionConfiguration = null;
					}
					if (mScreenName != null)
					{
						mScreenName = null;
					}
				}

				//------------------------------
				// release unmanaged resources

			}
		}
		protected virtual void OnCursorLocationChanged(EventArgs args)
		{
			if (this.CursorLocationChanged != null)
			{
				this.CursorLocationChanged(this, args);
			}
		}
		/// <summary>
		/// Get the current screen as an XMLScreen data structure
		/// </summary>
		/// <returns></returns>
		internal IXMLScreen GetScreenAsXML()
		{
			DisposeOfCurrentScreenXML();

			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			if (currentConnection.ExecuteAction(false, "DumpXML"))
			{
				//
				return XMLScreen.LoadFromString(currentConnection.GetAllStringData(false));
			}
			else
				return null;
		}
		#endregion

		#region Private Methods
		void currentConnection_CursorLocationChanged(object sender, EventArgs e)
		{
			this.OnCursorLocationChanged(e);
		}
		private void currentConnection_RunScriptEvent(string where)
		{
			lock (this)
			{
				DisposeOfCurrentScreenXML();

				if (sout != null && Debug) sout.WriteLine("mre.Release(1) from location " + where);
				semaphore.Release(1);
			}
		}
		/// <summary>
		/// Wait for some text to appear at the specified location
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="text"></param>
		/// <param name="timeoutMS"></param>
		/// <returns></returns>
		private void currentConnection_OnDisconnect(TNEmulator where, string Reason)
		{
			if (this.Disconnected != null)
				this.Disconnected(this, Reason);
		}
		#endregion

		#region Deprecated Methods
		[Obsolete("This method has been deprecated.  Please use SendKey instead. This method is only included for backwards compatibiity and might not exist in future releases.")]
		public bool SendKeyFromText(bool waitForScreenToUpdate, string text)
		{
			return SendKeyFromText(waitForScreenToUpdate, text, Config.DefaultTimeout);
		}
		[Obsolete("This method has been deprecated.  Please use SendKey instead.  This method is only included for backwards compatibiity and might not exist in future releases.")]
		public bool SendKeyFromText(bool waitForScreenToUpdate, string text, int timeout)
		{

			bool submit = false;
			bool success = false;

			if (sout != null && Debug == true)
			{
				sout.WriteLine("SendKeyFromText(" + waitForScreenToUpdate + ", \"" + text + "\", " + timeout + ")");
			}

			if (currentConnection == null)
			{
				throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			}

			if (text.Length < 2)
			{
				// No keys are less than 2 characters.
				return false;
			}


			if (this.Config.SubmitAllKeyboardCommands)
			{
				submit = true;
			}
			else
			{
				if (text.Substring(0, 2) == "PF")
				{
					submit = this.currentConnection.KeyboardCommandCausesSubmit("PF");
				}
				else if (text.Substring(0, 2) == "PA")
				{
					submit = this.currentConnection.KeyboardCommandCausesSubmit("PA");
				}
				else
				{
					submit = this.currentConnection.KeyboardCommandCausesSubmit(text);
				}
			}


			if (submit)
			{
				lock (this)
				{
					this.DisposeOfCurrentScreenXML();
					currentScreenXML = null;

					if (sout != null && Debug) sout.WriteLine("mre.Reset. Count was " + semaphore.Count);
					{
						// Clear to initial count (0)
						semaphore.Reset();
					}
				}

			}


			if (text.Substring(0, 2) == "PF")
			{
				success = this.currentConnection.ExecuteAction(submit, "PF", System.Convert.ToInt32(text.Substring(2, 2)));
			}
			else if (text.Substring(0, 2) == "PA")
			{
				success = this.currentConnection.ExecuteAction(submit, "PA", System.Convert.ToInt32(text.Substring(2, 2)));
			}
			else
			{
				success = this.currentConnection.ExecuteAction(submit, text);
			}

			if (sout != null && Debug)
			{
				sout.WriteLine("SendKeyFromText - submit = " + submit + " ok=" + success);
			}

			if (submit && success)
			{
				// Wait for a valid screen to appear
				if (waitForScreenToUpdate)
				{
					return Refresh(true, timeout);
				}
				else
				{
					return true;
				}
			}
			else
			{
				return success;
			}
		}
		[Obsolete("This method has been deprecated.  Please use SetText instead.  This method is only included for backwards compatibiity and might not exist in future releases.")]
		public bool SendText(string text)
		{
			if (currentConnection == null) throw new TNHostException("TNEmulator is not connected", "There is no currently open TN3270 connection", null);
			lock (this)
			{
				DisposeOfCurrentScreenXML();
				currentScreenXML = null;
			}
			return currentConnection.ExecuteAction(false, "String", text);
		}
		[Obsolete("This method has been deprecated.  Please use SetText instead.  This method is only included for backwards compatibiity and might not exist in future releases.")]
		public bool PutText(string text, int x, int y)
		{
			return SetText(text, x, y);
		}
		#endregion
	}

	public delegate void OnDisconnectDelegate(TNEmulator where, string Reason);
}
