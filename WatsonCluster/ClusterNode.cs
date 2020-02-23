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
        /// Buffer size to use when reading streams.  Default is 65536.
        /// </summary>
        public int StreamBufferSize
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
         
        /// <summary>
        /// Accept SSL certificates that are expired or unable to be validated.
        /// </summary>
        public bool AcceptInvalidCertificates = true;

        /// <summary>
        /// Enable or disable mutual authentication with SSL.
        /// </summary>
        public bool MutuallyAuthenticate = false;

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
        /// Event to fire when the cluster is healthy.
        /// </summary>
        public event EventHandler ClusterHealthy;

        /// <summary>
        /// Event to fire when the cluster is unhealthy.
        /// </summary>
        public event EventHandler ClusterUnhealthy;

        /// <summary>
        /// Event to fire when a message is received.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

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
                    Logger?.Invoke("[ClusterNode] Server object is null");
                    return false;
                }

                if (_ClusterClient == null)
                {
                    Logger?.Invoke("[ClusterNode] Client object is null");
                    return false;
                }

                if (String.IsNullOrEmpty(_CurrPeerIpPort))
                {
                    Logger?.Invoke("[ClusterNode] Peer information is null");
                    return false;
                }

                if (!_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    Logger?.Invoke("[ClusterNode] Server peer is not connected");
                    return false;
                }

                if (_ClusterClient.Connected)
                {
                    return true;
                }
                else
                {
                    Logger?.Invoke("[ClusterNode] Client is not connected");
                    return false;
                }
            }
        }

        /// <summary>
        /// Method to invoke when sending a log message.
        /// </summary>
        public Action<string> Logger = null;

        #endregion Public-Members

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private string _ListenerIp;
        private int _ListenerPort;
        private string _PeerIp;
        private int _PeerPort;
        private List<string> _PermittedIps;
        private string _CertFile = null;
        private string _CertPass = null;
        private string _PresharedKey = null;
        private ClusterServer _ClusterServer;
        private ClusterClient _ClusterClient;

        private string _CurrPeerIpPort;
        private DateTime _UnhealthyCalled = DateTime.Now;

        #endregion Private-Members

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

        #endregion Constructors-and-Factories

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
            _ClusterServer.Logger = Logger;
            _ClusterServer.StreamBufferSize = StreamBufferSize;
            _ClusterServer.PresharedKey = PresharedKey;
            _ClusterServer.ClientConnected += SrvClientConnect;
            _ClusterServer.ClientDisconnected += SrvClientDisconnect;
            _ClusterServer.MessageReceived += SrvStreamReceived;
            _ClusterServer.Start();

            _ClusterClient = new ClusterClient(_PeerIp, _PeerPort, _CertFile, _CertPass);
            _ClusterClient.AcceptInvalidCertificates = AcceptInvalidCertificates;
            _ClusterClient.MutuallyAuthenticate = MutuallyAuthenticate;
            _ClusterClient.Logger = Logger;
            _ClusterClient.StreamBufferSize = StreamBufferSize;
            _ClusterClient.PresharedKey = PresharedKey;
            _ClusterClient.ClusterHealthy += CliServerConnect;
            _ClusterClient.ClusterUnhealthy += CliServerDisconnect;
            _ClusterClient.MessageReceived += ClientStreamReceived;
            _ClusterClient.Start();
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="metadata">Dictionary containing metadata.</param>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public bool Send(Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public bool Send(byte[] data)
        { 
            return Send(null, data);
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="metadata">Dictionary containing metadata.</param>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public bool Send(Dictionary<object, object> metadata, byte[] data)
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

        /// <summary>
        /// Send a message to the peer using a stream.
        /// </summary>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(long contentLength, Stream stream)
        {
            return Send(null, contentLength, stream);
        }

        /// <summary>
        /// Send a message to the peer using a stream.
        /// </summary>
        /// <param name="metadata">Dictionary containing metadata.</param>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_ClusterClient != null)
            {
                if (_ClusterClient.Connected)
                {
                    return _ClusterClient.Send(metadata, contentLength, stream);
                }
            }
            else if (String.IsNullOrEmpty(_CurrPeerIpPort))
            {
                Logger?.Invoke("[ClusterNode] No peer connected");
                return false;
            }
            else if (_ClusterServer != null)
            {
                if (_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    return _ClusterServer.Send(_CurrPeerIpPort, metadata, contentLength, stream);
                }
            }

            Logger?.Invoke("[ClusterNode] Neither server or client are healthy");
            return false;
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="metadata">Dictionary containing metadata.</param>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(byte[] data)
        {
            return await SendAsync(null, data);
        }

        /// <summary>
        /// Send a message to the peer.
        /// </summary>
        /// <param name="metadata">Dictionary containing metadata.</param>
        /// <param name="data">Data to send to the peer node.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(Dictionary<object, object> metadata, byte[] data)
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

        /// <summary>
        /// Send a message to the peer node using a stream.
        /// </summary>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(long contentLength, Stream stream)
        {
            return await SendAsync(null, contentLength, stream);
        }

        /// <summary>
        /// Send a message to the peer using a stream.
        /// </summary>
        /// <param name="metadata">Dictionary containing metadata.</param>
        /// <param name="contentLength">The amount of data to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_ClusterClient != null)
            {
                if (_ClusterClient.Connected)
                {
                    return await _ClusterClient.SendAsync(metadata, contentLength, stream);
                }
            }
            else if (String.IsNullOrEmpty(_CurrPeerIpPort))
            {
                Logger?.Invoke("[ClusterNode] No peer connected");
                return false;
            }
            else if (_ClusterServer != null)
            {
                if (_ClusterServer.IsConnected(_CurrPeerIpPort))
                {
                    return await _ClusterServer.SendAsync(_CurrPeerIpPort, metadata, contentLength, stream);
                }
            }

            Logger?.Invoke("[ClusterNode] Neither server or client are healthy");
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

        #endregion Public-Methods

        #region Private-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_ClusterServer != null) _ClusterServer.Dispose();
                if (_ClusterClient != null) _ClusterClient.Dispose();
            }
        }
          
        private void SrvClientConnect(object sender, PeerConnectedEventArgs args)
        {
            _CurrPeerIpPort = args.IpPort;
            if (_ClusterClient != null && _ClusterClient.Connected)
            {
                ClusterHealthy?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SrvClientDisconnect(object sender, PeerConnectedEventArgs args)
        {
            _CurrPeerIpPort = null;
            if (ClusterUnhealthy != null)
            {
                TimeSpan ts = DateTime.Now - _UnhealthyCalled;
                if (ts.TotalSeconds > 1)
                {
                    _UnhealthyCalled = DateTime.Now;
                    ClusterUnhealthy?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void SrvStreamReceived(object sender, MessageReceivedEventArgs args)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(args.Metadata, args.ContentLength, args.DataStream));
        }

        private void CliServerConnect(object sender, EventArgs args)
        {
            if (_ClusterServer != null
                && !String.IsNullOrEmpty(_CurrPeerIpPort)
                && _ClusterServer.IsConnected(_CurrPeerIpPort))
            {
                ClusterHealthy?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CliServerDisconnect(object sender, EventArgs args)
        {
            if (ClusterUnhealthy != null)
            {
                TimeSpan ts = DateTime.Now - _UnhealthyCalled;
                if (ts.TotalSeconds > 1)
                {
                    _UnhealthyCalled = DateTime.Now;
                    ClusterUnhealthy?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ClientStreamReceived(object sender, MessageReceivedEventArgs args)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(args.Metadata, args.ContentLength, args.DataStream));
        }

        #endregion Private-Methods
    }
}