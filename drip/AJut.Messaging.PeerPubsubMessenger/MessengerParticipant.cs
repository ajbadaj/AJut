namespace AJut.Messaging.PeerPubSubMessenger
{
    using System;
    using AJut.TypeManagement;

    [TypeId("AJut.Messaging.PeerPubSubMessenger.MessengerParticipant")]
    public class MessengerParticipant : NotifyPropertyChanged
    {
        public MessengerParticipant () { }
        public MessengerParticipant (string userName, bool isCoordinator) : this(Guid.NewGuid(), userName, isCoordinator) { }

        public MessengerParticipant (Guid id, string userName, bool isCoordinator)
        {
            m_id = id;
            m_userName = userName;
            m_isCoordinator = isCoordinator;
        }

        private Guid m_id;
        public Guid Id
        {
            get => m_id;
            set => this.SetAndRaiseIfChanged(ref m_id, value);
        }

        private bool m_isCoordinator;
        public bool IsCoordinator
        {
            get => m_isCoordinator;
            set => this.SetAndRaiseIfChanged(ref m_isCoordinator, value);
        }

        private string m_userName;
        public string UserName
        {
            get => m_userName;
            set => this.SetAndRaiseIfChanged(ref m_userName, value);
        }
    }
}
