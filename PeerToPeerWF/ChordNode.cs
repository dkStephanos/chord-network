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

      public List<KeyValuePair<int, ChordResource>> resources;

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
         resources = new List<KeyValuePair<int, ChordResource>>();
         // If we're the starter node, initialize a dummy list of resources to demonstrate resource mgmt inside the chord
         if(ChordID == 1)
         {
            for(int i = 1; i < 101; i++)
            {
               resources.Add(new KeyValuePair<int, ChordResource>(i, new ChordResource(i, "File" + i + ".txt")));
            }
         }
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

      public bool isSuccessor(int nodeID)
      {
         bool isOurSuccessor = false;        // We need to determine if the joining node should be inserted after us, assume false to start
         int offsetSuccessorID = SuccessorID + 100;

         // If we are the only node in the chord, insert node after us
         if (PredecessorID == ChordID) isOurSuccessor = true;
         // If the joining nodeID is greater than our, but less than our successor, insert node after us
         if (nodeID > ChordID && nodeID < SuccessorID) isOurSuccessor = true;
         // Finally, if our successorID is less than our own, the above check will fail, so check if nodeID falls between ours and our successor + 100
         if (ChordID > SuccessorID && (nodeID > ChordID && nodeID < offsetSuccessorID)) isOurSuccessor = true;

         return isOurSuccessor;
      }

      public string listResources()
      {
         string resourceList = "Node resources:\n";

         // If there are resources, append them to the output string, otherwise, append "None"
         if (resources.Count > 0)
         {
            foreach (var resource in resources)
            {
               resourceList += "Key " + resource.Key + ") " + resource.Value.FilePath + "\n";
            }
         }
         else
         {
            resourceList += "None";
         }

         return resourceList;
      }

      public void sortResources()
      {
         // Sort resource list by key value (small-> large)
         resources.Sort(delegate (KeyValuePair<int, ChordResource> x, KeyValuePair<int, ChordResource> y)
         {
            return x.Key.CompareTo(y.Key);
         });
      }
   }
}
