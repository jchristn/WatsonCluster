# Watson Cluster

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/WatsonCluster/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# class using WatsonTCP to enable a one-to-one high availability cluster targeted to .NET Core and .NET Framework, with or without SSL.  Callbacks are used to notify the encompassing application when the cluster is healthy (client and server connected) or unhealthy (client or server disconnected).

![alt tag](https://github.com/jchristn/WatsonCluster/blob/master/assets/image.png)

## New in v1.2.x

- Stream support to enable transmission of larger messages or more efficient processing
- Simplified constructors, eliminated SSL-specific classes (merged together)

## Contributions

Thanks to @brudo for adding async support to WatsonCluster (v1.0.6) and all of your help from then!
 
## Simple Example

Refer to the ```TestNode``` project for a full example.

```
using WatsonCluster;
using System.IO;

// Initialize
ClusterNode node = new ClusterNode(
	"127.0.0.1", 	// listener IP
	8000, 			// listener port
	"127.0.0.1", 	// peer IP
	8001, 			// peer port
	null, 			// PFX certificate filename, for SSL
	null);			// PFX certificate file password

// Set configurable parameters
n.AcceptInvalidCertificates = true;		// if using SSL
n.MutuallyAuthenticate = true;			// if using SSL
n.Debug = false;						// console debugging
n.ReadDataStream = false;				// set to true to use MessageReceived, else use StreamReceived

// Set your callbacks
n.MessageReceived = MessageReceived;
n.StreamReceived = StreamReceived;
n.ClusterHealthy = ClusterHealthy;
n.ClusterUnhealthy = ClusterUnhealthy;

// Let's go!
n.Start();

static bool ClusterHealthy()
{
	// handle cluster healthy events
	return true;
}

static bool ClusterUnhealth()
{
	// handle cluster unhealthy events
	// don't worry, we'll try reconnecting on your behalf!
	return true;
}

static bool MessageReceived(byte[] data)
{
	// messages will appear here when ReadDataStream = true
	// process your message
	return true;
}

static bool StreamReceived(long contentLength, byte[] data)
{
	// messages will appear here when using ReadDataStream = false
	// read contentLength bytes from stream
	// process your message
	return true;
}
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

Notes from previous versions will be shown here.

v1.1.x

- SSL support

v1.0.x

- Initial release
