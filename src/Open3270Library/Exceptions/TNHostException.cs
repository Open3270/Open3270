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

namespace Open3270
{
	/// <summary>
	/// An object to return an exception from the 3270 host.
	/// </summary>
	public class TNHostException : Exception
	{
		private string mMessage = null;
		private string mAuditLog = null;
		private string mReason   = null;
		/// <summary>
		/// Constructor - used internally.
		/// </summary>
		/// <param name="message">The message text</param>
		/// <param name="auditlog">The audit log up to this exception</param>
		public TNHostException(string message, string reason, string auditlog)
		{
			mReason  = reason;
			mMessage = message;
			mAuditLog = auditlog;
		}
		/// <summary>
		/// Returns the audit log from the start to this exception. Useful for tracing an exception
		/// </summary>
		/// <value>The formatted audit log</value>
		public string AuditLog
		{
			get { return mAuditLog;  }
			set { mAuditLog = value; }
		}
		/// <summary>
		/// Returns a textual version of the error
		/// </summary>
		/// <returns>The error text.</returns>
		public override string ToString()
		{
			return "HostException '"+mMessage+"' "+Reason;
		}
		public override string Message
		{
			get
			{
				return mMessage;
			}
		}


		public string Reason
		{
			get { return mReason; }
			set { mReason = value; }
		}
	}

}
