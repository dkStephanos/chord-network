using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography;


namespace PeerToPeer
{
    public class PeerServer : IObservable<string>
    {
      private readonly ConcurrentBag<IObserver<string>> _observers;
      public ConcurrentBag<PeerClient> clients;
      private readonly AutoResetEvent _autoResetEvent;
      private readonly int _portNumber;
      private IPHostEntry _ipHostInfo;
      private IPEndPoint _localEndPoint;
      private Socket _listener;
      private int _numberOfConnections;
      private ConcurrentQueue<string> _messages;
       
      public int NumberOfConnections { get { return _numberOfConnections; } }

      // ChordID used to mark position in chord, default to 1 for initial chord, updated based on port number in constructor
      public int ChordID { get; set; } = 1;
      public int PredecessorID { get; set; } = 1;
      public int SuccessorID { get; set; } = 1;
      public int PredecessorPortNumber { get; set; } = 1;
      public int SuccessorPortNumber { get; set; } = 1;

      public int BackLog { get; set; } = 10;

      public bool TimeToExit { get; set; } = false;

      public IPAddress IPAddress { get; private set; }

      public PeerServer(AutoResetEvent autoResetEvent, int portNumber = 11000)
      {
         _observers = new ConcurrentBag<IObserver<string>>();
         clients = new ConcurrentBag<PeerClient>();
         _messages = new ConcurrentQueue<string>();
         _autoResetEvent = autoResetEvent;
         _portNumber = portNumber;
         _numberOfConnections = 0;
         SetUpLocalEndPoint();
         ChordID = portNumber == 11000 ? 1 : hashPortToNodeID(portNumber);
         // Initializes PredecessorID/PortNumber and SuccessorID/PortNumber to ChordID/PortNumber
         //(this will be correct if we're the intial node, otherwise will be updated when joining the chord)
         PredecessorID = ChordID;
         PredecessorPortNumber = _portNumber;
         SuccessorID = ChordID;
         SuccessorPortNumber = _portNumber;
      }

      private int hashPortToNodeID(int portNumber)
      {
         // Using SHA256 hashing, to convert the portNumber as a string to a hashed integer
         using (var sha256 = new SHA256Managed())
         {
               // Here we hash the port and convert to an int and make sure it is positive. 
               // We then need to reduce it to a value between 2-100 (100 is our maxNodes and 1 is our initial node)
               return Math.Abs(BitConverter.ToInt32(sha256.ComputeHash(Encoding.UTF8.GetBytes(portNumber.ToString())), 0)) % 98 + 2;
         }
      }

      public string GetServerInfo()
      {
         return ChordID + ':' + _portNumber.ToString();
      }

      public string GetPredecessorInfo()
      {
         return PredecessorID + ':' + PredecessorPortNumber.ToString();
      }

      public string GetSuccessorInfo()
      {
         return SuccessorID + ':' + SuccessorPortNumber.ToString();
      }

      public void ReportServerInfo()
      {
         ReportMessage("Server Info: " + ChordID + ':' + _portNumber.ToString());
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
         clients.Add(client);
         client.SetUpRemoteEndPoint(IPAddress, portNumber);
         client.ConnectToRemoteEndPoint();
         client.ChordID = nodeID;

         return client;
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
            default:
               break;
         }
      }

      public void HandleJoin(string[] parameters)
      {
         int joiningID = Int32.Parse(parameters[0]);
         int joiningPortNumber = Int32.Parse(parameters[1]);

         // If we are the only node in the chord, or if joining nodeID falls between ours and our Successor, we want to insert the node after us
         if(PredecessorID == SuccessorID || (joiningID > ChordID && joiningID < SuccessorID))
         {
            // First, create a new client instance for the joining node, so we can respond
            var client = AddClient(joiningID, joiningPortNumber);

            Task.Factory.StartNew(
               () => {
                     // Send a joinresponse with the ChordID:PortNumber of predecessor and successor to join
                     client.SendRequest("joinresponse " + ChordID + ":" + _portNumber + " " + SuccessorID + ":" + SuccessorPortNumber);
                  // Finally, set our Successor to the newly joining node
                  SuccessorID = joiningID;
                  SuccessorPortNumber = joiningPortNumber;
               }
            );

            // Finally, report a message so we can see we inserted a node into the chord
            ReportMessage("Added Node " + joiningID + " to the chord at port: " + joiningPortNumber);

         } else // Else, we forward the join message to our successor
         {
            // Loop through our clients, until we get our successor
            foreach(PeerClient client in clients)
            {
               // Once we find it, forward the join request, report a status message, and break out of the loop
               if (client.ChordID == SuccessorID)
               {
                  client.SendRequest("join " + joiningID + ":" + joiningPortNumber);
                  ReportMessage("Forwarded join request to node: " + SuccessorID);
                  break;
               }
            }
         }
      }

      public void HandleJoinResponse(string[] parameters)
      {
         string[] predeccesorNodeData = parameters[1].Split(':');
         string[] successorNodeData = parameters[2].Split(':');

         PredecessorID = Int32.Parse(predeccesorNodeData[0]);
         PredecessorPortNumber = Int32.Parse(predeccesorNodeData[1]);
         SuccessorID = Int32.Parse(successorNodeData[0]);
         SuccessorPortNumber = Int32.Parse(successorNodeData[1]);

         // Don't add a new client if our predecessor is the node we asked to join (which will be the only node in our clients bag)
         PeerClient client;
         clients.TryPeek(out client);
         if (client.ChordID != PredecessorID) AddClient(PredecessorID, PredecessorPortNumber);

         // Don't double add the client if our predeccessor and successor are currently the same node
         if (PredecessorID != SuccessorID) AddClient(SuccessorID, SuccessorPortNumber);

         // Finally, report a success message with our predecessor and successor data to confirm Chord joined
         ReportMessage("Node " + ChordID + " joined Chord. Predecessor: " + PredecessorID + ", Successor: " + SuccessorID);
      }
    }
}
