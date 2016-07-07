using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SNPPlib.Config;
using SNPPlib.Extensions;

namespace SNPPlib
{
    public class SnppClientProtocol : IDisposable
    {
        public static readonly Regex CallerIdFormat = new Regex(@"^[0-9]+$", RegexOptions.Compiled);//is this numeric or alphanumeric?
        public static readonly Regex LoginIdFormat = new Regex(@"^[a-z0-9]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex MessageFormat = new Regex(@"^[a-z0-9 ]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);//The spec says alphanumeric, what about punctuation?
        public static readonly Regex PagerIdFormat = new Regex(@"^[0-9]+$", RegexOptions.Compiled);

        #region Constructors

        /// <summary>
        /// Create a SnppClientProtocol object pointing to a given IPAddress and port.
        /// </summary>
        /// <param name="address">The IPAddress of the server.</param>
        /// <param name="port">The port of the server.</param>
        public SnppClientProtocol(IPAddress address, int port)
        {
            Address = address;
            Port = port;
        }

        /// <summary>
        /// Create a SnppClientProtocol object pointing to a given host name and port.
        /// </summary>
        /// <param name="host">The host name of the server.</param>
        /// <param name="port">The port of the server.</param>
        public SnppClientProtocol(string host, int port)
            : this(Dns.GetHostEntry(host).AddressList[0], port)
        {
            //What if there is more than one ip?
            _Host = host;
        }

        /// <summary>
        /// Create a SnppClientProtocol object pointing to a named configured server.
        /// </summary>
        /// <param name="name">The name of the configured server.</param>
        public SnppClientProtocol(string name)
            : this(Dns.GetHostEntry(SnppConfig.GetHost(name)).AddressList[0], SnppConfig.GetPort(name))
        {
            _Host = SnppConfig.GetHost(name);
        }

        /// <summary>
        /// Create a SnppClientProtocol object pointing to a single unnamed configured server.
        /// </summary>
        public SnppClientProtocol()
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
            //settable?
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
            //settable?
        }

        /// <summary>
        /// The port of the server.
        /// </summary>
        public int Port
        {
            get;
            private set;
            //settable?
        }

        private string _Host { get; set; }

        private Socket Socket { get; set; }

        #endregion Properties

        //option to re-try technical failures?

        #region Async Methods

