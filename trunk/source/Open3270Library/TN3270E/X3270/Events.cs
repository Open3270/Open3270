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
