using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    /// <summary>
    /// The Watson cluster server node (receives connections from clients).  Use ClusterNode, which encapsulates this class.
    /// </summary>
    internal class ClusterServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable full reading of input streams.  
        /// When enabled, use MessageReceived and Send(string ipPort, byte[] data).  
        /// When disabled, use StreamReceived and Send(string ipPort, long contentLength, Stream stream).
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
        /// Method to call when a client connects.  The client IP:port is passed to the method, and a response of true is expected.
        /// </summary>
        internal Func<string, bool> ClientConnected = null;

        /// <summary>
        /// Method to call when a client disconnects.  The client IP:port is passed to the method, and a response of true is expected.
        /// </summary>
        internal Func<string, bool> ClientDisconnected = null;

        /// <summary>
        /// Method to call when a message is received.  The client IP:port is passed to the method, along with a byte array containing the data, and a response of true is expected.
        /// Only use when ReadDataStream = true.
        /// </summary>
        internal Func<string, byte[], bool> MessageReceived = null;

        /// <summary>
        /// Method to call when a message is received.  The client IP:port is passed to the method, the number of bytes to read, the stream from which data should be read, and a response of true is expected.
        /// Only use when ReadDataStream = false.
        /// </summary>
        internal Func<string, long, Stream, bool> StreamReceived = null;

        #endregion

        #region Private-Members

        private int _ReadStreamBufferSize = 65536;
        private string _ListenerIp;
        private int _ListenerPort;
        private List<string> _PermittedIps;
        private string _CertFile;
        private string _CertPass;
        private string _PresharedKey;
        private WatsonTcpServer _WtcpServer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the cluster server.  Call .Start() to start.
        /// </summary>
        /// <param name="listenerIp">The IP address on which to listen.  If null, Watson will attempt to listen on any IP address.</param>
        /// <param name="listenerPort">The TCP port on which the cluster server should listen.</param>
        /// <param name="peerIp">The IP address of the peer client.</param>
        /// <param name="certFile">The SSL certificate filename, if any (PFX file format).  Leave null for non-SSL.</param>
        /// <param name="certPass">The SSL certificate file password, if any.</param> 
        internal ClusterServer(string listenerIp, int listenerPort, string peerIp, string certFile, string certPass)
        {
            if (String.IsNullOrEmpty(listenerIp)) listenerIp = "+";
            if (String.IsNullOrEmpty(peerIp)) throw new ArgumentNullException(nameof(peerIp));
            if (listenerPort < IPEndPoint.MinPort || listenerPort > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(listenerPort));

            _ListenerIp = listenerIp;
            _ListenerPort = listenerPort;
            _CertFile = certFile;
            _CertPass = certPass; 

            List<string> permittedIps = new List<string>();
            permittedIps.Add(peerIp); 
        }

        /// <summary>
        /// Initialize the cluster server.  Call .Start() to start.
        /// </summary>
        /// <param name="listenerIp">The IP address on which to listen.  If null, Watson will attempt to listen on any IP address.</param>
        /// <param name="listenerPort">The TCP port on which the cluster server should listen.</param>
        /// <param name="permittedIps">The list of IP addresses allowed to connect.</param>
        /// <param name="certFile">The SSL certificate filename, if any (PFX file format).  Leave null for non-SSL.</param>
        /// <param name="certPass">The SSL certificate file password, if any.</param> 
        internal ClusterServer(string listenerIp, int listenerPort, IEnumerable<string> permittedIps, string certFile, string certPass)
        {
            if (String.IsNullOrEmpty(listenerIp)) listenerIp = "+";
            if (permittedIps == null) throw new ArgumentNullException(nameof(permittedIps));
            if (listenerPort < IPEndPoint.MinPort || listenerPort > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(listenerPort));

            _ListenerIp = listenerIp;
            _ListenerPort = listenerPort;
            _PermittedIps = new List<string>(permittedIps);
            _CertFile = certFile;
            _CertPass = certPass; 
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Determines if a specified client is connected to the local server.
        /// </summary>
        /// <returns>True if connected.</returns>
        internal bool IsConnected(string ipPort)
        {
            if (_WtcpServer == null)
            {
                if (Debug) Console.WriteLine("Server is null");
                return false;
            }
            if (String.IsNullOrEmpty(ipPort)) return false;
            return _WtcpServer.IsClientConnected(ipPort);
        }

        /// <summary>
        /// Start the cluster server.
        /// </summary>
        internal void Start()
        {
            if (String.IsNullOrEmpty(_CertFile))
            {
                _WtcpServer = new WatsonTcpServer(_ListenerIp, _ListenerPort);
            }
            else
            {
                _WtcpServer = new WatsonTcpServer(_ListenerIp, _ListenerPort, _CertFile, _CertPass);
            }

            _WtcpServer.Debug = Debug;
            _WtcpServer.ReadDataStream = ReadDataStream;
            _WtcpServer.ReadStreamBufferSize = ReadStreamBufferSize;
            _WtcpServer.AcceptInvalidCertificates = AcceptInvalidCertificates;
            _WtcpServer.MutuallyAuthenticate = MutuallyAuthenticate;
            _WtcpServer.PresharedKey = PresharedKey;

            _WtcpServer.ClientConnected = ClientConnect;
            _WtcpServer.ClientDisconnected = ClientDisconnect;
            _WtcpServer.MessageReceived = MsgReceived;
            _WtcpServer.StreamReceived = StrmReceived;

            _WtcpServer.Start();
        }

        /// <summary>
        /// Send a message to the specified client.
        /// </summary>
        /// <param name="ipPort">The IP:port of the client.</param>
        /// <param name="data">Data to send to the client.</param>
        /// <returns>True if successful.</returns>
        internal bool Send(string ipPort, byte[] data)
        {
            if (_WtcpServer == null)
            {
                if (Debug) Console.WriteLine("Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                _WtcpServer.Send(ipPort, data);
                return true;
            }
            else
            {
                if (Debug) Console.WriteLine("Server is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Send a message to the specified client using a stream.
        /// </summary>
        /// <param name="ipPort">The IP:port of the client.</param>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        internal bool Send(string ipPort, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpServer == null)
            {
                if (Debug) Console.WriteLine("Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                return _WtcpServer.Send(ipPort, contentLength, stream);
            }
            else
            {
                if (Debug) Console.WriteLine("Server is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Send a message to the specified client, asynchronously.
        /// </summary>
        /// <param name="ipPort">The IP:port of the client.</param>
        /// <param name="data">Data to send to the client.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        internal async Task<bool> SendAsync(string ipPort, byte[] data)
        {
            if (_WtcpServer == null)
            {
                if (Debug) Console.WriteLine("Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                await _WtcpServer.SendAsync(ipPort, data);
                return true;
            }
            else
            {
                if (Debug) Console.WriteLine("Server is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Send a message to the specified client, asynchronously, using a stream.
        /// </summary>
        /// <param name="ipPort">The IP:port of the client.</param>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        internal async Task<bool> SendAsync(string ipPort, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpServer == null)
            {
                if (Debug) Console.WriteLine("Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                return await _WtcpServer.SendAsync(ipPort, contentLength, stream);
            }
            else
            {
                if (Debug) Console.WriteLine("Server is not connected, cannot send");
                return false;
            }
        }

        /// <summary>
        /// Destroy the server and release resources.
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
                if (_WtcpServer != null) _WtcpServer.Dispose();
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

        private bool ClientConnect(string ipPort)
        {
            if (Debug) Console.WriteLine("Client " + ipPort + " connected");
            return ClientConnected(ipPort);
        }

        private bool ClientDisconnect(string ipPort)
        {
            if (Debug) Console.WriteLine("Client " + ipPort + " disconnected");
            return ClientDisconnected(ipPort);
        }

        private bool MsgReceived(string ipPort, byte[] data)
        {
            if (Debug)
            {
                if (data != null && data.Length > 0)
                {
                    Console.WriteLine("Message received from " + ipPort + ": " + data.Length + " bytes");
                }
            }

            return MessageReceived(ipPort, data);
        }

        private bool StrmReceived(string ipPort, long contentLength, Stream stream)
        {
            if (Debug)
            {
                Console.WriteLine("Stream received from " + ipPort + ": " + contentLength + " bytes");
            }

            return StreamReceived(ipPort, contentLength, stream);
        }

        #endregion
    }
}
