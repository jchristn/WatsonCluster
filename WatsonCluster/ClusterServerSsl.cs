using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    /// <summary>
    /// The Watson cluster server node (receives connections from clients) with SSL.  Use ClusterNode, which encapsulates this class.
    /// </summary>
    public class ClusterServerSsl : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable mutual authentication with SSL.
        /// </summary>
        public bool MutuallyAuthenticate = false;

        #endregion

        #region Private-Members

        private int Port;
        private string CertFile;
        private string CertPass;
        private bool AcceptInvalidCerts;
        private bool Debug;
        private WatsonTcpSslServer Wtcp;

        private Func<string, bool> ClientConnected;
        private Func<string, bool> ClientDisconnected;
        private Func<string, byte[], bool> MessageReceived;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the cluster server.
        /// </summary>
        /// <param name="peerIp">The IP address of the peer client.</param>
        /// <param name="port">The TCP port on which the cluster server should listen.</param>
        /// <param name="certFile">The PFX file containing the certificate.</param>
        /// <param name="certPass">The password to the certificate.</param>
        /// <param name="acceptInvalidCerts">True to accept invalid SSL certificates.</param>
        /// <param name="debug">Enable or disable debug logging to the console.</param>
        /// <param name="clientConnected">Function to be called when the peer connects.</param>
        /// <param name="clientDisconnected">Function to be called when the peer disconnects.</param>
        /// <param name="messageReceived">Function to be called when a message is received from the peer.</param>
        public ClusterServerSsl(string peerIp, int port, string certFile, string certPass, bool acceptInvalidCerts, bool debug, Func<string, bool> clientConnected, Func<string, bool> clientDisconnected, Func<string, byte[], bool> messageReceived)
        {
            if (String.IsNullOrEmpty(peerIp)) throw new ArgumentNullException(nameof(peerIp));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));
            if (String.IsNullOrEmpty(certFile)) throw new ArgumentNullException(nameof(certFile));
            if (clientConnected == null) throw new ArgumentNullException(nameof(clientConnected));
            if (clientDisconnected == null) throw new ArgumentNullException(nameof(clientDisconnected));
            if (messageReceived == null) throw new ArgumentNullException(nameof(messageReceived));

            Port = port;
            CertFile = certFile;
            CertPass = certPass;
            AcceptInvalidCerts = acceptInvalidCerts;
            Debug = debug;
            ClientConnected = clientConnected;
            ClientDisconnected = clientDisconnected;
            MessageReceived = messageReceived;

            List<string> permittedIps = new List<string>();
            permittedIps.Add(peerIp);

            Wtcp = new WatsonTcpSslServer(null, Port, CertFile, CertPass, AcceptInvalidCerts, MutuallyAuthenticate, ClientConnect, ClientDisconnect, MsgReceived, Debug);
        }

        /// <summary>
        /// Start the cluster server.
        /// </summary>
        /// <param name="permittedIps">The list of IP addresses allowed to connect.</param>
        /// <param name="port">The TCP port on which the cluster server should listen.</param>
        /// <param name="certFile">The PFX file containing the certificate.</param>
        /// <param name="certPass">The password to the certificate.</param>
        /// <param name="acceptInvalidCerts">True to accept invalid SSL certificates.</param>
        /// <param name="debug">Enable or disable debug logging to the console.</param>
        /// <param name="clientConnected">Function to be called when the peer connects.</param>
        /// <param name="clientDisconnected">Function to be called when the peer disconnects.</param>
        /// <param name="messageReceived">Function to be called when a message is received from the peer.</param>
        public ClusterServerSsl(IEnumerable<string> permittedIps, int port, string certFile, string certPass, bool acceptInvalidCerts, bool debug, Func<string, bool> clientConnected, Func<string, bool> clientDisconnected, Func<string, byte[], bool> messageReceived)
        {
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));
            if (String.IsNullOrEmpty(certFile)) throw new ArgumentNullException(nameof(certFile));
            if (clientConnected == null) throw new ArgumentNullException(nameof(clientConnected));
            if (clientDisconnected == null) throw new ArgumentNullException(nameof(clientDisconnected));
            if (messageReceived == null) throw new ArgumentNullException(nameof(messageReceived));

            Port = port;
            CertFile = certFile;
            CertPass = certPass;
            AcceptInvalidCerts = acceptInvalidCerts;
            Debug = debug;
            ClientConnected = clientConnected;
            ClientDisconnected = clientDisconnected;
            MessageReceived = messageReceived;

            List<string> PermittedIps = null;
            if (permittedIps != null && permittedIps.Count() > 0) PermittedIps = new List<string>(permittedIps);

            Wtcp = new WatsonTcpSslServer(null, Port, CertFile, CertPass, AcceptInvalidCerts, MutuallyAuthenticate, ClientConnect, ClientDisconnect, MsgReceived, Debug);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Determines if a specified client is connected to the local server.
        /// </summary>
        /// <returns>True if connected.</returns>
        public bool IsConnected(string ipPort)
        {
            if (Wtcp == null)
            {
                if (Debug) Console.WriteLine("Server is null");
                return false;
            }
            if (String.IsNullOrEmpty(ipPort)) return false;
            return Wtcp.IsClientConnected(ipPort);
        }

        /// <summary>
        /// Send a message to the specified client.
        /// </summary>
        /// <param name="ipPort">The IP:port of the client.</param>
        /// <param name="data">Data to send to the client.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, byte[] data)
        {
            if (Wtcp == null)
            {
                if (Debug) Console.WriteLine("Server is null, cannot send");
                return false;
            }
            if (Wtcp.IsClientConnected(ipPort))
            {
                Wtcp.Send(ipPort, data);
                return true;
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
        public async Task<bool> SendAsync(string ipPort, byte[] data)
        {
            if (Wtcp == null)
            {
                if (Debug) Console.WriteLine("Server is null, cannot send");
                return false;
            }
            if (Wtcp.IsClientConnected(ipPort))
            {
                await Wtcp.SendAsync(ipPort, data);
                return true;
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
                if (Wtcp != null) Wtcp.Dispose();
            }
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

        #endregion
    }
}
