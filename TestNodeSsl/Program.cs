using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonCluster;

namespace TestNodeSsl
{
    class Program
    {
        static int localPort = 0;
        static int remotePort = 0;
        static string certFile = "";
        static string certPass = "";
        static ClusterNodeSsl n;

        static void Main(string[] args)
        {
            Console.Write("Local port  : ");
            localPort = Convert.ToInt32(Console.ReadLine());

            Console.Write("Remote port : ");
            remotePort = Convert.ToInt32(Console.ReadLine());

            Console.Write("Cert file   : ");
            certFile = Console.ReadLine();

            Console.Write("Cert pass   : ");
            certPass = Console.ReadLine();

            n = new ClusterNodeSsl("127.0.0.1", remotePort, localPort, certFile, certPass, true, ClusterHealthy, ClusterUnhealthy, MessageReceived, false);

            bool runForever = true;
            while (runForever)
            {
                Console.Write("Command [? for help]: ");
                string userInput = Console.ReadLine();
                if (String.IsNullOrEmpty(userInput)) continue;

                switch (userInput)
                {
                    case "?":
                        Console.WriteLine("---");
                        Console.WriteLine(" q          quit");
                        Console.WriteLine(" ?          this menu");
                        Console.WriteLine(" cls        clear screen");
                        Console.WriteLine(" send       send message to peer");
                        Console.WriteLine(" sendasync  send message to peer, asynchronously");
                        Console.WriteLine(" health     display cluster health");
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
                        if (Send(Encoding.UTF8.GetBytes(userInput)))
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "sendasync":
                        Console.Write("Data: ");
                        userInput = Console.ReadLine();
                        if (SendAsync(Encoding.UTF8.GetBytes(userInput)))
                        {
                            Console.WriteLine("Success");
                        }
                        else
                        {
                            Console.WriteLine("Failed");
                        }
                        break;

                    case "health":
                        Console.WriteLine("Healthy: " + n.IsHealthy());
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

        static bool Send(byte[] data)
        {
            return n.Send(data);
        }

        static bool SendAsync(byte[] data)
        {
            n.SendAsync(data).Wait();
            return true;
        }
    }
}
