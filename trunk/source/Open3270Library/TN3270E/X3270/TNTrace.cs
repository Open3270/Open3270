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

namespace Open3270
{
	internal enum TraceType
	{
		DS,
		DSN,
		AnsiChar,
		Event,
		NetData,
		Screen
	}
	//internal delegate void TraceDelegate(TraceType type, string text);
}

namespace Open3270.TN3270
{
	internal class TNTrace
	{

		Telnet telnet;
		public const int TRACELINE	=72;
		public bool optionTraceNetworkData = false;
		public bool optionTraceDS = false;
		public bool optionTraceDSN = false;
		public bool optionTraceAnsi = false;
		public bool optionTraceEvent = false;
		long ds_ts;
		const int LINEDUMP_MAX = 32;

		//public event TraceDelegate TraceEvent = null;
			

		IAudit mAudit = null;

		internal TNTrace(Telnet telnet, IAudit audit)
		{
			this.telnet = telnet;
			this.mAudit = audit;
		}
		public void Start()
		{
		}
		public void Stop(bool ansi)
		{
		}
		private void TraceEvent(TraceType type, string text)
		{
			if (mAudit != null)
			{
				mAudit.Write(text);
			}
		}
		public void WriteLine(string text)
		{
			if (!optionTraceDS)
				return;
			if (mAudit != null)
				mAudit.WriteLine(text);
		}
		// TN commands
		public void trace_ds(string fmt, params object[] args)
		{
			if (!optionTraceDS)
				return;
			
			TraceEvent(TraceType.DS, TraceFormatter.Format(fmt,args));
		}
		// TN bytes in english
		public void trace_dsn(string fmt, params object[] args)
		{
			if (!optionTraceDSN)
				return;
			TraceEvent(TraceType.DSN, TraceFormatter.Format(fmt,args));
		}
		// TN characters (in ansi mode)
		public void trace_char(char c)
		{
			if (!optionTraceAnsi)
				return;
			TraceEvent(TraceType.AnsiChar, ""+c);
		}
		// TN events
		public void trace_event(string fmt, params object[] args)
		{
			if (!optionTraceEvent)
				return;
			TraceEvent(TraceType.Event, TraceFormatter.Format(fmt,args));
		}
		// TN bytes in hex
		public void trace_netdata(char direction, byte[] buf, int len)
		{
			if (!optionTraceNetworkData)
				return;

			int offset;
			long ts = DateTime.Now.Ticks;

			if (telnet.Is3270) 
			{
				trace_dsn("%c +%f\n", direction, (double)(((ts-ds_ts)/10000)/1000.0));
			}
			ds_ts = ts;
			for (offset = 0; offset < len; offset++) 
			{
				if (0==(offset % LINEDUMP_MAX))
				{
					string temp = (offset!=0 ? "\n" : "");
					temp+=direction+" 0x";
					temp+=String.Format("{0:x3} ", offset);
					trace_dsn(temp);
				}
				trace_dsn(String.Format("{0:x2}", buf[offset]));
			}
			trace_dsn("\n");
		}
		// dump a screen (not used at present)
		public void trace_screen()
		{
			Console.WriteLine("--dump screen");
		}
		

		/* display a (row,col) */
		public string rcba(int baddr)
		{
			return "("+(baddr/telnet.Controller.ColumnCount+1)+","+(baddr%telnet.Controller.ColumnCount+1)+")";
		}

		//
	}
}
