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

using System;
using System.Collections.Generic;
using System.Text;

namespace Open3270.TN3270
{
	public static class FieldAttribute
	{
		public static bool IsFA(byte c)
		{
			return (((c) & ControllerConstant.FA_MASK) == ControllerConstant.FA_BASE);
		}
		public static bool IsModified(byte c) { return (c & ControllerConstant.FA_MODIFY) != 0; }
		public static bool IsNumeric(byte c) { return (c & ControllerConstant.FA_NUMERIC) != 0; }
		public static bool IsProtected(byte c) { return (c & ControllerConstant.FA_PROTECT) != 0; }
		public static bool IsProtectedAt(byte[] buffer, int index) { return (buffer[index] & ControllerConstant.FA_PROTECT) != 0; }
		public static bool IsSkip(byte c) { return (FieldAttribute.IsNumeric(c) && FieldAttribute.IsProtected(c)); }
		public static bool IsZero(byte c) { return ((c & ControllerConstant.FA_INTENSITY) == ControllerConstant.FA_INT_ZERO_NSEL); }
		public static bool IsHigh(byte c) { return ((c & ControllerConstant.FA_INTENSITY) == ControllerConstant.FA_INT_HIGH_SEL); }
		public static bool IsNormal(byte c)
		{
			return ((c & ControllerConstant.FA_INTENSITY) == ControllerConstant.FA_INT_NORM_NSEL ||
				(c & ControllerConstant.FA_INTENSITY) == ControllerConstant.FA_INT_NORM_SEL);
		}
		public static bool IsSelectable(byte c)
		{
			return ((c & ControllerConstant.FA_INTENSITY) == ControllerConstant.FA_INT_NORM_SEL ||
				(c & ControllerConstant.FA_INTENSITY) == ControllerConstant.FA_INT_HIGH_SEL);
		}
		public static bool IsIntense(byte c) { return ((c & ControllerConstant.FA_INT_HIGH_SEL) == ControllerConstant.FA_INT_HIGH_SEL); }
	}

    public struct FieldAttributes
    {
        public bool IsModified { get; set; }
        public bool IsNumeric { get; set; }
        public bool IsProtected { get; set; }
        public bool IsSkip { get; set; }
        public bool IsZero { get; set; }
        public bool IsHigh { get; set; }
        public bool IsNormal { get; set; }
        public bool IsSelectable { get; set; }
        public bool IsIntense { get; set; }
    }
}
