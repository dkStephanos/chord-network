using PeerToPeer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PeerToPeerWF
{
   public partial class PeerToPeerForm : Form
   {
      private AutoResetEvent _serverResetEvent;
      private PeerServer _server;
      
      public PeerToPeerForm()
      {
         InitializeComponent();
         _serverResetEvent = new AutoResetEvent(false);
      }

      private void PeerToPeerForm_Load(object sender, EventArgs e)
      {
      }

        private void SendCommand()
        {
            var command = commandBox.Text;
            ProcessCommand(command);

            commandBox.Clear();
        }

        private void btnSend_MouseClick(object sender, MouseEventArgs e)
        {
            SendCommand();
        }


        private void commandBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                SendCommand();
        }

        private void ProcessCommand(string command)
      {
         var length = command.Length;
         var index = command.IndexOf(' ');
         var cmd = command.Substring(0, index).ToLower();
         var parameters = command[(index+1)..length];
         
         switch (cmd)
         {
            case "set":
               ProcessSet(parameters);
               break;
            case "connect":
               ProcessConnect(parameters);
               break;
            case "send":
               ProcessSend(parameters);
               break;
            case "chat":            //Not fully implemented
                ProcessChat(parameters);
                break;
         }
         
      }

        //Not fully implemented
        private void ProcessChat(string parameters)
        {
            string[] paramArray = parameters.Split('|');
            var length = paramArray[0].Length;
                        var indexOfUser = paramArray[0].IndexOf(' ');
            var user = paramArray[0].Substring(0, indexOfUser).ToLower();
            var message = paramArray[0][(indexOfUser + 1)..length];
            string[] historyArray;
            string history;

            // If there is a message history on the chat, handle that
            if (paramArray.Length > 1) {
               historyArray = paramArray[1].Remove(0,1).Split(' ');

                // Check the message history, adding a new connection for each user not in clients
                bool alreadyConnected = false;
                foreach (var historyEntry in historyArray)
                {
                    foreach (var client in _server.clients)
                    {
                        if (client.GetUsername() == historyEntry.Split(':')[0])
                        {
                            alreadyConnected = true;
                            continue;
                        }
                    }

                    if (!alreadyConnected)
                    {
                        ProcessConnect(historyEntry);
                    }

                    alreadyConnected = false;
                }

                // Update the history, keeping amount of users the same, but appending self to list
                history = _server.GetServerInfo() + " " + historyArray[1];
            } else
            {
                // Create a message history with us as the only entry
                history = _server.GetServerInfo();
            }
            
            // Check if we have a connection to the addressed user, and if we do, send the message
            bool foundAddressedUser = false;
            Task.Factory.StartNew(
                () => {
                    foreach (var client in _server.clients)
                    {
                        if (client.GetUsername() == user)
                        {
                            client.SendRequest(message);
                            foundAddressedUser = true;
                            continue;
                        }
                    }
                }
             );
            
            // Otherwise, append the new message history, and forward it out to our connections
            if(!foundAddressedUser)
            {
                Task.Factory.StartNew(
                   () => {
                       foreach (var client in _server.clients)
                       {
                           client.SendRequest("chat " + user + " " + message + " | " + history);
                       }
                   }
                );
            }
        }

      private void ProcessSend(string parameters)
      {
         Task.Factory.StartNew(
            () => {
               foreach(var client in _server.clients)
               {
                  client.SendRequest(parameters);
               }
            }
         );
      }

      private void ProcessConnect(string parameters)
      {
         
         string username = parameters.Split(':')[0];
            int port = Int32.Parse(parameters.Split(':')[1]);
            var client = new PeerClient(username);
         _server.clients.Add(client);
         client.Subscribe(new StringObserver(txtMain));
         client.SetUpRemoteEndPoint(_server.IPAddress, port);
         client.ConnectToRemoteEndPoint();
            Task.Factory.StartNew(
            () => client.ReceiveResponse()
         ); 
      }

      private void ProcessSet(string parameters)
      {
         var options = parameters.Split(':');
         txtMain.Text += "User: " + options[0] + Environment.NewLine;
         txtMain.Text += "Port: " + options[1] + Environment.NewLine;
         int port = Int32.Parse(options[1]);
         string username = options[0].ToLower();
         _server = new PeerServer(_serverResetEvent, username, port);
         _server.Subscribe(new StringObserver(txtMain));
         _server.StartListening();
         Task.Factory.StartNew(
            () => _server.WaitForConnection()
         );
      }

    }
}
