using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    /// <summary>
    /// The Watson cluster client node (initiates connections to server).  Use ClusterNode, which encapsulates this class.
    /// </summary>
    internal class ClusterClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable full reading of input streams.
        /// When enabled, use MessageReceived and Send(byte[] data).  
        /// When disabled, use StreamReceived and Send(long contentLength, Stream stream).
        /// </summary>
        internal bool ReadDataStream = true;

        /// <summary>
        /// Buffer size to use when reading input and output streams.  Default is 65536.
        /// </summary>
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

        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        internal bool Debug = false;

        /// <summary>
        /// Accept SSL certificates that are expired or unable to be validated.
        /// </summary>
        internal bool AcceptInvalidCertificates = true;

        /// <summary>
        /// Enable or disable mutual authentication with SSL.
        /// </summary>
        internal bool MutuallyAuthenticate = false;

        /// <summary>
        /// Preshared key for TCP authentication.
        /// </summary>
        public string PresharedKey
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

        /// <summary>
        /// Method to call when the cluster is healthy.
        /// </summary>
        internal Func<bool> ClusterHealthy = null;

        /// <summary>
        /// Method to call when the cluster is unhealthy.
        /// </summary
        internal Func<bool> ClusterUnhealthy = null;

        /// <summary>
        /// Method to call when a message is received.  Only use when ReadDataStream = true.
        /// </summary>
        internal Func<byte[], bool> MessageReceived = null;

        /// <summary>
        /// Method to call when a stream is received.  Only use when ReadDataStream = false.
        /// </summary>
        internal Func<long, Stream, bool> StreamReceived = null;

        /// <summary>
        /// Determine if the cluster client is connected to the server.
        /// </summary>
        internal bool Connected
        {
            get
            {
                if (_WtcpClient != null) return _WtcpClient.Connected;
                return false;
            }
        }

        #endregion

        #region Private-Members

        private int _ReadStreamBufferSize = 65536;
        private string _ServerIp;
        private int _ServerPort;
        private string _CertFile;
        private string _CertPass;
        private string _PresharedKey;
        private WatsonTcpClient _WtcpClient = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the cluster client.  Call .Start() to start.
        /// </summary>
        /// <param name="serverIp">The IP address of the peer server.</param>
        /// <param name="serverPort">The TCP port of the peer server.</param> 
        /// <param name="certFile">The SSL certificate filename, if any (PFX file format).  Leave null for non-SSL.</param>
        /// <param name="certPass">The SSL certificate file password, if any.</param>
        internal ClusterClient(string serverIp, int serverPort, string certFile, string certPass)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < IPEndPoint.MinPort || serverPort > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(serverPort)); 

            _ServerIp = serverIp;
            _ServerPort = serverPort;
            _CertFile = certFile;
            _CertPass = certPass; 
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the cluster client.
        /// </summary>
        internal void Start()
        {
            Task.Run(() => MaintainConnection());
        }

        /// <summary>
        /// Send a message to the connected server.
        /// </summary>
        /// <param name="data">Data to send to the server.</param>
        /// <returns>True if successful.</returns>
        internal bool Send(byte[] data)
        {
            if (_WtcpClient == null)
            {
                if (Debug) Console.WriteLine("Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                _WtcpClient.Send(data);
                return true;
            }
            else
            {
                if (Debug) Console.WriteLine("Client is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Send a message to the connected server using a stream.
        /// </summary>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        internal bool Send(long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpClient == null)
            {
                if (Debug) Console.WriteLine("Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                return _WtcpClient.Send(contentLength, stream);
            }
            else
            {
                if (Debug) Console.WriteLine("Client is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Send a message to the server, asynchronously.
        /// </summary>
        /// <param name="data">Data to send to the server.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        internal async Task<bool> SendAsync(byte[] data)
        {
            if (_WtcpClient == null)
            {
                if (Debug) Console.WriteLine("Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                await _WtcpClient.SendAsync(data);
                return true;
            }
            else
            {
                if (Debug) Console.WriteLine("Client is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Send a message to the server, asynchronously, using a stream.
        /// </summary>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        internal async Task<bool> SendAsync(long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpClient == null)
            {
                if (Debug) Console.WriteLine("Client is null, cannot send");
                return false;
            }

            if (_WtcpClient.Connected)
            {
                return await _WtcpClient.SendAsync(contentLength, stream);
            }
            else
            {
                if (Debug) Console.WriteLine("Client is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Destroy the client and release resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

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
                        if (Debug) Console.WriteLine("Attempting connection to " + _ServerIp + ":" + _ServerPort);

                        if (String.IsNullOrEmpty(_CertFile))
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort);
                        }
                        else
                        {
                            _WtcpClient = new WatsonTcpClient(_ServerIp, _ServerPort, _CertFile, _CertPass);
                        }

                        _WtcpClient.Debug = Debug;
                        _WtcpClient.ReadDataStream = ReadDataStream;
                        _WtcpClient.ReadStreamBufferSize = ReadStreamBufferSize;
                        _WtcpClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
                        _WtcpClient.MutuallyAuthenticate = MutuallyAuthenticate;

                        _WtcpClient.AuthenticationRequested = AuthenticationRequested;
                        _WtcpClient.AuthenticationSucceeded = AuthenticationSucceeded;
                        _WtcpClient.AuthenticationFailure = AuthenticationFailed;

                        _WtcpClient.ServerConnected = ServerConnected;
                        _WtcpClient.ServerDisconnected = ServerDisconnected;
                        _WtcpClient.MessageReceived = MsgReceived;
                        _WtcpClient.StreamReceived = StrmReceived;

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
                        _WtcpClient.ReadDataStream = ReadDataStream;
                        _WtcpClient.ReadStreamBufferSize = ReadStreamBufferSize;
                        _WtcpClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
                        _WtcpClient.MutuallyAuthenticate = MutuallyAuthenticate;

                        _WtcpClient.AuthenticationRequested = AuthenticationRequested;
                        _WtcpClient.AuthenticationSucceeded = AuthenticationSucceeded;
                        _WtcpClient.AuthenticationFailure = AuthenticationFailed;

                        _WtcpClient.ServerConnected = ServerConnected;
                        _WtcpClient.ServerDisconnected = ServerDisconnected;
                        _WtcpClient.MessageReceived = MsgReceived;
                        _WtcpClient.StreamReceived = StrmReceived; 

                        if (Debug) Console.WriteLine("Attempting reconnect to " + _ServerIp + ":" + _ServerPort);

                        _WtcpClient.Start();
                    }

                    Task.Delay(1000).Wait();
                }
                catch (Exception e)
                {
                    if (Debug)
                    {
                        LogException(e);
                    }
                }
            }
        }

        private void LogException(Exception e)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine(" = Exception Type: " + e.GetType().ToString());
            Console.WriteLine(" = Exception Data: " + e.Data);
            Console.WriteLine(" = Inner Exception: " + e.InnerException);
            Console.WriteLine(" = Exception Message: " + e.Message);
            Console.WriteLine(" = Exception Source: " + e.Source);
            Console.WriteLine(" = Exception StackTrace: " + e.StackTrace);
            Console.WriteLine("================================================================================");
        }

        private string AuthenticationRequested()
        {
            Console.WriteLine("Authentication requested");
            if (!String.IsNullOrEmpty(PresharedKey)) return PresharedKey;
            throw new AuthenticationException("Preshared key not specified or not valid.");
        }

        private bool AuthenticationSucceeded()
        {
            Console.WriteLine("Authentication succeeded");
            return true;
        }

        private bool AuthenticationFailed()
        {
            throw new AuthenticationException("Preshared key not specified or not valid.");
        }

        private bool ServerConnected()
        {
            if (Debug) Console.WriteLine("Server " + _ServerIp + ":" + _ServerPort + " connected");
            if (ClusterHealthy != null) ClusterHealthy();
            return true;
        }

        private bool ServerDisconnected()
        {
            if (Debug) Console.WriteLine("Server " + _ServerIp + ":" + _ServerPort + " disconnected");
            if (ClusterUnhealthy != null) ClusterUnhealthy();
            return true;
        }

        private bool MsgReceived(byte[] data)
        {
            if (Debug)
            {
                if (data != null && data.Length > 0)
                {
                    Console.WriteLine("Message received: " + data.Length + " bytes");
                }
            }

            if (MessageReceived != null) return MessageReceived(data);
            else return false;
        }

        private bool StrmReceived(long contentLength, Stream stream)
        {
            if (Debug)
            {
                Console.WriteLine("Stream received: " + contentLength + " bytes");
            }

            if (StreamReceived != null) return StreamReceived(contentLength, stream);
            else return false;
        }

        #endregion
    }
}
