using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SNPPlib
{
    public static class SocketExtensions
    {
        public static Task ConnectTaskAsync(this Socket socket, EndPoint endpoint)
        {
            return Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null);
        }

        public static Task DisconnectTaskAsync(this Socket socket, bool reuseSocket)
        {
            return Task.Factory.FromAsync(socket.BeginDisconnect, socket.EndDisconnect, reuseSocket, null);
        }

        public static Task<int> SendTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            return Task.Factory.FromAsync<int>(socket.BeginSend(buffer, offset, size, flags, (i) => { }, socket), socket.EndSend);
        }

        public static Task<int> ReceiveTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            return Task.Factory.FromAsync<int>(socket.BeginReceive(buffer, offset, size, flags, null, socket), socket.EndReceive);
        }

        public static Task<Socket> AcceptTaskAsync(this Socket socket)
        {
            return Task.Factory.FromAsync<Socket>(socket.BeginAccept, socket.EndAccept, socket);
        }
    }
}