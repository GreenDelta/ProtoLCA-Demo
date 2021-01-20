# ProtoLCA-Demo
A small C# example that shows how to connect to openLCA 2 using a
[gRPC](https://grpc.io/) client. The example creates a process and tries to map
the flows of that process and link it to a background database. It than
calculates the linked process and returns an impact assessment result. Note that
openLCA 2 and its gRPC interface are still in development and all this is
experimental. 

## Usage
First you need to open a database in openLCA 2 and start the IPC server under
`Tools > Developer tools > IPC Server` on port `8080` with the  gRPC option
enabled:

![](images/start_grpc_server.png)

The server will run until you click on the stop button or close the dialog. You
can continue to work with the opened database while the server is running. Note
that in order to see updates of the client application in the database you may
have the refresh the navigation. You can do this via the `Refresh` button in the
navigation menu:

![](images/refresh_navigation.png)

Now you should be able to run the example application. 
