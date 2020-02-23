![alt tag](https://github.com/jchristn/watsoncluster/blob/master/assets/watson.ico)

# Watson Cluster

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/WatsonCluster/
[nuget-img]: https://badge.fury.io/nu/Object.svg

Watson Cluster is the simplest, easiest, and fastest way to build highly-available applications using traditional 1+1 clustering.  Watson Cluster enables a one-to-one high availability cluster and is targeted to .NET Core and .NET Framework, with or without SSL.  Events are used to notify the encompassing application when the cluster is healthy (client and server connected) or unhealthy (client or server disconnected).

![alt tag](https://github.com/jchristn/WatsonCluster/blob/master/assets/image.png)

## New in v3.0.0

- Breaking changes; migration from Func-based callbacks to Events
- Added send with metadata functionality
- Dependency update

## Contributions

Thanks to @brudo for adding async support to WatsonCluster (v1.0.6) and all of your help from then!
 
## Simple Example

Refer to the ```TestNode``` project for a full example.

```
using WatsonCluster; 

// Initialize
ClusterNode node = new ClusterNode(
  "127.0.0.1",  // listener IP
  8000,         // listener port
  "127.0.0.1",  // peer IP
  8001,         // peer port
  null,         // PFX certificate filename, for SSL
  null);        // PFX certificate file password

// Set configurable parameters
node.AcceptInvalidCertificates = true;   // if using SSL
node.MutuallyAuthenticate = true;        // always leave as false if not using SSL 

// Set events
node.MessageReceived += MessageReceived; 
node.ClusterHealthy += ClusterHealthy;
node.ClusterUnhealthy += ClusterUnhealthy;

// Implement events
static void ClusterHealthy(object sender, EventArgs args)
{
  Console.WriteLine("Cluster is healthy!");
}

static void ClusterUnhealthy(object sender, EventArgs args)
{
  Console.WriteLine("Cluster is unhealthy!");
}

static void MessageReceived(object sender, MessageReceivedEventArgs args)
{
  Console.WriteLine("New message: " + Encoding.UTF8.GetString(args.Data));
}

// Let's go!
node.Start();

// Send some messages
await node.Send("Hello, world!");
```

## Test App

The ```TestNode``` project will help you understand and exercise the class library.  You can spawn two instances of the TestNode app using opposing ports to test the functionality.  The TestNode app hard-codes to ```127.0.0.1```.  

```
Node 1
C:\node1> testnode.exe
Local port  : 8000
Remote port : 8001

Node 2
C:\node2> testnode.exe
Local port  : 8001
Remote port : 8000
```

## Running under Mono

While .NET Core is the preferred environment for multi-platform deployments, WatsonCluster supports Mono environments with .NET Framework.  
Watson works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux with Mono you must supply a specific IP address ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```

## Version History

Refer to CHANGELOG.md for details from previous versions.
