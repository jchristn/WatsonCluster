using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    internal class ClusterClient : IDisposable
    {
        #region Internal-Members

        internal int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Stream buffer size must be greater than zero.");
                _StreamBufferSize = value;
            }
        }
         
        internal bool AcceptInvalidCertificates = true; 
        internal bool MutuallyAuthenticate = false;

        internal string PresharedKey
        {
            get
            {
                return _PresharedKey;
            }
            set
            {
                if (!String.IsNullOrEmpty(value) && value.Length != 16) throw new ArgumentException("Preshared key must be exactly 16 bytes.");
                _PresharedKey = value;
            }
        }

        internal event EventHandler ClusterHealthy;
        internal event EventHandler ClusterUnhealthy;
        internal event EventHandler<MessageReceivedEventArgs> MessageReceived;

        internal bool Connected
        {
            get
            {
                if (_WtcpClient != null) return _WtcpClient.Connected;
                return false;
            }
        }
         
        internal Action<string> Logger = null;

        #endregion Internal-Members

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private string _ServerIp;
        private int _ServerPort;
        private string _CertFile;
        private string _CertPass;
        private string _PresharedKey;
        private WatsonTcpClient _WtcpClient = null;

        #endregion Private-Members

        #region Constructors-and-Factories

        internal ClusterClient(string serverIp, int serverPort, string certFile, string certPass)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < IPEndPoint.MinPort || serverPort > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(serverPort));

            _ServerIp = serverIp;
            _ServerPort = serverPort;
            _CertFile = certFile;
            _CertPass = certPass;
        }

        #endregion Constructors-and-Factories

        #region Internal-Methods

        internal void Start()
        {
            Task.Run(() => MaintainConnection());
        }

        internal bool Send(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(null, Encoding.UTF8.GetBytes(data));
        }

        internal bool Send(Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(metadata, Encoding.UTF8.GetBytes(data));
        }

        internal bool Send(byte[] data)
        { 
            return Send(null, data);
        }

        internal bool Send(Dictionary<object, object> metadata, byte[] data)
        { 
            MemoryStream stream = null;
            long contentLength = 0;

            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream(data);
                contentLength = data.Length;
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return Send(metadata, contentLength, stream);
        }

        internal bool Send(long contentLength, Stream stream)
        {
            return Send(null, contentLength, stream);
        }

        internal bool Send(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpClient == null)
            {
                Logger?.Invoke("[ClusterClient] Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                return _WtcpClient.Send(metadata, contentLength, stream);
            }
            else
            {
                Logger?.Invoke("[ClusterClient] Client is not connected, cannot send");
                return false;
            }
        }

        internal async Task<bool> SendAsync(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(null, Encoding.UTF8.GetBytes(data));
        }

        internal async Task<bool> SendAsync(Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(metadata, Encoding.UTF8.GetBytes(data));
        }

        internal async Task<bool> SendAsync(byte[] data)
        {
            return await SendAsync(null, data);
        }

        internal async Task<bool> SendAsync(Dictionary<object, object> metadata, byte[] data)
        {
            MemoryStream stream = null;
            long contentLength = 0;

            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream(data);
                contentLength = data.Length;
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return await SendAsync(metadata, contentLength, stream);
        }

        internal async Task<bool> SendAsync(long contentLength, Stream stream)
        {
            return await SendAsync(null, contentLength, stream);
        }

        internal async Task<bool> SendAsync(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpClient == null)
            {
                Logger?.Invoke("[ClusterClient] Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                return await _WtcpClient.SendAsync(metadata, contentLength, stream);
            }
            else
            {
                Logger?.Invoke("[ClusterClient] Client is not connected, cannot send");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion Internal-Methods

        #region Private-Methods

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_WtcpClient != null) _WtcpClient.Dispose();
            }
        }

        private void MaintainConnection()
        {
            while (true)
            {
                try
                {
                    if (_WtcpClient == null)
                    {
                        Logger?.Invoke("[ClusterClient] Attempting connection to " + _ServerIp + ":" + _ServerPort);

                        if (String.IsNullOrEmpty(_CertFile))
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort);
                        }
                        else
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort, _CertFile, _CertPass);
                        }

                        _WtcpClient.StreamBufferSize = StreamBufferSize;
                        _WtcpClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
                        _WtcpClient.MutuallyAuthenticate = MutuallyAuthenticate;

                        _WtcpClient.AuthenticationRequested = AuthenticationRequested;
                        _WtcpClient.AuthenticationSucceeded += AuthenticationSucceeded;
                        _WtcpClient.AuthenticationFailure += AuthenticationFailed;

                        _WtcpClient.ServerConnected += ServerConnected;
                        _WtcpClient.ServerDisconnected += ServerDisconnected;
                        _WtcpClient.StreamReceived += StreamReceived;

                        _WtcpClient.Start();
                    }
                    else if (!_WtcpClient.Connected)
                    {
                        if (String.IsNullOrEmpty(_CertFile))
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort);
                        }
                        else
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort, _CertFile, _CertPass);
                        }
                         
                        _WtcpClient.StreamBufferSize = StreamBufferSize;
                        _WtcpClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
                        _WtcpClient.MutuallyAuthenticate = MutuallyAuthenticate;

                        _WtcpClient.AuthenticationRequested = AuthenticationRequested;
                        _WtcpClient.AuthenticationSucceeded += AuthenticationSucceeded;
                        _WtcpClient.AuthenticationFailure += AuthenticationFailed;

                        _WtcpClient.ServerConnected += ServerConnected;
                        _WtcpClient.ServerDisconnected += ServerDisconnected;
                        _WtcpClient.StreamReceived += StreamReceived;

                        Logger?.Invoke("[ClusterClient] Attempting reconnection to " + _ServerIp + ":" + _ServerPort);

                        _WtcpClient.Start();
                    }

                    Task.Delay(1000).Wait();
                }
                catch (SocketException)
                {
                    Logger?.Invoke("[ClusterClient] Unable to connect to " + _ServerIp + ":" + _ServerPort); 
                }
                catch (Exception e)
                {
                    Logger?.Invoke("[ClusterClient] Exception while maintaining connection: " + e.ToString());
                }
            }
        }
         
        private string AuthenticationRequested()
        {
            if (!String.IsNullOrEmpty(PresharedKey))
            {
                Logger?.Invoke("[ClusterClient] Authentication requested by server, sending pre-shared key");
                return PresharedKey;
            }
            Logger?.Invoke("[ClusterClient] Authentication requested by server but no pre-shared key set");
            throw new AuthenticationException("Preshared key not specified or not valid.");
        }
         
        private void AuthenticationSucceeded(object sender, EventArgs args)
        {
            Logger?.Invoke("[ClusterClient] Authentication succeeded");
        }
         
        private void AuthenticationFailed(object sender, EventArgs args) 
        {
            Logger?.Invoke("[ClusterClient] Authentication failed");
            throw new AuthenticationException("Preshared key not specified or not valid.");
        }

        private void ServerConnected(object sender, EventArgs args)
        {
            Logger?.Invoke("[ClusterClient] Connected to server " + _ServerIp + ":" + _ServerPort);
            ClusterHealthy?.Invoke(this, EventArgs.Empty);
        }

        private void ServerDisconnected(object sender, EventArgs args)
        {
            Logger?.Invoke("[ClusterClient] Disconnected from server " + _ServerIp + ":" + _ServerPort);
            ClusterUnhealthy?.Invoke(this, EventArgs.Empty);
        }

        private void StreamReceived(object sender, StreamReceivedFromServerEventArgs args)
        {
            Logger?.Invoke("[ClusterClient] Stream received from server: " + args.ContentLength + " bytes");
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(args.Metadata, args.ContentLength, args.DataStream));
        }

        #endregion Private-Methods
    }
}