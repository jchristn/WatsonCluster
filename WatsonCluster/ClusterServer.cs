using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    internal class ClusterServer : IDisposable
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

        internal event EventHandler<PeerConnectedEventArgs> ClientConnected;
        internal event EventHandler<PeerConnectedEventArgs> ClientDisconnected;
        internal event EventHandler<MessageReceivedEventArgs> MessageReceived;

        internal Action<string> Logger = null;

        #endregion Internal-Members

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private string _ListenerIp;
        private int _ListenerPort;
        private List<string> _PermittedIps;
        private string _CertFile;
        private string _CertPass;
        private string _PresharedKey;
        private WatsonTcpServer _WtcpServer;

        #endregion Private-Members

        #region Constructors-and-Factories

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

        #endregion Constructors-and-Factories

        #region Public-Methods

        internal bool IsConnected(string ipPort)
        {
            if (_WtcpServer == null)
            {
                Logger?.Invoke("[ClusterServer] Server is null");
                return false;
            }
            if (String.IsNullOrEmpty(ipPort)) return false;
            return _WtcpServer.IsClientConnected(ipPort);
        }

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
             
            _WtcpServer.StreamBufferSize = StreamBufferSize;
            _WtcpServer.AcceptInvalidCertificates = AcceptInvalidCertificates;
            _WtcpServer.MutuallyAuthenticate = MutuallyAuthenticate;
            _WtcpServer.PresharedKey = PresharedKey;

            _WtcpServer.ClientConnected += ClientConnect;
            _WtcpServer.ClientDisconnected += ClientDisconnect;
            _WtcpServer.StreamReceived += StreamReceived;

            _WtcpServer.Start();
        }

        internal bool Send(string ipPort, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(ipPort, null, Encoding.UTF8.GetBytes(data));
        }

        internal bool Send(string ipPort, Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(ipPort, metadata, Encoding.UTF8.GetBytes(data));
        }

        internal bool Send(string ipPort, byte[] data)
        {
            return Send(ipPort, null, data);
        }

        internal bool Send(string ipPort, Dictionary<object, object> metadata, byte[] data)
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
            return Send(ipPort, metadata, contentLength, stream); 
        }

        internal bool Send(string ipPort, long contentLength, Stream stream)
        {
            return Send(ipPort, null, contentLength, stream);
        }

        internal bool Send(string ipPort, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpServer == null)
            {
                Logger?.Invoke("[ClusterServer] Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                return _WtcpServer.Send(ipPort, metadata, contentLength, stream);
            }
            else
            {
                Logger?.Invoke("[ClusterServer] Server is not connected, cannot send");
                return false;
            }

        }

        internal async Task<bool> SendAsync(string ipPort, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(ipPort, null, Encoding.UTF8.GetBytes(data));
        }

        internal async Task<bool> SendAsync(string ipPort, Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(ipPort, metadata, Encoding.UTF8.GetBytes(data));
        }

        internal async Task<bool> SendAsync(string ipPort, byte[] data)
        {
            return await SendAsync(ipPort, null, data);
        }

        internal async Task<bool> SendAsync(string ipPort, Dictionary<object, object> metadata, byte[] data)
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
            return await SendAsync(ipPort, metadata, contentLength, stream);

        }

        internal async Task<bool> SendAsync(string ipPort, long contentLength, Stream stream)
        {
            return await SendAsync(ipPort, null, contentLength, stream);
        }

        internal async Task<bool> SendAsync(string ipPort, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpServer == null)
            {
                Logger?.Invoke("[ClusterServer] Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                return await _WtcpServer.SendAsync(ipPort, metadata, contentLength, stream);
            }
            else
            {
                Logger?.Invoke("[ClusterServer] Server is not connected, cannot send");
                return false;
            } 
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion Public-Methods

        #region Private-Methods

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_WtcpServer != null) _WtcpServer.Dispose();
            }
        }
         
        private void ClientConnect(object sender, ClientConnectedEventArgs args)
        {
            Logger?.Invoke("[ClusterServer] Client " + args.IpPort + " connected");
            ClientConnected?.Invoke(this, new PeerConnectedEventArgs(args.IpPort)); 
        }

        private void ClientDisconnect(object sender, ClientDisconnectedEventArgs args)
        {
            Logger?.Invoke("[ClusterServer] Client " + args.IpPort + " disconnected");
            ClientDisconnected?.Invoke(this, new PeerConnectedEventArgs(args.IpPort));
        }

        private void StreamReceived(object sender, StreamReceivedFromClientEventArgs args)
        {
            Logger?.Invoke("[ClusterServer] Stream received from " + args.IpPort + ": " + args.ContentLength + " bytes");
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(args.Metadata, args.ContentLength, args.DataStream));
        }

        #endregion Private-Methods
    }
}