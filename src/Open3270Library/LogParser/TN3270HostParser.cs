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
using Open3270;

namespace Open3270.TN3270
{
	/// <summary>
	/// Summary description for LogParser.
	/// </summary>
	public class TN3270HostParser : IAudit
	{
		Telnet telnet;
		/// <summary>
		/// 
		/// </summary>
		public TN3270HostParser()
		{
			Open3270.ConnectionConfig config = new ConnectionConfig();
			config.HostName = "DUMMY_PARSER";
			TN3270API api = new TN3270API();
 
			telnet = new Telnet(api, this, config);
			telnet.Trace.optionTraceAnsi = true;
			telnet.Trace.optionTraceDS = true;
			telnet.Trace.optionTraceDSN = true;
			telnet.Trace.optionTraceEvent = true;
			telnet.Trace.optionTraceNetworkData = true;
			telnet.telnetDataEventOccurred += new TelnetDataDelegate(telnet_telnetDataEvent);

			telnet.Connect(null,null,0);
		}

		public ConnectionConfig Config
		{
			get { return telnet.Config; }
		}
		public string Status
		{
			get
			{
				string text = "";
				text+= "kybdinhibit = "+telnet.Keyboard.keyboardLock;
				return text;
			}
		}
		/// <summary>
		/// Parse a byte of host data
		/// </summary>
		/// <param name="ch"></param>
		public void Parse(byte ch)
		{
			if (!telnet.ParseByte(ch))
				Console.WriteLine("Disconnect should occur next");
			
		}
		#region IAudit Members

		public void Write(string text)
		{
			WriteLine(text);
		}

		public void WriteLine(string text)
		{
			// TODO:  Add LogParser.WriteLine implementation
			Console.Write(text);
		}

		#endregion

		private void telnet_telnetDataEvent(object parentData, TNEvent eventType, string text)
		{
			Console.WriteLine("EVENT "+eventType+" "+text);
		}
	}
}
