using System.Collections.Generic;

namespace CodeUtils
{
    public static class MessagingSystem<T>
    {
        private static readonly List<ISubscriber<T>> _subscribers = new List<ISubscriber<T>>();

        public static void Publish(T data)
        {
            for (int i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (_subscribers[i] != null)
                {
                    _subscribers[i].OnEvent(data);
                }
                else
                {
                    // Iterate in reverse so RemoveAt(i) never shifts an unvisited element.
                    // Log a warning rather than throwing — throwing mid-loop would silently
                    // skip all subscribers with a lower index.
                    _subscribers.RemoveAt(i);
                    UnityEngine.Debug.LogWarning($"[MessagingSystem] A null subscriber for event type '{typeof(T).Name}' was removed.");
                }
            }
        }

        public static void Subscribe(ISubscriber<T> subscriber)
        {
            _subscribers.Add(subscriber);
        }

        public static void Unsubscribe(ISubscriber<T> subscriber)
        {
            _subscribers.Remove(subscriber);
        }
    }

    public interface ISubscriber<T>
    {
        void OnEvent(T evt);
    }
}