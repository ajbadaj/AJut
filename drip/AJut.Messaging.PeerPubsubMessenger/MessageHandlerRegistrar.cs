namespace AJut.Messaging.PeerPubSubMessenger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MessageHandlerRegistrar
    {
        private readonly ConnectionTracker m_connection;
        private readonly Dictionary<object, PayloadEventManager> m_messageRecievedHandlers = new Dictionary<object, PayloadEventManager>();

        public MessageHandlerRegistrar (ConnectionTracker connection)
        {
            m_connection = connection;
        }

        public void RegisterMessageHandler<TPayload> (object owner, OnMessageReceived<TPayload> handler)
        {
            if (!m_messageRecievedHandlers.TryGetValue(owner, out PayloadEventManager eventManager))
            {
                eventManager = new PayloadEventManager();
                m_messageRecievedHandlers.Add(owner, eventManager);
            }

            eventManager.AddWithDowncast(handler);
        }

        public void Deregister (object owner)
        {
            m_messageRecievedHandlers.Remove(owner);
        }

        internal void RunAllHandlersFor (Message input)
        {
            Type payloadType = input.Payload.GetType();
            var sender = m_connection.ActiveParticipants.FirstOrDefault(p => p.Id == input.Source);

            foreach (PayloadEventManager eventManager in m_messageRecievedHandlers.Values)
            {
                if (eventManager.TryGetValue(payloadType, out List<OnMessageReceived> handlers))
                {
                    foreach (OnMessageReceived eventAction in handlers)
                    {
                        eventAction(sender, input.SentWhen, input.Id, input.Payload);
                    }
                }
            }
        }

        private class PayloadEventManager : Dictionary<Type, List<OnMessageReceived>>
        {
            public void AddWithDowncast<TPayload> (OnMessageReceived<TPayload> eventlikeHandler)
            {
                Type target = typeof(TPayload);
                if (!this.TryGetValue(target, out List<OnMessageReceived> handlers))
                {
                    handlers = new List<OnMessageReceived>();
                    this.Add(target, handlers);
                }

                handlers.Add((sender, when, id, payload) => eventlikeHandler(sender, when, id, (TPayload)payload));
            }
        }
    }
}
