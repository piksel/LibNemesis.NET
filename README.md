# LibNemesis.NET

A library for handling TCP connections using a reversed model. The purpose for this is to avoid having to deal with firewalls, port forwarding and dynamic IPs.

The terminology (that probably should be changed) refers to how one application (called a client) connects to multiple instances of another application (called servers). The terms do not explain the relationship between the applications unfortunatly, they are just due to how the concept was thought of.

## Flow

Whenever a server instance is created it will connect to it's specified client and identify itself with it's GUID.
The client will respond with a single byte where any non-zero value means that it did not accept the identity.

If the client accepted the identity the connection will remain active until either side sends a command followed by a zero-byte. The other side will then write a response to the socket and close it.

Whenever the connection to a client is closed, the server will attempt a new connection.

## Examples
### Client
```C#
  // Specify an EndPoint and a port to listen to
  var client = new Nemesis.Client(new IPEndPoint(IPAddress.Loopback, PORT));
  
  // Handle incoming commands with an event listener
  client.CommandRecieved += Client_CommandRecieved;
  
  // Send commands to a specific server
  var response = await client.SendCommand("Command", serverGuid);
```

### Server
```C#
  // Generate a GUID for the server
  var serverGuid = Guid.NewGuid();

  // Specify the GUID and the clients port and host
  var server = new Nemesis.Server(serverGuid, PORT, "localhost");
  
  // Handle incoming commands with an event listener
  server.CommandRecieved += Client_CommandRecieved;
  
  // Send commands to the client
  var response = await server.SendCommand("Command");
```

## Notes

There is **no** security *whatsoever* in the library. Any server can claim to be of any identity, therefore any real authentication should be handled within the calling application.

Both the command and response are strings, but the command is limited to 2048 UTF8 bytes for now.
