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
using System.Threading;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Serialization;

namespace Open3270.Library
{
	/// <summary>
	/// Internal - message base class
	/// </summary>
	[Serializable]
	[XmlInclude(typeof(HtmlMessage))]
	internal class Message
	{
		/// <summary>
		/// Internal - message type
		/// </summary>
		public string MessageType;
		

		public void Send(Socket socket)
		{
			// SOAP Serialization
			//Thread.CurrentThread.CurrentCulture = new CultureInfo("en-gb");
			//Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

			
			//SoapFormatter soap = new SoapFormatter();
			XmlSerializer soap = new XmlSerializer(typeof(Message));
			MemoryStream ms = new MemoryStream();
			soap.Serialize(ms, this);
			byte[] bMessage = ms.GetBuffer();
			ms.Close();
			//
			MessageHeader header = new MessageHeader();
			header.uMessageSize = (int)bMessage.Length;
			//
			//
			socket.Send(header.ToByte(), 0, MessageHeader.MessageHeaderSize, SocketFlags.None);
			socket.Send(bMessage, 0, bMessage.Length, SocketFlags.None);
			
		}
		public static Message CreateFromByteArray(byte[] data)
		{
			//Thread.CurrentThread.CurrentCulture = new CultureInfo("en-gb");
			//Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;
			
			//
			// SOAP Serializer
			Message msg = null;
			try
			{
				//SoapFormatter soap = new SoapFormatter();
				XmlSerializer soap = new XmlSerializer(typeof(Message));
				MemoryStream ms = new MemoryStream(data);
				object dso = soap.Deserialize(ms);
				
				try
				{
					msg = (Message)dso;
				}
				catch (Exception ef)
				{
					Audit.WriteLine("type="+dso.GetType()+" cast to Message threw exception "+ef);
					return null;
				}
				ms.Close();
			}
			catch (Exception ee)
			{
				Audit.WriteLine("Message serialization failed, error="+ee.ToString());
				return null;
			}
			Audit.WriteLine("Message type= "+msg.GetType());
			return msg;
		}
	}
	[Serializable]
	internal class HtmlMessage : Message
	{
		/// <summary>
		/// Internal - message type
		/// </summary>
		public byte[] Bytes;
		public string GetText()
		{
			if (Bytes==null)
				return null;
			return System.Text.Encoding.UTF8.GetString(Bytes);
		}
		public HtmlMessage()
		{
		}
		public HtmlMessage(string text)
		{
			this.Bytes = System.Text.Encoding.UTF8.GetBytes(text);
			this.MessageType = "Html";
		}
	}

	//
	internal class MessageHeader 
	{
		public int uMagicNumber; 
		public int uVersion;
		public int uMessageSize; 

		public const int ConstantForMagicNumber = 0x0FCDEEDC;
		public const int MessageHeaderSize = 12;

		public MessageHeader()
		{
			uMagicNumber = ConstantForMagicNumber;
			uVersion     = 1;
			uMessageSize = 0;
		}

		public MessageHeader(byte[] data)
		{
			if (data.Length != MessageHeaderSize) throw new ApplicationException("INTERNAL ERROR - MessageHeader constructor passed buffer of invalid length");
			int offset = 0;
			offset = ByteHandler.FromBytes(data, offset, out uMagicNumber);
			offset = ByteHandler.FromBytes(data, offset, out uVersion);
			offset = ByteHandler.FromBytes(data, offset, out uMessageSize);

			if (offset != 12) throw new ApplicationException("FATAL INTERNAL ERROR - MessageHeader is not 12 bytes long");
			if (uMagicNumber != ConstantForMagicNumber) throw new ApplicationException("FATAL COMMUNICATIONS ERROR - MessageHeader Magic number is invalid");
		}
		

		public byte[] ToByte()
		{
			byte[] result = new byte[MessageHeaderSize];
			int offset = 0;

			offset = ByteHandler.ToBytes(result, offset, uMagicNumber);
			offset = ByteHandler.ToBytes(result, offset, uVersion);
			offset = ByteHandler.ToBytes(result, offset, uMessageSize);
			return result;
		}
	}
}
