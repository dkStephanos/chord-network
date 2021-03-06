﻿using System;
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
      public Dictionary<int, KeyValuePair<int, int>> FingerTable { get; set; }

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
         // Initialize the keys for the fingerTable, setting values to -1 (to be set later upon joining, and updated on a timer)
         FingerTable = new Dictionary<int, KeyValuePair<int, int>>(6);
         int nextID;
         for (int j = 0; j < 6; j++)
         {
            // Calculate the nextID for the finger table, if it is greater than 100, subtract 100 because we have circled the chord
            nextID = ChordID + (int)Math.Pow(2, j);
            if (nextID > 100) nextID = nextID - 100;
            FingerTable.Add(nextID, new KeyValuePair<int, int>(-1, -1));
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

      // Converts a resource list to a string to be sent across the network
      public string marshalResources(List<KeyValuePair<int, ChordResource>> resourceList)
      {
         // Initialize output string, just set to None if we get an empty list
         string marshalledResources = resourceList.Count > 0 ? "" : "None";

         foreach(KeyValuePair<int, ChordResource> resource in resourceList)
         {
            marshalledResources += resource.Value.ResourceKey + ":" + resource.Value.FilePath;
            // If this isn't the last item in the list, also append a comma
            marshalledResources += resource.Equals(resourceList[resourceList.Count - 1]) ? "" : ",";
         }

         return marshalledResources;
      }

      // Converts a resource list to a string to be sent across the network
      public List<KeyValuePair<int, ChordResource>> unmarshalResources(string resourcesString)
      {
         // Initialize output string, just set to None if we get an empty list
         List<KeyValuePair<int, ChordResource>> newResources = new List<KeyValuePair<int, ChordResource>>();
         // Split resourceList by comma, getting an array of ResourceKey:FilePath strings
         string[] resourceList = resourcesString.Split(',');

         foreach (string resource in resourceList)
         {
            // Split the resource string by colon to get the key and filepath, create a new ChordResource out of them, and append to newResources
            newResources.Add(new KeyValuePair<int, ChordResource>(Int32.Parse(resource.Split(':')[0]), new ChordResource(Int32.Parse(resource.Split(':')[0]), resource.Split(':')[1])));
         }

         return newResources;
      }

      // Converts a resource list to a string to be sent across the network
      public void addResources(List<KeyValuePair<int, ChordResource>> resourcesToAdd)
      {
         // Add the new resources to our collection, and sor the result
         resources.AddRange(resourcesToAdd);
         sortResources();
      }

      // Takes a nodeID, removes any resource that belongs to the passed nodeID from the resources collection, and returns the split resource list
      // Takes optional paramter, forSuccessor, so we can add resources that are greater than nodeID, but should be included (like resource 100 for node 1)
      public List<KeyValuePair<int, ChordResource>> splitResources(int nodeID, bool forSuccessor = false)
      {
         List<KeyValuePair<int, ChordResource>> splitResourceList = new List<KeyValuePair<int, ChordResource>>();

         // If the nodeID is greater than ours, give them all the resources between our IDs
         if(nodeID > ChordID)
         {
            foreach (KeyValuePair<int, ChordResource> resource in resources)
            {
               if (resource.Key <= nodeID && resource.Key > ChordID)
               {
                  splitResourceList.Add(resource);
               }
            }

            resources.RemoveAll(resource => resource.Key <= nodeID && resource.Key > ChordID);
         } else  // Otherwise, our new successor has a lower ID, and gets all our resources less than its ID
         {
            foreach (KeyValuePair<int, ChordResource> resource in resources)
            {
               if (resource.Key <= nodeID)
               {
                  splitResourceList.Add(resource);
               }
            }

            resources.RemoveAll(resource => resource.Key <= nodeID);
         }
         // Finally, if this node is our successor, but also has a lower ID than ours, they should also get all the resources greater than our ID
         if(forSuccessor && nodeID < ChordID)
         {
            foreach (KeyValuePair<int, ChordResource> resource in resources)
            {
               if (resource.Key > ChordID)
               {
                  splitResourceList.Add(resource);
               }
            }

            resources.RemoveAll(resource => resource.Key > ChordID);
         }

         return splitResourceList;
      }

      // Takes a list of nodes (ChordID:PortNumber) in order of the current nodes present in the string, and updates the finger table accordingly
      public void updateFingerTable(string chordStructure)
      {
         // Split the chordStructure into its independent nodes
         string[] chordNodes = chordStructure.Split(',');
         int currentID;
         int currentPort;
         SortedList<int, int> currentChord = new SortedList<int, int>();

         // First step through the nodes in the structure, rip off their ID and port and add to a SortedList by ID
         foreach (string node in chordNodes)
         {
            // Rip off the ID and port number of the current node
            currentID = Int32.Parse(node.Split(':')[0]);
            currentPort = Int32.Parse(node.Split(':')[1]);

            currentChord.Add(currentID, currentPort);
         }
         
         // Set up a tempFingerTable to update as we loop through, also set a flag so we know if there isn't a node in the chord >= the entry
         Dictionary<int, KeyValuePair<int, int>> tempFingerTable = new Dictionary<int, KeyValuePair<int, int>>();
         bool nodeFound = false;

         // Step through each entry in the finger table, so we can assign a node
         foreach (var entry in FingerTable)
         {
            // For each finger table entry, step through the nodes to find the one responsible for the current entry key
            foreach (var node in currentChord)
            {
               // If the currentID in the structure is >= the finger table key, update the temp finger table, and break, moving on to the next entry
               if(node.Key >= entry.Key)
               {
                  tempFingerTable.Add(entry.Key, new KeyValuePair<int, int>(node.Key, node.Value));
                  nodeFound = true;
                  break;
               }
            }
            // If we get this far and we didn't find a node, we have traveled all the way around the chord, so set the node to the first in the chord
            if(nodeFound == false)
            {
               tempFingerTable.Add(entry.Key, new KeyValuePair<int, int>(currentChord.Keys[0], currentChord.Values[0]));
            } else // Otherwise, reset the nodeFound bool for the next pass
            {
               nodeFound = false;
            }
         }
         // Finally, now that we are out of the loop, set the FingerTable to our computed temp
         FingerTable = tempFingerTable;
      }

   } // end ChordNode
}
