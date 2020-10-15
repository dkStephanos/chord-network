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
         int port = Int32.Parse(parameters);
         var client = new PeerClient();
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
         txtMain.Text += "Port: " + parameters + Environment.NewLine;
         int port = Int32.Parse(parameters);
         _server = new PeerServer(_serverResetEvent, port);
         _server.Subscribe(new StringObserver(txtMain));
         _server.StartListening();
         Task.Factory.StartNew(
            () => _server.WaitForConnection()
         );
      }
    }
}
