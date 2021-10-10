namespace AJut.Messaging.PeerPubSubMessenger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Messaging.PeerPubSubMessenger.StandardMessages;
    using AJut.Threading;

    public class CoordinatorMessenger : Messenger
    {
        private readonly MessagingSessionManager m_manager;
        private ThreadWorker<Message, CoordinatorProcessorState, Message> m_coordinatorMessageLoop = new ThreadWorker<Message, CoordinatorProcessorState, Message>();


        // ==================================[ Construction ]===============================================

        public CoordinatorMessenger (MessagingSessionManager manager) : base(manager.CurrentUser.Id, new MessageHandlerRegistrar(manager.Connection))
        {
            m_manager = manager;
        }

        public bool Setup (int listenPort)
        {
            if (m_coordinatorMessageLoop.IsActive)
            {
                return true;
            }

            this.InputPort = listenPort;
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, listenPort);
            }
            catch (Exception exc)
            {
                listener = null;
                Logger.LogError("Failed to generate listener", exc);
                return false;
            }

            string portInfo = listenPort == 0 ? "-first open port-" : $"port {listenPort}";
            Logger.LogInfo($"Starting server listener on {portInfo} with source id: {this.SourceId}");
            try
            {
                listener.Start();

                var host = Dns.GetHostEntry(Dns.GetHostName());
                var localIP = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

                if (listener.LocalEndpoint is IPEndPoint ipEndpoint && localIP != null)
                {
                    this.InputPort = ipEndpoint.Port;
                    this.InputAddress = localIP;
                    Logger.LogInfo($"Starting server succeeded!\n\t> Final address: {this.InputAddress}\n\t> Final Port: {this.InputPort}\n\t> Source id: {this.SourceId}");
                }
                else
                {
                    throw new Exception($"Invalid endpoint type provided: {listener.LocalEndpoint.GetType().Name}");
                }
            }
            catch (Exception exc)
            {
                listener.Stop();
                Logger.LogError($"Failed to start listener (or retrieive port)", exc);
                return false;
            }

            var state = new CoordinatorProcessorState(listener, this.SourceId);
            state.ConnectedClientsChanged += this.State_ConnectedClientsChanged;
            m_coordinatorMessageLoop.IsActiveChanged += this.OnMessageLoopIsActiveChanged;
            m_coordinatorMessageLoop.ExecutionState.Add(state);

            Logger.LogInfo("::INBOX THREAD:: ThreadWorker Started");
            m_coordinatorMessageLoop.StartThreadLoop(BackgroundProcessMessage, $"AJut-MSG-Coordinator-Port_#{this.InputPort}_InputListener");
            m_coordinatorMessageLoop.OutputResults.DataReceived += this.Inbox_GotNewMessages;

            return true;
        }

        private void OnMessageLoopIsActiveChanged (object sender, EventArgs e)
        {
            this.ExecuteOnMainThread(() =>
            {
                if (!m_coordinatorMessageLoop.IsActive)
                {
                    m_manager.Connection.IsConnected = false;
                }
            });
        }

        private void State_ConnectedClientsChanged (object sender, Guid[] e)
        {
            this.ExecuteOnMainThread(() => this.ProcessConnections(e));
        }

        protected virtual void ProcessConnections (Guid[] connections)
        {
#if true
            // Active --and-- available, because we assume unavailable just means "not yet tracked in the system, but will be soon" - it's not up to us
            //  now, and it can't be for that matter, to set that since we just have ids
            var activeAndAvailable = m_manager.Connection.ActiveParticipants.Where(p => connections.Contains(p.Id)).ToArray();

            // Anything missing before can stay missing, but if it's in the connects list from our actually messenger - then it's out
            var stillMissing = m_manager.Connection.MissingParticipants.Where(p => !connections.Contains(p.Id)).ToArray();
            
            m_manager.Connection.ResetWithFullParticipantList(activeAndAvailable, stillMissing);


            //This is the right type of logic, essentially if I get a set of connections I need to QueueMessage for ParticipantLeftPayload
            //in some occaisions due to abrupt exits (I expect id {1, 2, 3} but suddenly only have {1, 3} - in that case 2 needs ParticipantLeft
            //but I also need to keep Connection proper since Coordinator isn't handling ParticipantLeft/ParticipantJoined - which might mean adding those
            //Too tired now though...


            List<Guid> previousParticipantIds = new List<Guid>(m_manager.Connection.ActiveParticipants.Select(p => p.Id));
            List<MessengerParticipant> toAdd = new List<MessengerParticipant>();
            foreach (var pid in connections)
            {
                // If the previous list doesn't have this connection, than it's a new one
                if (!previousParticipantIds.Remove(pid))
                {
                    toAdd.Add(m_manager.Connection.ActiveParticipants.First(p => p.Id == pid));
                }
            }

            // Anything remaining needs to be removed (probably an abrupt, or non-graceful exit)
            previousParticipantIds.ForEach(r => this.QueueMessage(new ParticipantLeftPayload { UserId = r }));
#else
            // Active --and-- available, because we assume unavailable just means "not yet tracked in the system, but will be soon" - it's not up to us
            //  now, and it can't be for that matter, to set that since we just have ids
            var activeAndAvailable = m_manager.Connection.ActiveParticipants.Where(p => connections.Contains(p.Id)).ToArray();
            m_manager.Connection.ActiveParticipants.Where(p => connections.Contains(p.Id)).ToArray();

            // Anything missing before can stay missing, but if it's in the connects list from our actually messenger - then it's out
            var stillMissing = m_manager.Connection.MissingParticipants.Where(p => !connections.Contains(p.Id)).ToArray();
            m_manager.Connection.ResetWithFullParticipantList(activeAndAvailable, stillMissing);
#endif
        }

        // ==================================[ Public Interface ]===============================================

        public int InputPort { get; protected set; }
        public IPAddress InputAddress { get; protected set; }

        // ==================================[ Utilities & Implementation ]===============================================

        protected override void ExecuteOnMainThread (Action action)
        {
            m_manager.DispatchOnUIThread.Invoke(action);
        }

        protected virtual bool HandleSpecialInputMessageInterpretation (Message message)
        {
            if (message.Payload is RequestJoinPayload joiner)
            {
                Logger.LogInfo($"Got new join request from: {joiner.UserName} -- with id: {message.Source}");
                m_manager.Connection.HandleParticpantJoined(message.Source, joiner.UserName);
                ParticipantJoinedPayload joinedPayload = new ParticipantJoinedPayload()
                {
                    UserId = message.Source,
                    UserName = joiner.UserName,
                    // Might be a good idea, needs thinking: AllActiveParticipants = m_manager.Connection.ActiveParticipants.Select(p => p.Id).ToArray()
                };

                var pjpMessage = new Message(message.Source, joinedPayload);
                this.QueueMessage(pjpMessage);
                this.InputMessageHandlers.RunAllHandlersFor(pjpMessage);

                var allParticipantsPayload = new FullParticipantListPayload
                {
                    AllActiveParticipants = m_manager.Connection.ActiveParticipants.ToArray(),
                    AllMissingParticipants = m_manager.Connection.MissingParticipants.ToArray(),
                };

                var allParticipantsReply = new Message(m_manager.CurrentUser.Id, allParticipantsPayload, message.Source);

                this.QueueMessage(allParticipantsReply);
                return true;
            }

            if (message.Payload is ParticipantGracefulShutdownPayload)
            {
                Logger.LogInfo($"Got new join graceful shutdown request from: {message.Source}, forwarding to all");
                var participantInfo = m_manager.Connection.HandleParticpantLeft(message.Source);
                if (participantInfo != null)
                {
                    var leftMessagePayload = new ParticipantLeftPayload()
                    {
                        UserId = participantInfo.Id,
                        UserName = participantInfo.UserName,
                    };

                    var pjpMessage = new Message(message.Source, leftMessagePayload);
                    Logger.LogInfo($"Participant '{participantInfo.Id}' initiated graceful shutdown, participant was succesfully identified as '{participantInfo.UserName}' and connected peer pool is being notified via message: {pjpMessage.Id}");
                    this.QueueMessage(pjpMessage);
                    this.InputMessageHandlers.RunAllHandlersFor(pjpMessage);
                }
                else
                {
                    Logger.LogError($"Participant '{message.Source}' initiated graceful shutdown, but associated particpant record was not located");
                }

                return true;
            }

            return false;
        }

        protected sealed override void RespondToInputMessage (Message message)
        {
            if (!this.HandleSpecialInputMessageInterpretation(message))
            {
                // Otherwise, simply forward it
                this.QueueMessage(message);
                this.InputMessageHandlers.RunAllHandlersFor(message);
            }
        }

        protected async override Task DoShutdown ()
        {
            await this.SendMessage(new ParticipantLeftPayload { UserId = m_manager.CurrentUser.Id, UserName = m_manager.CurrentUser.UserName });
            await m_coordinatorMessageLoop.ShutdownGracefullyAndWaitForCompletion();
            if (!m_coordinatorMessageLoop.LastRunResult)
            {
                Logger.LogError(m_coordinatorMessageLoop.LastRunResult.GetErrorReport());
            }

            Logger.LogInfo("::INBOX THREAD:: ThreadWorker Exited");
        }

        protected override void ProcessOutgoingMessage (Message message)
        {
            Logger.LogInfo($"\n\n======DEBUG======> Sending message {message.Id} of type {message.Payload.GetType().Name} with info:\n{message.ToJson()}\n\n");
            m_coordinatorMessageLoop.InputToProcess.Add(message);
        }

        protected override void ClearOutoingMessageQueue (Predicate<Message> messageFinder)
        {
            m_coordinatorMessageLoop.InputToProcess.Clear();
        }

        private void Inbox_GotNewMessages (object sender, EventArgs e)
        {
            var messages = m_coordinatorMessageLoop.OutputResults.TakeAll().Where(m => m.Id != Guid.Empty && m.Payload != null).ToArray();
            this.HandleGotInputMessages(messages);
        }

        private static readonly byte[] g_byteBuffer = new byte[256];
        private static void BackgroundProcessMessage (ThreadWorkerDataTracker<Message, CoordinatorProcessorState, Message> data)
        {
            CoordinatorProcessorState inboxProcessorState = data.ExecutionState.ToArray().First();

            // ====================[ Find ]============================
            if (inboxProcessorState.Listener.Pending())
            {
                TcpClient found;
                try
                {
                    found = inboxProcessorState.Listener.AcceptTcpClient();
                }
                catch (Exception exc)
                {
                    Logger.LogError("Error accepting new client!", exc);
                    found = null;
                }

                if (found != null && found.Connected)
                {
                    inboxProcessorState.AddConnectedClient(found);
                }
            }

            // ====================[ RECEIVE ]============================
            var incomingMessages = new List<Message>();
            foreach (OutputTargetInfo target in inboxProcessorState.Clients())
            {
                try
                {
                    if (!target.Client.Connected)
                    {
                        inboxProcessorState.RemoveConnectedClient(target);
                        continue;
                    }

                    NetworkStream stream = target.Client.GetStream();
                    string[] messageJsons = ProcessStreamInput(stream, g_byteBuffer);
                    foreach (string messageJson in messageJsons)
                    {
                        var message = Message.FromJsonText(messageJson);
                        if (message != null)
                        {
                            if (!target.IsTargetKnown)
                            {
                                target.Id = message.Source;
                            }

                            // Send a standard ack so the target knows we received the message, unless it's an ack, don't ack acks.
                            if (!(message.Payload is StandardAcknowledgementPayload))
                            {
                                var ack = message.CreateAck(inboxProcessorState.Source);
                                var ackBytes = ack.ToJsonBytes();
                                stream.Write(ackBytes, 0, ackBytes.Length);
                                Logger.LogInfo($"Ack sent for message {message.Id} to user {message.Source}");
                            }

                            // Enqueue the message
                            incomingMessages.Add(message);
                        }
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogError("Error processing input", exc);
                }
            }

            if (incomingMessages.Any())
            {
                data.OutputResults.AddRange(incomingMessages);
                data.OutputResults.NotifyDataReceived();
            }

            // ====================[ SEND ]============================
            Message outgoingMessage = data.InputToProcess.TakeNext();
            if (outgoingMessage != null)
            {
                foreach (var target in inboxProcessorState.Clients())
                {
                    try
                    {
                        if (!target.Client.Connected)
                        {
                            inboxProcessorState.RemoveConnectedClient(target);
                            continue;
                        }

                        if (outgoingMessage.FocusedTargets == null || outgoingMessage.FocusedTargets.Contains(target.Id))
                        {
                            var outputStream = target.Client.GetStream();
                            byte[] messageBytes = outgoingMessage.ToJsonBytes();
                            outputStream.Write(messageBytes, 0, messageBytes.Length);
                        }
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError("Error processing output", exc);
                    }
                }
            }

            inboxProcessorState.AnnounceCurrentClientListIfUpdated();
        }

        // ==================================[ Utility Classes ]===============================================

        protected class CoordinatorProcessorState : IDisposable
        {
            private readonly List<OutputTargetInfo> m_connectedClients = new List<OutputTargetInfo>();
            private bool m_hasUpdatedClientelList = false;

            public CoordinatorProcessorState (TcpListener listener, Guid sourceId)
            {
                this.Listener = listener;
                this.Source = sourceId;
            }

            public event EventHandler<Guid[]> ConnectedClientsChanged;

            public TcpListener Listener { get; private set; }
            public Guid Source { get; }

            public void AddConnectedClient (TcpClient clientComm)
            {
                var client = new OutputTargetInfo(clientComm);
                m_connectedClients.Add(client);
                client.IdWasSet += _OnClientIdWasSet;

                void _OnClientIdWasSet (object sender, EventArgs e)
                {
                    client.IdWasSet -= _OnClientIdWasSet;
                    m_hasUpdatedClientelList = true;
                }
            }

            public void AddConnectedClient (Guid clientId, TcpClient clientComm)
            {
                m_connectedClients.Add(new OutputTargetInfo(clientId, clientComm));
                m_hasUpdatedClientelList = true;
            }

            public void RemoveConnectedClient (OutputTargetInfo target)
            {
                target.Dispose();
                if (m_connectedClients.Remove(target))
                {
                    m_hasUpdatedClientelList = true;
                }
            }

            public void RemoveConnectedClient (Guid client)
            {
                var removed = m_connectedClients.RemoveFirst(t => t.Id == client);
                if (removed != null)
                {
                    removed.Dispose();
                    m_hasUpdatedClientelList = true;
                }
            }

            public IEnumerable<OutputTargetInfo> Clients ()
            {
                foreach (OutputTargetInfo client in m_connectedClients.ToList())
                {
                    try
                    {
                        // test client
                        if (!client.Client.Connected)
                        {
                            this.RemoveConnectedClient(client.Id);
                        }
                    }
                    catch
                    {
                        this.RemoveConnectedClient(client.Id);
                        continue;
                    }

                    yield return client;
                }
            }

            public void AnnounceCurrentClientListIfUpdated ()
            {
                if (m_hasUpdatedClientelList)
                {
                    try
                    {
                        var connections = new List<Guid>();
                        connections.Add(this.Source);
                        connections.AddRange(this.m_connectedClients.Select(c => c.Id));
                        this.ConnectedClientsChanged?.Invoke(this, connections.ToArray());
                        m_hasUpdatedClientelList = false;
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError("Updating clientel list failed", exc);
                    }
                }
            }

            public void Dispose ()
            {
                m_connectedClients.ClearAndDisposeAll();
                this.Listener?.Stop();
                this.Listener = null;
            }
        }
    }
}
