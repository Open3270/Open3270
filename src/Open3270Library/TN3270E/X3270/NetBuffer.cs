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
using System.Collections;

namespace Open3270.TN3270
{
	/// <summary>
	/// Summary description for NetBuffer.
	/// </summary>
	internal class NetBuffer
	{
		ArrayList bytebuffer;
		internal NetBuffer()
		{
			bytebuffer = new ArrayList();
		}
		internal NetBuffer(byte[] data, int start, int len)
		{
			int i;
			bytebuffer = new ArrayList();
			for (i=0; i<len; i++)
			{
				bytebuffer.Add(data[i+start]);
			}
		}
		public byte[] Data
		{
			get
			{
				return (byte[])bytebuffer.ToArray(typeof(byte));
			}
		}
		public NetBuffer CopyFrom(int start, int len)
		{
			NetBuffer temp = new NetBuffer();
			int i;
			for (i=0; i<len; i++)
			{
				temp.Add((byte)this.bytebuffer[i+start]);
			}
			return temp;
		}
		public string AsString(int start, int len)
		{
			string temp = "";
			int i;
			for (i=0; i<len; i++)
			{
				temp+=System.Convert.ToChar((byte)bytebuffer[i+start]);
			}
			return temp;
		}
		public void Add(byte b)
		{
			bytebuffer.Add(b);
		}
		public void Add(string b)
		{
			int i;
			for (i=0; i<b.Length; i++)
			{
				Add(b[i]);
			}
		}
		public void Add(int b)
		{
			Add((byte)b);
		}
		public void Add(char b)
		{
			Add((byte)b);
		}
		public void IncrementAt(int index, int increment)
		{
			byte v = (byte)bytebuffer[index];
			v = (byte)(v+increment);
			bytebuffer[index] = v;
		}
		public void Add16At(int index, int v16bit)
		{
			bytebuffer[index] = (byte)((v16bit&0xFF00)>>8);
			bytebuffer[index+1] =(byte)(v16bit&0x00FF); 
		}
		public void Add16(int v16bit)
		{
			Add((v16bit&0xFF00)>>8);
			Add(v16bit&0x00FF);
		}
		public void Add32(int v32bit)
		{
			Add((byte)((v32bit&0xFF000000)>>24));
			Add((v32bit&0x00FF0000)>>16);
			Add((v32bit&0x0000FF00)>>8);
			Add(v32bit& 0x000000FF);
		}
		public int Index
		{
			get { return bytebuffer.Count;}
		}
		//
		/*
		 * store3270in
		 *	Store a character in the 3270 input buffer, checking for buffer
		 *	overflow and reallocating ibuf if necessary.
		 */
		void store3270in(byte c)
		{
throw new ApplicationException("oops");
			/*
			if (ibptr - ibuf >= ibuf_size) 
			{
				ibuf_size += BUFSIZ;
				throw new ApplicationException("BUGBUG");
				//ibuf = (unsigned char *)Realloc((char *)ibuf, ibuf_size);
				ibptr = ibuf + ibuf_size - BUFSIZ;
			}
			*ibptr++ = c;
			*/
		}

		/*
		 * space3270out
		 *	Ensure that <n> more characters will fit in the 3270 output buffer.
		 *	Allocates the buffer in BUFSIZ chunks.
		 *	Allocates hidden space at the front of the buffer for TN3270E.
		 */
		void space3270out(int n)
		{
			throw new ApplicationException("oops");
			/*
			unsigned nc = 0;	// amount of data currently in obuf 
			unsigned more = 0;

			if (obuf_size)
				nc = obptr - obuf;

			while ((nc + n + EH_SIZE) > (obuf_size + more)) 
			{
				more += BUFSIZ;
			}

			if (more) 
			{
				obuf_size += more;
				throw new ApplicationException("BUGBUG");
				//obuf_base = (unsigned char *)Realloc((char *)obuf_base,
				//								 obuf_size);
				obuf = obuf_base + EH_SIZE;
				obptr = obuf + nc;
			}
			*/
		}
	}
}
