namespace AJut.Messaging.PeerPubSubMessenger.StandardMessages
{
    using System;
    using AJut.TypeManagement;

    [TypeId("StdMessage-GONE")]
    public class ParticipantLeftPayload
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
    }
}
