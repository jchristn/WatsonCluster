using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WatsonCluster
{
    /// <summary>
    /// Event arguments for when a stream is received from the server.
    /// </summary>
    public class MessageReceivedEventArgs
    {
        internal MessageReceivedEventArgs(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            _Data = null;

            Metadata = metadata;
            ContentLength = contentLength;
            DataStream = stream;
        }

        /// <summary>
        /// The metadata received from the server.
        /// </summary>
        public Dictionary<object, object> Metadata { get; }

        /// <summary>
        /// The number of data bytes that should be read from DataStream.
        /// </summary>
        public long ContentLength { get; }

        /// <summary>
        /// The stream containing the message data.
        /// Note: if you access Data, the stream in DataStream will be fully read.
        /// </summary>
        public Stream DataStream { get; }

        /// <summary>
        /// The byte array containing the message data.
        /// Note: if you access Data, the stream in DataStream will be fully read.
        /// </summary>
        public byte[] Data 
        { 
            get
            {
                if (_Data != null) return _Data;
                if (ContentLength <= 0) return null;
                _Data = StreamToBytes(DataStream);
                return _Data;
            }
        }

        private byte[] _Data = null;

        private byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        } 
    }
}
