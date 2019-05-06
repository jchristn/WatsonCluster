using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonCluster
{
    /// <summary>
    /// A Watson cluster node, which includes both a cluster server and client.
    /// </summary>
    public class ClusterNode : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable full reading of input streams.  When enabled, use MessageReceived.  When disabled, use StreamReceived.
        /// </summary>
        public bool ReadDataStream = true;

        /// <summary>
        /// Buffer size to use when reading input and output streams.  Default is 65536.
        /// </summary>
        public int ReadStreamBufferSize
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
        public bool Debug = false;

        /// <summary>
        /// Accept SSL certificates that are expired or unable to be validated.
        /// </summary>
        public bool AcceptInvalidCertificates = true;

        /// <summary>
        /// Enable or disable mutual authentication with SSL.
        /// </summary>
        public bool MutuallyAuthenticate = false;

        /// <summary>
        /// Method to call when the cluster is healthy.
        /// </summary>
        public Func<bool> ClusterHealthy;

        /// <summary>
        /// Method to call when the cluster is unhealthy.
        /// </summary>
        public Func<bool> ClusterUnhealthy = null;

        /// <summary>
        /// Method to call when a message is received.  Only use when ReadDataStream = true.
        /// </summary>
        public Func<byte[], bool> MessageReceived = null;

        /// <summary>
        /// Method to call when a stream is received.  Only use when ReadDataStream = false.
        /// </summary>
        public Func<long, Stream, bool> StreamReceived = null;

        /// <summary>
        /// Determine if the cluster is healthy (i.e. both nodes are bidirectionally connected).
        /// </summary>
        /// <returns>True if healthy.</returns>
        public bool IsHealthy
        {
            get
            {
                if (_ClusterServer == null)
                {
                    if (Debug) Console.WriteLine("Server object is null");
                    return false;
                }

                if (_ClusterClient == null)
                {
                    if (Debug) Console.WriteLine("Client object is null");
                    return false;
                }

                if (String.IsNullOrEmpty(_CurrPeerIpPort))
                {
                    if (Debug) Console.WriteLine("Peer information is null");
                    return false;
                }

                if (!_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    if (Debug) Console.WriteLine("Server peer is not connected");
                    return false;
                }

                if (_ClusterClient.Connected)
                {
                    if (Debug) Console.WriteLine("Client is not connected");
                    return true;
                }

                return false;

            }
        }

        #endregion

        #region Private-Members

        private int _ReadStreamBufferSize = 65536;
        private string _ListenerIp;
        private int _ListenerPort;
        private string _PeerIp;
        private int _PeerPort;
        private List<string> _PermittedIps;
        private string _CertFile;
        private string _CertPass; 
        private ClusterServer _ClusterServer;
        private ClusterClient _ClusterClient;

        private string _CurrPeerIpPort;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the cluster node.  Call .Start() to start.
        /// </summary>
        /// <param name="listenerIp">The IP address on which to listen.  If null, Watson will attempt to listen on any IP address.</param>
        /// <param name="listenerPort">The TCP port on which the cluster server should listen.</param> 
        /// <param name="peerIp">The IP address of the peer cluster node.</param>
        /// <param name="peerPort">The TCP port of the peer cluster node.</param>
        /// <param name="certFile">The SSL certificate filename, if any (PFX file format).  Leave null for non-SSL.</param>
        /// <param name="certPass">The SSL certificate file password, if any.</param> 
        public ClusterNode(
            string listenerIp,
            int listenerPort,
            string peerIp,
            int peerPort,
            string certFile,
            string certPass)
        {
            if (String.IsNullOrEmpty(peerIp)) throw new ArgumentNullException(nameof(peerIp));
            if (peerPort < 1) throw new ArgumentOutOfRangeException(nameof(peerPort));
            if (listenerPort < 1) throw new ArgumentOutOfRangeException(nameof(listenerPort));

            _ListenerIp = listenerIp;
            _ListenerPort = listenerPort;
            _PeerIp = peerIp;
            _PeerPort = peerPort;
            _CertFile = certFile;
            _CertPass = certPass; 
        }

        /// <summary>
        /// Initialize the cluster node.  Call .Start() to start.
        /// </summary>
        /// <param name="listenerIp">The IP address on which to listen.  If null, Watson will attempt to listen on any IP address.</param>
        /// <param name="listenerPort">The TCP port on which the cluster server should listen.</param>
        /// <param name="peerIp">The IP address of the peer cluster node.</param>
        /// <param name="peerPort">The TCP port of the peer cluster node.</param>
        /// <param name="permittedIps">The list of IP addresses allowed to connect.</param> 
        /// <param name="certFile">The SSL certificate filename, if any (PFX file format).  Leave null for non-SSL.</param>
        /// <param name="certPass">The SSL certificate file password, if any.</param> 
        public ClusterNode(
            string listenerIp,
            int listenerPort,
            string peerIp,
            int peerPort,
            IEnumerable<string> permittedIps,
            string certFile,
            string certPass)
        {
            if (String.IsNullOrEmpty(listenerIp)) listenerIp = "+";
            if (String.IsNullOrEmpty(peerIp)) throw new ArgumentNullException(nameof(peerIp));
            if (peerPort < 1) throw new ArgumentOutOfRangeException(nameof(peerPort));
            if (listenerPort < 1) throw new ArgumentOutOfRangeException(nameof(listenerPort));

            _ListenerIp = listenerIp;
            _ListenerPort = listenerPort;
            _PeerIp = peerIp;
            _PeerPort = peerPort;
            _CertFile = certFile;
            _CertPass = certPass; 

            _PermittedIps = null;
            if (permittedIps != null && permittedIps.Count() > 0) permittedIps = new List<string>(permittedIps);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the cluster node.
        /// </summary>
        public void Start()
        {
            if (_PermittedIps == null)
            {
                _ClusterServer = new ClusterServer(_ListenerIp, _ListenerPort, _PeerIp, _CertFile, _CertPass);
            }
            else
            {
                _ClusterServer = new ClusterServer(_ListenerIp, _ListenerPort, _PermittedIps, _CertFile, _CertPass);
            }

            _ClusterServer.AcceptInvalidCertificates = AcceptInvalidCertificates;
            _ClusterServer.MutuallyAuthenticate = MutuallyAuthenticate;
            _ClusterServer.Debug = Debug;
            _ClusterServer.ReadDataStream = ReadDataStream;
            _ClusterServer.ReadStreamBufferSize = ReadStreamBufferSize;
            _ClusterServer.ClientConnected = SrvClientConnect;
            _ClusterServer.ClientDisconnected = SrvClientDisconnect;
            _ClusterServer.MessageReceived = SrvMsgReceived;
            _ClusterServer.StreamReceived = SrvStrmReceived;
            _ClusterServer.Start();

            _ClusterClient = new ClusterClient(_PeerIp, _PeerPort, _CertFile, _CertPass);
            _ClusterClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
            _ClusterClient.MutuallyAuthenticate = MutuallyAuthenticate;
            _ClusterClient.Debug = Debug;
            _ClusterClient.ReadDataStream = ReadDataStream;
            _ClusterClient.ReadStreamBufferSize = ReadStreamBufferSize;
            _ClusterClient.ClusterHealthy = CliServerConnect;
            _ClusterClient.ClusterUnhealthy = CliServerDisconnect;
            _ClusterClient.MessageReceived = CliMsgReceived;
            _ClusterClient.StreamReceived = CliStrmReceived;
            _ClusterClient.Start();
        }

        /// <summary>
        /// Send a message to the peer node.
        /// </summary>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public bool Send(byte[] data)
        {
            if (_ClusterClient != null)
            {
                if (_ClusterClient.Connected)
                {
                    return _ClusterClient.Send(data);
                }
            }
            else if (String.IsNullOrEmpty(_CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("No peer connected");
                return false;
            }
            else if (_ClusterServer != null)
            {
                if (_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    return _ClusterServer.Send(_CurrPeerIpPort, data);
                }
            }

            if (Debug) Console.WriteLine("Neither server or client are healthy");
            return false;
        }

        /// <summary>
        /// Send a message to the peer node using a stream.
        /// </summary>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_ClusterClient != null)
            {
                if (_ClusterClient.Connected)
                {
                    return _ClusterClient.Send(contentLength, stream);
                }
            }
            else if (String.IsNullOrEmpty(_CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("No peer connected");
                return false;
            }
            else if (_ClusterServer != null)
            {
                if (_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    return _ClusterServer.Send(_CurrPeerIpPort, contentLength, stream);
                }
            }

            if (Debug) Console.WriteLine("Neither server or client are healthy");
            return false;
        }

        /// <summary>
        /// Send a message to the peer node, asynchronously.
        /// </summary>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        public async Task<bool> SendAsync(byte[] data)
        {
            if (_ClusterClient != null)
            {
                if (_ClusterClient.Connected)
                {
                    return await _ClusterClient.SendAsync(data);
                }
            }
            else if (String.IsNullOrEmpty(_CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("No peer connected");
                return false;
            }
            else if (_ClusterServer != null)
            {
                if (_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    return await _ClusterServer.SendAsync(_CurrPeerIpPort, data);
                }
            }

            if (Debug) Console.WriteLine("Neither server or client are healthy");
            return false;
        }

        /// <summary>
        /// Send a message to the peer node, asynchronously, using a stream.
        /// </summary>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>Task with Boolean indicating if the message was sent successfully.</returns>
        public async Task<bool> SendAsync(long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_ClusterClient != null)
            {
                if (_ClusterClient.Connected)
                {
                    return await _ClusterClient.SendAsync(contentLength, stream);
                }
            }
            else if (String.IsNullOrEmpty(_CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("No peer connected");
                return false;
            }
            else if (_ClusterServer != null)
            {
                if (_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    return await _ClusterServer.SendAsync(_CurrPeerIpPort, contentLength, stream);
                }
            }

            if (Debug) Console.WriteLine("Neither server or client are healthy");
            return false;
        }

        /// <summary>
        /// Destroy the cluster node and release resources.
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
                if (_ClusterServer != null) _ClusterServer.Dispose();
                if (_ClusterClient != null) _ClusterClient.Dispose();
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

        private bool SrvClientConnect(string ipPort)
        {
            _CurrPeerIpPort = ipPort;
            if (_ClusterClient != null && _ClusterClient.Connected)
            {
                if (ClusterHealthy != null) ClusterHealthy();
            }
            return true;
        }

        private bool SrvClientDisconnect(string ipPort)
        {
            _CurrPeerIpPort = null;
            if (ClusterUnhealthy != null) ClusterUnhealthy();
            return true;
        }

        private bool SrvMsgReceived(string ipPort, byte[] data)
        {
            if (MessageReceived != null) MessageReceived(data);
            return true;
        }

        private bool SrvStrmReceived(string ipPort, long contentLength, Stream stream)
        {
            if (StreamReceived != null) StreamReceived(contentLength, stream);
            return true;
        }

        private bool CliServerConnect()
        {
            if (_ClusterServer != null
                && !String.IsNullOrEmpty(_CurrPeerIpPort)
                && _ClusterServer.IsConnected(_CurrPeerIpPort))
            {
                ClusterHealthy();
            }
            return true;
        }

        private bool CliServerDisconnect()
        {
            ClusterUnhealthy();
            return true;
        }

        private bool CliMsgReceived(byte[] data)
        {
            if (MessageReceived != null) MessageReceived(data);
            return true;
        }

        private bool CliStrmReceived(long contentLength, Stream stream)
        {
            if (StreamReceived != null) StreamReceived(contentLength, stream);
            return true;
        }

        #endregion
    }
}
