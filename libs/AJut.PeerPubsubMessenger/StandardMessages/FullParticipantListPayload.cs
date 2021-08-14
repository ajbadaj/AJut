namespace AJut.PeerPubSubMessenger.StandardMessages
{
    using AJut.TypeManagement;

    [TypeId("StdMessage-AllParticipants")]
    public class FullParticipantListPayload
    {
        public MessengerParticipant[] AllActiveParticipants { get; set; }
        public MessengerParticipant[] AllMissingParticipants { get; set; }
    }
}
