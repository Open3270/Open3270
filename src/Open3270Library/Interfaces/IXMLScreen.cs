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
using Open3270.TN3270;
using System;
using System.IO;

namespace Open3270
{
	public class StringPosition
	{
		public int x;
		public int y;
		public string str;
		public int indexInStringArray;
	}

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
		/// Does the text on the screen contain this text.
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <returns>Returns index of string that was found in the array of strings, NOT the position on the screen</returns>
		int LookForTextStrings(string[] text);

		/// <summary>
		/// Does the text on the screen contain this text.
		/// </summary>
		/// <param name="offset"></param>
		/// <param name="length"></param>
		/// <returns>StringPoisition structure filled out for the string that was found.</returns>
		StringPosition LookForTextStrings2(string[] text);


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
