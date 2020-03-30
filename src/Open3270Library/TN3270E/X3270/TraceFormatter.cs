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
using System.Text;

namespace Open3270.TN3270
{

	internal class TraceFormatter
	{
		internal TraceFormatter()
		{
		}

		static public string Format(string fmt, params object[] args)
		{
			StringBuilder builder = new StringBuilder();
			int i = 0;
			int argindex = 0;
			while (i<fmt.Length)
			{
				if (fmt[i]=='%')
				{
					switch (fmt[i+1])
					{
						case '0':
							if (fmt.Substring(i).StartsWith("%02x"))
							{
								try
								{
									int v = System.Convert.ToInt32(""+args[argindex]);
									builder.Append(v.ToString("X2"));
								}
								catch (System.FormatException)
								{
									builder.Append("??");
								}
								catch (System.OverflowException)
								{
									builder.Append("??");
								}
								catch (System.ArgumentException)
								{
									builder.Append("??");
								}
							}
							else
								throw new ApplicationException("Format '"+fmt.Substring(i)+"' not known");
							break;
						case 'c':
							builder.Append(System.Convert.ToChar((char)args[argindex]));
							break;
						case 'f':
							builder.Append((double)args[argindex]);
							break;
						case 'd':
						case 's':
						case 'u':
							if (args[argindex]==null)
								builder.Append("(null)");
							else
								builder.Append(args[argindex].ToString());
							break;
						case 'x':
							builder.Append(String.Format("{0:x}", args[argindex]));
							break;
						case 'X':
							builder.Append(String.Format("{0:X}", args[argindex]));
							break;
						default:
							throw new ApplicationException("Format '%"+fmt[i+1]+"' not known");
					}
					i++;
					argindex++;
				}
				else
					builder.Append(""+fmt[i]);
				i++;
			}
			return builder.ToString();
		}
	}
}
