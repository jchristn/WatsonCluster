using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    internal class ClusterServer : IDisposable
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

        internal Func<string, Task> ClientConnected = null;

        internal Func<string, Task> ClientDisconnected = null;

        internal Func<string, long, Stream, Task> MessageReceived = null;

        #endregion Internal-Members

        #region Private-Members

        private int _ReadStreamBufferSize = 65536;
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
                if (Debug) Log("Server is null");
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

            _WtcpServer.Debug = Debug;
            _WtcpServer.ReadDataStream = false;
            _WtcpServer.ReadStreamBufferSize = ReadStreamBufferSize;
            _WtcpServer.AcceptInvalidCertificates = AcceptInvalidCertificates;
            _WtcpServer.MutuallyAuthenticate = MutuallyAuthenticate;
            _WtcpServer.PresharedKey = PresharedKey;

            _WtcpServer.ClientConnected = ClientConnect;
            _WtcpServer.ClientDisconnected = ClientDisconnect;
            _WtcpServer.StreamReceived = StrmReceived;

            _WtcpServer.Start();
        }

        internal async Task<bool> Send(string ipPort, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

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
            return await Send(ipPort, contentLength, stream);
        }

        internal async Task<bool> Send(string ipPort, long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            if (_WtcpServer == null)
            {
                if (Debug) Log("Server is null, cannot send");
                return false;
            }
            if (_WtcpServer.IsClientConnected(ipPort))
            {
                return await _WtcpServer.SendAsync(ipPort, contentLength, stream);
            }
            else
            {
                if (Debug) Log("Server is not connected, cannot send");
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

        private async Task ClientConnect(string ipPort)
        {
            if (Debug) Log("Client " + ipPort + " connected");
            if (ClientConnected != null) await ClientConnected(ipPort);
        }

        private async Task ClientDisconnect(string ipPort)
        {
            if (Debug) Log("Client " + ipPort + " disconnected");
            if (ClientDisconnected != null) await ClientDisconnected(ipPort);
        }

        private async Task StrmReceived(string ipPort, long contentLength, Stream stream)
        {
            if (Debug)
            {
                Log("Stream received from " + ipPort + ": " + contentLength + " bytes");
            }

            if (MessageReceived != null) await MessageReceived(ipPort, contentLength, stream);
        }

        #endregion Private-Methods
    }
}