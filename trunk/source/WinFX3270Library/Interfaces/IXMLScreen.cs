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
using Open3270.TN3270;
using System;
using System.IO;

namespace Open3270
{
	/// <summary>
	/// An interface to a 3270 Screen object. Allows you to manually manipulate a screen.
	/// </summary>
	public interface IXMLScreen
	{
		/// <summary>
		/// Returns the name of the screen as identified from the XML Connection file
		/// </summary>
		/// <value>The name of the screen</value>
		string Name { get; }
		/// <summary>
		/// Returns a formatted text string representing the screen image
		/// </summary>
		/// <returns>The textual representation of the screen</returns>
		string Dump();
		/// <summary>
		/// Streams the screen out to a TextWriter file
		/// </summary>
		/// <param name="stream">An open stream to write the screen image to</param>
		void Dump(IAudit stream);

		/// <summary>
		/// Get text at a specified location from the screen
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		string GetText(int x, int y, int length);

		/// <summary>
		/// Get text at a specified 3270 offset on the screen
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		string GetText(int offset, int length);

		/// <summary>
		/// Get an entire row from the screen
		/// </summary>
		/// <param name="row"></param>
		/// <returns></returns>
		string GetRow(int row);

		/// <summary>
		/// Get a character from the screen
		/// </summary>
		/// <param name="offset"></param>
		/// <returns></returns>
		char GetCharAt(int offset);

		/// <summary>
		/// Width of screen in characters
		/// </summary>
		int CX { get; }


		/// <summary>
		/// Height of screen in characters
		/// </summary>
		int CY { get; }

		/// <summary>
		/// Returns this screen as an XML text string
		/// </summary>
		/// <param name="useCachedValue">False to refresh the cached screen image into XML - 
		/// This should never be needed unless you manually update the IXMLScreen object. Default is true.
		/// </param>
		/// <returns>XML Text for screen</returns>
		string GetXMLText(bool RefreshCachedValue);

		/// <summary>
		/// Returns this screen as an XML text string. Always use the cached value (preferred option).
		/// </summary>
		/// <returns>XML Text for screen</returns>
		string GetXMLText();

        string[] GetUnformatedStrings();

		/// <summary>
		/// Returns a unique id for the screen so you can tell whether it's changed since you last
		/// looked - doesn't necessarily mean the content has changed, just that we think it might
		/// have
		/// </summary>
		Guid ScreenGuid { get; }

		XMLScreenField[] Fields { get; }
	}
}
