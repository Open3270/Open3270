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
			int cols = telnet.Controller.ColumnCount;
			int y = baddr /  cols + 1;
			int x = baddr % cols +1;
			return "(baddr=" + baddr + ",cols="+cols+", y=" + y + ",x=" + x + ")";
		}

		//
	}
}
