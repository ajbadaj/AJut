namespace AJut.Bench.Models
{
    using System;

    // Wire-message shape - the high-volume Call Familiar case. ~10 properties, primitives only.
    public class TinyMessage
    {
        public Guid SessionId { get; set; }
        public Guid SenderId { get; set; }
        public string MessageType { get; set; }
        public DateTime Timestamp { get; set; }
        public int Sequence { get; set; }
        public string Payload { get; set; }
        public bool IsRetransmit { get; set; }
        public int Priority { get; set; }
        public string Channel { get; set; }
        public Guid? CorrelationId { get; set; }
    }
}
