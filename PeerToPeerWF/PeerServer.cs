﻿using System;
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
               // Process the incoming data
               byte[] msg = Encoding.ASCII.GetBytes(request);
               handler.Send(msg);

            }
         } while (!shutdown);

         Interlocked.Decrement(ref _numberOfConnections);
         ReportMessage($"Number of connections: {_numberOfConnections}");

         handler.Shutdown(SocketShutdown.Both);
         handler.Close();
      }

      public IDisposable Subscribe(IObserver<string> observer)
      {
         if (!_observers.Contains(observer))
            _observers.Add(observer);

         return new MessageUnsubscriber(_observers, observer);
      }

      public void ReportMessage(string message)
      {
         foreach(var observer in _observers)
         {
            observer.OnNext(message);
         }
      }
   }
}
