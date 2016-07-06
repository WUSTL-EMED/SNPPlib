using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SNPPlib
{
    public static class SocketExtensions
    {
        public static Task<Socket> AcceptTaskAsync(this Socket socket)
        {
            return Task.Factory.FromAsync<Socket>(socket.BeginAccept, socket.EndAccept, socket);
        }

        public static Task ConnectTaskAsync(this Socket socket, EndPoint endpoint)
        {
            return Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, endpoint, null);
        }

        public static Task DisconnectTaskAsync(this Socket socket, bool reuseSocket)
        {
            return Task.Factory.FromAsync(socket.BeginDisconnect, socket.EndDisconnect, reuseSocket, null);
        }

        public static string Receive(this Socket socket, int size, Encoding encoding = null, SocketFlags flags = SocketFlags.None)//buffer size?
        {
            var buffer = new byte[size];
            socket.Receive(buffer, 0, size, flags);
            return (encoding ?? Encoding.ASCII).GetString(buffer).TrimEnd('\0');
        }

        public static Task<int> ReceiveTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            return Task.Factory.FromAsync<int>(socket.BeginReceive(buffer, offset, size, flags, null, socket), socket.EndReceive);
        }

        public static async Task<string> ReceiveTaskAsync(this Socket socket, int size, Encoding encoding = null, SocketFlags flags = SocketFlags.None)//buffer size?
        {
            var buffer = new byte[size];
            await socket.ReceiveTaskAsync(buffer, 0, size, flags);
            return (encoding ?? Encoding.ASCII).GetString(buffer).TrimEnd('\0');
        }

        public static int Send(this Socket socket, string data, Encoding encoding = null, SocketFlags flags = SocketFlags.None)
        {
            return socket.Send((encoding ?? Encoding.ASCII).GetBytes(data), 0, (encoding ?? Encoding.ASCII).GetByteCount(data), flags);
        }

        public static Task<int> SendTaskAsync(this Socket socket, byte[] buffer, int offset, int size, SocketFlags flags = SocketFlags.None)
        {
            return Task.Factory.FromAsync<int>(socket.BeginSend(buffer, offset, size, flags, (i) => { }, socket), socket.EndSend);
        }

        public static Task<int> SendTaskAsync(this Socket socket, string data, Encoding encoding = null, SocketFlags flags = SocketFlags.None)
        {
            return socket.SendTaskAsync((encoding ?? Encoding.ASCII).GetBytes(data), 0, (encoding ?? Encoding.ASCII).GetByteCount(data), flags);
        }
    }
}