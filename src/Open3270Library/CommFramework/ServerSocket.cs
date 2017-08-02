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
using System.Net;
using System.Net.Sockets;

namespace Open3270.Library
{
	internal delegate void OnConnectionDelegate(ClientSocket sock);
	internal delegate void OnConnectionDelegateRAW(Socket sock);

	internal enum ServerSocketType
	{
		RAW,
		ClientServer
	}
	/// <summary>
	/// Summary description for ServerSocket.
	/// </summary>
	internal class ServerSocket
	{
		public event OnConnectionDelegate OnConnect;
		public event OnConnectionDelegateRAW OnConnectRAW;
		Socket mSocket;
		ServerSocketType mSocketType;
		private AsyncCallback callbackProc ;

		public ServerSocket()
		{
			mSocketType = ServerSocketType.ClientServer;
		}
		public ServerSocket(ServerSocketType socketType)
		{
			mSocketType = socketType;
		}
		public void Close()
		{
			try
			{
				Console.WriteLine("ServerSocket.CLOSE");
				mSocket.Close();
			}
			catch (Exception)
			{
			}
			mSocket = null;
		}
		public void Listen(int port)
		{
			//IPHostEntry lipa = Dns.Resolve("host.contoso.com");
			IPEndPoint lep = new IPEndPoint(IPAddress.Any, port);

			mSocket				= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			// Create New EndPoint
			// This is a non blocking IO
			mSocket.Blocking		= false ;	

			mSocket.Bind(lep);
			//
			mSocket.Listen(1000);
			//
			// Assign Callback function to read from Asyncronous Socket
			callbackProc	= new AsyncCallback(ConnectCallback);
			//
			mSocket.BeginAccept(callbackProc, null);
		}
		private void ConnectCallback( IAsyncResult ar )
		{
			Socket newSocket = null;
			try
			{

				try
				{
					newSocket = mSocket.EndAccept(ar);
				}
				catch (System.ObjectDisposedException)
				{
					
					//Console.WriteLine("Server socket error - ConnectCallback failed "+ee.Message);
					mSocket = null;
					return;
				}

				try
				{
					Audit.WriteLine("Connection received - call OnConnect");
					//
					if (this.OnConnectRAW != null)
						this.OnConnectRAW(newSocket);
					//
					if (this.OnConnect != null)
					{
						ClientSocket socket = new ClientSocket(newSocket);
						socket.FXSocketType = this.mSocketType;
						this.OnConnect(socket);
					}

					// restart accept
					mSocket.BeginAccept(callbackProc, null);
				}
				catch (System.ObjectDisposedException)
				{
					newSocket.Close();
					newSocket = null;
				}
				catch (Exception e)
				{
					Console.WriteLine("Exception occured in AcceptCallback\n"+e);
					newSocket.Close();
					newSocket = null;
				}
			}
			finally
			{
				// wait for the next incoming connection
				if (mSocket != null)
					mSocket.BeginAccept(callbackProc, null);
			}
		}
	}
}
