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
                            await remote.SendTaskAsync("220 SNPP (V3) Gateway Ready\r\n");

                            //TODO: Give the remote a session token, pass the token to the command handlers

                            Task.Factory.StartNew(async () =>
                            {
                                var remoteId = Guid.NewGuid();
                                try
                                {
                                    while (true)
                                    {
                                        tokenSource.Token.ThrowIfCancellationRequested();

                                        var request = (await remote.ReceiveTaskAsync(1024)).Split(new string[] { " ", "\r", "\n" }, 2, StringSplitOptions.RemoveEmptyEntries);
                                        var command = request.Length > 0 ? request[0] : default(string);
                                        var argument = request.Length > 1 ? request[1] : default(string);

                                        //Getting double requests, what is the junk?
                                        if (String.IsNullOrWhiteSpace(command))
                                            continue;

                                        Func<Guid, string, Task<string>> func;
                                        var funcExists = Commands.TryGetValue(command, out func) && func != null;

                                        if (command == "QUIT")
                                        {
                                            //Special case, we always want to quit but we may need cleanup elsewhere.
                                            if (funcExists)
                                            {
                                                try
                                                {
                                                    await func(remoteId, argument);//timeout?
                                                }
                                                catch (Exception) { }
                                            }

                                            await remote.SendTaskAsync("221 OK, Goodbye\r\n");
                                            await remote.DisconnectTaskAsync(true);
                                            break;
                                        }
                                        else if (funcExists)
                                        {
                                            if (command == "DATA")
                                            {
                                                //Special case, need to keep getting input until "\r\n.\r\n".
                                                await remote.SendTaskAsync("354 Begin Input; End with <CRLF>'.'<CRLF>\r\n");
                                                argument = String.Empty;

                                                do
                                                {
                                                    argument += await remote.ReceiveTaskAsync(1024);
                                                }
                                                while (!argument.EndsWith("\r\n.\r\n"));
                                                argument = argument.Substring(0, argument.LastIndexOf("\r\n.\r\n")).TrimStart(new char[] { '\r', '\n' });
                                            }

                                            var response = "554 Error, failed (technical reason)\r\n";
                                            try
                                            {
                                                response = await func(remoteId, argument);//timeout?
                                                if (!response.EndsWith("\r\n"))
                                                    response += "\r\n";
                                            }
                                            catch (Exception) { }

                                            await remote.SendTaskAsync(response);
                                        }
                                        else
                                        {
                                            await remote.SendTaskAsync("500 Command Not Implemented\r\n");
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