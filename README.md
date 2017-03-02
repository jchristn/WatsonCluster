# Watson Cluster

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/WatsonCluster/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# class using Watson TCP to enable a one-to-one high availability cluster.  Callbacks are used to notify the encompassing application when the cluster is healthy (client and server connected) or unhealthy (client or server disconnected).

![alt tag](https://github.com/jchristn/WatsonCluster/blob/master/assets/image.png)

## Contributions
Thanks to @brudo for adding async support to WatsonCluster (v1.0.6)!

## Test App
A test project using the ClusterNode class is included which will help you understand and exercise the class library.  You can spawn two instances of the TestNode app using opposing ports to test the functionality.  The TestNode app hard-codes to localhost.

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
Watson works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```
