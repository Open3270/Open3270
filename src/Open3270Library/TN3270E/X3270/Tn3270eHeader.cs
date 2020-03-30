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

namespace Open3270.TN3270
{

	internal class TnHeader
	{

		#region Constants

		public const int EhSize = 5;

		// Header request flags
		public const byte RqfErrorConditionCleared = 0x00;

		#region Constant Categories (As nested classes)

		public static class HeaderReponseData
		{
			public const byte PosDeviceEnd = 0x00;
			public const byte NegCommandReject = 0x00;
			public const byte NegInterventionRequired = 0x01;
			public const byte NegOperationCheck = 0x02;
			public const byte NegComponentDisconnected = 0x03;
		}

		public static class HeaderReponseFlags
		{
			public const byte NoResponse = 0x00;
			public const byte ErrorResponse = 0x01;
			public const byte AlwaysResponse = 0x02;
			public const byte PositiveResponse = 0x00;
			public const byte NegativeResponse = 0x01;
		}

		public static class Ops
		{
			public const int Associate = 0;
			public const int Connect = 1;
			public const int DeviceType = 2;
			public const int Functions = 3;
			public const int Is = 4;
			public const int Reason = 5;
			public const int Reject = 6;
			public const int Request = 7;
			public const int Send = 8;
		}

		public static class NegotiationReasonCodes
		{
			public const int ConnPartner = 0;
			public const int DeviceInUse = 1;
			public const int InvAssociate = 2;
			public const int InvDeviceName = 3;
			public const int InvDeviceType = 4;
			public const int TypeNameError = 5;
			public const int UnknownError = 6;
			public const int UnsupportedReq = 7;
		}

		#endregion Constant Categories (As nested classes)

		#endregion Constants



		#region Fields

		private DataType3270 dataType;
		private byte requestFlag;
		private byte responseFlag;
		private byte[] sequenceNumber = new byte[2]; /* actually, 16 bits, unaligned (!) */

		#endregion Fields



		#region Properties

		public DataType3270 DataType
		{
			get { return dataType; }
			set { dataType = value; }
		}

		public byte RequestFlag
		{
			get { return requestFlag; }
			set { requestFlag = value; }
		}
		
		public byte ResponseFlag
		{
			get { return responseFlag; }
			set { responseFlag = value; }
		}

		public byte[] SequenceNumber
		{
			get { return sequenceNumber; }
			private set { sequenceNumber = value; }
		}

		#endregion Properties



		#region Constructors and disposal

		public TnHeader()
		{
		}

		public TnHeader(byte[] buf)
		{
			switch (buf[0])
			{
				case 0: dataType = DataType3270.Data3270; break;
				case 1: dataType = DataType3270.DataScs; break;
				case 2: dataType = DataType3270.Response; break;
				case 3: dataType = DataType3270.BindImage; break;
				case 4: dataType = DataType3270.Unbind; break;
				case 5: dataType = DataType3270.NvtData; break;
				case 6: dataType = DataType3270.Request; break;
				case 7: dataType = DataType3270.SscpLuData; break;
				case 8: dataType = DataType3270.PrintEoj; break;
				default:
					throw new ApplicationException("data_type =" + buf[0] + " not known");
			}
			requestFlag = buf[1];
			responseFlag = buf[2];
			sequenceNumber[0] = buf[3];
			sequenceNumber[1] = buf[4];
		}

		#endregion Constructors and disposal



		#region Public Methods

		private byte ByteFromDataType()
		{
			byte ch = 0;
			switch (dataType)
			{
				case DataType3270.Data3270: ch = 0; break;
				case DataType3270.DataScs: ch = 1; break;
				case DataType3270.Response: ch = 2; break;
				case DataType3270.BindImage: ch = 3; break;
				case DataType3270.Unbind: ch = 4; break;
				case DataType3270.NvtData: ch = 5; break;
				case DataType3270.Request: ch = 6; break;
				case DataType3270.SscpLuData: ch = 7; break;
				case DataType3270.PrintEoj: ch = 8; break;
				default:
					throw new ApplicationException("data_type =" + dataType + " not known");
			}
			return ch;
		}


		public void OnToByte(byte[] buf)
		{
			buf[0] = ByteFromDataType();
			buf[1] = requestFlag;
			buf[2] = responseFlag;
			buf[3] = sequenceNumber[0];
			buf[4] = sequenceNumber[1];

		}


		private void AddWithDoubledIAC(NetBuffer buffer, byte character)
		{
			buffer.Add(character);
			if (character == 255) // IAC
			{
				buffer.Add(character);
			}
		}


		public void AddToNetBuffer(NetBuffer buffer)
		{
			this.AddWithDoubledIAC(buffer, ByteFromDataType());
			this.AddWithDoubledIAC(buffer, requestFlag);
			this.AddWithDoubledIAC(buffer, responseFlag);
			this.AddWithDoubledIAC(buffer, sequenceNumber[0]);
			this.AddWithDoubledIAC(buffer, sequenceNumber[1]);
		}

		#endregion Public Methods

	}
}


