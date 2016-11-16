using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonCluster
{
    public class ClusterClient
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

        public ClusterClient(string serverIp, int serverPort, bool debug, Func<bool> clusterHealthy, Func<bool> clusterUnhealthy, Func<byte[], bool> messageReceived)
        {
            if (String.IsNullOrEmpty(serverIp)) throw new ArgumentNullException(nameof(serverIp));
            if (serverPort < 1) throw new ArgumentOutOfRangeException(nameof(serverPort));
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

        public bool IsConnected()
        {
            if (Wtcp == null) 
            {
                if (Debug) Console.WriteLine("Client is null");
                return false;
            }
            return Wtcp.IsConnected();
        }

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

        #endregion

        #region Private-Methods

        private void EstablishConnection()
        {
            bool firstRun = true;
            while (true)
            {
                try
                {
                    if (!firstRun)
                    {
                        Thread.Sleep(1000);
                        firstRun = false;
                    }

                    if (Wtcp == null)
                    {
                        if (Debug) Console.WriteLine("Attempting connection to " + ServerIp + ":" + ServerPort);
                        Wtcp = new WatsonTcpClient(ServerIp, ServerPort, ServerConnected, ServerDisconnected, MsgReceived, Debug);
                        continue;
                    }

                    if (!Wtcp.IsConnected())
                    {
                        if (Debug) Console.WriteLine("Attempting reconnect to " + ServerIp + ":" + ServerPort);
                        Wtcp.Dispose();
                        Wtcp = new WatsonTcpClient(ServerIp, ServerPort, ServerConnected, ServerDisconnected, MsgReceived, Debug);
                        continue;
                    }
                }
                catch (Exception)
                {

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
