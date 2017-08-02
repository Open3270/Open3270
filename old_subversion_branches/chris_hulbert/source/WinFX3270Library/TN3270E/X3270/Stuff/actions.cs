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
using System.Collections;

namespace Open3270.TN3270
{
	internal delegate bool ActionDelegate(params object[] args);

	internal enum iaction 
	{
		IA_STRING, IA_PASTE, IA_REDRAW,
		IA_KEYPAD, IA_DEFAULT, IA_KEY,
		IA_MACRO, IA_SCRIPT, IA_PEEK,
		IA_TYPEAHEAD, IA_FT, IA_COMMAND, IA_KEYMAP
	};

	/// <summary>
	/// Summary description for actions.
	/// </summary>
	internal class Actions
	{
		internal class XtActionRec
		{
			public ActionDelegate proc;
			public string name;
			public bool CausesSubmit;
			public XtActionRec(string name, bool CausesSubmit, ActionDelegate fn)
			{
				this.CausesSubmit = CausesSubmit;
				this.proc = fn;
				this.name = name.ToLower();
			}
		}

		XtActionRec[] _actions = null;
		int actioncount;
		Telnet telnet;
		internal Actions(Telnet tn)
		{

			telnet = tn;
			_actions = new XtActionRec[] {
					new XtActionRec( "printtext",	false,	new ActionDelegate(telnet.print.PrintText_action )),
					new XtActionRec( "flip",		false,	new ActionDelegate(telnet.keyboard.Flip_action )),
					new XtActionRec( "ascii",		false,	new ActionDelegate(telnet.tnctlr.Ascii_action )),
					new XtActionRec( "dumpxml",		false,	new ActionDelegate(telnet.tnctlr.DumpXML_action )),
					new XtActionRec( "asciifield",	false,	new ActionDelegate(telnet.tnctlr.AsciiField_action )),
					new XtActionRec( "attn",		true,	new ActionDelegate(telnet.keyboard.Attn_action )),
					new XtActionRec( "backspace",	false,	new ActionDelegate(telnet.keyboard.BackSpace_action )),
					new XtActionRec( "backtab",		false,	new ActionDelegate(telnet.keyboard.BackTab_action )),
					new XtActionRec( "circumnot",	false,	new ActionDelegate(telnet.keyboard.CircumNot_action )),
					new XtActionRec( "clear",		true,	new ActionDelegate(telnet.keyboard.Clear_action )),
					new XtActionRec( "cursorselect", false,	new ActionDelegate(telnet.keyboard.CursorSelect_action )),
					new XtActionRec( "delete", 		 false,	new ActionDelegate(telnet.keyboard.Delete_action )),
					new XtActionRec( "deletefield",	 false,	new ActionDelegate(telnet.keyboard.DeleteField_action )),
					new XtActionRec( "deleteword",	 false, new ActionDelegate(telnet.keyboard.DeleteWord_action )),
					new XtActionRec( "down",		 false, new ActionDelegate(telnet.keyboard.Down_action )),
					new XtActionRec( "dup",			 false, new ActionDelegate(telnet.keyboard.Dup_action )),
					new XtActionRec("emulateinput",  true,	new ActionDelegate(telnet.keyboard.EmulateInput_action )),
					new XtActionRec( "enter",		 true,	new ActionDelegate(telnet.keyboard.Enter_action )),
					new XtActionRec( "erase",		 false, new ActionDelegate(telnet.keyboard.Erase_action )),
					new XtActionRec( "eraseeof",	 false, new ActionDelegate(telnet.keyboard.EraseEOF_action )),
					new XtActionRec( "eraseinput",	 false, new ActionDelegate(telnet.keyboard.EraseInput_action )),
					new XtActionRec( "fieldend",	false,	new ActionDelegate(telnet.keyboard.FieldEnd_action )),
					new XtActionRec( "fields",		false,	new ActionDelegate(telnet.keyboard.Fields_action )),
					new XtActionRec( "fieldget",	false,	new ActionDelegate(telnet.keyboard.FieldGet_action )),
					new XtActionRec( "fieldset",	false,	new ActionDelegate(telnet.keyboard.FieldSet_action )),
					new XtActionRec( "fieldmark",	false,	new ActionDelegate(telnet.keyboard.FieldMark_action )),
					new XtActionRec( "fieldexit",	false,	new ActionDelegate(telnet.keyboard.FieldExit_action )),
					new XtActionRec( "hexString",	false,	new ActionDelegate(telnet.keyboard.HexString_action)),
					new XtActionRec( "home",		false,  new ActionDelegate(telnet.keyboard.Home_action )),
					new XtActionRec( "insert",		false,  new ActionDelegate(telnet.keyboard.Insert_action )),
					new XtActionRec( "interrupt",	true, 	new ActionDelegate(telnet.keyboard.Interrupt_action )),
					new XtActionRec( "key",			false,  new ActionDelegate(telnet.keyboard.Key_action )),
					new XtActionRec( "left",		false,  new ActionDelegate(telnet.keyboard.Left_action )),
					new XtActionRec( "left2", 		false,  new ActionDelegate(telnet.keyboard.Left2_action )),
					new XtActionRec( "monocase",	false, 	new ActionDelegate(telnet.keyboard.MonoCase_action )),
					new XtActionRec( "movecursor",	false,	new ActionDelegate(telnet.keyboard.MoveCursor_action )),
					new XtActionRec( "Newline",		false,	new ActionDelegate(telnet.keyboard.Newline_action )),
					new XtActionRec( "NextWord",	false,	new ActionDelegate(telnet.keyboard.NextWord_action )),
					new XtActionRec( "PA",			true,   new ActionDelegate(telnet.keyboard.PA_action )),
					new XtActionRec( "PF",			true,   new ActionDelegate(telnet.keyboard.PF_action )),
					new XtActionRec( "PreviousWord",false,	new ActionDelegate(telnet.keyboard.PreviousWord_action )),
					new XtActionRec( "Reset",		true,  new ActionDelegate(telnet.keyboard.Reset_action )),
					new XtActionRec( "Right",		false,	new ActionDelegate(telnet.keyboard.Right_action )),
					new XtActionRec( "Right2",		false,	new ActionDelegate(telnet.keyboard.Right2_action )),
					new XtActionRec( "String",		true,	new ActionDelegate(telnet.keyboard.String_action )),
					new XtActionRec( "SysReq",		true,	new ActionDelegate(telnet.keyboard.SysReq_action )),
					new XtActionRec( "Tab",			false,  new ActionDelegate(telnet.keyboard.Tab_action )),
					new XtActionRec( "ToggleInsert", false,	new ActionDelegate(telnet.keyboard.ToggleInsert_action )),
					new XtActionRec( "ToggleReverse",false,	new ActionDelegate(telnet.keyboard.ToggleReverse_action )),
					new XtActionRec( "Up",			false,	new ActionDelegate(telnet.keyboard.Up_action )),
				};

			actioncount = _actions.Length;
		}

