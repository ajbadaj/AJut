namespace AJut.Bench.Models
{
    using System;
    using AJut.Text.AJson;

    // Wire-message shape - the high-volume case the spike was sized against. ~10 properties,
    // primitives only. The [OptimizeAJson] marker gates the V2 source-gen path - reflection
    // benches still hit the type fine, the marker just says "emit a fast helper too."
    [OptimizeAJson]
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

    // Identical shape, no [OptimizeAJson] - lets the V2 reflection-baseline row force the
    // reflection path instead of being dispatched to the generated helper. The generator's
    // dispatch table is keyed on the runtime Type, so a non-annotated sibling is the cleanest
    // way to compare the two V2 paths apples-to-apples.
    public class TinyMessageReflection
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
