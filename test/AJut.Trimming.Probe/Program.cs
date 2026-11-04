// Trimming verification entry point. Publishes trimmed (full mode) and exercises the AJson
// source-generated path on a marked type, then TermMatcher and term formulas end to end. If the trimmer drops
// anything load-bearing, this returns non-zero or throws.

using AJut.Text;
using AJut.Text.AJson;

int ajsonResult = TrimmedRoundTrip.Run();
if (ajsonResult != 0)
{
    return ajsonResult;
}

int matcherResult = TermMatcherProbe.Run();
return matcherResult != 0 ? matcherResult : TermFormulaProbe.Run();

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

// Parses, evaluates, searches, maps, and renders a TermMatcher, and parses one it refuses, so every path it has goes
//  through the trimmer
internal static class TermMatcherProbe
{
    public static int Run ()
    {
        TermParseResult<TermMatcher> parsed = TermExpression.ParseMatcher(
            "code & (frontend | user interface | \"this & that\") & !legacy"
        );
        if (!parsed)
        {
            return 6;
        }

        TermMatcher expression = parsed.Value;
        if (!expression.Evaluate("code", "user interface") || expression.Evaluate("code", "frontend", "legacy"))
        {
            return 7;
        }

        if (!expression.IsMatch("Code for the User Interface") || expression.IsMatch("legacy code for the frontend"))
        {
            return 8;
        }

        TermMatcher upper = expression.MapTerms(term => term.ToUpperInvariant());
        if (upper.ToString() != "CODE & (FRONTEND | USER INTERFACE | \"THIS & THAT\") & !LEGACY")
        {
            return 9;
        }

        TermParseResult<TermMatcher> refused = TermExpression.ParseMatcher("code & frontend | ui");
        if (refused || (refused.ErrorIndex != 16))
        {
            return 10;
        }

        return 0;
    }
}

// Parses and evaluates a formula with steps over labeled values, a template that sees its result, a table function and a
//  host function, refuses a formula it can't check, and tokenizes one, so every path term formulas have goes through the
//  trimmer
internal static class TermFormulaProbe
{
    public static int Run ()
    {
        TermParseResult<TermFunctions> functions = TermExpression.ParseFunctions(
            [new TermFunctionSource("cap", ["x", "high"], "min($x, $high)")],
            [new TermHostFunction("doubled", [eTermKind.Int], eTermKind.Int, arguments => TermValue.Int(arguments[0].AsInt * 2))]
        );

        if (!functions)
        {
            return 11;
        }

        TermDeclarations scoring = new TermDeclarations(functions.Value).IntList("scores").Int("bonus");
        TermParseResult<TermFormula> total = TermExpression.ParseFormula(
            "$best = cap(max($scores), 10)\n$best + doubled($bonus)",
            scoring,
            eTermKind.Int
        );

        if (!total)
        {
            return 12;
        }

        TermValues values = scoring.CreateValues()
            .Set("scores", new[] { TermValue.Int(12, "twelve"), TermValue.Int(4), TermValue.Int(7) })
            .Set("bonus", 1);

        TermEvaluation evaluated = total.Value.Evaluate(values);
        if (!evaluated || (evaluated.Value.AsInt != 12))
        {
            return 13;
        }

        TermParseResult<TermFormula> template = TermExpression.ParseTemplate(
            "$total ($scores, $(fixed(7 / 2, 2)))",
            total.Value.DeclarationsWithResult("total")
        );

        string? said = template ? template.Value.Evaluate(evaluated.ValuesWithResult("total")).Value.ToString() : null;
        if (said != "12 (twelve, 4, 7, 3.50)")
        {
            return 14;
        }

        TermParseResult<TermFormula> refused = TermExpression.ParseFormula("$bonus / 2", scoring, eTermKind.Int);
        if (refused || (refused.ErrorIndex != 7))
        {
            return 15;
        }

        if (TermExpression.TokenizeTemplate("$scores is $(1 + 2)").Count != 7)
        {
            return 16;
        }

        return 0;
    }
}
