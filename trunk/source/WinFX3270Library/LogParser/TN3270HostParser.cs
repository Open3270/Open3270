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
