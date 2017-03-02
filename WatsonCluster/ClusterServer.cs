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
    /// The Watson cluster server node (receives connections from clients).  Use ClusterNode, which encapsulates this class.
    /// </summary>
    public class ClusterServer : IDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private int Port;
        private bool Debug;
        private WatsonTcpServer Wtcp;

        private Func<string, bool> ClientConnected;
        private Func<string, bool> ClientDisconnected;
        private Func<string, byte[], bool> MessageReceived;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the cluster server.
        /// </summary>
        /// <param name="port">The TCP port on which the cluster server should listen.</param>
        /// <param name="debug">Enable or disable debug logging to the console.</param>
        /// <param name="clientConnected">Function to be called when the peer connects.</param>
        /// <param name="clientDisconnected">Function to be called when the peer disconnects.</param>
        /// <param name="messageReceived">Function to be called when a message is received from the peer.</param>
        public ClusterServer(int port, bool debug, Func<string, bool> clientConnected, Func<string, bool> clientDisconnected, Func<string, byte[], bool> messageReceived)
        {
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));
            if (clientConnected == null) throw new ArgumentNullException(nameof(clientConnected));
            if (clientDisconnected == null) throw new ArgumentNullException(nameof(clientDisconnected));
            if (messageReceived == null) throw new ArgumentNullException(nameof(messageReceived));

            Port = port;
            Debug = debug;
            ClientConnected = clientConnected;
            ClientDisconnected = clientDisconnected;
            MessageReceived = messageReceived;
            Wtcp = new WatsonTcpServer(null, Port, ClientConnect, ClientDisconnect, MsgReceived, Debug);
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
