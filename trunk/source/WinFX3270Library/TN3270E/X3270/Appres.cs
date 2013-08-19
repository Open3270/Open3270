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

	internal class Appres
	{
		enum ToggleType
		{
			Initial,
			Interactive,
			Action,
			Final
		}

		internal class Toggle
		{
			public bool toggleValue;

			// Has the value changed since init 
			public bool changed;
			public string[] labels;

			internal Toggle()
			{
				toggleValue = false;
				changed = false;
				labels = new string[2];
				labels[0] = null;
				labels[1] = null;
			}
		}


		public const int MonoCase = 0;
		public const int AltCursor = 1;
		public const int CursorBlink = 2;
		public const int ShowTiming = 3;
		public const int CursorPos = 4;
		public const int DSTrace = 5;
		public const int ScrollBar = 6;
		public const int LINE_WRAP = 7;
		public const int BlankFill = 8;
		public const int ScreenTrace = 9;
		public const int EventTrace = 10;
		public const int MarginedPaste = 11;
		public const int RectangleSelect = 12;
		private const int NToggles = 14;


		Toggle[] toggles;
		public Appres()
		{
			toggles = new Toggle[NToggles];
			int i;
			for (i = 0; i < NToggles; i++)
			{
				toggles[i] = new Toggle();
			}
			// Set defaults
			this.mono = false;
			this.extended = true;
			this.m3279 = false;
			this.modified_sel = false;
			this.apl_mode = false;
			this.scripted = true;
			this.numeric_lock = false;
			this.secure = false;
			//this.oerr_lock = false;
			this.typeahead = true;
			this.debug_tracing = true;

			//this.model = "4";
			this.hostsfile = null;
			this.port = "telnet";
			this.charset = "bracket";
			this.termname = null;
			this.macros = null;
			this.trace_dir = "/tmp";
			this.oversize = null;

			this.icrnl = true;
			this.inlcr = false;
			this.onlcr = true;
			this.erase = "^H";
			this.kill = "^U";
			this.werase = "^W";
			this.rprnt = "^R";
			this.lnext = "^V";
			this.intr = "^C";
			this.quit = "^\\";
			this.eof = "^D";

		}

		public bool Toggled(int ix)
		{
			return toggles[ix].toggleValue;
		}
		public void ToggleTheValue(Toggle t)
		{
			t.toggleValue = !t.toggleValue;
			t.changed = true;
		}
		public void ToggleTheValue(int ix)
		{
			toggles[ix].toggleValue = !toggles[ix].toggleValue;
			toggles[ix].changed = true;
		}
		public void SetToggle(int ix, bool v)
		{
			toggles[ix].toggleValue = v;
			toggles[ix].changed = true;

		}

		public bool ToggleAction(params object[] args)
		{
			throw new ApplicationException("toggle_action not implemented");
		}

		/* Application resources */
		/* Options (not toggles) */
		public bool mono;
		public bool extended;
		public bool m3279;
		public bool modified_sel;
		//public bool	once;
		public bool apl_mode;
		public bool scripted;
		public bool numeric_lock;
		public bool secure;
		//public bool oerr_lock;
		public bool typeahead;
		public bool debug_tracing;
		public bool disconnect_clear = false;
		//public bool highlight_bold;
		public bool color8 = false;

		/* Named resources */
		//public string conf_dir;
		//public string model;
		public string hostsfile;
		public string port;
		public string charset;
		public string termname;
		public string macros;
		public string trace_dir;
		//public string trace_file;
		//public string screentrace_file;
		//public string trace_file_size;
		public string oversize;
		//public string connectfile_name;
		//public string idle_command;
		//public bool idle_command_enabled;
		//public string idle_timeout;



		/* Line-mode TTY parameters */
		public bool icrnl;
		public bool inlcr;
		public bool onlcr;
		public string erase;
		public string kill;
		public string werase;
		public string rprnt;
		public string lnext;
		public string intr;
		public string quit;
		public string eof;

	}
}
