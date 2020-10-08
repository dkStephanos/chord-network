using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace PeerToPeer
{
   public class MessageUnsubscriber : IDisposable
   {
      private readonly ConcurrentBag<IObserver<string>> _observers;
      private IObserver<string> _observer;

      public MessageUnsubscriber(ConcurrentBag<IObserver<string>> observers, IObserver<string> observer)
      {
         _observers = observers;
         _observer = observer;
      }

      public void Dispose()
      {
         if (!(_observer == null)) _observers.TryTake(out _observer);
      }
   }
}