        /// <summary>
        /// Send an alert level (ALER) command asynchronously.
        /// </summary>
        /// <param name="alertLevel">The alert level.</param>
        /// <returns>The response.</returns>
        public async Task<SnppResponse> AlertAsync(AlertLevel alertLevel)
        {
            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "ALER {0}", (int)alertLevel));
        }

        /// <summary>
        /// Send a caller id (CALL) command asynchronously.
        /// </summary>
        /// <param name="callerId">The caller id.  Must be numeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="callerId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="callerId"/> parameter was not numeric.</exception>
        public async Task<SnppResponse> CallerAsync(string callerId)
        {
            if (callerId == null)
                throw new ArgumentNullException("callerId");
            if (String.IsNullOrWhiteSpace(callerId))
                throw new ArgumentException(Resource.CallerIdRequired, "callerId");
            if (!MessageFormat.IsMatch(callerId))
                throw new ArgumentException(Resource.CallerIdNumeric, "callerId");

            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "CALL {0}", callerId));
        }

        /// <summary>
        /// Attempt to connect to the server asynchronously.
        /// </summary>
        /// <returns>True if the connection was established, otherwise false.</returns>
        public async Task<bool> ConnectAsync()
        {
            var remote = new IPEndPoint(Address, Port);
            if (Socket == null)//The socket _should_ be reusable.
                Socket = new Socket(remote.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);//Not sure we need this.
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            await Socket.ConnectTaskAsync(remote);

            var response = new SnppResponse(await Socket.ReceiveTaskAsync(256));//TODO: Response parser
            if (response.Code == ResponseCode.GatewayReady)
                return true;
            await Socket.DisconnectTaskAsync(true);
            return false;
        }

        /// <summary>
        /// Send a coverage (COVE) command asynchronously.
        /// </summary>
        /// <param name="alternateArea">The alternate coverage area id.</param>
        /// <returns>The response.</returns>
        public async Task<SnppResponse> CoverageAsync(int alternateArea)
        {
            //are areas always numbers?
            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "COVE {0}", alternateArea));
        }

        /// <summary>
        /// Send a multi-line message (DATA) command asynchronously.
        /// </summary>
        /// <param name="text">The message lines.  Must be alphanumeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="text"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter contained no elements.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter was not alphanumeric.</exception>
        public async Task<SnppResponse> DataAsync(IEnumerable<string> text)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            if (!text.Any(_ => !String.IsNullOrWhiteSpace(_)))//TODO: Strip out blank lines?
                throw new ArgumentException(Resource.MessageRequired, "text");
            if (text.Any(_ => !MessageFormat.IsMatch(_)))
                throw new ArgumentException(Resource.MessageAlphanumeric, "text");

            var response = await SendAsync("DATA");
            if (response.Code == ResponseCode.BeginInput)
            {
                foreach (var line in text)
                {
                    await SendAsync(line);
                }
                response = await SendAsync("\r\n.");
            }
            return response;
        }

        /// <summary>
        /// Disconnect the socket asynchronously.
        /// </summary>
        public async Task DisconnectAsync()//Send a quit?
        {
            await Socket.DisconnectTaskAsync(true);
        }

        /// <summary>
        /// Send a help (HELP) command asynchronously.
        /// </summary>
        /// <returns>The response.</returns>
        public async Task<SnppResponse> HelpAsync()
        {
            return await SendAsync("HELP");
        }

        /// <summary>
        /// Send a hold-until (HOLD) command asynchronously.
        /// </summary>
        /// <param name="time">The DateTime to hold the message until.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="time"/> parameter was null.</exception>
        public async Task<SnppResponse> HoldUntilAsync(DateTime time)
        {
            return await HoldUntilAsync(time, TimeSpan.Zero);
        }

        /// <summary>
        /// Send a hold-until (HOLD) command asynchronously.
        /// </summary>
        /// <param name="time">The DateTime to hold the message until.</param>
        /// <param name="gmtOffset">The offset from GMT of the hold time.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="time"/> parameter was null.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "gmt", Justification = "It stands for greenwich mean time.")]
        public async Task<SnppResponse> HoldUntilAsync(DateTime time, TimeSpan gmtOffset)
        {
            if (time == null)
                throw new ArgumentNullException("time");
            //Check if the date is in the past? I don't know if it matters.

            var gmt = gmtOffset != TimeSpan.Zero ? (gmtOffset < TimeSpan.Zero ? "-" : String.Empty) + gmtOffset.ToString("hhmm", CultureInfo.InvariantCulture) : String.Empty;
            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "HOLD {0} {1}", time.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture), gmt).TrimEnd());
        }

        /// <summary>
        /// Send a service level (LEVE) command asynchronously.
        /// <para>If this command is not issued the default service level is "Normal".</para>
        /// </summary>
        /// <param name="serviceLevel">The service level.</param>
        /// <returns>The response.</returns>
        public async Task<SnppResponse> LevelAsync(ServiceLevel serviceLevel)
        {
            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "LEVE {0}", (int)serviceLevel));
        }

        /// <summary>
        /// Send a login (LOGI) command asynchronously.
        /// </summary>
        /// <param name="loginId">The login id.  Must be alphanumeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="loginId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="loginId"/> parameter was not alphanumeric.</exception>
        public async Task<SnppResponse> LoginAsync(string loginId)
        {
            return await LoginAsync(loginId, null);
        }

        /// <summary>
        /// Send a login (LOGI) command asynchronously.
        /// </summary>
        /// <param name="loginId">The login id.  Must be alphanumeric.</param>
        /// <param name="password">The login password.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="loginId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="loginId"/> parameter was not alphanumeric.</exception>
        public async Task<SnppResponse> LoginAsync(string loginId, string password)
        {
            if (loginId == null)
                throw new ArgumentNullException("loginId");
            if (String.IsNullOrWhiteSpace(loginId))
                throw new ArgumentException(Resource.LoginIdRequired, "loginId");
            if (!LoginIdFormat.IsMatch(loginId))
                throw new ArgumentException(Resource.LoginIdAlphanumeric, "loginId");

            //TODO: Is there a specific allow format for passwords? e.g. alpha-numeric.

            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "LOGI {0} {1}", loginId, password ?? String.Empty).TrimEnd());
        }

        /// <summary>
        /// Send a single-line message (MESS) command asynchronously.
        /// </summary>
        /// <param name="text">The message.  Must be alphanumeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="text"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter was not alphanumeric.</exception>
        public async Task<SnppResponse> MessageAsync(string text)
        {
            //message should be alphanumeric
            //message is single line
            //if issued more than once before SEND should produce 503 ERROR, Message Already Entered
            if (text == null)
                throw new ArgumentNullException("text");
            if (String.IsNullOrWhiteSpace(text))
                throw new ArgumentException(Resource.MessageRequired, "text");
            if (!MessageFormat.IsMatch(text))
                throw new ArgumentException(Resource.MessageAlphanumeric, "text");

            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "MESS {0}", text));
        }

        /// <summary>
        /// Send a pager id (PAGE) command asynchronously.
        /// </summary>
        /// <param name="pagerId">The pager id.  Must be numeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="pagerId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="pagerId"/> parameter was not numeric.</exception>
        public async Task<SnppResponse> PagerAsync(string pagerId)//valid password chars?
        {
            return await PagerAsync(pagerId, null);
        }

        /// <summary>
        /// Send a pager id (PAGE) command asynchronously.
        /// </summary>
        /// <param name="pagerId">The pager id.  Must be numeric.</param>
        /// <param name="password">The pager password.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="pagerId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="pagerId"/> parameter was not numeric.</exception>
        public async Task<SnppResponse> PagerAsync(string pagerId, string password)//valid password chars?
        {
            if (pagerId == null)
                throw new ArgumentNullException("pagerId");//alphanumeric or just numeric?
            if (String.IsNullOrWhiteSpace(pagerId))
                throw new ArgumentException(Resource.PagerRequired, "pagerId");//alphanumeric or just numeric?
            if (!PagerIdFormat.IsMatch(pagerId))
                throw new ArgumentException(Resource.PagerIdNumeric, "pagerId");

            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "PAGE {0} {1}", pagerId, password ?? String.Empty).TrimEnd());
        }

        /// <summary>
        /// Send a quit (QUIT) command and disconnect the socket asynchronously.
        /// </summary>
        public async Task QuitAsync()
        {
            if (Socket.Connected)//reliable?
                await SendAsync("QUIT");
            //Does it matter whether or not they respond as expected? We should probably just disconnect anyway.
            await Socket.DisconnectTaskAsync(true);
        }

        /// <summary>
        /// Send a reset (RESE) command asynchronously.
        /// <para>Resets the state of the connection to as if it were freshly opened.</para>
        /// </summary>
        /// <returns>The response/</returns>
        public async Task<SnppResponse> ResetAsync()
        {
            //Resets the state of the connection to as if it were freshly opened
            return await SendAsync("RESE");
        }

        /// <summary>
        /// Send a send (SEND) command asynchronously.
        /// </summary>
        /// <returns>The response.</returns>
        public async Task<SnppResponse> SendAsync()
        {
            return await SendAsync("SEND");
        }

        /// <summary>
        /// Send a subject (SUBJ) command asynchronously.
        /// </summary>
        /// <param name="messageSubject">The subject.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="messageSubject"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="messageSubject"/> parameter was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="messageSubject"/> parameter was not alphanumeric.</exception>
        public async Task<SnppResponse> SubjectAsync(string messageSubject)
        {
            if (messageSubject == null)
                throw new ArgumentNullException("messageSubject");
            if (String.IsNullOrWhiteSpace(messageSubject))
                throw new ArgumentException(Resource.SubjectTextRequired, "messageSubject");
            if (!MessageFormat.IsMatch(messageSubject))
                throw new ArgumentException(Resource.SubjectAlphanumeric, "messageSubject");
            return await SendAsync(String.Format(CultureInfo.InvariantCulture, "SUBJ {0}", messageSubject));
        }

        /// <summary>
        /// Send a 2-way (2WAY) command asynchronously.
        /// <para>Not implemented.</para>
        /// </summary>
        /// <returns>The response.</returns>
        /// <exception cref="System.NotImplementedException">This method is not implemented.</exception>
        public async Task<SnppResponse> TwoWayAsync()
        {
            await Task.FromResult(false);//suppress warning for now
            throw new NotImplementedException();
            //return await SendAsync("2WAY");
        }

        private async Task<SnppResponse> SendAsync(string command, int responseSize = 1024)
        {
            //TODO: check for crlf in command?
            await Socket.SendTaskAsync(command + "\r\n");

            //handling multi-part responses? ResponseCode.MultiLineResponse; handling long responses?
            var response = new SnppResponse(await Socket.ReceiveTaskAsync(responseSize));
            if (response.Code == ResponseCode.FatalError)
                await Socket.DisconnectTaskAsync(true);//Do we want to do anything to the response/throw?
            return response;
        }

        #endregion Async Methods

        #region Sync Methods

        /// <summary>
        /// Send an alert level (ALER) command.
        /// </summary>
        /// <param name="alertLevel">The alert level.</param>
        /// <returns>The response.</returns>
        public SnppResponse Alert(AlertLevel alertLevel)
        {
            return Send(String.Format(CultureInfo.InvariantCulture, "ALER {0}", (int)alertLevel));
        }

        /// <summary>
        /// Send a caller id (CALL) command.
        /// </summary>
        /// <param name="callerId">The caller id.  Must be numeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="callerId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="callerId"/> parameter was not numeric.</exception>
        public SnppResponse Caller(string callerId)
        {
            if (callerId == null)
                throw new ArgumentNullException("callerId");
            if (String.IsNullOrWhiteSpace(callerId))
                throw new ArgumentException(Resource.CallerIdRequired, "callerId");
            if (!MessageFormat.IsMatch(callerId))
                throw new ArgumentException(Resource.CallerIdNumeric, "callerId");

            return Send(String.Format(CultureInfo.InvariantCulture, "CALL {0}", callerId));
        }

        /// <summary>
        /// Attempt to connect to the server.
        /// </summary>
        /// <returns>True if the connection was established, otherwise false.</returns>
        public bool Connect()
        {
            var remote = new IPEndPoint(Address, Port);
            if (Socket == null)//The socket _should_ be reusable.
                Socket = new Socket(remote.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);//Not sure we need this.
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Socket.Connect(remote);

            var response = new SnppResponse(Socket.Receive(256));//TODO: Response parser
            if (response.Code == ResponseCode.GatewayReady)
                return true;
            Socket.Disconnect(true);
            return false;
        }

        /// <summary>
        /// Send a coverage (COVE) command.
        /// </summary>
        /// <param name="alternateArea">The alternate coverage area id.</param>
        /// <returns>The response.</returns>
        public SnppResponse Coverage(int alternateArea)
        {
            //are areas always numbers?
            return Send(String.Format(CultureInfo.InvariantCulture, "COVE {0}", alternateArea));
        }

        /// <summary>
        /// Send a multi-line message (DATA) command.
        /// </summary>
        /// <param name="text">The message lines.  Must be alphanumeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="text"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter contained no elements.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter was not alphanumeric.</exception>
        public SnppResponse Data(IEnumerable<string> text)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            if (!text.Any(_ => !String.IsNullOrWhiteSpace(_)))//TODO: Strip out blank lines?
                throw new ArgumentException(Resource.MessageRequired, "text");
            if (text.Any(_ => !MessageFormat.IsMatch(_)))
                throw new ArgumentException(Resource.MessageAlphanumeric, "text");

            var response = Send("DATA");
            if (response.Code == ResponseCode.BeginInput)
            {
                foreach (var line in text)
                {
                    Send(line);
                }
                response = Send("\r\n.");
            }
            return response;
        }

        /// <summary>
        /// Disconnect the socket.
        /// </summary>
        public void Disconnect()//Send a quit?
        {
            Socket.Disconnect(true);
        }

        /// <summary>
        /// Send a help (HELP) command.
        /// </summary>
        /// <returns>The response.</returns>
        public SnppResponse Help()
        {
            return Send("HELP");
        }

        /// <summary>
        /// Send a hold-until (HOLD) command.
        /// </summary>
        /// <param name="time">The DateTime to hold the message until.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="time"/> parameter was null.</exception>
        public SnppResponse HoldUntil(DateTime time)
        {
            return HoldUntil(time, TimeSpan.Zero);
        }

        /// <summary>
        /// Send a hold-until (HOLD) command.
        /// </summary>
        /// <param name="time">The DateTime to hold the message until.</param>
        /// <param name="gmtOffset">The offset from GMT of the hold time.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="time"/> parameter was null.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "gmt", Justification = "It stands for greenwich mean time.")]
        public SnppResponse HoldUntil(DateTime time, TimeSpan gmtOffset)
        {
            if (time == null)
                throw new ArgumentNullException("time");
            //Check if the date is in the past? I don't know if it matters.

            var gmt = gmtOffset != TimeSpan.Zero ? (gmtOffset < TimeSpan.Zero ? "-" : String.Empty) + gmtOffset.ToString("hhmm", CultureInfo.InvariantCulture) : String.Empty;
            return Send(String.Format(CultureInfo.InvariantCulture, "HOLD {0} {1}", time.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture), gmt).TrimEnd());
        }

        /// <summary>
        /// Send a service level (LEVE) command.
        /// <para>If this command is not issued the default service level is "Normal".</para>
        /// </summary>
        /// <param name="serviceLevel">The service level.</param>
        /// <returns>The response.</returns>
        public SnppResponse Level(ServiceLevel serviceLevel)
        {
            return Send(String.Format(CultureInfo.InvariantCulture, "LEVE {0}", (int)serviceLevel));
        }

        /// <summary>
        /// Send a login (LOGI) command.
        /// </summary>
        /// <param name="loginId">The login id.  Must be alphanumeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="loginId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="loginId"/> parameter was not alphanumeric.</exception>
        public SnppResponse Login(string loginId)
        {
            return Login(loginId, null);
        }

        /// <summary>
        /// Send a login (LOGI) command.
        /// </summary>
        /// <param name="loginId">The login id.  Must be alphanumeric.</param>
        /// <param name="password">The login password.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="loginId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="loginId"/> parameter was not alphanumeric.</exception>
        public SnppResponse Login(string loginId, string password)
        {
            if (loginId == null)
                throw new ArgumentNullException("loginId");//alphanumeric or just numeric?
            if (String.IsNullOrWhiteSpace(loginId))
                throw new ArgumentException(Resource.LoginIdRequired, "loginId");//alphanumeric or just numeric?
            if (!LoginIdFormat.IsMatch(loginId))
                throw new ArgumentException(Resource.LoginIdAlphanumeric, "loginId");

            return Send(String.Format(CultureInfo.InvariantCulture, "LOGI {0} {1}", loginId, password ?? String.Empty).TrimEnd());
        }

        /// <summary>
        /// Send a single-line message (MESS) command.
        /// </summary>
        /// <param name="text">The message.  Must be alphanumeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="text"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="text"/> parameter was not alphanumeric.</exception>
        public SnppResponse Message(string text)
        {
            //message should be alphanumeric
            //message is single line
            //if issued more than once before SEND should produce 503 ERROR, Message Already Entered
            if (text == null)
                throw new ArgumentNullException("text");
            if (String.IsNullOrWhiteSpace(text))
                throw new ArgumentException(Resource.MessageRequired, "text");
            if (!MessageFormat.IsMatch(text))
                throw new ArgumentException(Resource.MessageAlphanumeric, "text");

            return Send(String.Format(CultureInfo.InvariantCulture, "MESS {0}", text));
        }

        /// <summary>
        /// Send a pager id (PAGE) command.
        /// </summary>
        /// <param name="pagerId">The pager id.  Must be numeric.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="pagerId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="pagerId"/> parameter was not numeric.</exception>
        public SnppResponse Pager(string pagerId)//valid password chars?
        {
            return Pager(pagerId, null);
        }

        /// <summary>
        /// Send a pager id (PAGE) command.
        /// </summary>
        /// <param name="pagerId">The pager id.  Must be numeric.</param>
        /// <param name="password">The pager password.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="pagerId"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="pagerId"/> parameter was not numeric.</exception>
        public SnppResponse Pager(string pagerId, string password)//valid password chars?
        {
            if (pagerId == null)
                throw new ArgumentNullException("pagerId");//alphanumeric or just numeric?
            if (String.IsNullOrWhiteSpace(pagerId))
                throw new ArgumentException(Resource.PagerRequired, "pagerId");//alphanumeric or just numeric?
            if (!PagerIdFormat.IsMatch(pagerId))
                throw new ArgumentException(Resource.PagerIdNumeric, "pagerId");

            return Send(String.Format(CultureInfo.InvariantCulture, "PAGE {0} {1}", pagerId, password ?? String.Empty).TrimEnd());
        }

        /// <summary>
        /// Send a quit (QUIT) command and disconnect the socket.
        /// </summary>
        public void Quit()
        {
            if (Socket.Connected)//reliable?
                Send("QUIT");
            //Does it matter whether or not they respond as expected? We should probably just disconnect anyway.
            Socket.Disconnect(true);
        }

        /// <summary>
        /// Send a reset (RESE) command.
        /// <para>Resets the state of the connection to as if it were freshly opened.</para>
        /// </summary>
        /// <returns>The response/</returns>
        public SnppResponse Reset()
        {
            //Resets the state of the connection to as if it were freshly opened
            return Send("RESE");
        }

        /// <summary>
        /// Send a send (SEND) command.
        /// </summary>
        /// <returns>The response.</returns>
        public SnppResponse Send()
        {
            return Send("SEND");
        }

        /// <summary>
        /// Send a subject (SUBJ) command.
        /// </summary>
        /// <param name="messageSubject">The subject.</param>
        /// <returns>The response.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="messageSubject"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="messageSubject"/> parameter was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="messageSubject"/> parameter was not alphanumeric.</exception>
        public SnppResponse Subject(string messageSubject)
        {
            if (messageSubject == null)
                throw new ArgumentNullException("messageSubject");
            if (String.IsNullOrWhiteSpace(messageSubject))
                throw new ArgumentException(Resource.SubjectTextRequired, "messageSubject");
            if (!MessageFormat.IsMatch(messageSubject))
                throw new ArgumentException(Resource.SubjectAlphanumeric, "messageSubject");
            return Send(String.Format(CultureInfo.InvariantCulture, "SUBJ {0}", messageSubject));
        }

        /// <summary>
        /// Send a 2-way (2WAY) command.
        /// <para>Not implemented.</para>
        /// </summary>
        /// <returns>The response.</returns>
        /// <exception cref="System.NotImplementedException">This method is not implemented.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is not implemented yet.  This supression won't be needed once it is.")]
        public SnppResponse TwoWay()
        {
            throw new NotImplementedException();
            //return Send("2WAY");
        }

        private SnppResponse Send(string command, int responseSize = 1024)
        {
            //TODO: check for crlf in command?
            Socket.Send(command + "\r\n");

            //handling multi-part responses? ResponseCode.MultiLineResponse; handling long responses?
            var response = new SnppResponse(Socket.Receive(responseSize));
            if (response.Code == ResponseCode.FatalError)
                Socket.Disconnect(true);//Do we want to do anything to the response/throw?
            return response;
        }

        #endregion Sync Methods

        #region IDisposable

        ~SnppClientProtocol()
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