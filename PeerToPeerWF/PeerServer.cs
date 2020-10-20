using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using ChordNodeServer;

namespace PeerToPeer
{
    public class PeerServer : IObservable<string>
    {
      private readonly ConcurrentBag<IObserver<string>> _observers;
      public ConcurrentDictionary<int, PeerClient> clients;
      private readonly AutoResetEvent _autoResetEvent;
      private readonly int _portNumber;
      private IPHostEntry _ipHostInfo;
      private IPEndPoint _localEndPoint;
      private Socket _listener;
      private int _numberOfConnections;
      private ConcurrentQueue<string> _messages;
      public ChordNode node;
       
      public int NumberOfConnections { get { return _numberOfConnections; } }
      
      public int BackLog { get; set; } = 10;

      public bool TimeToExit { get; set; } = false;

      public IPAddress IPAddress { get; private set; }

      public PeerServer(AutoResetEvent autoResetEvent, int portNumber = 11000)
      {
         _observers = new ConcurrentBag<IObserver<string>>();
         clients = new ConcurrentDictionary<int, PeerClient>();
         _messages = new ConcurrentQueue<string>();
         _autoResetEvent = autoResetEvent;
         _portNumber = portNumber;
         _numberOfConnections = 0;
         SetUpLocalEndPoint();
         node = new ChordNode(portNumber);
      }

      public string GetServerInfo()
      {
         return node.ChordID + ':' + _portNumber.ToString();
      }

      public string GetPredecessorInfo()
      {
         return node.PredecessorID + ':' + node.PredecessorPortNumber.ToString();
      }

      public string GetSuccessorInfo()
      {
         return node.SuccessorID + ':' + node.SuccessorPortNumber.ToString();
      }

      public void ReportServerInfo()
      {
         ReportMessage("Server Info: " + node.ChordID + ':' + _portNumber.ToString());
      }

      public void ReportNodeInfo()
      {
         ReportMessage("Predecessor: " + node.PredecessorID + ':' + node.PredecessorPortNumber);
         ReportMessage("Successor: " + node.SuccessorID + ':' + node.SuccessorPortNumber);
      }

      public void ReportResources()
      {
         ReportMessage(node.listResources());
      }

      private void SetUpLocalEndPoint()
      {
         _ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
         IPAddress = _ipHostInfo.AddressList[0];
         _localEndPoint = new IPEndPoint(IPAddress, _portNumber);
      }

