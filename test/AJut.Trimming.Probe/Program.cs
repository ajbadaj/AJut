// Trimming verification entry point. Publishes trimmed (full mode) and exercises the AJson
// source-generated path on a marked type. If the trimmer drops anything load-bearing, this
// returns non-zero or throws.

using AJut.Text.AJson;

return TrimmedRoundTrip.Run();

internal static class TrimmedRoundTrip
{
    public static int Run ()
    {
        Probe original = new Probe { Name = "trim-test", Score = 7, Active = true, Tag = "init-only" };

        Json json = JsonHelper.BuildJsonForObject(original);
        if (json.HasErrors)
        {
            return 1;
        }

        string serialized = json.ToString();
        Json reparsed = JsonHelper.ParseText(serialized);
        if (reparsed.HasErrors)
        {
            return 2;
        }

        Probe round = JsonHelper.BuildObjectForJson<Probe>(reparsed);
        if (round.Name != original.Name || round.Score != original.Score || round.Active != original.Active)
        {
            return 3;
        }

        // The generated reader sets an init-only property through an UnsafeAccessor, so the setter it names has to survive
        //  trimming too
        if (round.Tag != original.Tag)
        {
            return 5;
        }

        if (!AJsonGeneratedDispatch.IsRegistered(typeof(Probe)))
        {
            // The dispatch table wasn't populated -> the source generator never ran or the
            //  module-initializer was trimmed away. Either way, the optimization promise is
            //  broken.
            return 4;
        }

        return 0;
    }
}

[OptimizeAJson]
public class Probe
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool Active { get; set; }
    public string Tag { get; init; } = string.Empty;
}
