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
using System.Web;
using System.Text;
using System.Security; 
using System.Security.Permissions;
using System.IO;

namespace Open3270.Library
{
	public interface IAuditable
	{
		void DumpTo(StringBuilder message, bool admin);
	}
	public enum AuditType
	{
		Exception
	}
	/// <summary>
	/// Summary description for Audit.
	/// </summary>
	internal class Audit
	{
		static string _auditFile;
		static bool   _auditOn = false;
		private static DateTime LoadTime = DateTime.Now;
		static Audit()
		{
			_auditOn = false;
			_auditFile = null;
		}
		static public bool AuditOn 
		{
			get { return _auditOn; }
			set { _auditOn = value; }
		}
		static public string AuditFile
		{
			get { return _auditFile; }
			set { _auditFile = value; }
		}
		public static void WriteLine(string text)
		{
			if (_auditOn)
			{
				if (Audit._auditFile != null)
				{
					lock (Audit._auditFile)
					{
						try
						{
							Console.WriteLine(text);
							//
							// Demand file permission so that we work within the Internet Explorer sandbox
							//
							FileIOPermission permission = new FileIOPermission( PermissionState.Unrestricted );
							permission.AddPathList(FileIOPermissionAccess.Append, AuditFile);
							permission.Demand(); 
							//
							StreamWriter sw = File.AppendText(_auditFile);
							try
							{
								string date = DateTime.Now.ToShortDateString()+"-"+DateTime.Now.ToShortTimeString()+"::";
								sw.WriteLine(date+text);
							}
							finally
							{
								sw.Close();
							}
							permission.Deny();
						}
						catch (Exception ee)
						{
							Console.WriteLine("EXCEPTION ON AUDIT "+ee);

						}
					}
				}
				else
				{
					Console.WriteLine(text);
				}
			}
		}

        private static void WriteAuditInternal(AuditType Type, string Text)
		{
			WriteLine(Text);
		}
	}
}
