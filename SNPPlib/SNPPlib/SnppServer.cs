using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SNPPlib.Config;
using SNPPlib.Extensions;

namespace SNPPlib
{
    public class SnppServer : IDisposable
    {
        #region Constructors

        /// <summary>
        /// Create a SnppServer object pointing to a given IPAddress and port.
        /// </summary>
        /// <param name="address">The IPAddress of the server.</param>
        /// <param name="port">The port of the server.</param>
        public SnppServer(IPAddress address, int port)
        {
            Address = address;
            Port = port;
            Commands = new Dictionary<string, Func<Guid, string, Task<string>>>();
        }

        /// <summary>
        /// Create a SnppServer object pointing to a given host name and port.
        /// </summary>
        /// <param name="host">The host name of the server.</param>
        /// <param name="port">The port of the server.</param>
        public SnppServer(string host, int port)
            : this(Dns.GetHostEntry(host).AddressList[0], port)
        {
            _Host = host;
        }

        /// <summary>
        /// Create a SnppServer object pointing to a named configured server.
        /// </summary>
        /// <param name="name">The name of the configured server.</param>
        public SnppServer(string name)
            : this(Dns.GetHostEntry(SnppConfig.GetHost(name)).AddressList[0], SnppConfig.GetPort(name))
        {
            _Host = SnppConfig.GetHost(name);
        }

        /// <summary>
        /// Create a SnppServer object pointing to a single unnamed configured server.
        /// </summary>
        public SnppServer()
            : this(Dns.GetHostEntry(SnppConfig.GetHost(null)).AddressList[0], SnppConfig.GetPort(null))
        {
            _Host = SnppConfig.GetHost(null);
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// The IPAddress of the server.
        /// </summary>
        public IPAddress Address
        {
            get;
            private set;
            //settable until listening?
        }

        /// <summary>
        /// The host name of the server.
        /// </summary>
        public string Host
        {
            get
            {
                return _Host ?? Address.ToString();
            }
            //settable until listening?
        }

        /// <summary>
        /// The port of the server.
        /// </summary>
        public int Port
        {
            get;
            private set;
            //settable until listening?
        }

        private string _Host { get; set; }

        private Dictionary<string, Func<Guid, string, Task<string>>> Commands { get; set; }

        private Socket Socket { get; set; }

        #endregion Properties

        #region Methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public void AddCommand(string command, Func<Guid, string, Task<string>> handler)
        {
            //TODO: Better way of handling the registering of functions?
            Commands.Add(command, handler);
        }

        public async Task<CancellationTokenSource> Listen()
        {
            return await Listen(100);
        }

        public async Task<CancellationTokenSource> Listen(int backlog)
        {
            //TODO: Only allow single call?
            //Allow specifying port here and listen on more than one port?

            var local = new IPEndPoint(Address, Port);
            if (Socket == null)//The socket _should_ be reusable.
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

                                        var request = (await remote.ReceiveTaskAsync(1024)).Split(new char[] { ' ', '\r', '\n' }, 2, StringSplitOptions.RemoveEmptyEntries);
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

        #endregion Methods

        #region IDisposable

        ~SnppServer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Socket != null)
                {
                    Socket.Dispose();
                    Socket = null;
                }
            }
        }

        #endregion IDisposable
    }
}