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
        /// Create a SnppServer object pointing to a given port.
        /// </summary>
        /// <param name="port">The port of the server.</param>
        public SnppServer(int port)
        {
            Port = port;
            Commands = new Dictionary<string, Func<Guid, CancellationTokenSource, string, Task<string>>>();
        }

        /// <summary>
        /// Create a SnppServer object pointing to a named configured server.
        /// </summary>
        /// <param name="name">The name of the configured server.</param>
        public SnppServer(string name)
            : this(SnppConfig.GetPort(name))
        {
        }

        /// <summary>
        /// Create a SnppServer object pointing to a single unnamed configured server.
        /// </summary>
        public SnppServer()
            : this(SnppConfig.GetPort(null))
        {
        }

        #endregion Constructors

        #region Properties

        public IPProtectionLevel IPProtectionLevel
        {
            get
            {
                return _IPProtectionLevel;
            }
            set
            {
                if (Socket != null)
                    Socket.SetIPProtectionLevel(value);
                _IPProtectionLevel = value;
            }
        }

        /// <summary>
        /// The port of the server.
        /// </summary>
        public int Port { get; private set; }

        public int ReceiveTimeout
        {
            get
            {
                return _ReceiveTimeout;
            }
            set
            {
                if (Socket != null)
                    Socket.ReceiveTimeout = value;
                _ReceiveTimeout = value;
            }
        }

        public int SendTimeout
        {
            get
            {
                return _SendTimeout;
            }
            set
            {
                if (Socket != null)
                    Socket.SendTimeout = value;
                _SendTimeout = value;
            }
        }

        private IPProtectionLevel _IPProtectionLevel { get; set; }

        private int _ReceiveTimeout { get; set; }

        private int _SendTimeout { get; set; }

        private Dictionary<string, Func<Guid, CancellationTokenSource, string, Task<string>>> Commands { get; set; }

        private Socket Socket { get; set; }

        #endregion Properties

        #region Methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public void AddCommand(string command, Func<Guid, CancellationTokenSource, string, Task<string>> handler)
        {
            //TODO: Better way of handling the registering of functions?
            Commands.Add(command.ToUpperInvariant(), handler);
        }

        public async Task<CancellationTokenSource> Listen()
        {
            return await Listen(100);
        }

        public async Task<CancellationTokenSource> Listen(int backlog)
        {
            //TODO: Only allow single call?
            //Allow specifying port here and listen on more than one port?

            if (Socket == null)
            {
                Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);//Not sure we need this.
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.AcceptConnection, true);
                Socket.DualMode = true;
            }

            var listenerToken = new CancellationTokenSource();

            try
            {
                //Multiple local IP?
                //Socket.Bind(new IPEndPoint(Dns.GetHostAddresses(Host)[0], Port));
                Socket.Bind(new IPEndPoint(IPAddress.Any, Port));
                Socket.Listen(backlog);

                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            listenerToken.Token.ThrowIfCancellationRequested();

                            var remote = await Socket.AcceptTaskAsync();
                            await remote.SendTaskAsync("220 SNPP (V3) Gateway Ready\r\n");

                            //TODO: Give the remote a session token, pass the token to the command handlers

                            Task.Factory.StartNew(async () =>
                            {
                                var remoteId = Guid.NewGuid();
                                var remoteToken = CancellationTokenSource.CreateLinkedTokenSource(listenerToken.Token);
                                try
                                {
                                    while (true)
                                    {
                                        remoteToken.Token.ThrowIfCancellationRequested();

                                        var request = (await remote.ReceiveTaskAsync()).Split(new char[] { ' ', '\r', '\n' }, 2, StringSplitOptions.RemoveEmptyEntries);
                                        if (request.Length == 0)
                                        {
                                            SocketError error;
                                            remote.Send(new byte[1], 0, 1, SocketFlags.None, out error);
                                            continue;
                                        }

                                        var command = request.Length > 0 ? request[0].ToUpperInvariant() : default(string);
                                        var argument = request.Length > 1 ? request[1] : default(string);

                                        //Getting double requests, what is the junk?
                                        if (String.IsNullOrWhiteSpace(command))
                                            continue;

                                        Func<Guid, CancellationTokenSource, string, Task<string>> func;
                                        var funcExists = Commands.TryGetValue(command, out func) && func != null;

                                        if (command == "QUIT")
                                        {
                                            //Special case, we always want to quit but we may need cleanup elsewhere.
                                            if (funcExists)
                                            {
                                                try
                                                {
                                                    await func(remoteId, remoteToken, argument);//timeout?
                                                }
                                                catch (Exception) { }
                                            }

                                            await remote.SendTaskAsync("221 OK, Goodbye\r\n");
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
                                                    argument += await remote.ReceiveTaskAsync();
                                                }
                                                while (!argument.EndsWith("\r\n.\r\n"));
                                                argument = argument.Substring(0, argument.LastIndexOf("\r\n.\r\n")).TrimStart(new char[] { '\r', '\n' });
                                            }

                                            var response = "554 Error, failed (technical reason)\r\n";
                                            try
                                            {
                                                response = await func(remoteId, remoteToken, argument);//timeout?
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
                                    remote.Shutdown(SocketShutdown.Send);//Causes putty to error out, not sure if this is right.
                                    remote.Close();
                                }
                            }, listenerToken.Token, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning, TaskScheduler.Default).Forget();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    finally
                    {
                        //cleanup
                        Socket.Shutdown(SocketShutdown.Both);
                        Socket.Close();
                        Socket = null;
                    }
                }, listenerToken.Token, TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning, TaskScheduler.Default).Forget();
            }
            catch (Exception e)//TODO: More specific exceptions
            {
                Console.WriteLine(e.ToString());
                return null;
            }

            return await Task.FromResult(listenerToken);
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