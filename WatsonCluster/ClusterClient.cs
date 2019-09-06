using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    internal class ClusterClient : IDisposable
    {
        #region Internal-Members

        internal int ReadStreamBufferSize
        {
            get
            {
                return _ReadStreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Read stream buffer size must be greater than zero.");
                _ReadStreamBufferSize = value;
            }
        }

        internal bool Debug = false;

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

        internal Func<Task> ClusterHealthy = null;

        internal Func<Task> ClusterUnhealthy = null;

        internal Func<long, Stream, Task> MessageReceived = null;

        internal bool Connected
        {
            get
            {
                if (_WtcpClient != null) return _WtcpClient.Connected;
                return false;
            }
        }

        #endregion Internal-Members

        #region Private-Members

        private int _ReadStreamBufferSize = 65536;
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

        internal async Task<bool> Send(byte[] data)
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
            return await Send(contentLength, stream);
        }

        internal async Task<bool> Send(long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpClient == null)
            {
                if (Debug) Log("Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                return await _WtcpClient.SendAsync(contentLength, stream);
            }
            else
            {
                if (Debug) Log("Client is not connected, cannot send");
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
                        if (Debug) Log("Attempting connection to " + _ServerIp + ":" + _ServerPort);

                        if (String.IsNullOrEmpty(_CertFile))
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort);
                        }
                        else
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort, _CertFile, _CertPass);
                        }

                        _WtcpClient.Debug = Debug;
                        _WtcpClient.ReadDataStream = false;
                        _WtcpClient.ReadStreamBufferSize = ReadStreamBufferSize;
                        _WtcpClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
                        _WtcpClient.MutuallyAuthenticate = MutuallyAuthenticate;

                        _WtcpClient.AuthenticationRequested = AuthenticationRequested;
                        _WtcpClient.AuthenticationSucceeded = AuthenticationSucceeded;
                        _WtcpClient.AuthenticationFailure = AuthenticationFailed;

                        _WtcpClient.ServerConnected = ServerConnected;
                        _WtcpClient.ServerDisconnected = ServerDisconnected;
                        _WtcpClient.StreamReceived = StreamReceived;

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

                        _WtcpClient.Debug = Debug;
                        _WtcpClient.ReadDataStream = false;
                        _WtcpClient.ReadStreamBufferSize = ReadStreamBufferSize;
                        _WtcpClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
                        _WtcpClient.MutuallyAuthenticate = MutuallyAuthenticate;

                        _WtcpClient.AuthenticationRequested = AuthenticationRequested;
                        _WtcpClient.AuthenticationSucceeded = AuthenticationSucceeded;
                        _WtcpClient.AuthenticationFailure = AuthenticationFailed;

                        _WtcpClient.ServerConnected = ServerConnected;
                        _WtcpClient.ServerDisconnected = ServerDisconnected;
                        _WtcpClient.StreamReceived = StreamReceived;

                        if (Debug) Log("Attempting reconnect to " + _ServerIp + ":" + _ServerPort);

                        _WtcpClient.Start();
                    }

                    Task.Delay(1000).Wait();
                }
                catch (SocketException)
                {
                    Log("Unable to connect to peer");
                }
                catch (Exception e)
                {
                    if (Debug)
                    {
                        LogException("MaintainConnection", e);
                    }
                }
            }
        }

        private void Log(string msg)
        {
            if (Debug) Console.WriteLine(msg);
        }

        private void LogException(string method, Exception e)
        {
            Log("");
            Log("An exception was encountered.");
            Log("   Method        : " + method);
            Log("   Type          : " + e.GetType().ToString());
            Log("   Data          : " + e.Data);
            Log("   Inner         : " + e.InnerException);
            Log("   Message       : " + e.Message);
            Log("   Source        : " + e.Source);
            Log("   StackTrace    : " + e.StackTrace);
            Log("");
        }

        private string AuthenticationRequested()
        {
            Log("Authentication requested");
            if (!String.IsNullOrEmpty(PresharedKey)) return PresharedKey;
            throw new AuthenticationException("Preshared key not specified or not valid.");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private async Task AuthenticationSucceeded()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Log("Authentication succeeded");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private async Task AuthenticationFailed()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            throw new AuthenticationException("Preshared key not specified or not valid.");
        }

        private async Task ServerConnected()
        {
            if (Debug) Log("Server " + _ServerIp + ":" + _ServerPort + " connected");
            if (ClusterHealthy != null) await ClusterHealthy();
        }

        private async Task ServerDisconnected()
        {
            if (Debug) Log("Server " + _ServerIp + ":" + _ServerPort + " disconnected");
            if (ClusterUnhealthy != null) await ClusterUnhealthy();
        }

        private async Task StreamReceived(long contentLength, Stream stream)
        {
            if (Debug)
            {
                Log("Stream received: " + contentLength + " bytes");
            }

            if (MessageReceived != null) await MessageReceived(contentLength, stream);
        }

        #endregion Private-Methods
    }
}