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

namespace Open3270.TN3270
{
	/// <summary>
	/// Summary description for Idle.
	/// </summary>
	internal class Idle : IDisposable
	{

		// 7 minutes
		const int IdleMilliseconds = (7 * 60 * 1000);


		Timer idleTimer = null;
		Telnet telnet;
		Random rand;

		bool idleWasIn3270 = false;
		bool randomize = false;
		bool isTicking = false;

		int milliseconds;

		internal Idle(Telnet tn)
		{
			telnet = tn;
		}

		// Initialization
		void Initialize()
		{
			// Register for state changes.
			this.telnet.Connected3270 += telnet_Connected3270;

			// Seed the random number generator (we seem to be the only user).
			this.rand = new Random();
		}

		void telnet_Connected3270(object sender, Connected3270EventArgs e)
		{
			this.IdleIn3270(e.Is3270);
		}

		/// <summary>
		/// Process a timeout value.
		/// </summary>
		/// <param name="t"></param>
		/// <returns></returns>
		int ProcessTimeoutValue(string t)
		{
			if (t == null || t.Length == 0)
			{
				this.milliseconds = IdleMilliseconds;
				this.randomize = true;
				return 0;
			}

			if (t[0] == '~')
			{
				randomize = true;
				t = t.Substring(1);
			}
			throw new ApplicationException("process_timeout_value not implemented");
		}


		/// <summary>
		/// Called when a host connects or disconnects.
		/// </summary>
		/// <param name="in3270"></param>
		void IdleIn3270(bool in3270)
		{
			if (in3270 && !this.idleWasIn3270)
			{
				this.idleWasIn3270 = true;
			}
			else
			{
				if (this.isTicking)
				{
					this.telnet.Controller.RemoveTimeOut(idleTimer);
					this.isTicking = false;
				}
				this.idleWasIn3270 = false;
			}
		}


		void TimedOut(object state)
		{
			lock (this.telnet)
			{
				this.telnet.Trace.trace_event("Idle timeout\n");
				//Console.WriteLine("PUSH MACRO ignored (BUGBUG)");
				//push_macro(idle_command, false);
				this.ResetIdleTimer();
			}
		}


		/// <summary>
		/// Reset (and re-enable) the idle timer.  Called when the user presses an AID key.
		/// </summary>
		public void ResetIdleTimer()
		{
			if (this.milliseconds != 0)
			{
				int idleMsNow;

				if (this.isTicking)
				{
					this.telnet.Controller.RemoveTimeOut(this.idleTimer);
					this.isTicking = false;
				}

				idleMsNow = this.milliseconds;

				if (randomize)
				{
					idleMsNow = this.milliseconds;
					if ((rand.Next(100) % 2) != 0)
					{
						idleMsNow += rand.Next(this.milliseconds / 10);
					}
					else
					{
						idleMsNow -= rand.Next(this.milliseconds / 10);
					}
				}

				this.telnet.Trace.trace_event("Setting idle timeout to " + idleMsNow);
				this.idleTimer = telnet.Controller.AddTimeout(idleMsNow, new System.Threading.TimerCallback(TimedOut));
				this.isTicking = true;
			}
		}



		public void Dispose()
		{
			if (this.telnet != null)
			{
				this.telnet.Connected3270 -= telnet_Connected3270;
			}

		}
	}
}
