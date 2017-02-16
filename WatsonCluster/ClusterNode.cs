using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonCluster
{
    public class ClusterNode : IDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private int LocalPort;
        private string PeerIp;
        private int PeerPort;
        private bool Debug;
        private ClusterServer Server;
        private ClusterClient Client;

        private Func<bool> ClusterHealthy;
        private Func<bool> ClusterUnhealthy;
        private Func<byte[], bool> MessageReceived;

        private string CurrPeerIpPort;

        #endregion

        #region Constructors-and-Factories

        public ClusterNode(
            string peerIp, 
            int peerPort, 
            int localPort, 
            Func<bool> clusterHealthy, 
            Func<bool> clusterUnhealthy, 
            Func<byte[], bool> messageReceived,
            bool debug)
        {
            if (String.IsNullOrEmpty(peerIp)) throw new ArgumentNullException(nameof(peerIp));
            if (peerPort < 1) throw new ArgumentOutOfRangeException(nameof(peerPort));
            if (localPort < 1) throw new ArgumentOutOfRangeException(nameof(localPort));
            if (clusterHealthy == null) throw new ArgumentNullException(nameof(clusterHealthy));
            if (clusterUnhealthy == null) throw new ArgumentNullException(nameof(clusterUnhealthy));
            if (messageReceived == null) throw new ArgumentNullException(nameof(messageReceived));

            PeerIp = peerIp;
            PeerPort = peerPort;
            LocalPort = localPort;
            ClusterHealthy = clusterHealthy;
            ClusterUnhealthy = clusterUnhealthy;
            MessageReceived = messageReceived;
            Debug = debug;

            Server = new ClusterServer(LocalPort, Debug, SrvClientConnect, SrvClientDisconnect, SrvMsgReceived);
            Client = new ClusterClient(PeerIp, PeerPort, Debug, CliServerConnect, CliServerDisconnect, CliMsgReceived);
        }

        #endregion

        #region Public-Methods

        public bool IsHealthy()
        {
            if (Server == null)
            {
                if (Debug) Console.WriteLine("Server object is null");
                return false;
            }
            
            if (Client == null)
            {
                if (Debug) Console.WriteLine("Client object is null");
                return false;
            }

            if (String.IsNullOrEmpty(CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("Peer information is null");
                return false;
            }

            if (!Server.IsConnected(CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("Server peer is not connected");
                return false;
            }

            if (Client.IsConnected())
            {
                if (Debug) Console.WriteLine("Client is not connected");
                return true;
            }

            return false;
        }

        public bool Send(byte[] data)
        {
            if (Client != null)
            {
                if (Client.IsConnected())
                {
                    return Client.Send(data);
                }
            }
            else if (String.IsNullOrEmpty(CurrPeerIpPort))
            {
                if (Debug) Console.WriteLine("No peer connected");
                return false;
            }
            else if (Server != null)
            {
                if (Server.IsConnected(CurrPeerIpPort))
                {
                    return Server.Send(CurrPeerIpPort, data);
                }
            }

            if (Debug) Console.WriteLine("Neither server or client are healthy");
            return false;
        }

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
                if (Server != null) Server.Dispose();
                if (Client != null) Client.Dispose();
            }
        }

        private bool SrvClientConnect(string ipPort)
        {
            CurrPeerIpPort = ipPort;
            if (Client != null
                && Client.IsConnected())
            {
                ClusterHealthy();
            }
            return true;
        }

        private bool SrvClientDisconnect(string ipPort)
        {
            CurrPeerIpPort = null;
            ClusterUnhealthy();
            return true;
        }

        private bool SrvMsgReceived(string ipPort, byte[] data)
        {
            MessageReceived(data);
            return true;
        }

        private bool CliServerConnect()
        {
            if (Server != null
                && !String.IsNullOrEmpty(CurrPeerIpPort)
                && Server.IsConnected(CurrPeerIpPort))
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
            MessageReceived(data);
            return true;
        }

        #endregion
    }
}
