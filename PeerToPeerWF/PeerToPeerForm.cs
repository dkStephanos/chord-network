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
         this.ActiveControl = commandBox;
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
         var cmd = command; 
         if(index != -1) cmd = command.Substring(0, index).ToLower();
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
            case "join":
               ProcessJoin(parameters);
               break;
            case "exit":
               ProcessExit();
               break;
            case "nodeinfo":
               ProcessNodeInfo();
               break;
            case "showresources":
               ProcessShowResources();
               break;
         }       
      }

      private void ProcessSet(string parameters)
      {
         txtMain.Text += "Port: " + parameters + Environment.NewLine;
         int port = Int32.Parse(parameters);
         _server = new PeerServer(_serverResetEvent, port);
         _server.Subscribe(new StringObserver(txtMain));
         _server.StartListening();
         _server.ReportServerInfo();
         Task.Factory.StartNew(
            () => _server.WaitForConnection()
         );
      }

        private void ProcessConnect(string parameters)
        {
            int nodeID = Int32.Parse(parameters.Split(':')[0]);
            int port = Int32.Parse(parameters.Split(':')[1]);
            var client = new PeerClient();
            _server.clients.TryAdd(nodeID, client);
            client.Subscribe(new StringObserver(txtMain));
            client.SetUpRemoteEndPoint(_server.IPAddress, port);
            client.ConnectToRemoteEndPoint();
            client.ChordID = nodeID;
            Task.Factory.StartNew(
            () => client.ReceiveResponse()
         );
        }

        private void ProcessSend(string parameters)
        {
            Task.Factory.StartNew(
               () => {
                   foreach (var client in _server.clients)
                   {
                       client.Value.SendRequest(parameters);
                   }
               }
            );
        }

      private void ProcessJoin(string parameters)
      {
         Task.Factory.StartNew(
               () => {
                  foreach (var client in _server.clients)
                  {
                     client.Value.SendRequest("join " + parameters);
                  }
               }
            );
      }

      private void ProcessExit()
      {
         Task.Factory.StartNew(
               () => {
                  // To initiate leaving the chord, we alert our successor we are leaving, who its new predecessor is (ours)
                  // and transfer our resources
                  _server.clients[_server.node.SuccessorID].SendRequest("leaverequest " + _server.node.PredecessorID + ":" + _server.node.PredecessorPortNumber);
               }
            );
      }

      private void ProcessNodeInfo()
      {
         _server.ReportServerInfo();
         _server.ReportNodeInfo();
      }

      private void ProcessShowResources()
      {
         _server.ReportResources();
      }
   }
}
