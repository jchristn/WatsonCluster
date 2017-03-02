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
