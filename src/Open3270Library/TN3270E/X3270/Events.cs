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
using System.Text;
using System.Collections;

namespace Open3270.TN3270
{
	internal class EventNotification
	{
		public string error;
		object[] data;
		public EventNotification(string error, object[] data)
		{
			this.error = error;
			this.data = data;
		}
		public override string ToString()
		{
			return TraceFormatter.Format(error,data);
		}

	}
	internal delegate void Error(string error);
	/// <summary>
	/// Summary description for Events.
	/// </summary>
	internal class Events
	{
		Telnet telnet;
		ArrayList events;
		
		internal Events(Telnet tn)
		{
			telnet = tn;
			events = new ArrayList();
		}
		public void Clear()
		{
			events = new ArrayList();
		}
		public string GetErrorAsText()
		{
			if (events.Count==0)
				return null;
			StringBuilder builder = new StringBuilder();
			for (int i=0; i<events.Count; i++)
			{
				builder.Append(events[i].ToString());
			}
			
			return builder.ToString();

		}
		public bool IsError()
		{
			if (events.Count>0)
				return true;
			else
				return false;
		}
		
		public void ShowError(string error, params object[] args)
		{
			events.Add(new EventNotification(error, args));
			Console.WriteLine("ERROR"+TraceFormatter.Format(error,args));
			//telnet.FireEvent(error, args);
		}
		public void Warning(string warning)
		{
			Console.WriteLine("warning=="+warning);
		}
		public void RunScript(string where)
		{
			//Console.WriteLine("Run Script "+where);
			lock (telnet)
			{
				if ((telnet.Keyboard.keyboardLock | KeyboardConstants.DeferredUnlock) == KeyboardConstants.DeferredUnlock)
				{
					telnet.Keyboard.KeyboardLockClear(KeyboardConstants.DeferredUnlock, "defer_unlock");
					if (telnet.IsConnected)
						telnet.Controller.ProcessPendingInput();
				}
			}
																									 
																									
			
			if (telnet.TelnetApi != null)
				telnet.TelnetApi.RunScript(where);
			
		}
	}
}
