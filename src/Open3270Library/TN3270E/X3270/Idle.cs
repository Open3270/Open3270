#region License
/* 
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