		//public iaction ia_cause;

		public string[] ia_name = new string[] {
												   "String", "Paste", "Screen redraw", "Keypad", "Default", "Key",
												   "Macro", "Script", "Peek", "Typeahead", "File transfer", "Command",
												   "Keymap"
											   };

		/*
		 * Return a name for an action.
		 */
		string action_name(ActionDelegate action)
		{
			int i;

			for (i = 0; i < actioncount; i++)
			{
				if (_actions[i].proc == action)
					return _actions[i].name;
			}
			return "(unknown)";
		}


		/*
		 * Wrapper for calling an action internally.
		 */
		public bool action_internal(ActionDelegate action, params object[] args)
		{
			return action(args);
		}
		Hashtable actionLookup = new Hashtable();
		ArrayList datacapture = null;
		ArrayList datastringcapture = null;
		public void action_output(string data)
		{
			action_output(data, false);
		}
		private string encodeXML(string data)
		{
			//data = data.Replace("\"", "&quot;");
			//data = data.Replace(">", "&gt;");
			data = data.Replace("<", "&lt;");
			data = data.Replace("&", "&amp;");
			return data;
		}
		public void action_output(string data, bool encode)
		{
			if (datacapture==null)
				datacapture = new ArrayList();
			if (datastringcapture==null)
				datastringcapture = new ArrayList();

			datacapture.Add(System.Text.Encoding.ASCII.GetBytes(data));
			//
			if (encode)
			{
				data = encodeXML(data);
			}
			//
			datastringcapture.Add(data);
		}
		public void action_output(byte[] data, int length)
		{
			action_output(data,length,false);
		}
		public void action_output(byte[] data, int length, bool encode)
		{
			if (datacapture==null)
				datacapture = new ArrayList();
			if (datastringcapture==null)
				datastringcapture = new ArrayList();

			//
			byte[] temp = new byte[length];
			int i;
			for (i=0; i<length; i++)
			{
				temp[i] = data[i];
			}
			datacapture.Add(temp);
			string strdata = System.Text.Encoding.ASCII.GetString(temp);
			if (encode)
			{
				strdata = encodeXML(strdata);
			}

			datastringcapture.Add(strdata);
		}
		public string GetStringData(int index)
		{
			if (datastringcapture ==null)
				return null;
			if (index>=0 && index < datastringcapture.Count)
				return (string)datastringcapture[index];
			else
				return null;
		}
		public byte[] GetByteData(int index)
		{
			if (datacapture ==null)
				return null;
			if (index>=0 && index < datacapture.Count)
				return (byte[])datacapture[index];
			else
				return null;
		}
		public bool KeyboardCommandCausesSubmit(string name, params object[] args)
		{
			XtActionRec rec = actionLookup[name.ToLower()] as XtActionRec;
			if (rec != null)
			{
				return rec.CausesSubmit;
			}
			int i;
			for (i=0; i<_actions.Length; i++)
			{
				if (_actions[i].name.ToLower()==name.ToLower())
				{
					actionLookup[name.ToLower()] = _actions[i];
					return _actions[i].CausesSubmit;
				}
			}
			throw new ApplicationException("Sorry, action '"+name+"' is not known");
		}
		public bool Execute(bool submit, string name, params object[] args)
		{
			this.telnet.events.Clear();
			// Check that we're connected
			if (!telnet.CONNECTED) 
			{
				throw new Open3270.TNHostException("TN3270 Host is not connected", telnet.DisconnectReason, null);
			}

			datacapture = null;
			datastringcapture = null;
			XtActionRec rec = actionLookup[name.ToLower()] as XtActionRec;
			if (rec != null)
			{
				return rec.proc(args);
			}
			int i;
			for (i=0; i<_actions.Length; i++)
			{
				if (_actions[i].name.ToLower()==name.ToLower())
				{
					actionLookup[name.ToLower()] = _actions[i];
					return _actions[i].proc(args);
				}
			}
			throw new ApplicationException("Sorry, action '"+name+"' is not known");

		}

	}
}
