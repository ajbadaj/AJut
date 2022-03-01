namespace AJut.Messaging.PeerPubSubMessenger.StandardMessages
{
    using AJut.TypeManagement;

    [TypeId("StdMessage-ACK")]
    public class StandardAcknowledgementPayload
    {
        public bool IsAcknowledged { get; set; } = true;
    }
}
