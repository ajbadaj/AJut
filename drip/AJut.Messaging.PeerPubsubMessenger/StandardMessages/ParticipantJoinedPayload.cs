namespace AJut.Messaging.PeerPubSubMessenger.StandardMessages
{
    using System;
    using AJut.TypeManagement;

    [TypeId("StdMessage-ONJOIN")]
    public class ParticipantJoinedPayload
    {
        public string UserName { get; set; }
        public Guid UserId { get; set; }
    }
}
