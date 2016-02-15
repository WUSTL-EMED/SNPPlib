using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SNPPlib
{
    public class Server
    {
        public Server(IPAddress address, ushort port)
        {
            Address = address;
            Port = port;
            Commands = new Dictionary<string, Func<Guid, string, Task<string>>>();
        }

        public Server(string host = "localhost", ushort port = 444)
            : this(Dns.GetHostEntry(host).AddressList[0], port)
        {
        }

        private IPAddress Address { get; set; }
        private Dictionary<string, Func<Guid, string, Task<string>>> Commands { get; set; }
        private ushort Port { get; set; }
        private Socket Socket { get; set; }

        public void AddCommand(string command, Func<Guid, string, Task<string>> handler)
        {
            Commands.Add(command, handler);
        }

        public async Task<CancellationTokenSource> Listen(int backlog = 100)
        {
            //TODO: Only allow single call?
            //Allow specifying port here and listen on more than one port?

            var local = new IPEndPoint(Address, Port);
            Socket = new Socket(local.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);//Not sure we need this.
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            var tokenSource = new CancellationTokenSource();

            try
            {
                Socket.Bind(local);
                Socket.Listen(backlog);

                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            tokenSource.Token.ThrowIfCancellationRequested();

                            var remote = await Socket.AcceptTaskAsync();
                            var hello = "220 SNPP (V3) Gateway Ready\r\n";
                            await remote.SendTaskAsync(Encoding.ASCII.GetBytes(hello), 0, Encoding.ASCII.GetByteCount(hello), SocketFlags.None);

                            //TODO: Give the remote a session token, pass the token to the command handlers

                            Task.Factory.StartNew(async () =>
                            {
                                var remoteId = Guid.NewGuid();
                                try
                                {
                                    while (true)
                                    {
                                        tokenSource.Token.ThrowIfCancellationRequested();

                                        var buffer = new byte[1024];//TODO: Proper buffer sizes????????
                                        await remote.ReceiveTaskAsync(buffer, 0, 1024);
                                        var request = Encoding.ASCII.GetString(buffer).TrimEnd(new char[] { '\0' }).Split(new string[] { " ", "\r", "\n" }, 2, StringSplitOptions.RemoveEmptyEntries);

                                        var command = request.Length > 0 ? request[0] : default(string);
                                        var argument = request.Length > 1 ? request[1] : default(string);

                                        //Getting double requests, what is the junk?
                                        if (String.IsNullOrEmpty(command))
                                            continue;

                                        Func<Guid, string, Task<string>> func;

                                        if (command == "QUIT")
                                        {
                                            //Special case, we always want to quit but we may need cleanup elsewhere.
                                            if (Commands.TryGetValue(command, out func))
                                                await func(remoteId, argument);

                                            var response = "221 OK, Goodbye\r\n";
                                            await remote.SendTaskAsync(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetByteCount(response), SocketFlags.None);
                                            await remote.DisconnectTaskAsync(true);
                                            break;
                                        }
                                        else if (Commands.TryGetValue(command, out func))
                                        {
                                            if (command == "DATA")
                                            {
                                                //Special case, need to keep getting input until "\r\n.\r\n".
                                                var resp = "354 Begin Input; End with <CRLF>'.'<CRLF>\r\n";
                                                await remote.SendTaskAsync(Encoding.ASCII.GetBytes(resp), 0, Encoding.ASCII.GetByteCount(resp), SocketFlags.None);
                                                argument = String.Empty;

                                                do
                                                {
                                                    var buff = new byte[1024];//TODO: Proper buffer sizes????????
                                                    await remote.ReceiveTaskAsync(buff, 0, 1024);
                                                    argument += Encoding.ASCII.GetString(buff).TrimEnd('\0');
                                                }
                                                while (!argument.EndsWith("\r\n.\r\n"));
                                                argument = argument.Substring(0, argument.LastIndexOf("\r\n.\r\n")).TrimStart(new char[] { '\r', '\n' });
                                            }

                                            var response = await func(remoteId, argument);
                                            if (!response.EndsWith("\r\n"))
                                                response += "\r\n";
                                            await remote.SendTaskAsync(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetByteCount(response), SocketFlags.None);
                                        }
                                        else
                                        {
                                            var response = "500 Command Not Implemented\r\n";
                                            await remote.SendTaskAsync(Encoding.ASCII.GetBytes(response), 0, Encoding.ASCII.GetByteCount(response), SocketFlags.None);
                                        }
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                }
                                finally
                                {
                                    //cleanup
                                }
                            }, tokenSource.Token, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning, TaskScheduler.Default).Forget();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    finally
                    {
                        //cleanup
                    }
                }, tokenSource.Token, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning, TaskScheduler.Default).Forget();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }

            return await Task.FromResult(tokenSource);
        }
    }
}