namespace AJut.PeerPubSubMessenger
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using AJut;
    using AJut.PeerPubSubMessenger.StandardMessages;
    using AJut.Storage;
    using AJut.Threading;

    public class ParticipantMessenger : Messenger
    {
        private readonly MessagingSessionManager m_manager;
        private ThreadWorker<Message, CoordinatorProcessorState, Message> m_p2pServerMessagePump = new ThreadWorker<Message, CoordinatorProcessorState, Message>();

        // ==================================[ Construction ]===============================================

        public ParticipantMessenger (MessagingSessionManager manager) : base(manager.CurrentUser.Id, new MessageHandlerRegistrar(manager.Connection))
        {
            m_manager = manager;
        }

        // ==================================[ Public Interface ]===============================================

        public Guid CoordinatorId { get; private set; }
        public IPAddress CoordinatorAddress { get; private set; }
        public int CoordinatorPort { get; private set; }

        public async Task<Result> SubscribeToCoordinatorMessages (string connectionInfo)
        {
            Logger.LogInfo($"Attempting to add output connection to coordinator with connection info: {connectionInfo}");

            string[] connectionInfoSplit = connectionInfo.Split(':');
            if (connectionInfoSplit.Length != 3)
            {
                return _LogAndReturnError("connection info");
            }

            if (!Guid.TryParse(connectionInfoSplit[0], out Guid coordinatorId))
            {
                return _LogAndReturnError("source id");
            }

            if (!IPAddress.TryParse(connectionInfoSplit[1], out IPAddress coordinatorAddress))
            {
                return _LogAndReturnError("address");
            }

            if (!int.TryParse(connectionInfoSplit[2], out int coordinatorPort))
            {
                return _LogAndReturnError("port");
            }

            var outputClient = new TcpClient();

            await outputClient.ConnectAsync(coordinatorAddress, coordinatorPort).ConfigureAwait(false);
            if (outputClient.Connected)
            {
                this.CoordinatorId = coordinatorId;
                this.CoordinatorAddress = coordinatorAddress;
                this.CoordinatorPort = coordinatorPort;

                Logger.LogInfo("Output client connected");
                this.TrackOutputTarget(coordinatorId, outputClient);
                m_p2pServerMessagePump.IsActiveChanged += this.OnMessagePumpIsActiveChanged;
                m_p2pServerMessagePump.ExecutionState.Add(new CoordinatorProcessorState(outputClient, this.SourceId));
                Logger.LogInfo("Setting up outbox worker");
                m_p2pServerMessagePump.Start(BackgroundMessageLoop, "Messenger Outbox Processor: Participant");
                m_p2pServerMessagePump.OutputResults.DataReceived += this.OutputResults_DataReceived;

                this.SetupOutboxTimerIfNotAlreadySetup();
                return Result.Success();
            }

            outputClient.Dispose();
            return Result.Error("Connection attempt failed");

            Result _LogAndReturnError (string part)
            {
                string error = $"There was something wrong with the {part} you provided, connection info should have been in the format \"id:address:port\", but was instead \"{connectionInfo}\".";
                Logger.LogError(error);
                return Result<MessagingSessionManager>.Error(error);
            }
        }

        private void OnMessagePumpIsActiveChanged (object sender, EventArgs e)
        {
            this.ExecuteOnMainThread(() =>
            {
                if (!m_p2pServerMessagePump.IsActive)
                {
                    m_manager.Connection.IsConnected = false;
                }
            });
        }

        // =====================[ Utilities & Implementations ]=============================

        protected async override Task DoShutdown ()
        {
            await this.SendMessage(new ParticipantGracefulShutdownPayload());
            await m_p2pServerMessagePump.ShutdownGracefullyAndWaitForCompletion();
        }

        protected override void ExecuteOnMainThread (Action action)
        {
            m_manager.DispatchOnUIThread.Invoke(action);
        }

        protected override void RespondToInputMessage (Message input)
        {
            if (input.Payload is ParticipantJoinedPayload joined && joined.UserId != m_manager.CurrentUser.Id)
            {
                Logger.LogInfo($"Participant joined {joined.UserId} - {joined.UserName}");
                m_manager.Connection.HandleParticpantJoined(joined.UserId, joined.UserName);
            }
            else if (input.Payload is ParticipantLeftPayload left)
            {
                Logger.LogInfo($"Participant left {left.UserId} - {left.UserName}");
                if (this.CoordinatorId == left.UserId)
                {
                    Logger.LogInfo($"Coordinator shut down messaging");
                    m_manager.Connection.IsConnected = false;
                }
                else
                {
                    m_manager.Connection.HandleParticpantLeft(left.UserId);
                }
            }
            else if (input.Payload is FullParticipantListPayload fullParticipantList)
            {
                m_manager.Connection.ResetWithFullParticipantList(fullParticipantList.AllActiveParticipants, fullParticipantList.AllMissingParticipants);
            }

            this.InputMessageHandlers.RunAllHandlersFor(input);
        }

        protected override void ProcessOutgoingMessage (Message message)
        {
            Logger.LogInfo($"\n\n======DEBUG======> Sending message {message.Id} of type {message.Payload.GetType().Name} with info:\n{message.ToJson()}\n\n");
            m_p2pServerMessagePump.InputToProcess.Add(message);
        }

        protected override void ClearOutoingMessageQueue (Predicate<Message> messageFinder)
        {
            m_p2pServerMessagePump.InputToProcess.RemoveAll(messageFinder);
        }

        private void OutputResults_DataReceived (object sender, EventArgs e)
        {
            var messages = m_p2pServerMessagePump.OutputResults.TakeAll().Where(m => m.Id != Guid.Empty && m.Payload != null).ToArray();
            this.HandleGotInputMessages(messages);
        }

        // =====================[ BKG Worker Stuff ]=============================
        private static void BackgroundMessageLoop (ThreadWorkerDataTracker<Message, CoordinatorProcessorState, Message> data)
        {
            Logger.LogInfo("::OUTBOX THREAD:: ProcessOutbox started");
            try
            {
                byte[] byteBuffer = new byte[256];
                while (data.ShouldContinue)
                {
                    CoordinatorProcessorState target = data.ExecutionState.ToArray().FirstOrDefault();
                    if (target == null)
                    {
                        continue;
                    }

                    Message next = data.InputToProcess.TakeNext();
                    bool anyMessagesReceived = false;
                    NetworkStream stream = target.Connection.GetStream();

                    // ====================[ SEND ]============================
                    // If we have a message, send it
                    if (next != null)
                    {
                        try
                        {
                            Logger.LogInfo($"::OUTBOX THREAD:: Message away: {next.Id}");
                            byte[] messageBytes = next.ToJsonBytes();
                            stream.Write(messageBytes, 0, messageBytes.Length);
                            next = null;
                        }
                        catch (Exception exc)
                        {
                            Logger.LogError("::OUTBOX THREAD:: Error sending message!", exc);
                        }
                    }

                    if (next != null)
                    {
                        data.InputToProcess.Add(next);
                        Logger.LogInfo($"::OUTBOX THREAD:: Message {next.Id} was not sent, queueing for retry until we actually have a place to send it");
                    }

                    // ====================[ RECEIVE ]============================
                    // While we're looking, check for messages
                    Message[] receivedMessages = ProcessStreamInput(stream, byteBuffer).Select(Message.FromJsonText).ToArray();
                    data.OutputResults.AddRange(receivedMessages);
                    anyMessagesReceived = receivedMessages.Length > 0;
                    if (anyMessagesReceived)
                    {
                        Logger.LogInfo($"::OUTBOX THREAD:: processed {receivedMessages.Length} new messages, passed to output results!");
                    }

                    foreach (Message message in receivedMessages)
                    {
                        // Send a standard ack so the target knows we received the message, unless it's an ack, don't ack acks.
                        if (!(message.Payload is StandardAcknowledgementPayload))
                        {
                            var ack = message.CreateAck(target.Source);
                            var ackBytes = ack.ToJsonBytes();
                            stream.Write(ackBytes, 0, ackBytes.Length);
                            Logger.LogInfo($"Ack sent for message {message.Id} to user {message.Source}");
                        }

                    }

                    if (anyMessagesReceived)
                    {
                        data.OutputResults.NotifyDataReceived();
                    }

                    Thread.Sleep(0);
                }
            }
            catch (Exception exc)
            {
                Logger.LogError("::OUTBOX THREAD:: Exception encountered in ProcessOutbox", exc);
            }

            Logger.LogInfo("::OUTBOX THREAD:: ProcessOutbox exiting");
        }

        private class CoordinatorProcessorState : IDisposable
        {
            public CoordinatorProcessorState (TcpClient connection, Guid sourceId)
            {
                this.Connection = connection;
                this.Source = sourceId;
            }

            public void Dispose ()
            {
                this.Connection.Dispose();
                this.Connection = null;
            }

            public TcpClient Connection { get; private set; }
            public Guid Source { get; }
        }
    }
}
