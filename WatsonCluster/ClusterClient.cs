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
    /// The Watson cluster client node (initiates connections to server).  Use ClusterNode, which encapsulates this class.
    /// </summary>
    public class ClusterClient : IDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private string ServerIp;
        private int ServerPort;
        private bool Debug;
        private WatsonTcpClient Wtcp;

        private Func<bool> ClusterHealthy;
        private Func<bool> ClusterUnhealthy;
        private Func<byte[], bool> MessageReceived;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the cluster client.
        /// </summary>
        /// <param name="serverIp">The IP address of the peer server.</param>
        /// <param name="serverPort">The TCP port of the peer server.</param>
        /// <param name="debug">Enable or disable debug logging to the console.</param>
        /// <param name="clusterHealthy">Function to be called when the cluster becomes healthy.</param>
        /// <param name="clusterUnhealthy">Function to be called when the cluster becomes unhealthy.</param>
        /// <param name="messageReceived">Function to be called when a message is received from the peer.</param>
        public ClusterClient(string serverIp, int serverPort, bool debug, Func<bool> clusterHealthy, Func<bool> clusterUnhealthy, Func<byte[], bool> messageReceived)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < IPEndPoint.MinPort || serverPort > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if (clusterHealthy == null) throw new ArgumentNullException(nameof(clusterHealthy));
            if (clusterUnhealthy == null) throw new ArgumentNullException(nameof(clusterUnhealthy));
            if (messageReceived == null) throw new ArgumentNullException(nameof(messageReceived));

            ServerIp = serverIp;
            ServerPort = serverPort;
            Debug = debug;
            Wtcp = null;
            ClusterHealthy = clusterHealthy;
            ClusterUnhealthy = clusterUnhealthy;
            MessageReceived = messageReceived;
            Task.Run(() => EstablishConnection());
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Determines if the client is connected to the peer server.
        /// </summary>
        /// <returns>True if connected.</returns>
        public bool IsConnected()
        {
            if (Wtcp == null) 
            {
                if (Debug) Console.WriteLine("Client object is null");
                return false;
            }
            return Wtcp.IsConnected();
        }

        /// <summary>
        /// Send a message to the connected server.
        /// </summary>
        /// <param name="data">Data to send to the server.</param>
        /// <returns>True if successful.</returns>
        public bool Send(byte[] data)
        {
            if (Wtcp == null)
            {
                if (Debug) Console.WriteLine("Client is null, cannot send");
                return false;
            }

            if (Wtcp.IsConnected())
            {
                Wtcp.Send(data);
                return true;
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
        public async Task<bool> SendAsync(byte[] data)
        {
            if (Wtcp == null)
            {
                if (Debug) Console.WriteLine("Client is null, cannot send");
                return false;
            }

            if (Wtcp.IsConnected())
            {
                await Wtcp.SendAsync(data);
                return true;
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
                if (Wtcp != null) Wtcp.Dispose();
            }
        }

        private void EstablishConnection()
        {
            while (true)
            {
                try
                {
                    if (Wtcp == null)
                    {
                        if (Debug) Console.WriteLine("Attempting connection to " + ServerIp + ":" + ServerPort);
                        Wtcp = new WatsonTcpClient(ServerIp, ServerPort, ServerConnected, ServerDisconnected, MsgReceived, Debug);
                    }
                    else if (!Wtcp.IsConnected())
                    {
                        if (Debug) Console.WriteLine("Attempting reconnect to " + ServerIp + ":" + ServerPort);
                        Wtcp.Dispose();
                        Wtcp = new WatsonTcpClient(ServerIp, ServerPort, ServerConnected, ServerDisconnected, MsgReceived, Debug);
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception e)
                {
                    if (Debug) Console.WriteLine("Exception: " + e.Message);
                }
            }
        }

        private bool ServerConnected()
        {
            if (Debug) Console.WriteLine("Server " + ServerIp + ":" + ServerPort + " connected");
            ClusterHealthy();
            return true;
        }

        private bool ServerDisconnected()
        {
            if (Debug) Console.WriteLine("Server " + ServerIp + ":" + ServerPort + " disconnected");
            ClusterUnhealthy();
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

            return MessageReceived(data);
        }

        #endregion
    }
}
