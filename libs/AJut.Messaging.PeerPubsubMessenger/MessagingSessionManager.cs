namespace AJut.Messaging.PeerPubSubMessenger
{
    using System;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Messaging.PeerPubSubMessenger.StandardMessages;
    using AJut.Storage;
    using AJut.TypeManagement;

    public delegate void OnMessageReceived (MessengerParticipant sender, DateTime sentWhen, Guid messageId, object payload);
    public delegate void OnMessageReceived<T> (MessengerParticipant sender, DateTime sentWhen, Guid messageId, T payload);
    public delegate void DispatchedAction (Action action);

    public class MessagingSessionManager : NotifyPropertyChanged
    {
        private Messenger m_activeMessenger;
        private ConnectionTracker m_connection;

        // ============================[ Construction ]===========================================
        static MessagingSessionManager()
        {
            Logger.LogInfo($"Registering all AJut.{nameof(PeerPubSubMessenger)} type ids");
            TypeIdRegistrar.RegisterAllTypeIds(typeof(MessagingSessionManager).Assembly);
        }

        protected MessagingSessionManager (DispatchedAction dispatch, MessengerParticipant currentUser)
        {
            this.DispatchOnUIThread = dispatch;
            this.CurrentUser = currentUser;
        }

        // ============================[ Properties ]===========================================

        public Messenger ActiveMessenger
        {
            get => m_activeMessenger;
            private set => this.SetAndRaiseIfChanged(ref m_activeMessenger, value);
        }

        public ConnectionTracker Connection
        {
            get => m_connection;
            set => this.SetAndRaiseIfChanged(ref m_connection, value);
        }

        public MessengerParticipant CurrentUser { get; }
        public DispatchedAction DispatchOnUIThread { get; }

        // ============================[ Public Methods ]===========================================

        public static Result<MessagingSessionManager> StartCoordinatorSession (string userName, DispatchedAction mainThreadRunner)//CampaignViewModel campaign, string filePath)
        {
            Logger.LogInfo("======================[ Start Session ]============================");
            var currentUser = new MessengerParticipant(userName, isCoordinator: true);
            var connectionManager = new MessagingSessionManager(mainThreadRunner, currentUser);
            var startResult = connectionManager.StartCoordinatorSession();
            if (!startResult)
            {
                return Result<MessagingSessionManager>.Error(startResult.GetErrorReport());
            }

            return Result<MessagingSessionManager>.Success(connectionManager);
        }

        public static async Task<Result<MessagingSessionManager>> JoinSessionAndCreateManager (string connectionInfo, string userName, DispatchedAction mainThreadRunner)
        {
            Logger.LogInfo("======================[ Join ]============================");
            Logger.LogInfo($"Connecting to: {connectionInfo}");
            Logger.LogInfo($"Connecting as: {userName}");

            var user = new MessengerParticipant(userName, false);
            var connectionManager = new MessagingSessionManager(mainThreadRunner, user);
            var startResult = await connectionManager.StartParticipantSession(connectionInfo);
            if (!startResult)
            {
                return Result<MessagingSessionManager>.Error(startResult.GetErrorReport());
            }

            return new Result<MessagingSessionManager>(connectionManager);
        }

        protected virtual Result StartCoordinatorSession ()
        {
            Logger.LogInfo("======================[ Starting Coordinator Session ]============================");
            this.Connection = new ConnectionTracker(this.CurrentUser, DateTime.Now);
            var coordinatorEndpoint = this.SetupCoordinatorMessengerOnAnyOpenPort();
            if (!coordinatorEndpoint)
            {
                Logger.LogInfo("::START SESSION:: Coordinator failed to startup");
                return Result.ErrorJoin(Result.Error("There seems to be trouble starting up a coordinator session"), coordinatorEndpoint);
            }

            Logger.LogInfo("::START SESSION:: Success!");
            return new Result();
        }

        private async Task<Result> StartParticipantSession (string connectionInfo)
        {
            Logger.LogInfo("======================[ Starting Participant Session ]============================");
            this.Connection = new ConnectionTracker(this.CurrentUser, DateTime.Now);
            var startResult = await this.SetupParticipantMessenger(connectionInfo);
            if (!startResult)
            {
                Logger.LogInfo("::JOIN SESSION:: Participant failed to startup");
                return Result.ErrorJoin(Result.Error("There seems to be trouble starting up a participant session"), startResult);
            }

            Logger.LogInfo("::START SESSION:: Success!");
            return Result.Success();
        }

        /// <summary>
        /// Ends the session on the server, closes down local connections
        /// </summary>
        public async Task<Result> EndSession ()
        {
            Logger.LogInfo("======================[ End Session ]============================");
            try
            {
                await this.ActiveMessenger.Shutdown();
                this.ActiveMessenger = null;
                return Result.Success();
            }
            catch (Exception exc)
            {
                Logger.LogError("::END SESSION:: Error shutting down messenger", exc);
                return Result.Error("There was a problem shutting down your messenger. If the problem persists, consider manually shutting down the application");
            }
        }

        // ============================[ Utility Methods ]===========================================

        protected void DestroyMessengerSynchronously ()
        {
            this.ActiveMessenger.Dispose();
            this.ActiveMessenger = null;
        }


        private Result SetupCoordinatorMessengerOnAnyOpenPort ()
        {
            const int kAnyOpenPort = 0;
            return this.SetupCoordinatorMessenger(kAnyOpenPort);
        }
        
        protected virtual Result SetupCoordinatorMessenger (int port)
        {
            var coordinatorMessenger = this.GenerateCoordinatorMessenger();
            if (!coordinatorMessenger.Setup(port))
            {
                coordinatorMessenger.Dispose();
                string portInfo = port == 0 ? "-any open port-" : port.ToString();
                return Result.Error($"Setup failed for provided port: {portInfo}");
            }

            this.ActiveMessenger = coordinatorMessenger;
            return Result.Success();
        }

        private async Task<Result> SetupParticipantMessenger (string connectionInfo)
        {
            var participantMessenger = this.GenerateParticipantMessenger();
            var setupResult = await participantMessenger.SubscribeToCoordinatorMessages(connectionInfo).ConfigureAwait(false);
            if (!setupResult)
            {
                participantMessenger.Dispose();
                return Result.Error(setupResult.GetErrorReport());
            }

            this.ActiveMessenger = participantMessenger;
            if (!await this.ActiveMessenger.SendMessage(new RequestJoinPayload { UserName = this.CurrentUser.UserName }))
            {
                participantMessenger.Dispose();
                this.ActiveMessenger.Dispose();
                Logger.LogError("RequestJoin message failed");
                return Result.Error("Coordinator error accepting join request");
            }
            return Result.Success();
        }

        protected virtual CoordinatorMessenger GenerateCoordinatorMessenger() => new CoordinatorMessenger(this);
        protected virtual ParticipantMessenger GenerateParticipantMessenger () => new ParticipantMessenger(this);
    }
}
