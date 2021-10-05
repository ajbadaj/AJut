namespace AJut.Messaging.PeerPubSubMessenger
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    public class ConnectionTracker : NotifyPropertyChanged
    {
        private readonly ObservableCollection<MessengerParticipant> m_activeParticipants = new ObservableCollection<MessengerParticipant>();
        private readonly ObservableCollection<MessengerParticipant> m_missingParticipants = new ObservableCollection<MessengerParticipant>();
        //private readonly List<MessengerParticipant> m_allParticipants = new List<MessengerParticipant>();

        public ConnectionTracker (MessengerParticipant user, DateTime startedTime, IEnumerable<MessengerParticipant> expectedParticipants = null)
        {
            this.ActiveParticipants = new ReadOnlyObservableCollection<MessengerParticipant>(m_activeParticipants);
            this.MissingParticipants = new ReadOnlyObservableCollection<MessengerParticipant>(m_missingParticipants);
            this.ThisUser = user;

            this.ConnectionStartTime = startedTime;
            if (expectedParticipants != null)
            {
                m_missingParticipants.AddEach(expectedParticipants.Where(p => p != user));
            }

            m_activeParticipants.Add(user);
            //if (expectedParticipants != null)
            //{
            //    m_allParticipants.AddRange(expectedParticipants);
            //}

            //if (!m_allParticipants.Contains(user))
            //{
            //    m_allParticipants.Add(user);
            //}
        }

        public event EventHandler<EventArgs> Disconnected;
        public MessengerParticipant ThisUser { get; }
        public DateTime ConnectionStartTime { get; }
        public ReadOnlyObservableCollection<MessengerParticipant> ActiveParticipants { get; }
        public ReadOnlyObservableCollection<MessengerParticipant> MissingParticipants { get; }

        private bool m_isConnected = true;
        public bool IsConnected
        {
            get => m_isConnected;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_isConnected, value) && !value)
                {
                    this.Disconnected?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void HandleParticpantJoined (Guid targetId, string userName)
        {
            var missing = this.MissingParticipants.FirstOrDefault(p => p.Id == targetId);
            if (missing != null)
            {
                m_activeParticipants.Add(missing);
            }
            else
            {
                m_activeParticipants.Add(new MessengerParticipant(targetId, userName, false));
            }
        }

        public MessengerParticipant HandleParticpantLeft (Guid targetId)
        {
            var removed = m_activeParticipants.FirstOrDefault(p => p.Id == targetId);
            if (removed != null)
            {
                m_activeParticipants.Remove(removed);
                m_missingParticipants.Add(removed);
                return removed;
            }

            return null;
        }

        public void ResetWithFullParticipantList (IEnumerable<MessengerParticipant> allActiveParticipants, IEnumerable<MessengerParticipant> allMissingParticipants)
        {
            m_activeParticipants.RemoveAll(p => p.Id != this.ThisUser.Id);
            if (allActiveParticipants != null)
            {
                m_activeParticipants.AddEach(allActiveParticipants.Where(p => p.Id != this.ThisUser.Id));
            }

            m_missingParticipants.Clear();
            if (allMissingParticipants != null)
            {
                m_missingParticipants.AddEach(allMissingParticipants);
            }
        }
    }
}
