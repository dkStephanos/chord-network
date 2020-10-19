using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ChordNodeServer
{
   public class ChordNode
   {
      // ChordID used to mark position in chord, default to 1 for initial chord, updated based on port number in constructor
      public int ChordID { get; set; } = 1;
      public int PortNumber { get; set; } = 11000;
      public int PredecessorID { get; set; } = 1;
      public int SuccessorID { get; set; } = 1;
      public int PredecessorPortNumber { get; set; } = 1;
      public int SuccessorPortNumber { get; set; } = 1;

      public ChordNode(int portNumber)
      {
         ChordID = portNumber == 11000 ? 1 : hashPortToNodeID(portNumber);
         PortNumber = portNumber;
         // Initializes PredecessorID/PortNumber and SuccessorID/PortNumber to ChordID/PortNumber
         //(this will be correct if we're the intial node, otherwise will be updated when joining the chord)
         PredecessorID = ChordID;
         PredecessorPortNumber = portNumber;
         SuccessorID = ChordID;
         SuccessorPortNumber = portNumber;
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
   }
}
