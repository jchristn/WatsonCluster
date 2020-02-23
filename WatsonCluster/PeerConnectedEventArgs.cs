using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WatsonCluster
{
    /// <summary>
    /// Event arguments for when a peer connects.
    /// </summary>
    public class PeerConnectedEventArgs
    {
        internal PeerConnectedEventArgs(string ipPort)
        {
            IpPort = ipPort;
        }

        /// <summary>
        /// The IP:port of the peer.
        /// </summary>
        public string IpPort { get; }
    }
}
