using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace PeerToPeer
{
   public class PeerServer : IObservable<string>
   {
      private readonly ConcurrentBag<IObserver<string>> _observers;
      private readonly AutoResetEvent _autoResetEvent;
      private readonly int _portNumber;
      private IPHostEntry _ipHostInfo;
      private IPEndPoint _localEndPoint;
      private Socket _listener;
      private int _numberOfConnections;
      public int NumberOfConnections { get { return _numberOfConnections; } }
      private string _username;

      public int BackLog { get; set; } = 10;

      public bool TimeToExit { get; set; } = false;

      public IPAddress IPAddress { get; private set; }

      public PeerServer(AutoResetEvent autoResetEvent, string username, int portNumber = 11000)
      {
         _observers = new ConcurrentBag<IObserver<string>>();
         _autoResetEvent = autoResetEvent;
         _portNumber = portNumber;
         _numberOfConnections = 0;
         _username = username;
         SetUpLocalEndPoint();
      }

      public string GetServerInfo()
      {
            return _username + ':' + _portNumber.ToString();
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
         do
         {
            data = "";
            // Process the connection to read the incoming data
            while (true)
            {
               int bytesRec = handler.Receive(buffer);
               data += Encoding.ASCII.GetString(buffer, 0, bytesRec);
               int index = data.IndexOf("<EOF>");
               if (index > -1)
               {
                  request = data.Substring(0, index);
                  break;
               }
            }
            ReportMessage($"RECEIVED:{request}");
            // Process the incoming data
            byte[] msg = Encoding.ASCII.GetBytes(request);
            handler.Send(msg);
         } while (request != "Exit");

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
