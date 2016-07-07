using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SNPPlib.Config;
using SNPPlib.Extensions;

namespace SNPPlib
{
    //TODO: Ignore "unsupported method error" option?
    //TODO: Do we need our own exception types? Would that make more sense?
    public class SnppClient : IDisposable
    {
        #region Constructors

        /// <summary>
        /// Create a SnppClient object pointing to a given IPAddress and port.
        /// </summary>
        /// <param name="address">The IPAddress of the server.</param>
        /// <param name="port">The port of the server.</param>
        /// <param name="loginId">The login id of the server.</param>
        /// <param name="password">The password of the server.</param>
        public SnppClient(IPAddress address, ushort port, string loginId = null, string password = null)
        {
            Client = new SnppClientProtocol(address, port);
            LoginId = loginId;
            Password = password;
        }

        /// <summary>
        /// Create a SnppClient object pointing to a given host name and port.
        /// </summary>
        /// <param name="host">The host name of the server.</param>
        /// <param name="port">The port of the server.</param>
        /// <param name="loginId">The login id of the server.</param>
        /// <param name="password">The password of the server.</param>
        public SnppClient(string host, ushort port, string loginId = null, string password = null)
        {
            Client = new SnppClientProtocol(host, port);
            LoginId = loginId;
            Password = password;
        }

        /// <summary>
        /// Create a SnppClient object pointing to a configured server.
        /// </summary>
        /// <param name="name">The name of the configured server or nothing if there is one unnamed server configured.</param>
        public SnppClient(string name = null)
        {
            Client = new SnppClientProtocol(name);
            LoginId = SnppConfig.GetLoginId(name);
            Password = SnppConfig.GetPassword(name);
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// The IPAddress of the server.
        /// </summary>
        public IPAddress Address
        {
            get
            {
                return Client.Address;
            }
            //settable?
        }

        /// <summary>
        /// The host name of the server.
        /// </summary>
        public string Host
        {
            get
            {
                return Client.Host;
            }
            //settable?
        }

        /// <summary>
        /// The port of the server.
        /// </summary>
        public ushort Port
        {
            get
            {
                return Client.Port;
            }
            //settable?
        }

        /// <summary>
        /// The login id of the server.
        /// </summary>
        public string LoginId { get; set; }

        /// <summary>
        /// The password of the server.
        /// </summary>
        public string Password { private get; set; }

        private SnppClientProtocol Client { get; set; }

        #endregion Properties

        #region Async Methods

        /// <summary>
        /// Send a SnppMessage asynchronously.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The response from the last command sent.  If an error occurs the operation will be aborted and the error response will be returned.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="message"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Message"/> was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Pagers"/> contained no elements.</exception>
        public async Task<SnppResponse> SendAsync(SnppMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            if (String.IsNullOrWhiteSpace(message.Message))
                throw new ArgumentException(Resource.MessageRequired, "message");
            if (!message.Pagers.Pagers.Any())
                throw new ArgumentException(Resource.PagerRequired, "pagers");

            //This is super gross.
            //Should we throw instead of returning the response? I don't know if we should consider that exceptional.
            try
            {
                SnppResponse resp;

                if (!await Client.ConnectAsync())
                    return SnppResponse.FatalResponse(Resource.ConnectionError);//Throw instead?

                if (!String.IsNullOrWhiteSpace(LoginId))
                {
                    resp = await Client.LoginAsync(LoginId, Password);//TODO: What if password is empty or whitespace?
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                foreach (var pager in message.Pagers.Pagers)
                {
                    resp = (await Client.PagerAsync(pager));
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                if (!String.IsNullOrWhiteSpace(message.Subject))
                {
                    resp = await Client.SubjectAsync(message.Subject);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                if (message.Data.Count > 1)
                {
                    resp = await Client.DataAsync(message.Data);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }
                else
                {
                    resp = await Client.MessageAsync(message.Message);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                if (message.ServiceLevel.HasValue)
                {
                    resp = await Client.LevelAsync(message.ServiceLevel.Value);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                return (await Client.SendAsync());
            }
            finally
            {
                Client.QuitAsync().Forget();//Not sure how well this will work.
            }
        }

        /// <summary>
        /// Try to send a SnppMessage asynchronously.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>True if no errors occured, otherwise false.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="message"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Message"/> was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Pagers"/> contained no elements.</exception>
        public async Task<bool> TrySendAsync(SnppMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            if (String.IsNullOrWhiteSpace(message.Message))
                throw new ArgumentException(Resource.MessageRequired, "message");
            if (!message.Pagers.Pagers.Any())
                throw new ArgumentException(Resource.PagerRequired, "pagers");

            try
            {
                return (await SendAsync(message)).Code == ResponseCode.Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion Async Methods

        #region Sync Methods

        /// <summary>
        /// Send a SnppMessage.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The response from the last command sent.  If an error occurs the operation will be aborted and the error response will be returned.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="message"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Message"/> was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Pagers"/> contained no elements.</exception>
        public SnppResponse Send(SnppMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            if (String.IsNullOrWhiteSpace(message.Message))
                throw new ArgumentException(Resource.MessageRequired, "message");
            if (!message.Pagers.Pagers.Any())
                throw new ArgumentException(Resource.PagerRequired, "pagers");

            //This is super gross.
            //Should we throw instead of returning the response? I don't know if we should consider that exceptional.
            try
            {
                SnppResponse resp;

                if (!Client.Connect())
                    return SnppResponse.FatalResponse(Resource.ConnectionError);//Throw instead?

                if (!String.IsNullOrWhiteSpace(LoginId))
                {
                    resp = Client.Login(LoginId, Password);//TODO: What if password is empty or whitespace?
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                foreach (var pager in message.Pagers.Pagers)
                {
                    resp = Client.Pager(pager);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                if (!String.IsNullOrWhiteSpace(message.Subject))
                {
                    resp = Client.Subject(message.Subject);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                if (message.Data.Count > 1)
                {
                    resp = Client.Data(message.Data);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }
                else
                {
                    resp = Client.Message(message.Message);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                if (message.ServiceLevel.HasValue)
                {
                    resp = Client.Level(message.ServiceLevel.Value);
                    if (resp.Code != ResponseCode.Success)
                        return resp;
                }

                return (Client.Send());
            }
            finally
            {
                Client.Quit();//Not sure how well this will work.
            }
        }

        /// <summary>
        /// Try to send a SnppMessage.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>True if no errors occured, otherwise false.</returns>
        /// <exception cref="System.ArgumentNullException">The <paramref name="message"/> parameter was null.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Message"/> was empty.</exception>
        /// <exception cref="System.ArgumentException">The <paramref name="message"/> parameter <see cref="SnppMessage.Pagers"/> contained no elements.</exception>
        public bool TrySend(SnppMessage message)
        {
            if (message == null)
                throw new ArgumentNullException("message");
            if (String.IsNullOrWhiteSpace(message.Message))
                throw new ArgumentException(Resource.MessageRequired, "message");
            if (!message.Pagers.Pagers.Any())
                throw new ArgumentException(Resource.PagerRequired, "pagers");

            try
            {
                return Send(message).Code == ResponseCode.Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion Sync Methods

        #region IDisposable

        ~SnppClient()
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
                if (Client != null)
                {
                    Client.Dispose();
                }
            }
        }

        #endregion IDisposable
    }
}