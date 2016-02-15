using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SNPPlib
{
    public class Client
    {
        private Regex CallerIdFormat = new Regex(@"^[0-9]+$", RegexOptions.Compiled);//is this numeric or alphanumeric?
        private Regex MessageFormat = new Regex(@"^[a-z0-9 ]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex PagerIdFormat = new Regex(@"^[0-9]+$", RegexOptions.Compiled);
        private Regex LoginIdFormat = new Regex(@"^[a-z0-9]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Client(IPAddress address, ushort port)
        {
            Address = address;
            Port = port;
        }

        public Client(string host, ushort port)
            : this(Dns.GetHostEntry(host).AddressList[0], port)
        {
            //What if there is more than one ip?
        }

        public enum AlertLevel : byte
        {
            DoNotAlert = 0,
            Alert = 1
        }

        public enum ServiceLevel : byte
        {
            Priority = 0,
            Normal = 1,//default priority if not specified for a set of commands
            FiveMinutes = 2,
            FifteenMinutes = 3,
            OneHour = 4,
            FourHours = 5,
            TwelveHours = 6,
            TwentyFourHours = 7,
            CarrierSpecific1 = 8,
            CarrierSpecific2 = 9,
            CarrierSpecific3 = 10,
            CarrierSpecific4 = 11
        }

        private IPAddress Address { get; set; }
        private ushort Port { get; set; }
        private Socket Socket { get; set; }

        //option to re-try technical failures?

        public async Task<Response> Alert(AlertLevel level)
        {
            var response = await Send(String.Format("ALER {0}", (byte)level));
            return response;
        }

        public async Task<Response> CallerId(string callerId)
        {
            if (callerId == null)
                throw new ArgumentNullException("callerId");
            if (!MessageFormat.IsMatch(callerId))
                throw new ArgumentException("Caller id must be numeric.", "callerId");

            var response = await Send(String.Format("CALL {0}", callerId));
            return response;
        }

        public async Task<bool> Connect()
        {
            var remote = new IPEndPoint(Address, Port);
            Socket = new Socket(remote.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);//Not sure we need this.
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            await Socket.ConnectTaskAsync(remote);

            var buffer = new byte[256];//TODO: Proper buffer sizes????????
            await Socket.ReceiveTaskAsync(buffer, 0, 256);
            var resp = new Response(Encoding.ASCII.GetString(buffer));//TODO: Response parser

            if (resp.Code == ResponseCode.GatewayReady)
                return true;
            await Socket.DisconnectTaskAsync(true);
            return false;
        }

        public async Task<Response> Coverage(ushort alertnateArea)
        {
            //are areas always numbers?
            var response = await Send(String.Format("COVE {0}", alertnateArea));
            return response;
        }

        public async Task<Response> Data(string[] message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            if (message.Length == 0)
                throw new ArgumentException("A message must contain at least one line of text.", "message");
            if (message.Any(_ => !MessageFormat.IsMatch(_)))
                throw new ArgumentException("Messages must be alphanumeric.", "message");

            var response = await Send("DATA");
            if (response.Code == ResponseCode.BeginInput)
            {
                foreach (var line in message)
                {
                    await Send(line);
                }
                response = await Send("\r\n.");
            }
            return response;
        }

        public async Task<bool> Disconnect()//Send a quit?
        {
            await Socket.DisconnectTaskAsync(true);
            return true;
        }

        public async Task<Response> Help()
        {
            var response = await Send("HELP");
            return response;
        }

        public async Task<Response> HoldUntil(DateTime time, TimeSpan? gmtDifference = null)
        {
            if (time == null)
                throw new ArgumentNullException("time");
            //Check if the date is in the past? I don't know if it matters.

            var gmt = gmtDifference.HasValue ? (gmtDifference < TimeSpan.Zero ? "-" : String.Empty) + gmtDifference.Value.ToString("hhmm") : String.Empty;
            var response = await Send(String.Format("HOLD {0} {1}", time.ToString("yyMMddHHmmss"), gmt).TrimEnd());
            return response;
        }

        public async Task<Response> Level(ServiceLevel level)
        {
            var response = await Send(String.Format("LEVE {0}", (byte)level));
            return response;
        }

        public async Task<Response> Login(string loginId, string password = null)
        {
            if (loginId == null)
                throw new ArgumentNullException("loginId");//alphanumeric or just numeric?
            if (!LoginIdFormat.IsMatch(loginId))
                throw new ArgumentException("Login ids must be alphanumeric.", "loginId");

            var response = await Send(String.Format("LOGI {0} {1}", loginId, password ?? String.Empty).TrimEnd());
            return response;
        }

        public async Task<Response> Message(string message)
        {
            //message should be alphanumeric
            //message is single line
            //if issued more than once before SEND should produce 503 ERROR, Message Already Entered
            if (message == null)
                throw new ArgumentNullException("message");
            if (!MessageFormat.IsMatch(message))
                throw new ArgumentException("Messages must be alphanumeric.", "message");

            var response = await Send(String.Format("MESS {0}", message));
            return response;
        }

        public async Task<Response> Pager(string pagerId, string password = null)//valid password chars?
        {
            if (pagerId == null)
                throw new ArgumentNullException("pagerId");//alphanumeric or just numeric?
            if (!PagerIdFormat.IsMatch(pagerId))
                throw new ArgumentException("Pager ids must be numeric.", "pagerId");

            var response = await Send(String.Format("PAGE {0} {1}", pagerId, password ?? String.Empty).TrimEnd());
            return response;
        }

        public async Task Quit()
        {
            var response = await Send("QUIT");
            //Does it matter whether or not they respond as expected? We should probably just disconnect anyway.
            //if (resp.Code == ResponseCode.Goodbye) ;

            await Socket.DisconnectTaskAsync(true);
        }

        public async Task<Response> Reset()
        {
            //Resets the state of the connection to as if it were freshly opened
            var response = await Send("RESE");
            return response;
        }

        public async Task<Response> Send()
        {
            var response = await Send("SEND");
            return response;
        }

        public async Task<Response> Subject(string messageSubject)
        {
            if (messageSubject == null)
                throw new ArgumentNullException("messageSubject");
            if (!MessageFormat.IsMatch(messageSubject))
                throw new ArgumentException("Subjects must be alphanumeric.", "messageSubject");

            var response = await Send(String.Format("SUBJ {0}", messageSubject));
            return response;
        }

        public async Task<Response> TwoWay()
        {
            //throw new NotImplementedException();

            var response = await Send("2WAY");
            return response;
        }

        private async Task<Response> Send(string command, int responseSize = 1024)
        {
            //check for crlf in command
            command = command + "\r\n";
            await Socket.SendTaskAsync(Encoding.ASCII.GetBytes(command), 0, Encoding.ASCII.GetByteCount(command));

            //var response = new StringBuilder();
            //var buffer = new byte[256];
            //do
            //{
            //    await Socket.ReceiveTaskAsync(buffer, 0, 256);
            //    response.Append(Encoding.ASCII.GetString(buffer));
            //}
            //while (response.ToString().IndexOf("\r\n") == -1);

            //return response.ToString();

            //handling multi-part responses? ResponseCode.MultiLineResponse
            //handling long responses?

            var buffer = new byte[responseSize];//TODO: Proper buffer sizes????????
            await Socket.ReceiveTaskAsync(buffer, 0, responseSize);
            var response = new Response(Encoding.ASCII.GetString(buffer));

            if (response.Code == ResponseCode.FatalError)
                await Socket.DisconnectTaskAsync(true);//Do we want to do anything to the response/throw?
            return response;
        }
    }
}