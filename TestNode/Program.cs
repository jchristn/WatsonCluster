using System;
using System.Collections.Generic;
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
                        success = node.Send(userInput);
                        if (success)
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
                        ms.Seek(0, SeekOrigin.Begin);
                        success = node.Send(data.Length, ms);
                        if (success)
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "send md":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        Dictionary<object, object> md = new Dictionary<object, object>();
                        md.Add("Key1", "Value1");
                        md.Add("Key2", "Value2");
                        success = node.Send(md, userInput);
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
            node.PresharedKey = "0000000000000000";

            node.MessageReceived += MessageReceived;
            node.ClusterHealthy += ClusterHealthy;
            node.ClusterUnhealthy += ClusterUnhealthy;
            node.Logger = Logger;
            node.Start();
        }

        private static void Menu()
        {
            Console.WriteLine("---");
            Console.WriteLine(" q              quit");
            Console.WriteLine(" ?              this menu");
            Console.WriteLine(" cls            clear screen");
            Console.WriteLine(" send           send message to peer");
            Console.WriteLine(" send stream    send message to peer using stream");
            Console.WriteLine(" health         display cluster health");
            Console.WriteLine("");
        }

        private static void ClusterHealthy(object sender, EventArgs args)
        {
            Console.WriteLine("NOTICE: cluster is healthy");
        }

        private static void ClusterUnhealthy(object sender, EventArgs args)
        {
            Console.WriteLine("NOTICE: cluster is unhealthy");
        }

        private static void MessageReceived(object sender, MessageReceivedEventArgs args)
        {
            if (args.ContentLength < 1) return;

            int bytesRead = 0;
            int bufferLen = 65536;
            byte[] buffer = new byte[bufferLen];
            long bytesRemaining = args.ContentLength;

            Console.Write("Received " + args.ContentLength + " bytes: ");

            while (bytesRemaining > 0)
            {
                bytesRead = args.DataStream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    byte[] consoleBuffer = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, consoleBuffer, 0, bytesRead);
                    Console.Write(Encoding.UTF8.GetString(consoleBuffer));
                }

                bytesRemaining -= bytesRead;
            }

            if (args.Metadata != null && args.Metadata.Count > 0)
            {
                Console.WriteLine("");
                Console.WriteLine("Metadata:");
                foreach (KeyValuePair<object, object> curr in args.Metadata)
                    Console.WriteLine("  " + curr.Key.ToString() + ": " + curr.Value.ToString());
            }

            Console.WriteLine("");
        }

        private static void Logger(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}