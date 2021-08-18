namespace AJut.Messaging.PeerPubSubMessenger.StandardMessages
{
    using System;
    using AJut.TypeManagement;

    [TypeId("StdMessage-REQJOIN")]
    public class RequestJoinPayload
    {
        public string UserName { get; set; }
    }
}
