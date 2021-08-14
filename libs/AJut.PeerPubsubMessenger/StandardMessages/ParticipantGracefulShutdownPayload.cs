namespace AJut.PeerPubSubMessenger.StandardMessages
{
    using AJut.TypeManagement;

    [TypeId("StdMessage-GRACEFULEXIT")]
    public class ParticipantGracefulShutdownPayload
    {
        public int Test { get; set; }
    }
}
