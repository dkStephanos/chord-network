using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Timers;
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
      private System.Timers.Timer timer;
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
         // Set up timer to poll chord for structure every 15 seconds
         timer = new System.Timers.Timer(15000);
         timer.Elapsed += pollChord;
         timer.Start();
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

      public void ReportFingerTable()
      {
         string fingerTableStr = "";
         foreach(var entry in node.FingerTable)
         {
            fingerTableStr += "\nShortcut: " + entry.Key + ", NodeID: " + entry.Value.Key + " NodePort: " + entry.Value.Value;
         }

         ReportMessage("Finger Table:" + fingerTableStr);
      }

      public void ReportResources()
      {
         ReportMessage(node.listResources());
      }

      public void ReportClients()
      {
         string clientReport = "Current Clients:\n";

         foreach (var client in clients)
         {
            clientReport += "NodeID: " + client.Value.ChordID + "\n";
         }

         ReportMessage(clientReport);
      }

      public void StopPoll()
      {
         timer.Stop();
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
         byte[] buffer = new byte[1536];
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
               ReportMessage($"RECEIVED: {request}");
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
            case "updatepredecessor":
               HandleUpdatePredecessor(parameters);
               break;
            case "updatesuccessor":
               HandleUpdateSuccessor(parameters);
               break;
            case "updateresources":
               HandleUpdateResources(parameters);
               break;
            case "poll":
               HandlePoll(parameters);
               break;
            case "getresource":
               HandleGetResource(parameters);
               break;
            case "resourceresponse":
               HandleResourceResponse(parameters);
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
            string resourcesToSend = node.marshalResources(node.splitResources(joiningID, isOurSuccessor));

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

         // Unmarshal the resources attatched to joinresponse message and add them to the node (if they exist)
         if(parameters[3] != "None") node.addResources(node.unmarshalResources(parameters[3]));

         // Don't add a new client if our predecessor is the node we asked to join (which will be the only node in our clients bag)
         foreach(var client in clients)
         {
            if (client.Value.ChordID != node.PredecessorID) AddClient(node.PredecessorID, node.PredecessorPortNumber);

            // Don't double add the client if our predeccessor and successor are currently the same node
            if (node.PredecessorID != node.SuccessorID) AddClient(node.SuccessorID, node.SuccessorPortNumber);
         }

         // Send an updatepredecessor msg to our successor so we can get our resources
         Task.Factory.StartNew(
            () => {
               clients[node.SuccessorID].SendRequest("updatepredecessor " + node.ChordID + ":" + node.PortNumber);
            }
         );

         // Finally, report a success message with our predecessor and successor data to confirm Chord joined
         ReportMessage("Node " + node.ChordID + " joined Chord. Predecessor: " + node.PredecessorID + ", Successor: " + node.SuccessorID);
      }

      public void HandleLeaveRequest(string[] parameters)
      {
         string[] newPredecessor = parameters[1].Split(':');

         ReportMessage("Predecessor " + node.PredecessorID + " is leaving the chord. Setting " + newPredecessor[0] + " as new predecessor.");

         // store our old predecessorID so we can send the confirm message
         int leavingNodeID = node.PredecessorID;

         // Set new predecessor
         node.PredecessorID = Int32.Parse(newPredecessor[0]);
         node.PredecessorPortNumber = Int32.Parse(newPredecessor[1]);
  
         // If we were the only two nodes in the chord, update our successor as well
         if(leavingNodeID == node.SuccessorID)
         {
            node.SuccessorID = Int32.Parse(newPredecessor[0]);
            node.SuccessorPortNumber = Int32.Parse(newPredecessor[1]);
         }

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

         // Finally, send the confirm message so the leavingnode knows to disconnect
         Task.Factory.StartNew(
               () => {
                  clients[leavingNodeID].SendRequest("leaveresponse success");
                  // Finally, kill our connection to the client
                  DisconnectClient(leavingNodeID);
               }
            );
      }
      
      public void HandleLeaveResponse(string[] parameters)
      {
         // If our leave was successful, report a final message, and shutdown all our connections
         if (parameters[1] == "success")
         {
            ReportMessage("Leaving Chord. Nodes " + node.PredecessorID + " and " + node.SuccessorID + " are now linked.");
            DisconnectAllClients();
            _listener.Shutdown(SocketShutdown.Both);
            _listener.Disconnect(true);
            
         }
      }
      public void HandleUpdatePredecessor(string[] parameters)
      {
         string[] newPredecessor = parameters[1].Split(':');

         // If our predecessor is already correct, don't do anything, else set it, open the connection and split our resources, sending them to our predecessor
         if(node.PredecessorID != Int32.Parse(newPredecessor[0]))
         {
            // Save the odl PredecessorID so we can confirm the change and report that we are updating
            int oldPredecessorID = node.PredecessorID;
            ReportMessage("Updating Predecessor to " + newPredecessor[0]);

            // Set new Predecessor
            node.PredecessorID = Int32.Parse(newPredecessor[0]);
            node.PredecessorPortNumber = Int32.Parse(newPredecessor[1]);

            // initiate a connection if we dont' already have one,
            AddClient(node.PredecessorID, node.PredecessorPortNumber);

            // Split our resources that are being sent to the predecessor, and marshal them for transport
            string splitResources = node.marshalResources(node.splitResources(node.PredecessorID));

            // finally, if we are getting an updatePredecessor method, our new Predecessor needs resources, so send an updateresources msg with the split resources
            clients[node.PredecessorID].SendRequest("updateresources " + splitResources);
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

      public void HandleUpdateResources(string[] parameters)
      {
         node.addResources(node.unmarshalResources(parameters[1]));

         ReportMessage("Updated resources. Now responsible for " + node.resources.Count + " resources.");
      }

      // Initiates the poll of the chord structure
      public void pollChord(Object source, ElapsedEventArgs e)
      {
         string chordStructure = node.ChordID + ":" + node.PortNumber;
         clients[node.SuccessorID].SendRequest("poll " + chordStructure);
      }

      // Appends node info to poll message, and forwards to successor, unless we started the poll, in which case update our finger table
      public void HandlePoll(string[] parameters)
      {
         // First, get the first entry in the chordStructure poll to see if we've come full circle
         int chordID = Int32.Parse(parameters[1].Split(',')[0].Split(':')[0]);
         
         // If the poll has completed the trip around the chord, update our finger table
         if(chordID == node.ChordID)
         {
            node.updateFingerTable(parameters[1]);

            // Then, add all shortcuts in fingerTable to clients if not already there
            foreach (var entry in node.FingerTable)
            {
               AddClient(entry.Value.Key, entry.Value.Value);
            }

            // Finally, report that we updated the finger table
            ReportMessage("Finger Table updated");
         } else // Otherwise append data and forward to successor
         {
            parameters[1] += "," + node.ChordID + ":" + node.PortNumber;
            clients[node.SuccessorID].SendRequest("poll " + parameters[1]);
         }
      }

      public void RequestResource(int resourceID)
      {
         // Set bool value to see if we found the responsible node in our finger table
         bool foundNode = false;

         // First, make sure we aren't currently the owner of the resource
         foreach (var resource in node.resources)
         {
            if (resource.Key == resourceID)
            {
               ReportMessage(resource.Value.ResourceKey + ":" + resource.Value.FilePath);
               foundNode = true;
               break;
            }
         }

         // First, check our predecessor
         if(node.PredecessorID >= resourceID && foundNode == false)
         {
            // getresource request contains id of requested resource and ChordID:PortNumber of requesting node
            clients[node.PredecessorID].SendRequest("getresource " + resourceID + " " + node.ChordID + ":" + node.PortNumber);
            foundNode = true;
         } else if (foundNode == false) // Otherwise, check our fingerTable
         {
            // Step through our finger table, if we find the responsible node, send a getresource request and set foundNode to true
            foreach (var entry in node.FingerTable)
            {
               // If the key is >= to the resource ID, that node is responsible, if the value is -1, the finger table has been initialized yet
               if (entry.Value.Key >= resourceID && entry.Value.Key != -1)
               {
                  // getresource request contains id of requested resource and ChordID:PortNumber of requesting node
                  clients[entry.Value.Key].SendRequest("getresource " + resourceID + " " + node.ChordID + ":" + node.PortNumber);
                  foundNode = true;
                  break;
               }
            }
         }      

         // If we don't find the responsible node, then forward it to the furthest node in our finger table
         if (foundNode == false)
         {
            // Get the furthest offset by adding the furthest power of 2 to our own id
            int furthestOffset = node.ChordID + (int)Math.Pow(2, node.FingerTable.Count - 1);
            if (furthestOffset > 100) furthestOffset -= 100;

            // Make sure our finger table is updated first, otherwise log a message
            if(node.FingerTable[furthestOffset].Key != -1)
            {
               clients[node.FingerTable[furthestOffset].Key].SendRequest("getresource " + resourceID + " " + node.ChordID + ":" + node.PortNumber);
            } else
            {
               ReportMessage("FingerTable waiting on update");
            }
         }
      }

      public void HandleGetResource(string[] parameters)
      {
         // Parse data from request
         int resourceID = Int32.Parse(parameters[1]);
         int requestingNodeID = Int32.Parse(parameters[2].Split(':')[0]);
         int requestingNodePort = Int32.Parse(parameters[2].Split(':')[1]);
         bool resourceFound = false;

         // If the requested resource is in  our possession, send a resourceresponse message with the data
         foreach(var resource in node.resources)
         {
            if(resource.Value.ResourceKey == resourceID)
            {
               // First add the node as a client if it isn't already there
               PeerClient client = AddClient(requestingNodeID, requestingNodePort);
               client.SendRequest("resourceresponse " + resource.Value.ResourceKey + ":" + resource.Value.FilePath);
               resourceFound = true;
            }
         }
         // Otherwise, forward the request
         if (resourceFound != true)
         {
            ForwardResourceRequest(resourceID, requestingNodeID, requestingNodePort);
         }
      }

      public void ForwardResourceRequest(int resourceID, int requestingID, int requestingPort)
      {
         bool foundNode = false;

         // First, check our predecessor
         if (node.PredecessorID >= resourceID && foundNode == false)
         {
            // getresource request contains id of requested resource and ChordID:PortNumber of requesting node
            clients[node.PredecessorID].SendRequest("getresource " + resourceID + " " + requestingID + ":" + requestingPort);
            foundNode = true;
         }
         else // Otherwise, check our fingerTable
         {
            // Step through our finger table, if we find the responsible node, send a getresource request and set foundNode to true
            foreach (var entry in node.FingerTable)
            {
               // If the key is >= to the resource ID, that node is responsible, if the value is -1, the finger table has been initialized yet
               if (entry.Value.Key >= resourceID && entry.Value.Key != -1)
               {
                  // getresource request contains id of requested resource and ChordID:PortNumber of requesting node
                  clients[entry.Value.Key].SendRequest("getresource " + resourceID + " " + requestingID + ":" + requestingPort);
                  foundNode = true;
                  break;
               }
            }
         }

         // If we don't find the responsible node, then forward it to the furthest node in our finger table
         if (foundNode == false)
         {
            // Get the furthest offset by adding the furthest power of 2 to our own id
            int furthestOffset = node.ChordID + (int)Math.Pow(2, node.FingerTable.Count - 1);
            if (furthestOffset > 100) furthestOffset -= 100;

            // Make sure our finger table is updated first, otherwise just forward to our successor
            if (node.FingerTable[furthestOffset].Key != -1)
            {
               clients[node.FingerTable[furthestOffset].Key].SendRequest("getresource " + resourceID + " " + requestingID + ":" + requestingPort);
            }
            else
            {
               clients[node.SuccessorID].SendRequest("getresource " + resourceID + " " + requestingID + ":" + requestingPort);
            }
         }
      }

      // Takes in the return data for a resource request, and reports the data
      public void HandleResourceResponse(string[] parameters)
      {
         ReportMessage(parameters[1]);
      }

   } // end namespace
} // end server class
