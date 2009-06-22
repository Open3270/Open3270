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
