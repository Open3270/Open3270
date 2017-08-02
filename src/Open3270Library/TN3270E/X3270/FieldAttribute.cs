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
