namespace AJut.PeerPubSubMessenger
{
    using AJut;
    using AJut.PeerPubSubMessenger.StandardMessages;
    using AJut.Text.AJson;
    using System;
    using System.Text;

    public class Message
    {
        public const char kEndOfTransmission = (char)4;

        public Message ()
        {
        }

        private Message (Guid messageId, Guid sourceId, object payload, Guid[] focusedTargets = null)
        {
            this.SentWhen = DateTime.Now;
            this.Id = messageId;
            this.Source = sourceId;
            this.Payload = payload;
            this.FocusedTargets = focusedTargets.IsNullOrEmpty() ? null : focusedTargets;
        }

        public Message (Guid sourceId, object payload, params Guid[] focusedTargets) : this(Guid.NewGuid(), sourceId, payload, focusedTargets)
        {
        }

        internal Message CreateAck (Guid sourceId)
        {
            return new Message(this.Id, sourceId, new StandardAcknowledgementPayload());
        }

        public Guid Id { get; set; }
        public Guid[] FocusedTargets { get; set; }
        public Guid Source { get; set; }
        public DateTime SentWhen { get; set; }
        public object Payload { get; set; }

        public static readonly JsonBuilder.Settings kDefaultJsonSettings = new JsonBuilder.Settings
        {
            TypeIdToWrite = eTypeIdInfo.TypeIdAttributed
        };

        public static Message FromJsonText (string messageText)
        {
            Json json = JsonHelper.ParseText(messageText);
            if (json.HasErrors)
            {
                Logger.LogError($"Could not parse message: {messageText}\nJson Errors:\n{json.GetErrorReport()}");
            }

            return JsonHelper.BuildObjectForJson<Message>(json);
        }

        public byte[] ToJsonBytes ()
        {
            return Encoding.ASCII.GetBytes(this.ToJson() + Message.kEndOfTransmission);
        }

        public string ToJson ()
        {
            var json = JsonHelper.BuildJsonForObject(this, kDefaultJsonSettings);
            if (json.HasErrors)
            {
                Logger.LogError($"Error creating reply for message {this.Id}, error was...\n{String.Join("\n\t", json.Errors)}");
                return null;
            }

            return json.ToString();
        }
    }
}
