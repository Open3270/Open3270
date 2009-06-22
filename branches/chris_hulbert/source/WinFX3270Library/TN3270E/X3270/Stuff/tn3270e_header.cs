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

namespace Open3270.TN3270
{


	internal enum TN3270E_DT
	{
		TN3270_DATA,//		0x00
	SCS_DATA,//		0x01
	RESPONSE,//		0x02
	BIND_IMAGE,//		0x03
	UNBIND,//		0x04
	NVT_DATA,//		0x05
	REQUEST,//		0x06
	SSCP_LU_DATA,//		0x07
	PRINT_EOJ//		0x08
}




	/// <summary>
	/// Summary description for tn3270e_header.
	/// </summary>
	internal class TN3270E_HEADER
	{
		public static int EH_SIZE = 5;
		public TN3270E_DT data_type;
		public byte request_flag;
		public byte response_flag;
		public byte[] seq_number = new byte[2]; /* actually, 16 bits, unaligned (!) */

		/* Header response data. */
		public static byte TN3270E_POS_DEVICE_END		=0x00;
		public static byte TN3270E_NEG_COMMAND_REJECT	=0x00;
		public static byte TN3270E_NEG_INTERVENTION_REQUIRED =0x01;
		public static byte TN3270E_NEG_OPERATION_CHECK	=0x02;
		public static byte TN3270E_NEG_COMPONENT_DISCONNECTED =0x03;

		/* Header response flags. */
		public static byte TN3270E_RSF_NO_RESPONSE		=0x00;
		public static byte TN3270E_RSF_ERROR_RESPONSE	=0x01;
		public static byte TN3270E_RSF_ALWAYS_RESPONSE	=0x02;
		public static byte TN3270E_RSF_POSITIVE_RESPONSE	=0x00;
		public static byte TN3270E_RSF_NEGATIVE_RESPONSE	=0x01;


		public const int TN3270E_OP_ASSOCIATE		= 0;
		public const int TN3270E_OP_CONNECT			= 1;
		public const int TN3270E_OP_DEVICE_TYPE		= 2;
		public const int TN3270E_OP_FUNCTIONS		= 3;
		public const int TN3270E_OP_IS				= 4;
		public const int TN3270E_OP_REASON			= 5;
		public const int TN3270E_OP_REJECT			= 6;
		public const int TN3270E_OP_REQUEST			= 7;
		public const int TN3270E_OP_SEND			= 8;

		/* Negotiation reason-codes. */
		public const int TN3270E_REASON_CONN_PARTNER	= 0;
		public const int TN3270E_REASON_DEVICE_IN_USE	= 1;
		public const int TN3270E_REASON_INV_ASSOCIATE	= 2;
		public const int TN3270E_REASON_INV_DEVICE_NAME	= 3;
		public const int TN3270E_REASON_INV_DEVICE_TYPE	= 4;
		public const int TN3270E_REASON_TYPE_NAME_ERROR	= 5;
		public const int TN3270E_REASON_UNKNOWN_ERROR	= 6;
		public const int TN3270E_REASON_UNSUPPORTED_REQ	= 7;

		/* Header request flags. */
		public const byte TN3270E_RQF_ERR_COND_CLEARED	=0x00;


		public TN3270E_HEADER()
		{
		}
		private byte byteFromDataType()
		{
			byte ch = 0;
			switch (data_type)
			{
				case TN3270E_DT.TN3270_DATA: ch = 0; break;
				case TN3270E_DT.SCS_DATA: ch = 1; break;
				case TN3270E_DT.RESPONSE: ch = 2; break;
				case TN3270E_DT.BIND_IMAGE: ch = 3; break;
				case TN3270E_DT.UNBIND: ch = 4; break;
				case TN3270E_DT.NVT_DATA: ch = 5; break;
				case TN3270E_DT.REQUEST: ch = 6; break;
				case TN3270E_DT.SSCP_LU_DATA: ch = 7; break;
				case TN3270E_DT.PRINT_EOJ: ch = 8; break;
				default:
					throw new ApplicationException("data_type ="+data_type+" not known");
			}
			return ch;
		}
		public void OnToByte(byte[] buf)
		{
			buf[0] = byteFromDataType();
			buf[1] = request_flag;
			buf[2] = response_flag;
			buf[3] = seq_number[0];
			buf[4] = seq_number[1];
			
		}
		public TN3270E_HEADER(byte[] buf)
		{
			switch (buf[0])
			{
				case 0: data_type = TN3270E_DT.TN3270_DATA; break;
				case 1: data_type = TN3270E_DT.SCS_DATA; break;
				case 2: data_type = TN3270E_DT.RESPONSE; break;
				case 3: data_type = TN3270E_DT.BIND_IMAGE; break;
				case 4: data_type = TN3270E_DT.UNBIND; break;
				case 5: data_type = TN3270E_DT.NVT_DATA; break;
				case 6: data_type = TN3270E_DT.REQUEST; break;
				case 7: data_type = TN3270E_DT.SSCP_LU_DATA; break;
				case 8: data_type = TN3270E_DT.PRINT_EOJ; break;
				default:
					throw new ApplicationException("data_type ="+buf[0]+" not known");
			}
			request_flag = buf[1];
			response_flag = buf[2];
			seq_number[0] = buf[3];
			seq_number[1] = buf[4];
		}
		private void AddWithDoubledIAC(NetBuffer buffer, byte ch)
		{
			buffer.Add(ch);
			if (ch==255) // IAC
				buffer.Add(ch);
		}
		
		public void AddToNetBuffer(NetBuffer buffer)
		{
			AddWithDoubledIAC(buffer,byteFromDataType());
			AddWithDoubledIAC(buffer,request_flag);
			AddWithDoubledIAC(buffer,response_flag);
			AddWithDoubledIAC(buffer,seq_number[0]);
			AddWithDoubledIAC(buffer,seq_number[1]);
		}
	}
}


