using System;
using System.Collections.Generic;
using System.Text;

namespace ChordNodeServer
{
   public class ChordResource
   {
      public int ResourceKey { get; set; }
      public string FilePath { get; set; }

      public ChordResource(int key, string filePath)
      {
         ResourceKey = key;
         FilePath = filePath;
      }
   }
}