      public void StartListening()
      {
         _listener = new Socket(IPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
         _listener.Bind(_localEndPoint);
         _listener.Listen(BackLog);
      }

      public void WaitForConnection()
      {
         do
         {
            ReportMessage("Waiting for a connection...");
            Socket handler = _listener.Accept();
            Task.Factory.StartNew(
               () => HandleRequest(handler)
            );
         } while (!TimeToExit);
         _autoResetEvent.Set();
      }

      public PeerClient AddClient(int nodeID, int portNumber)
      {
         var client = new PeerClient();

         // Only AddClient if a connection doesn't exist and the requested nodeID isn't our own
         if (nodeID != node.ChordID && !clients.ContainsKey(nodeID))
         {
            clients.TryAdd(nodeID, client);
            client.SetUpRemoteEndPoint(IPAddress, portNumber);
            client.ConnectToRemoteEndPoint();
            client.ChordID = nodeID;
         } else if(clients.ContainsKey(nodeID))    // If we do already have a connection, set that client to return
         {
            client = clients[nodeID];
         }
         
         return client;
      }

      public void DisconnectClient(int nodeID)
      {
         foreach (var client in clients)
         {
            if (client.Key == nodeID)
            {
               client.Value.Disconnect();
               break;
            }
         }
      }

      public void DisconnectAllClients()
      {
         foreach (var client in clients)
         {
            client.Value.Disconnect();
         }

         clients.Clear();
      }

      public IDisposable Subscribe(IObserver<string> observer)
      {
         if (!_observers.Contains(observer))
            _observers.Add(observer);

         return new MessageUnsubscriber(_observers, observer);
      }

      public void ReportMessage(string message)
      {
         foreach (var observer in _observers)
         {
            observer.OnNext(message);
         }
      }

      // Handles incoming requests to server using a buffer to handle bursts, parsing requests and passing off to HandleMessage
      private void HandleRequest(Socket handler)
      {
         Interlocked.Increment(ref _numberOfConnections);
         ReportMessage($"Number of connections: {_numberOfConnections}");
         byte[] buffer = new byte[1024];
         string data;
         string request;
         bool shutdown = false;
         do
         {
            data = "";
            // Process the connection to read the incoming data
            int bytesRec = handler.Receive(buffer);
            data += Encoding.ASCII.GetString(buffer, 0, bytesRec);
            string[] bufferMsgs = data.Split("<EOF>");
            foreach (string bufferMsg in bufferMsgs)
            {
               if (bufferMsg != "") _messages.Enqueue(bufferMsg);
            }
            // Drain (empty) the message que, passing each off to the HandleMessage question to perform neccessary operation
            while (_messages.Count > 0)
            {
               _messages.TryDequeue(out request);
               ReportMessage($"RECEIVED:{request}");
               if (request == "Exit")
               {
                  shutdown = true;
                  break;
               }
               HandleMessage(request);

            }
         } while (!shutdown);

         Interlocked.Decrement(ref _numberOfConnections);
         ReportMessage($"Number of connections: {_numberOfConnections}");

         handler.Shutdown(SocketShutdown.Both);
         handler.Close();
      }

      // Interprets message request types and passes them off to the corresponding handler
      public void HandleMessage(string message)
      {
         string[] parameters = message.Split(' ');
         
         switch(parameters[0])
         {
            case "join":
               HandleJoin(parameters[1].Split(':'));
               break;
            case "joinresponse":
               HandleJoinResponse(parameters);
               break;
            case "leaverequest":
               HandleLeaveRequest(parameters);
               break;
            case "leaveresponse":
               HandleLeaveResponse(parameters);
               break;
            case "updatesuccessor":
               HandleUpdateSuccessor(parameters);
               break;
            default:
               break;
         }
      }

      public void HandleJoin(string[] parameters)
      {
         int joiningID = Int32.Parse(parameters[0]);
         int joiningPortNumber = Int32.Parse(parameters[1]);
         bool isOurSuccessor = node.isSuccessor(joiningID);


         // If the joining node is our successor, proccess the join
         if(isOurSuccessor)
         {
            // First, create a new client instance for the joining node, so we can respond
            var client = AddClient(joiningID, joiningPortNumber);

            // Then split our resources that will belong to the joining node
            string resourcesToSend = node.marshalResources(node.splitResources(joiningID));

            Task.Factory.StartNew(
               () => {
                  // Send a joinresponse with the ChordID:PortNumber of predecessor and successor to join
                  client.SendRequest("joinresponse " + node.ChordID + ":" + _portNumber + " " + node.SuccessorID + ":" + node.SuccessorPortNumber + " " + resourcesToSend);
                  // Finally, set our Successor to the newly joining node (and our Predecessor if we were the only node)
                  if(node.PredecessorID == node.ChordID)
                  {
                     node.PredecessorID = joiningID;
                     node.PredecessorPortNumber = joiningPortNumber;
                  }
                  node.SuccessorID = joiningID;
                  node.SuccessorPortNumber = joiningPortNumber;
               }
            );

            // Finally, report a message so we can see we inserted a node into the chord
            ReportMessage("Added Node " + joiningID + " to the chord at port: " + joiningPortNumber);

         } else // Else, we forward the join message to our successor
         {
            // Get our successor from the clients dict
            PeerClient client;
            clients.TryGetValue(node.SuccessorID, out client);

            // Once we have it, forward the join request, report a status message, and break out of the loop
            client.SendRequest("join " + joiningID + ":" + joiningPortNumber);
            ReportMessage("Forwarded join request to node: " + node.SuccessorID);
         }
      }

      public void HandleJoinResponse(string[] parameters)
      {
         string[] predeccesorNodeData = parameters[1].Split(':');
         string[] successorNodeData = parameters[2].Split(':');

         node.PredecessorID = Int32.Parse(predeccesorNodeData[0]);
         node.PredecessorPortNumber = Int32.Parse(predeccesorNodeData[1]);
         node.SuccessorID = Int32.Parse(successorNodeData[0]);
         node.SuccessorPortNumber = Int32.Parse(successorNodeData[1]);

         // Unmarshal the resources attatched to joinresponse message and add them to the node
         node.resources.AddRange(node.unmarshalResources(parameters[3]));

         // Don't add a new client if our predecessor is the node we asked to join (which will be the only node in our clients bag)
         foreach(var client in clients)
         {
            if (client.Value.ChordID != node.PredecessorID) AddClient(node.PredecessorID, node.PredecessorPortNumber);

            // Don't double add the client if our predeccessor and successor are currently the same node
            if (node.PredecessorID != node.SuccessorID) AddClient(node.SuccessorID, node.SuccessorPortNumber);
         }
         
         // Finally, report a success message with our predecessor and successor data to confirm Chord joined
         ReportMessage("Node " + node.ChordID + " joined Chord. Predecessor: " + node.PredecessorID + ", Successor: " + node.SuccessorID);
      }

      public void HandleLeaveRequest(string[] parameters)
      {
         string[] newPredecessor = parameters[1].Split(':');

         ReportMessage("Predecessor " + node.PredecessorID + " is leaving the chord. Setting " + newPredecessor[0] + " as new predecessor.");

         // Set new predecessor
         node.PredecessorID = Int32.Parse(newPredecessor[0]);
         node.PredecessorPortNumber = Int32.Parse(newPredecessor[1]);

         //Unmarshal and append the resources from the leaving node (parameters[2]) to our own
         node.addResources(node.unmarshalResources(parameters[2]));

         // initiate a connection if we dont' already have one,
         PeerClient client = AddClient(node.PredecessorID, node.PredecessorPortNumber);

         // Tell it we are its new successor(unless we're now the last node)
         if (node.PredecessorID != node.ChordID)
         {
            Task.Factory.StartNew(
               () => {
                  client.SendRequest("updatesuccessor " + node.ChordID + ":" + node.PortNumber);
               }
            );
         }
      }
      
      public void HandleLeaveResponse(string[] parameters)
      {
         // If our leave was successful, report a final message, and shutdown all our connections
         if (parameters[1] == "success")
         {
            ReportMessage("Leaving Chord. Nodes " + node.PredecessorID + " and " + node.SuccessorID + " are now linked.");
            DisconnectAllClients();
         }
      }

      public void HandleUpdateSuccessor(string[] parameters)
      {
         string[] newSuccessor = parameters[1].Split(':');

         // Save the odl successorID so we can confirm the change and report that we are updating
         int oldSuccessorID = node.SuccessorID;
         ReportMessage("Updating Successor to " + newSuccessor[0]);

         // Set new Successor
         node.SuccessorID = Int32.Parse(newSuccessor[0]);
         node.SuccessorPortNumber = Int32.Parse(newSuccessor[1]);

         // initiate a connection if we dont' already have one,
         AddClient(node.SuccessorID, node.SuccessorPortNumber);

         // finally, if we are getting an updatesuccessor method, our previous successor triggered a leave, so send the leaveresponse message
         clients[oldSuccessorID].SendRequest("leaveresponse success");
      }

   } // end namespace
} // end server class
