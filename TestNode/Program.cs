using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonCluster;

namespace TestNode
{
    class Program
    {
        static int localPort = 0;
        static int remotePort = 0;
        static ClusterNode n;

        static void Main(string[] args)
        {
            while (localPort < 1 || remotePort < 1)
            {
                Console.Write("Local port  : ");
                localPort = Convert.ToInt32(Console.ReadLine());
                Console.Write("Remote port : ");
                remotePort = Convert.ToInt32(Console.ReadLine());
            }

            n = new ClusterNode("127.0.0.1", localPort, "127.0.0.1", remotePort, null, null);
            n.AcceptInvalidCertificates = true;
            n.MutuallyAuthenticate = true;
            n.Debug = false;
            n.ReadDataStream = false;
            n.PresharedKey = "0000000000000000";
            n.MessageReceived = MessageReceived;
            n.StreamReceived = StreamReceived;
            n.ClusterHealthy = ClusterHealthy;
            n.ClusterUnhealthy = ClusterUnhealthy;

            n.Start();

            bool runForever = true;
            while (runForever)
            {
                Console.Write("Command [? for help]: ");
                string userInput = Console.ReadLine();
                if (String.IsNullOrEmpty(userInput)) continue;

                byte[] data = null;
                MemoryStream ms = null;

                switch (userInput)
                {
                    case "?":
                        Console.WriteLine("---");
                        Console.WriteLine(" q                   quit");
                        Console.WriteLine(" ?                   this menu");
                        Console.WriteLine(" cls                 clear screen");
                        Console.WriteLine(" send bytes          send message to peer");
                        Console.WriteLine(" send bytes async    send message to peer, asynchronously");
                        Console.WriteLine(" send stream         send message to peer using a stream");
                        Console.WriteLine(" send stream async   send message to peer using a stream, asynchronously");
                        Console.WriteLine(" health              display cluster health");
                        break;

                    case "q":
                        runForever = false;
                        break;

                    case "cls":
                        Console.Clear();
                        break;

                    case "send bytes":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        if (n.Send(Encoding.UTF8.GetBytes(userInput)))
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "send bytes async":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        if (n.SendAsync(Encoding.UTF8.GetBytes(userInput)).Result)
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "send stream":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        data = Encoding.UTF8.GetBytes(userInput);
                        ms = new MemoryStream(data);
                        if (n.Send(data.Length, ms))
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "send stream async":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        data = Encoding.UTF8.GetBytes(userInput);
                        ms = new MemoryStream(data);
                        if (n.SendAsync(data.Length, ms).Result)
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break; 

                    case "health":
                        Console.WriteLine("Healthy: " + n.IsHealthy);
                        break;
                }
            }
        }

        static bool ClusterHealthy()
        {
            Console.WriteLine("NOTICE: cluster is healthy");
            return true;
        }

        static bool ClusterUnhealthy()
        {
            Console.WriteLine("NOTICE: cluster is unhealthy");
            return true;
        }

        static bool MessageReceived(byte[] data)
        {
            if (data == null || data.Length < 1) return true;
            Console.WriteLine("NOTICE: data received (" + data.Length + " bytes):");
            Console.WriteLine(Encoding.UTF8.GetString(data));
            return true;
        }

        static bool StreamReceived(long contentLength, Stream stream)
        {
            if (contentLength < 1) return true;

            int bytesRead = 0;
            int bufferLen = 65536;
            byte[] buffer = new byte[bufferLen];
            long bytesRemaining = contentLength;

            Console.WriteLine("NOTICE: stream received " + contentLength + " bytes:");

            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    byte[] consoleBuffer = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, consoleBuffer, 0, bytesRead);
                    Console.Write(Encoding.UTF8.GetString(consoleBuffer));
                }

                bytesRemaining -= bytesRead;
            }

            Console.WriteLine(""); 
            return true;
        } 
    }
}
