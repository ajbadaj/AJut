// Trimming verification entry point. Publishes trimmed (full mode) and exercises the AJson
// source-generated path on a marked type, then TermExpression end to end. If the trimmer drops
// anything load-bearing, this returns non-zero or throws.

using AJut.Text;
using AJut.Text.AJson;

int ajsonResult = TrimmedRoundTrip.Run();
return ajsonResult != 0 ? ajsonResult : TermExpressionProbe.Run();

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

// Parses, evaluates, searches, maps, and renders a TermExpression, and parses one it refuses, so every path it has goes
//  through the trimmer
internal static class TermExpressionProbe
{
    public static int Run ()
    {
        TermExpression.ParseResult parsed = TermExpression.Parse("code & (frontend | user interface | \"this & that\") & !legacy");
        if (!parsed)
        {
            return 6;
        }

        TermExpression expression = parsed.Value;
        if (!expression.Evaluate("code", "user interface") || expression.Evaluate("code", "frontend", "legacy"))
        {
            return 7;
        }

        if (!expression.IsMatch("Code for the User Interface") || expression.IsMatch("legacy code for the frontend"))
        {
            return 8;
        }

        TermExpression upper = expression.MapTerms(term => term.ToUpperInvariant());
        if (upper.ToString() != "CODE & (FRONTEND | USER INTERFACE | \"THIS & THAT\") & !LEGACY")
        {
            return 9;
        }

        TermExpression.ParseResult refused = TermExpression.Parse("code & frontend | ui");
        if (refused || (refused.ErrorIndex != 16))
        {
            return 10;
        }

        return 0;
    }
}
