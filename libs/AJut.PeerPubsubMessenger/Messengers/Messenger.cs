namespace AJut.PeerPubSubMessenger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AJut;
    using AJut.PeerPubSubMessenger.StandardMessages;

    public abstract class Messenger : IDisposable
    {
        public const char kEndOfTransmission = (char)4;
        private static readonly TimeSpan kOutboxTimeout = TimeSpan.FromSeconds(3.0);
        private const int kOutboxTimerIntervalMS = 1000;

        // Outbox
        private readonly Dictionary<Guid, SentMessageAcknowledgementTracking> m_outbox = new Dictionary<Guid, SentMessageAcknowledgementTracking>();
        private Timer m_outboxTimeoutTimer;
        private readonly List<OutputTargetInfo> m_outputTargets = new List<OutputTargetInfo>();

        // ==================================[ Construction ]=================================================
        protected Messenger (Guid sourceId, MessageHandlerRegistrar inputMessgeHandlers)
        {
            this.SourceId = sourceId;
            this.InputMessageHandlers = inputMessgeHandlers;
        }

        // ==================================[ Properties ]===================================================
        public Guid SourceId { get; }

        // ==================================[ Public Methods ]===============================================

        public async void Dispose ()
        {
            await this.Shutdown();
            Logger.LogInfo($"Coordinator messenger service disposed for source id: {this.SourceId}");
        }

        public async Task Shutdown ()
        {
            await this.DoShutdown();
            m_outboxTimeoutTimer?.Dispose();
            m_outboxTimeoutTimer = null;

            m_outputTargets.ClearAndDisposeAll();
            m_outbox.Clear();
        }

        protected virtual Task DoShutdown () => Task.CompletedTask;

        public MessageHandlerRegistrar InputMessageHandlers { get; }

        public void QueueMessage (object payload)
        {
            this.ProcessOutgoingMessage(new Message(this.SourceId, payload));
        }

        public void QueueMessage (Message message)
        {
            this.ProcessOutgoingMessage(message);
        }

        public async Task<bool> SendMessage (object messagePayload, params Guid[] focusedTargets)
        {
            return await this.SendMessage(new Message(this.SourceId, messagePayload, focusedTargets)).ConfigureAwait(false);
        }

        protected async Task<bool> SendMessage (Message message)
        {
            var messageTracker = new SentMessageAcknowledgementTracking(message, MatchFocusTargetsForAcks(message));
            m_outbox.Add(message.Id, messageTracker);
            this.ProcessOutgoingMessage(message);
            Logger.LogInfo($"Message {message.Id} added to outbox 'to process' list");
            return await messageTracker.Task;
        }

        protected virtual IEnumerable<OutputTargetInfo> MatchFocusTargetsForAcks (Message message)
        {
            if (message.FocusedTargets.IsNullOrEmpty())
            {
                return m_outputTargets;
            }

            return m_outputTargets.Where(t => message.FocusedTargets.Contains(t.Id));
        }

        // ==================================[ Utility Methods ]===============================================
        protected void TrackOutputTarget (Guid coordinatorId, TcpClient client)
        {
            m_outputTargets.Add(new OutputTargetInfo(coordinatorId, client));
        }

        protected void GotAcknowledgement (Message response)
        {
            if (response == null)
            {
                return;
            }

            if (m_outbox.TryGetValue(response.Id, out SentMessageAcknowledgementTracking tracker))
            {
                if (tracker.HandleAcknowledgement(response))
                {
                    m_outbox.Remove(response.Id);
                }
            }
        }

        protected void SetupOutboxTimerIfNotAlreadySetup ()
        {
            if (m_outboxTimeoutTimer != null)
            {
                return;
            }

            m_outboxTimeoutTimer = new Timer(this.CheckOutboxTimeout, null, 0, kOutboxTimerIntervalMS);
        }

        private void CheckOutboxTimeout (object state)
        {
            var now = DateTime.Now;
            foreach (SentMessageAcknowledgementTracking tracker in m_outbox.Values.ToList())
            {
                if (tracker.IsComplete)
                {
                    m_outbox.Remove(tracker.MessageId);
                }

                if (now - tracker.MessageTimeStamp > kOutboxTimeout)
                {
                    // Message could re-queue itself if it fails (nobody to send it to maybe)
                    this.ClearOutoingMessageQueue(m => m.Id == tracker.MessageId);
                    m_outbox.Remove(tracker.MessageId);
                    tracker.HandleFailed();
                }
            }
        }
        private static readonly string[] kMT = new string[0];
        protected static string[] ProcessStreamInput (NetworkStream stream, byte[] byteBuffer)
        {
            try
            {
                // Loop to receive all the data sent by the client.
                var fullData = new StringBuilder();
                int bytesRead;
                while (stream.DataAvailable && stream.CanRead && (bytesRead = stream.Read(byteBuffer, 0, byteBuffer.Length)) != 0)
                {
                    // Translate data bytes to a ASCII string.
                    fullData.Append(Encoding.ASCII.GetString(byteBuffer, 0, bytesRead));
                }

                return fullData.ToString().Split(new[] { kEndOfTransmission }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception exc)
            {
                Logger.LogError("Error processing input", exc);
                return kMT;
            }
        }

        protected void HandleGotInputMessages (Message[] inputs)
        {
            try
            {
                this.ExecuteOnMainThread(() =>
                {
                    foreach (Message message in inputs)
                    {
                        Logger.LogInfo($"\n\n======DEBUG======\n\n==> Got new inbox message '{message.Id}' of type: {message.Payload?.GetType().Name ?? "--null--"}\n{message.ToJson()}\n\n");

                        if (message.Payload is StandardAcknowledgementPayload ack)
                        {
                            this.GotAcknowledgement(message);
                            continue;
                        }

                        this.RespondToInputMessage(message);
                    }
                });
            }
            catch (Exception exc)
            {
                Logger.LogError("Error processing inbox messages", exc);
            }
        }

        // ==================================[ Protected Methods ]=============================================

        protected abstract void RespondToInputMessage (Message input);

        protected abstract void ExecuteOnMainThread (Action action);

        protected abstract void ProcessOutgoingMessage (Message message);

        protected abstract void ClearOutoingMessageQueue (Predicate<Message> messageFinder);

        // ==================================[ Utility Classes ]===============================================
        protected class OutputTargetInfo : IDisposable
        {
            private Guid m_id = Guid.Empty;

            public OutputTargetInfo (TcpClient client)
            {
                this.Client = client;
            }

            public OutputTargetInfo (Guid id, TcpClient client)
            {
                this.Id = id;
                this.Client = client;
            }

            public event EventHandler<EventArgs> IdWasSet;

            public Guid Id
            {
                get => m_id;
                set
                {
                    m_id = value;
                    if (m_id != Guid.Empty)
                    {
                        this.IdWasSet?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            public TcpClient Client { get; }
            public bool IsTargetKnown => this.Id != Guid.Empty;

            public void Dispose ()
            {
                try
                {
                    this.Client.Close();
                }
                catch { }
            }
        }

        protected class SentMessageAcknowledgementTracking
        {
            private readonly List<OutputTargetInfo> m_remainingTargets;
            private readonly TaskCompletionSource<bool> m_onComplete;

            public SentMessageAcknowledgementTracking (Message message, IEnumerable<OutputTargetInfo> targets)
            {
                this.MessageId = message.Id;
                this.MessageTimeStamp = message.SentWhen;
                m_onComplete = new TaskCompletionSource<bool>();
                m_remainingTargets = new List<OutputTargetInfo>(targets);
            }

            public Guid MessageId { get; }
            public DateTime MessageTimeStamp { get; }
            public Task<bool> Task => m_onComplete.Task;
            public bool IsComplete { get; private set; }

            /// <summary>
            /// Handles the acknowledgement, and indicates of all acknowledgements have been received
            /// </summary>
            /// <returns>true for all received, false for not all received</returns>
            public bool HandleAcknowledgement (Message ack)
            {
                if (this.MessageId == ack.Id)
                {
                    int numRemoved = m_remainingTargets.RemoveAll(t => t.Id == ack.Source);
                    if (numRemoved != 1)
                    {
                        Logger.LogError($"Acknowledgement for message {this.MessageId} encountered odd scenario, expected 1 target removal, got {numRemoved}");
                    }
                }

                Logger.LogInfo($"Receieved acknowledgement for message {this.MessageId} - {m_remainingTargets.Count} acks remaining");
                if (m_remainingTargets.Count == 0)
                {
                    this.SetResult(true);
                    return true;
                }

                return false;
            }

            public void HandleFailed ()
            {
                this.SetResult(false);
            }

            private void SetResult (bool result)
            {
                if (this.IsComplete)
                {
                    return;
                }

                Logger.LogInfo($"Acknowledgement tracker for message {this.MessageId} - final result is {result}, with {m_remainingTargets.Count} acks remaining, triggered {DateTime.Now - this.MessageTimeStamp} ago");
                m_onComplete.TrySetResult(result);
                this.IsComplete = true;
            }
        }
    }
}
