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

namespace Open3270.TN3270
{
	/// <summary>
	/// Summary description for Idle.
	/// </summary>
	internal class Idle
	{
		System.Threading.Timer idle_id = null;
		Telnet telnet;

		bool idle_was_in3270 = false;
		//bool idle_enabled = false;
		//string idle_command= null;
		//string idle_timeout_string = null;
		int idle_ms;
		bool idle_randomize = false;
		bool idle_ticking = false;

		internal Idle(Telnet tn)
		{
			telnet = tn;
		}
		/*
		 *	idle.c
		 *		This module handles the idle command.
		 */


		/* Macros. */
		const int IDLE_MS		= (7 * 60 * 1000);	/* 7 minutes */


		//#define BN	(bool *)NULL


		Random rand;

		/* Initialization. */
		void idle_init()
		{
			/* Register for state changes. */
			telnet.register_schange(STCALLBACK.ST_3270_MODE, new SChangeDelegate(idle_in3270));

			/* Seed the random number generator (we seem to be the only user). */
			rand = new Random();
		}

		/* Process a timeout value. */
		int  process_timeout_value(string t)
		{
			//int idle_n;

			if (t==null || t.Length==0) 
			{
				idle_ms = IDLE_MS;
				idle_randomize = true;
				return 0;
			}

			if (t[0] == '~') 
			{
				idle_randomize = true;
				t = t.Substring(1);
			}
			throw new ApplicationException("process_timeout_value not implemented");
		}

		/* Called when a host connects or disconnects. */
		void idle_in3270(bool in3270)
		{
			if (in3270 && !idle_was_in3270) 
			{
				idle_was_in3270 = true;
			} 
			else 
			{
				if (idle_ticking) 
				{
					telnet.tnctlr.RemoveTimeOut(idle_id);
					idle_ticking = false;
				}
				idle_was_in3270 = false;
			}
		}

		/*
		 * Idle timeout.
		 */
		void idle_timeout(object state)
		{
			lock (telnet)
			{
				telnet.trace.trace_event("Idle timeout\n");
				//Console.WriteLine("PUSH MACRO ignored (BUGBUG)");
				//push_macro(idle_command, false);
				reset_idle_timer();
			}
		}

		/*
		 * Reset (and re-enable) the idle timer.  Called when the user presses an AID
		 * key.
		 */
		public void reset_idle_timer()
		{
			if (idle_ms!=0) 
			{
				int idle_ms_now;

				if (idle_ticking) 
				{
					telnet.tnctlr.RemoveTimeOut(idle_id);
					idle_ticking = false;
				}
				idle_ms_now = idle_ms;
				if (idle_randomize) 
				{
					idle_ms_now = idle_ms;
					if ((rand.Next(100) % 2)!=0)
						idle_ms_now += rand.Next(idle_ms / 10);
					else
						idle_ms_now -= rand.Next(idle_ms / 10);
				}
				telnet.trace.trace_event("Setting idle timeout to "+idle_ms_now);
				idle_id = telnet.tnctlr.AddTimeout(idle_ms_now, new System.Threading.TimerCallback(idle_timeout));
				idle_ticking = true;
			}
		}


	}
}
