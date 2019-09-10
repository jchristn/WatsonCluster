using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WatsonCluster;

namespace TestNode
{
    internal class Program
    {
        private static int localPort = 0;
        private static int remotePort = 0;
        private static ClusterNode node;

        private static void Main(string[] args)
        {
            InitializeNode();

            bool runForever = true;
            bool success = false;

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
                        Menu();
                        break;

                    case "q":
                        runForever = false;
                        break;

                    case "cls":
                        Console.Clear();
                        break;

                    case "send":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        success = node.Send(Encoding.UTF8.GetBytes(userInput)).Result;
                        if (success)
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "send str":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        data = Encoding.UTF8.GetBytes(userInput);
                        ms = new MemoryStream(data);
                        ms.Seek(0, SeekOrigin.Begin);
                        success = node.Send(data.Length, ms).Result;
                        if (success)
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "health":
                        Console.WriteLine("Healthy: " + node.IsHealthy);
                        break;
                }
            }
        }

        private static void InitializeNode()
        {
            while (localPort < 1 || remotePort < 1)
            {
                Console.Write("Local port  : ");
                localPort = Convert.ToInt32(Console.ReadLine());
                Console.Write("Remote port : ");
                remotePort = Convert.ToInt32(Console.ReadLine());
            }

            node = new ClusterNode("127.0.0.1", localPort, "127.0.0.1", remotePort, null, null);
            node.AcceptInvalidCertificates = true;
            node.MutuallyAuthenticate = false;
            node.Debug = false;
            node.PresharedKey = "0000000000000000";

            node.MessageReceived = MessageReceived;
            node.ClusterHealthy = ClusterHealthy;
            node.ClusterUnhealthy = ClusterUnhealthy;

            node.Start();
        }

        private static void Menu()
        {
            Console.WriteLine("---");
            Console.WriteLine(" q           quit");
            Console.WriteLine(" ?           this menu");
            Console.WriteLine(" cls         clear screen");
            Console.WriteLine(" send        send message to peer");
            Console.WriteLine(" send str    send message to peer using stream");
            Console.WriteLine(" health      display cluster health");
            Console.WriteLine("");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private static async Task ClusterHealthy()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("NOTICE: cluster is healthy");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private static async Task ClusterUnhealthy()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Console.WriteLine("NOTICE: cluster is unhealthy");
        }

        private static async Task MessageReceived(long contentLength, Stream stream)
        {
            if (contentLength < 1) return;

            int bytesRead = 0;
            int bufferLen = 65536;
            byte[] buffer = new byte[bufferLen];
            long bytesRemaining = contentLength;

            Console.Write("Received " + contentLength + " bytes: ");

            while (bytesRemaining > 0)
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    byte[] consoleBuffer = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, consoleBuffer, 0, bytesRead);
                    Console.Write(Encoding.UTF8.GetString(consoleBuffer));
                }

                bytesRemaining -= bytesRead;
            }

            Console.WriteLine("");
        }
    }
}