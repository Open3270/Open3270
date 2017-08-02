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
using System.IO;
using System.Text;


namespace Open3270
{
	/// <summary>
	/// An error occured identifying a screen. Usually, this means that the screen didn't match
	/// any of the match rules you've defined.
	/// </summary>
	/// <remarks></remarks>
	public class TNIdentificationException : Exception
	{
		private string mPage;
		private string mDump;
		/// <summary>
		/// Identification exception
		/// </summary>
		/// <param name="page">The page we're coming from (not the page we're on!)</param>
		/// <param name="screen">The IXMLScreen object for the screen that we couldn't recognize</param>
		public TNIdentificationException(string page, IXMLScreen screen)
		{
			mPage = page;
			if (screen==null)
				mDump = null;
			else
				mDump = screen.Dump();
		}
		/// <summary>
		/// Provides a textual representation of the exception.
		/// </summary>
		/// <returns>Returns the textual representation of the exception.</returns>
		public override string ToString()
		{
			return "TNIdentificationException current screen='"+mPage+"'. Dump is \n\n"+mDump+"\n\n";
		}
	}
}