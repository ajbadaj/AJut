namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using AJut.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TermFormulaTests
    {
        // ===========[ Usage ]================================================
        // Each of these is a whole use, start to finish, the way a host would write it

        [TestMethod]
        public void TermFormula_Usage_AFormulaOverDeclaredValues ()
        {
            TermDeclarations order = new TermDeclarations().Float("price").Int("quantity");
            TermParseResult<TermFormula> parsed = TermExpression.ParseFormula(
                "$price * $quantity",
                order,
                wants: eTermKind.Float
            );
            Assert.IsTrue(parsed, parsed.GetErrorReport());

            TermValues values = order.CreateValues();
            values.Set("price", 2.5);
            values.Set("quantity", 5);

            TermEvaluation evaluated = parsed.Value.Evaluate(values);
            Assert.IsTrue(evaluated, evaluated.GetErrorReport());
            Assert.AreEqual(12.5, evaluated.Value.AsFloat);
        }

        [TestMethod]
        public void TermFormula_Usage_ATemplate ()
        {
            TermDeclarations player = new TermDeclarations().String("name").Int("points");
            TermFormula template = MustParseTemplate(
                "$name has $points points$($points >= 10 ? \", a new record\" : \"\")",
                player
            );

            TermValues ada = player.CreateValues().Set("name", "Ada").Set("points", 12);
            TermValues grace = player.CreateValues().Set("name", "Grace").Set("points", 7);
            Assert.AreEqual("Ada has 12 points, a new record", MustEvaluate(template, ada).ToString());
            Assert.AreEqual("Grace has 7 points", MustEvaluate(template, grace).ToString());
        }

        [TestMethod]
        public void TermFormula_Usage_ATemplateSeesAFormulasStepsAndResult ()
        {
            TermDeclarations round = new TermDeclarations().IntList("scores").Int("bonus");
            TermFormula total = MustParse("$best = max($scores)\n$best + $bonus", round, eTermKind.Int);
            TermFormula summary = MustParseTemplate(
                "$total: the best of $scores was $best, plus $bonus",
                total.DeclarationsWithResult("total")
            );

            TermEvaluation evaluated = total.Evaluate(round.CreateValues().Set("scores", new[] { 4, 9, 6 }).Set("bonus", 2));
            Assert.AreEqual(11L, evaluated.Value.AsInt);
            TermValues withTotal = evaluated.ValuesWithResult("total");
            Assert.AreEqual("11: the best of 4, 9, 6 was 9, plus 2", MustEvaluate(summary, withTotal).ToString());
        }

        [TestMethod]
        public void TermFormula_Usage_TableFunctionsWrittenAsText ()
        {
            TermFunctions functions = MustParseFunctions(
                new TermFunctionSource("taxRate", [], "0.2"),
                new TermFunctionSource("withTax", ["price"], "$price * (1 + taxRate())")
            );

            TermDeclarations shop = new TermDeclarations(functions).Float("price");
            TermValues values = shop.CreateValues().Set("price", 10);
            Assert.AreEqual(12.0, MustEvaluate(MustParse("withTax($price)", shop), values).AsFloat);
            TermFormula template = MustParseTemplate("Costs $(withTax($price)) with tax", shop);
            Assert.AreEqual("Costs 12 with tax", MustEvaluate(template, values).ToString());
        }

        [TestMethod]
        public void TermFormula_Usage_AHostFunctionWrittenInCSharp ()
        {
            var discounts = new Dictionary<string, double> { ["SPRING"] = 0.25 };
            var discount = new TermHostFunction(
                "discount",
                [eTermKind.String],
                eTermKind.Float,
                arguments => TermValue.Float(discounts.TryGetValue(arguments[0].AsString, out double off) ? off : 0)
            );

            TermFunctions functions = MustParseFunctions([], discount);
            TermDeclarations shop = new TermDeclarations(functions).Float("price").String("code");
            TermFormula formula = MustParse("$price * (1 - discount($code))", shop);
            Assert.AreEqual(30.0, MustEvaluate(formula, shop.CreateValues().Set("price", 40).Set("code", "SPRING")).AsFloat);
        }

        [TestMethod]
        public void TermFormula_Usage_AnOptionalValueCheckedWithHas ()
        {
            TermDeclarations score = new TermDeclarations().Int("base").Int("bonus", optional: true);
            TermFormula total = MustParse("$base + (has($bonus) ? $bonus : 0)", score, eTermKind.Int);
            TermFormula said = MustParseTemplate("$base$( has($bonus) ? \" plus $bonus\" : \"\" )", score);

            TermValues without = score.CreateValues().Set("base", 5);
            Assert.AreEqual(5L, MustEvaluate(total, without).AsInt);
            Assert.AreEqual("5", MustEvaluate(said, without).ToString());

            TermValues with = score.CreateValues().Set("base", 5).Set("bonus", 3);
            Assert.AreEqual(8L, MustEvaluate(total, with).AsInt);
            Assert.AreEqual("5 plus 3", MustEvaluate(said, with).ToString());
        }

        [TestMethod]
        public void TermFormula_Usage_LabeledValues ()
        {
            TermDeclarations medals = new TermDeclarations().IntList("medals");
            TermValues values = medals.CreateValues().Set("medals", Medals(1, 3, 2));

            Assert.AreEqual("Bronze, Gold, Silver", MustEvaluate(MustParseTemplate("$medals", medals), values).ToString());
            Assert.AreEqual("Gold", MustEvaluate(MustParse("max($medals)", medals), values).ToString());
            Assert.AreEqual(6L, MustEvaluate(MustParse("sum($medals)", medals), values).AsInt);
            Assert.AreEqual("1, 3, 2", MustEvaluate(MustParseTemplate("$(number($medals))", medals), values).ToString());
            Assert.AreEqual(1L, MustEvaluate(MustParse("count(label($medals) == \"Gold\")", medals), values).AsInt);
        }

        [TestMethod]
        public void TermExpression_Usage_TokensForAnEditor ()
        {
            string text = "max($scores) + 1 >= true ? \"a $x\" : y";
            eTermTokenKind[] kinds = TermExpression.TokenizeFormula(text).Select(token => token.Kind).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    eTermTokenKind.Function, eTermTokenKind.Punctuation, eTermTokenKind.Variable, eTermTokenKind.Punctuation,
                    eTermTokenKind.Operator, eTermTokenKind.Number, eTermTokenKind.Operator, eTermTokenKind.Keyword,
                    eTermTokenKind.Operator, eTermTokenKind.String, eTermTokenKind.String, eTermTokenKind.Variable,
                    eTermTokenKind.String, eTermTokenKind.Operator, eTermTokenKind.Error,
                },
                kinds
            );

            string template = "$name costs \\$$(1 + 2)";
            IReadOnlyList<TermToken> tokens = TermExpression.TokenizeTemplate(template);
            CollectionAssert.AreEqual(
                new[]
                {
                    eTermTokenKind.Variable, eTermTokenKind.Text, eTermTokenKind.Escape, eTermTokenKind.Punctuation,
                    eTermTokenKind.Number, eTermTokenKind.Operator, eTermTokenKind.Number, eTermTokenKind.Punctuation,
                },
                tokens.Select(token => token.Kind).ToArray()
            );

            Assert.AreEqual("$name", template.Substring(tokens[0].Start, tokens[0].Length));
            Assert.AreEqual(template.Length, tokens[^1].Start + tokens[^1].Length);
        }

        // ===========[ Numbers and printing ]=================================

        [DataTestMethod]
        [DataRow("0.1 + 0.2", "0.3")]
        [DataRow("10 / 3", "3.33333333333333")]
        [DataRow("7 / 1", "7")]
        [DataRow("7 / 2", "3.5")]
        [DataRow("-1 % 24", "23")]
        [DataRow("fixed(3.5, 2)", "3.50")]
        [DataRow("fixed(7, 1)", "7.0")]
        [DataRow("fixed(2.345, 2)", "2.35")]
        [DataRow("round(2.345, 2)", "2.35")]
        [DataRow("round(2.5)", "3")]
        [DataRow("round(-2.5)", "-3")]
        [DataRow("trunc(-3.5)", "-3")]
        [DataRow("floor(-3.5)", "-4")]
        [DataRow("ceil(3.2)", "4")]
        [DataRow("floor((0.7 + 0.1) * 10)", "8")]
        [DataRow("1 < 2", "true")]
        [DataRow("\"a\" == \"a\" && \"a\" != \"A\"", "true")]
        public void TermFormula_Print_LikeASpreadsheet (string formula, string expected)
        {
            Assert.AreEqual(expected, MustEvaluate(MustParse(formula, new TermDeclarations()), new TermDeclarations().CreateValues()).ToString());
        }

        [TestMethod]
        public void TermFormula_Print_IsTheSameUnderAnyCulture ()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                TermDeclarations declarations = new TermDeclarations().FloatList("f");
                TermFormula template = MustParseTemplate("$(10 / 4) $f $(fixed(1.5, 2))", declarations);
                Assert.AreEqual("2.5 1.25, 3 1.50", MustEvaluate(template, declarations.CreateValues().Set("f", new[] { 1.25, 3.0 })).ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        [TestMethod]
        public void TermFormula_Print_LabelThenPrintRuleThenDefault ()
        {
            TermDeclarations shop = new TermDeclarations().Float("chance", print: value => (value * 100).ToString("0", CultureInfo.InvariantCulture) + "%");
            TermFormula template = MustParseTemplate("$chance, $($chance * 2)", shop);

            Assert.AreEqual("46%, 0.92", MustEvaluate(template, shop.CreateValues().Set("chance", 0.46)).ToString());
            Assert.AreEqual("lucky, 0.92", MustEvaluate(template, shop.CreateValues().Set("chance", TermValue.Float(0.46, "lucky"))).ToString());
        }

        [TestMethod]
        public void TermFormula_Print_AnIntPrintRuleSurvivesBeingTakenAsANumber ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("i", print: value => value + " pts");
            TermValues values = declarations.CreateValues().Set("i", 5);
            Assert.AreEqual("5 pts", MustEvaluate(MustParseTemplate("$(true ? $i : 2.5)", declarations), values).ToString());
            Assert.AreEqual("5 pts", MustEvaluate(MustParseTemplate("$(max($i, 2.5))", declarations), values).ToString());
            Assert.AreEqual("5 pts", MustEvaluate(MustParse("$i", declarations, eTermKind.Float), values).ToString());
        }

        [TestMethod]
        public void TermFormula_Rounding_LeavesWholeNumbersAndTinyFractionsRight ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("a");
            TermValues values = declarations.CreateValues().Set("a", 1234567890123457L);
            Assert.AreEqual(1234567890123457L, MustEvaluate(MustParse("floor($a)", declarations), values).AsInt);
            Assert.AreEqual("1234567890123457", MustEvaluate(MustParse("fixed($a, 0)", declarations), values).AsString);
            Assert.AreEqual(1L, MustEvaluate(MustParse("ceil(1 / 1000000000000000000000000000000.0)", declarations), values).AsInt);
        }

        // ===========[ Kinds, steps and conditionals ]========================

        [TestMethod]
        public void TermFormula_Kinds_AWholeNumberGoesWhereANumberIsWanted ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("count").Float("rate");
            TermValues values = declarations.CreateValues().Set("count", 4).Set("rate", 1);

            TermFormula widened = MustParse("$count", declarations, eTermKind.Float);
            Assert.AreEqual(eTermKind.Float, widened.ResultKind);
            Assert.AreEqual(4.0, MustEvaluate(widened, values).AsFloat);
            Assert.AreEqual(eTermKind.Float, MustParse("$count + $rate", declarations).ResultKind);
            Assert.AreEqual(eTermKind.Float, MustParse("$count / 2", declarations).ResultKind);
            Assert.AreEqual(eTermKind.Int, MustParse("floor($count / 2)", declarations).ResultKind);
            Assert.AreEqual("4", MustEvaluate(MustParse("$count", declarations, eTermKind.String), values).AsString);
        }

        [TestMethod]
        public void TermFormula_Conditional_PrintedSidesCanDiffer ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("points");
            TermFormula template = MustParseTemplate("$($points >= 10 ? \"plenty\" : $points)", declarations);

            Assert.AreEqual("plenty", MustEvaluate(template, declarations.CreateValues().Set("points", 12)).ToString());
            Assert.AreEqual("7", MustEvaluate(template, declarations.CreateValues().Set("points", 7)).ToString());
        }

        [TestMethod]
        public void TermFormula_Steps_CanBeRedefinedAndChangeKind ()
        {
            TermDeclarations scores = new TermDeclarations().IntList("scores");
            TermValues values = scores.CreateValues().Set("scores", new[] { 10, 3, 8, 10 });

            TermFormula redefined = MustParse("$high = count($scores >= 8); $high = $high + count($scores == 10); $high", scores, eTermKind.Int);
            Assert.AreEqual(5L, MustEvaluate(redefined, values).AsInt);

            TermFormula changesKind = MustParse("$x = 1\n$x = \"one\"\n$x", scores);
            Assert.AreEqual("one", MustEvaluate(changesKind, values).AsString);
        }

        [TestMethod]
        public void TermFormula_Steps_OneNamedLikeTheResultGivesWayToIt ()
        {
            TermDeclarations scores = new TermDeclarations().IntList("scores");
            TermFormula formula = MustParse("$total = sum($scores)\n$total + 1", scores, eTermKind.Int);
            TermFormula template = MustParseTemplate("$total", formula.DeclarationsWithResult("total"));
            TermEvaluation evaluated = formula.Evaluate(scores.CreateValues().Set("scores", new[] { 3, 9 }));
            Assert.AreEqual("13", MustEvaluate(template, evaluated.ValuesWithResult("total")).ToString());

            var ignoringCase = new TermParseOptions { IgnoreNameCase = true };
            TermFormula shouty = MustParse("$Total = sum($scores)\n$Total", scores, eTermKind.Int, ignoringCase);
            TermParseResult<TermFormula> shoutyTemplate = TermExpression.ParseTemplate("$TOTAL", shouty.DeclarationsWithResult("total"), ignoringCase);
            Assert.IsTrue(shoutyTemplate, shoutyTemplate.GetErrorReport());
        }

        [TestMethod]
        public void TermFormula_Names_AreCaseSensitiveUnlessTurnedOff ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("score");
            AssertRefused(TermExpression.ParseFormula("$Score + 1", declarations), 0, "Did you mean $score?");
            AssertRefused(TermExpression.ParseFormula("Max(1, 2)", declarations), 0, "Did you mean max?");

            var ignoringCase = new TermParseOptions { IgnoreNameCase = true };
            TermFormula formula = MustParse("Max($Score, 2) + 1", declarations, options: ignoringCase);
            Assert.AreEqual(8L, MustEvaluate(formula, declarations.CreateValues().Set("score", 7)).AsInt);
        }

        // ===========[ Lists ]================================================

        [TestMethod]
        public void TermFormula_Lists_GoItemByItem ()
        {
            TermDeclarations declarations = new TermDeclarations().IntList("scores").Int("bonus");
            TermValues values = declarations.CreateValues().Set("scores", new[] { 3, 6, 1, 6 }).Set("bonus", 2);

            Assert.AreEqual("5, 8, 3, 8", MustEvaluate(MustParse("$scores + $bonus", declarations), values).ToString());
            Assert.AreEqual("6, 12, 2, 12", MustEvaluate(MustParse("$scores * 2", declarations), values).ToString());
            Assert.AreEqual("false, true, false, true", MustEvaluate(MustParse("$scores > 4", declarations, eTermKind.BoolList), values).ToString());
            Assert.AreEqual("16", MustEvaluate(MustParse("sum($scores)", declarations), values).ToString());
            Assert.AreEqual("1", MustEvaluate(MustParse("min($scores)", declarations), values).ToString());
            Assert.AreEqual("4", MustEvaluate(MustParse("count($scores)", declarations), values).ToString());
            Assert.AreEqual("2", MustEvaluate(MustParse("count($scores > 4)", declarations), values).ToString());
            Assert.AreEqual("3 6 1 6", MustEvaluate(MustParse("join($scores, \" \")", declarations), values).ToString());
        }

        [TestMethod]
        public void TermFormula_Lists_HighestAndLowestKeepTheOriginalOrder ()
        {
            TermDeclarations declarations = new TermDeclarations().IntList("scores");
            TermValues values = declarations.CreateValues().Set("scores", new[] { 3, 6, 1, 6 });

            Assert.AreEqual("6, 6", MustEvaluate(MustParse("highest($scores, 2)", declarations), values).ToString());
            Assert.AreEqual("3, 1", MustEvaluate(MustParse("lowest($scores, 2)", declarations), values).ToString());
            Assert.AreEqual("3, 6, 1, 6", MustEvaluate(MustParse("highest($scores, 9)", declarations), values).ToString());

            TermDeclarations medals = new TermDeclarations().IntList("medals");
            TermValues labeled = medals.CreateValues().Set("medals", Medals(2, 3, 2));
            Assert.AreEqual("Silver, Gold", MustEvaluate(MustParse("highest($medals, 2)", medals), labeled).ToString());
            Assert.AreEqual("Silver", MustEvaluate(MustParse("min($medals)", medals), labeled).ToString());
            Assert.AreEqual("7", MustEvaluate(MustParse("sum($medals)", medals), labeled).ToString());
        }

        [TestMethod]
        public void TermFormula_BuiltIns_ClampAbsAvgAnyAndAll ()
        {
            TermDeclarations declarations = new TermDeclarations().IntList("scores").Int("score").IntList("medals").IntList("none");
            TermValues values = declarations.CreateValues()
                .Set("scores", new[] { 3, 12, -4 })
                .Set("score", -7)
                .Set("medals", Medals(1, 3, 2))
                .Set("none", Array.Empty<int>());

            Assert.AreEqual("0", MustEvaluate(MustParse("clamp($score, 0, 10)", declarations), values).ToString());
            Assert.AreEqual("3, 10, 0", MustEvaluate(MustParse("clamp($scores, 0, 10)", declarations), values).ToString());
            Assert.AreEqual("2, Gold, Silver", MustEvaluate(MustParse("clamp($medals, 2, 3)", declarations), values).ToString());
            Assert.AreEqual(eTermKind.Int, MustParse("clamp($score, 0, 10)", declarations).ResultKind);
            Assert.AreEqual(eTermKind.Float, MustParse("clamp($score, 0, 2.5)", declarations).ResultKind);

            Assert.AreEqual("7", MustEvaluate(MustParse("abs($score)", declarations), values).ToString());
            Assert.AreEqual("3, 12, 4", MustEvaluate(MustParse("abs($scores)", declarations), values).ToString());

            Assert.AreEqual("3.66666666666667", MustEvaluate(MustParse("avg($scores)", declarations), values).ToString());
            Assert.AreEqual("1.5", MustEvaluate(MustParse("avg(1, 2)", declarations), values).ToString());
            Assert.AreEqual(eTermKind.Float, MustParse("avg(2, 4)", declarations).ResultKind);

            Assert.AreEqual("true", MustEvaluate(MustParse("any($scores > 10)", declarations), values).ToString());
            Assert.AreEqual("false", MustEvaluate(MustParse("all($scores > 0)", declarations), values).ToString());
            Assert.AreEqual("false", MustEvaluate(MustParse("any($none > 0)", declarations), values).ToString());
            Assert.AreEqual("true", MustEvaluate(MustParse("all($none > 0)", declarations), values).ToString());

            AssertEvaluationFails(declarations, values, "clamp(1, 5, $score)", 0, "has a low of 5 above its high of -7");
            AssertEvaluationFails(declarations, values, "avg($none)", 0, "avg() of an empty list");
            AssertRefused(TermExpression.ParseFormula("any($score > 1)", declarations), 4, "any() wants a list, and $score is a single whole number here");
            AssertRefused(TermExpression.ParseFormula("all($scores)", declarations), 4, "all() wants a list of true or false values");
            AssertRefused(TermExpression.ParseFormula("clamp($score, $scores, 1)", declarations), 14, "clamp() wants a single number for its low");
            AssertRefused(TermExpression.ParseFormula("clamp($score, 1)", declarations), 0, "clamp() takes a number and the low and high to keep it between");
        }

        // ===========[ Text and templates ]===================================

        [TestMethod]
        public void TermFormula_Template_EscapesAndNamesRunningOn ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("decade").String("name");
            TermValues values = declarations.CreateValues().Set("decade", 1990).Set("name", "Ada");

            Assert.AreEqual("$5 and \\ and \\x \"quoted\"", MustEvaluate(MustParseTemplate("\\$5 and \\\\ and \\x \"quoted\"", declarations), values).ToString());
            Assert.AreEqual("1990s, Ada's", MustEvaluate(MustParseTemplate("$($decade)s, $name's", declarations), values).ToString());
            Assert.AreEqual(string.Empty, MustEvaluate(MustParseTemplate(string.Empty, declarations), values).ToString());
        }

        [TestMethod]
        public void TermFormula_QuotedText_TakesValuesAndNestsQuotes ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("wins").Int("losses");
            TermFormula formula = MustParse("\"$wins wins, $( $losses > 5 ? \"many\" : \"few\" ) losses\"", declarations);
            Assert.AreEqual("7 wins, few losses", MustEvaluate(formula, declarations.CreateValues().Set("wins", 7).Set("losses", 3)).AsString);
        }

        // ===========[ Functions ]============================================

        [TestMethod]
        public void TermFunctions_AuthorsOwnPrinting ()
        {
            TermFunctions functions = MustParseFunctions(
                new TermFunctionSource("money", ["x"], "\"\\$$(fixed($x, 2))\""),
                new TermFunctionSource("pct", ["x"], "\"$(fixed($x * 100, 0))%\"")
            );

            TermDeclarations shop = new TermDeclarations(functions).Float("price").Float("chance");
            TermFormula template = MustParseTemplate("Costs $(money($price)), a $(pct($chance)) chance", shop);
            Assert.AreEqual("Costs $4.50, a 46% chance", MustEvaluate(template, shop.CreateValues().Set("price", 4.5).Set("chance", 0.46)).ToString());
        }

        [TestMethod]
        public void TermFunctions_ATableFunctionWinsOverABuiltIn ()
        {
            Assert.IsTrue(TermExpression.IsBuiltIn("max"));
            Assert.IsFalse(TermExpression.IsBuiltIn("taxRate"));

            TermFunctions functions = MustParseFunctions(new TermFunctionSource("max", ["x"], "100"));
            TermDeclarations declarations = new TermDeclarations(functions).IntList("scores");
            Assert.AreEqual(100L, MustEvaluate(MustParse("max($scores)", declarations), declarations.CreateValues().Set("scores", new[] { 1, 2 })).AsInt);
        }

        [TestMethod]
        public void TermFunctions_AreListedForAnEditor ()
        {
            Assert.IsTrue(TermExpression.BuiltInFunctions.Any(function => (function.Name == "highest") && (function.Source == eTermFunctionSource.BuiltIn)));

            var shout = new TermHostFunction("shout", [eTermKind.String], eTermKind.String, arguments => TermValue.String(arguments[0].AsString.ToUpperInvariant()));
            TermFunctions functions = MustParseFunctions([new TermFunctionSource("between", ["x", "low", "high"], "$x >= $low && $x <= $high")], shout);
            Assert.AreEqual("between(x, low, high)", functions.Functions[0].Usage);
            Assert.AreEqual(eTermFunctionSource.Host, functions.Functions[1].Source);
        }

        [TestMethod]
        public void TermFunctions_HostFunctions_AreCheckedAndCanFail ()
        {
            var doubled = new TermHostFunction("doubled", [eTermKind.Float], eTermKind.Float, arguments => TermValue.Float(arguments[0].AsFloat * 2));
            var refuses = new TermHostFunction("refuses", [], eTermKind.Int, _ => TermValue.Error("the shop is closed"));
            TermFunctions functions = MustParseFunctions([], doubled, refuses);

            TermDeclarations declarations = new TermDeclarations(functions).Int("a").String("s");
            Assert.AreEqual(6.0, MustEvaluate(MustParse("doubled($a)", declarations), declarations.CreateValues().Set("a", 3)).AsFloat);
            AssertRefused(TermExpression.ParseFormula("doubled($s)", declarations), 8, "doubled() wants a number for argument 1");

            TermEvaluation failed = MustParse("refuses() + 1", declarations).Evaluate(declarations.CreateValues());
            Assert.IsFalse(failed);
            StringAssert.Contains(failed.GetErrorReport(), "refuses() at position 1: the shop is closed");
        }

        // ===========[ Errors while typing ]==================================

        [TestMethod]
        public void TermFormula_Parse_UnknownNameSuggestsTheNearOne ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("score").Int("bonus");
            AssertRefused(TermExpression.ParseFormula("$scor + $bonus", declarations), 0, "Unknown name '$scor' at position 1", "Did you mean $score?");
            AssertRefused(TermExpression.ParseFormula("mx($score)", declarations), 0, "Unknown function 'mx'", "Did you mean max?");
        }

        [TestMethod]
        public void TermFormula_Parse_PlusIsForNumbersOnly ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("score");
            AssertRefused(TermExpression.ParseFormula("\"Total: \" + $score", declarations), 10, "'+' needs numbers, and its left side is text (position 11)");
        }

        [TestMethod]
        public void TermFormula_Parse_AndAndOrCantMix ()
        {
            TermDeclarations flags = new TermDeclarations().Bool("a").Bool("b").Bool("c");
            AssertRefused(TermExpression.ParseFormula("$a && $b || $c", flags), 9, "($a && $b) || $c", "$a && ($b || $c)");
            Assert.IsTrue(TermExpression.ParseFormula("($a && $b) || $c", flags));
        }

        [TestMethod]
        public void TermFormula_Parse_StepRules ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("score");
            AssertRefused(TermExpression.ParseFormula("$score = $score + 1\n$score", declarations), 0, "$score at position 1 comes from the host");
            AssertRefused(TermExpression.ParseFormula("$x = $y + 1; $y = 2; $x", declarations), 5, "$y is used before it's set (position 6; it's set at position 14)");
            AssertRefused(TermExpression.ParseFormula("$y = $y + 1\n$y", declarations), 5, "$y at position 6 is used in its own first definition");
        }

        [TestMethod]
        public void TermFormula_Parse_FractionWhereAWholeNumberIsWanted ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("total").Float("x");
            AssertRefused(
                TermExpression.ParseFormula("$total / 2", declarations, eTermKind.Int),
                7,
                "'/' can leave a fraction (position 8), and a whole number is wanted",
                "trunc(), floor(), ceil(), or round()"
            );

            AssertRefused(TermExpression.ParseFormula("round($x, 2.0)", declarations), 10, "The number 2.0 has a fraction");
            Assert.IsTrue(TermExpression.ParseFormula("floor($total / 2)", declarations, eTermKind.Int));
        }

        [TestMethod]
        public void TermFormula_Parse_TwoSidesDifferUnlessPrinted ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("points");
            AssertRefused(TermExpression.ParseFormula("$points >= 10 ? \"plenty\" : $points", declarations, eTermKind.Int), 14, "two sides of '? :'");
            Assert.IsTrue(TermExpression.ParseFormula("$points >= 10 ? \"plenty\" : $points", declarations, eTermKind.String));
        }

        [TestMethod]
        public void TermFormula_Parse_OptionalValueNeedsAHasCheck ()
        {
            TermDeclarations declarations = new TermDeclarations().IntList("scores").Int("bonus", optional: true).Int("extra", optional: true);
            AssertRefused(TermExpression.ParseFormula("max($scores) + $bonus", declarations), 15, "$bonus at position 16 might not be set");
            Assert.IsTrue(TermExpression.ParseFormula("has($bonus) && $bonus > 2", declarations));
            Assert.IsTrue(TermExpression.ParseFormula("(has($bonus) && has($extra)) && $bonus + $extra > 0", declarations));
            AssertRefused(TermExpression.ParseFormula("has($bonus) ? 0 : $bonus", declarations), 18, "might not be set");
            AssertRefused(TermExpression.ParseFormula("has($bonus) && $scores > $bonus", declarations), 0, "can only guard single true or false values");
        }

        [TestMethod]
        public void TermFormula_Parse_MistakesInTheShapeOfTheText ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("a");
            AssertRefused(TermExpression.ParseFormula("", declarations), 0, "The formula is empty");
            AssertRefused(TermExpression.ParseFormula("($a + 1", declarations), 0, "never closed");
            AssertRefused(TermExpression.ParseFormula("$a +", declarations), 3, "'+' at position 4 has nothing after it");
            AssertRefused(TermExpression.ParseFormula("$a $a", declarations), 3, "Something is missing before '$a'", "a new line for a new step");
            AssertRefused(TermExpression.ParseFormula("max(1 2)", declarations), 6, "a ',' between arguments");
            AssertRefused(TermExpression.ParseFormula("$a & 1", declarations), 3, "Use '&&'");
            AssertRefused(TermExpression.ParseFormula("a + 1", declarations), 0, "needs a '$' in front");
            AssertRefused(TermExpression.ParseFormula("$a + $a = 2", declarations), 8, "To compare, use '=='");
            AssertRefused(TermExpression.ParseFormula("\"open", declarations), 0, "never closed");
            AssertRefused(TermExpression.ParseFormula("99999999999999999999", declarations), 0, "too big");
            AssertRefused(TermExpression.ParseTemplate("costs $ 5", declarations), 6, "write \\$");
            AssertRefused(TermExpression.ParseTemplate("$( $a", declarations), 0, "never closed");
            AssertRefused(TermExpression.ParseTemplate("$()", declarations), 0, "is empty");
        }

        [TestMethod]
        public void TermFormula_Parse_WrittenNumbersAreCheckedWhileTyping ()
        {
            TermDeclarations declarations = new TermDeclarations().Float("x").IntList("scores");
            AssertRefused(TermExpression.ParseFormula("clamp($x, 300, -15)", declarations), 0, "has a low of 300 above its high of -15");
            AssertRefused(TermExpression.ParseFormula("round($x, 20)", declarations), 10, "takes 0 to 15 decimal places");
            AssertRefused(TermExpression.ParseFormula("fixed($x, -1)", declarations), 10, "takes 0 to 15 decimal places");
            AssertRefused(TermExpression.ParseFormula("highest($scores, -1)", declarations), 17, "can't keep -1 items");
            AssertRefused(TermExpression.ParseFormula("$x / 0", declarations), 3, "Dividing by zero at position 4");
            AssertRefused(TermExpression.ParseFormula("$x % (0)", declarations), 3, "'%' by zero");
            Assert.IsTrue(TermExpression.ParseFormula("clamp($x, -15, 300) / 2", declarations));

            // A function's body is checked the same way when it's registered
            TermParseResult<TermFunctions> parsed = TermExpression.ParseFunctions([new TermFunctionSource("test", ["value"], "clamp($value, 300, -15)")]);
            Assert.IsFalse(parsed);
            StringAssert.Contains(parsed.TermErrors[0].Message, "has a low of 300 above its high of -15");
            Assert.AreEqual("test", parsed.TermErrors[0].FunctionName);
        }

        [TestMethod]
        public void TermFormula_Parse_KindMistakes ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("score").IntList("scores").String("name");
            AssertRefused(TermExpression.ParseFormula("$name < 3", declarations), 6, "'<' compares numbers");
            AssertRefused(TermExpression.ParseFormula("$name == 3", declarations), 6, "'==' compares text with a number");
            AssertRefused(TermExpression.ParseFormula("count($score)", declarations), 6, "count() wants a list, and $score is a single whole number here");
            AssertRefused(TermExpression.ParseFormula("max($scores, 12)", declarations), 0, "never a mix");
            AssertRefused(TermExpression.ParseFormula("$scores >= 8 ? 1 : 0", declarations), 13, "A list of true or false values can't decide '?'");
            AssertRefused(TermExpression.ParseFormula("$score", declarations, eTermKind.StringList), 0, "is wanted");
        }

        [TestMethod]
        public void TermFunctions_Cycles_AreRefusedWhenRegistered ()
        {
            TermParseResult<TermFunctions> direct = TermExpression.ParseFunctions([new TermFunctionSource("again", ["x"], "again($x) + 1")]);
            Assert.IsFalse(direct);
            StringAssert.Contains(direct.GetErrorReport(), "again calls itself at position 1");
            Assert.AreEqual("again", direct.TermErrors[0].FunctionName);

            TermParseResult<TermFunctions> around = TermExpression.ParseFunctions(
            [
                new TermFunctionSource("ping", ["x"], "pong($x)"),
                new TermFunctionSource("pong", ["x"], "ping($x) + 1"),
                new TermFunctionSource("fine", ["x"], "$x * 2"),
            ]);

            Assert.IsFalse(around);
            Assert.AreEqual(2, around.TermErrors.Count);
            StringAssert.Contains(around.TermErrors[0].Message, "ping calls pong at position 1, which calls ping");

            // The rest of the table still works, and a call to a broken one fails at the call
            TermDeclarations declarations = new TermDeclarations(around.Value).Int("a");
            Assert.AreEqual(6L, MustEvaluate(MustParse("fine($a)", declarations), declarations.CreateValues().Set("a", 3)).AsInt);
            AssertRefused(TermExpression.ParseFormula("1 + ping($a)", declarations), 4, "ping($a) at position 5 can't run until ping is fixed");
        }

        [TestMethod]
        public void TermFunctions_BodyMistakes_ShowWhenRegistered ()
        {
            TermParseResult<TermFunctions> parsed = TermExpression.ParseFunctions(
            [
                new TermFunctionSource("countAtLeast", ["list", "minimum"], "count($list >= $minimun)"),
                new TermFunctionSource("bad name", [], "1"),
                new TermFunctionSource("broken", [], "(1 +"),
                new TermFunctionSource("tally", ["x"], "count($x, 1)"),
                new TermFunctionSource("true", [], "1"),
                new TermFunctionSource("constant", [], "$nope"),
            ]);

            Assert.AreEqual(6, parsed.TermErrors.Count);
            StringAssert.Contains(parsed.TermErrors[0].Message, "Unknown name '$minimun'");
            StringAssert.Contains(parsed.TermErrors[0].Message, "Did you mean $minimum?");
            Assert.AreEqual(15, parsed.TermErrors[0].Index);
            Assert.AreEqual(-1, parsed.TermErrors[1].Index);
            Assert.AreEqual("broken", parsed.TermErrors[2].FunctionName);
            StringAssert.Contains(parsed.TermErrors[3].Message, "count() takes one list, and is given 2 arguments");
            StringAssert.Contains(parsed.TermErrors[4].Message, "'true' can't be a function's name");
            StringAssert.Contains(parsed.TermErrors[5].Message, "and constant has none");
        }

        [TestMethod]
        public void TermFunctions_KindMistakeInsideABody_IsReportedAtTheCallAndInTheBody ()
        {
            TermFunctions functions = MustParseFunctions(new TermFunctionSource("countAtLeast", ["list", "minimum"], "count($list >= $minimum)"));
            TermDeclarations declarations = new TermDeclarations(functions).Int("score").IntList("scores");

            TermParseResult<TermFormula> parsed = TermExpression.ParseFormula("countAtLeast($score, 8)", declarations);
            AssertRefused(parsed, 0, "countAtLeast($score, 8) at position 1: inside countAtLeast, count() wants a list, and $list is a single whole number here");
            Assert.AreEqual(2, parsed.TermErrors.Count);
            Assert.AreEqual("countAtLeast", parsed.TermErrors[1].FunctionName);
            Assert.AreEqual(6, parsed.TermErrors[1].Index);

            Assert.IsTrue(TermExpression.ParseFormula("countAtLeast($scores, 8)", declarations));
        }

        // ===========[ Errors while evaluating ]==============================

        [TestMethod]
        public void TermFormula_Evaluate_ValueCausedErrorsComeBackAsResults ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("a").Int("b").IntList("d").IntList("e").Int("n");
            TermValues values = declarations.CreateValues()
                .Set("a", 5).Set("b", 0).Set("d", new[] { 1, 2, 3 }).Set("e", new[] { 1, 2 }).Set("n", -1);

            AssertEvaluationFails(declarations, values, "$a / $b", 3, "Dividing by zero at position 4");
            AssertEvaluationFails(declarations, values, "$a % $b", 3, "'%' by zero");
            AssertEvaluationFails(declarations, values, "$d + $e", 3, "Two lists of different lengths");
            AssertEvaluationFails(declarations, values, "highest($d, $n)", 12, "can't keep -1 items");

            TermDeclarations empty = new TermDeclarations().IntList("d");
            AssertEvaluationFails(empty, empty.CreateValues().Set("d", Array.Empty<int>()), "max($d)", 0, "max() of an empty list");
            Assert.AreEqual(0L, MustEvaluate(MustParse("sum($d) + count($d)", empty), empty.CreateValues().Set("d", Array.Empty<int>())).AsInt);
        }

        [TestMethod]
        public void TermFormula_Evaluate_MissingValueAndTooBigAWholeNumber ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("a").Int("b");
            AssertEvaluationFails(declarations, declarations.CreateValues().Set("a", 1), "$a + $b", 5, "$b at position 6 wasn't given a value");

            TermValues huge = declarations.CreateValues().Set("a", 9007199254740991L).Set("b", 2);
            AssertEvaluationFails(declarations, huge, "$a * $b", 3, "too big to hold exactly");
            AssertEvaluationFails(declarations, huge, "$a + 1", 3, "too big to hold exactly");
            Assert.IsTrue(MustParse("$a / 1 * $b", declarations).Evaluate(huge));
            AssertRefused(TermExpression.ParseFormula("9007199254740992", declarations), 0, "too big");
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => TermValue.Int(9007199254740992L));
        }

        [TestMethod]
        public void TermFormula_Evaluate_ErrorInsideAFunctionNamesBoth ()
        {
            TermFunctions functions = MustParseFunctions(new TermFunctionSource("share", ["total", "people"], "$each = $total / $people\n$each"));
            TermDeclarations declarations = new TermDeclarations(functions).Int("gold");

            TermEvaluation failed = MustParse("1 + share($gold, 0)", declarations).Evaluate(declarations.CreateValues().Set("gold", 10));
            Assert.IsFalse(failed);
            Assert.AreEqual(4, failed.ErrorIndex);
            StringAssert.Contains(failed.TermErrors[0].Message, "share($gold, 0) at position 5: inside share, dividing by zero at position 16");
            Assert.AreEqual("share", failed.TermErrors[1].FunctionName);
            Assert.AreEqual(15, failed.TermErrors[1].Index);
        }

        // ===========[ Safety ]===============================================

        [TestMethod]
        public void TermFormula_Parse_NestsUpToTheLimitAndRefusesPastIt ()
        {
            TermDeclarations none = new TermDeclarations();
            int limit = TermParseOptions.kDefaultMaxDepth;
            string atTheLimit = new string('(', limit) + "1" + new string(')', limit);
            Assert.AreEqual(1L, MustEvaluate(MustParse(atTheLimit, none), none.CreateValues()).AsInt);

            string pastTheLimit = new string('(', limit + 1) + "1" + new string(')', limit + 1);
            AssertRefused(TermExpression.ParseFormula(pastTheLimit, none), limit, "nests more than 64 deep");

            Assert.IsFalse(TermExpression.ParseFormula(new string('-', 100_000) + "1", none));
            Assert.IsFalse(TermExpression.ParseTemplate(String.Concat(Enumerable.Repeat("$(\"", 100_000)), none));
        }

        [TestMethod]
        public void TermFormula_Parse_AMegabyteParsesInBoundedTime ()
        {
            TermDeclarations none = new TermDeclarations();
            string longChain = String.Concat(Enumerable.Repeat("1 + ", 250_000)) + "1";

            Stopwatch timer = Stopwatch.StartNew();
            TermFormula formula = MustParse(longChain, none);
            TermEvaluation evaluated = formula.Evaluate(none.CreateValues(), new TermEvaluateOptions { MaxWork = 10_000_000 });
            timer.Stop();

            Assert.AreEqual(250_001L, evaluated.Value.AsInt);
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(10), $"Took {timer.Elapsed}");

            string longTemplate = new string('x', 1_000_000) + "$(1)";
            var plenty = new TermEvaluateOptions { MaxWork = 10_000_000 };
            Assert.AreEqual(1_000_001, MustEvaluate(MustParseTemplate(longTemplate, none), none.CreateValues(), plenty).AsString.Length);
        }

        [TestMethod]
        public void TermFormula_Evaluate_WorkLimitStopsDoublingChains ()
        {
            var sources = new List<TermFunctionSource> { new TermFunctionSource("f0", ["x"], "$x + 1") };
            var texts = new List<TermFunctionSource> { new TermFunctionSource("s0", ["x"], "\"$x$x\"") };
            for (int index = 1; index <= 30; ++index)
            {
                sources.Add(new TermFunctionSource($"f{index}", ["x"], $"f{index - 1}($x) + f{index - 1}($x)"));
                texts.Add(new TermFunctionSource($"s{index}", ["x"], $"s{index - 1}(s{index - 1}($x))"));
            }

            sources.AddRange(texts);
            TermFunctions functions = MustParseFunctions(sources.ToArray());
            TermDeclarations declarations = new TermDeclarations(functions).Int("a").String("s");

            Stopwatch timer = Stopwatch.StartNew();
            AssertEvaluationFails(MustParse("f30($a)", declarations), declarations.CreateValues().Set("a", 1), 0, "This took too much work to figure out");
            AssertEvaluationFails(MustParse("s30(\"ab\")", declarations), declarations.CreateValues().Set("a", 1), 0, "too much work");
            timer.Stop();
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(10), $"Took {timer.Elapsed}");

            TermValues longText = declarations.CreateValues().Set("a", 1).Set("s", new string('x', 2_000_000));
            AssertEvaluationFails(declarations, longText, "$s == $s", 3, "too much work");
            AssertEvaluationFails(declarations, longText, "label($s)", 0, "too much work");
        }

        [TestMethod]
        public void TermFormula_Evaluate_FunctionsCallingFunctionsShareTheDepthLimit ()
        {
            var sources = new List<TermFunctionSource> { new TermFunctionSource("g0", ["x"], "$x") };
            for (int index = 1; index <= 80; ++index)
            {
                sources.Add(new TermFunctionSource($"g{index}", ["x"], $"g{index - 1}($x)"));
            }

            TermFunctions functions = MustParseFunctions(sources.ToArray());
            TermDeclarations declarations = new TermDeclarations(functions).Int("a");
            Assert.IsTrue(TermExpression.ParseFormula("g40($a)", declarations));
            AssertRefused(TermExpression.ParseFormula("g80($a)", declarations), 0, "more than 64 deep");
        }

        [TestMethod]
        public void TermFormula_HostileText_NeverThrows ()
        {
            TermFunctions functions = MustParseFunctions(new TermFunctionSource("f", ["x"], "$x"));
            TermDeclarations declarations = new TermDeclarations(functions).Int("a").IntList("d").Int("m", optional: true);
            string[] hostile =
            {
                "$", "$(", "$()", "\"", "\"$(", "((((", ")", "1/", "max(", "max(,)", "$a ? : 1", "? 1 : 2", "\\", "\\$", "$a =", "= 1",
                "$a ==", "!", "-", "f()", "f(1, 2)", "has()", "has(1)", "has($nope)", "$m", "count()", "highest($d)", "join(1)",
                "round(1, 2, 3)", "fixed(1)", "$a;;;$a", "\n\n", ";", "1.", ".5", "1..2", "$a $a", "\"a\" + 1", "true + 1", "$d[0]",
                "$a.b", "\x00e9", "\U00010348", "$\x00e9", "$( $a", "$a)", "\"$(\"\")\"", "$(1)$(", "$d >= 8 ? 1 : 0",
                "has($m) || $m", "-$d", "!$d", "!$a", "-\"a\"", "label()", "number(\"a\")",
            };

            foreach (string text in hostile)
            {
                TermExpression.ParseFormula(text, declarations);
                TermExpression.ParseFormula(text, declarations, eTermKind.Int);
                TermExpression.ParseTemplate(text, declarations);
                TermExpression.TokenizeFormula(text);
                TermExpression.TokenizeTemplate(text);
                TermExpression.ParseFunctions([new TermFunctionSource("h", ["x"], text)]);
            }
        }

        // ===========[ Host mistakes throw ]==================================

        [TestMethod]
        public void TermValues_HostMistakes_Throw ()
        {
            TermDeclarations declarations = new TermDeclarations().Int("score").Float("chance").IntList("scores");
            TermValues values = declarations.CreateValues();

            Assert.ThrowsException<ArgumentException>(() => values.Set("nope", 1));
            Assert.ThrowsException<ArgumentException>(() => values.Set("score", "seven"));
            Assert.ThrowsException<ArgumentException>(() => values.Set("score", 7.5));
            Assert.ThrowsException<ArgumentException>(() => values.Set("scores", 1));
            Assert.ThrowsException<ArgumentException>(() => values.Set("score", new[] { 1, 2 }));
            Assert.ThrowsException<InvalidOperationException>(() => declarations.Int("later"));
            Assert.ThrowsException<ArgumentException>(() => new TermDeclarations().Int("two words"));
            Assert.ThrowsException<ArgumentException>(() => new TermDeclarations().Int("same").Int("same"));

            // A whole number is fine where a number is declared
            values.Set("chance", 1);
            Assert.AreEqual(1.0, values.TryGetValue("chance", out TermValue chance) ? chance.AsFloat : -1);
        }

        [TestMethod]
        public void TermFormula_DeclarationsWithResult_RefusesAHostNameAndIsMadeOnce ()
        {
            TermDeclarations declarations = new TermDeclarations().IntList("scores");
            TermFormula formula = MustParse("$high = count($scores >= 5)\n$high", declarations, eTermKind.Int);
            Assert.ThrowsException<ArgumentException>(() => formula.DeclarationsWithResult("scores"));
            Assert.AreSame(formula.DeclarationsWithResult("total"), formula.DeclarationsWithResult("total"));
            Assert.ThrowsException<InvalidOperationException>(() => MustParse("1 / count($scores)", declarations).Evaluate(declarations.CreateValues().Set("scores", Array.Empty<int>())).ValuesWithResult("total"));
        }

        // ===========[ Helpers ]==============================================

        private static IEnumerable<TermValue> Medals (params int[] places)
        {
            return places.Select(place => TermValue.Int(place, place switch { 1 => "Bronze", 2 => "Silver", _ => "Gold" }));
        }

        private static TermFormula MustParse (string text, TermDeclarations declarations, eTermKind? wants = null, TermParseOptions options = default)
        {
            TermParseResult<TermFormula> parsed = TermExpression.ParseFormula(text, declarations, wants, options);
            Assert.IsTrue(parsed, $"'{Shorten(text)}' didn't parse: {parsed.GetErrorReport()}");
            return parsed.Value;
        }

        private static TermFormula MustParseTemplate (string text, TermDeclarations declarations)
        {
            TermParseResult<TermFormula> parsed = TermExpression.ParseTemplate(text, declarations);
            Assert.IsTrue(parsed, $"'{Shorten(text)}' didn't parse: {parsed.GetErrorReport()}");
            return parsed.Value;
        }

        private static TermFunctions MustParseFunctions (params TermFunctionSource[] sources)
        {
            return MustParseFunctions(sources, Array.Empty<TermHostFunction>());
        }

        private static TermFunctions MustParseFunctions (TermFunctionSource[] sources, params TermHostFunction[] hostFunctions)
        {
            TermParseResult<TermFunctions> parsed = TermExpression.ParseFunctions(sources, hostFunctions);
            Assert.IsTrue(parsed, parsed.GetErrorReport());
            return parsed.Value;
        }

        private static TermValue MustEvaluate (TermFormula formula, TermValues values, TermEvaluateOptions options = default)
        {
            TermEvaluation evaluated = formula.Evaluate(values, options);
            Assert.IsTrue(evaluated, $"'{Shorten(formula.Text)}' didn't evaluate: {evaluated.GetErrorReport()}");
            return evaluated.Value;
        }

        private static void AssertRefused<T> (TermParseResult<T> parsed, int errorIndex, params string[] expectedParts)
        {
            Assert.IsFalse(parsed, "Expected a refusal");
            foreach (string part in expectedParts)
            {
                StringAssert.Contains(parsed.GetErrorReport(), part);
            }

            Assert.AreEqual(errorIndex, parsed.ErrorIndex, parsed.GetErrorReport());
        }

        private static void AssertEvaluationFails (TermDeclarations declarations, TermValues values, string text, int errorIndex, string expected)
        {
            AssertEvaluationFails(MustParse(text, declarations), values, errorIndex, expected);
        }

        private static void AssertEvaluationFails (TermFormula formula, TermValues values, int errorIndex, string expected)
        {
            TermEvaluation evaluated = formula.Evaluate(values);
            Assert.IsFalse(evaluated, $"Expected '{Shorten(formula.Text)}' to fail");
            StringAssert.Contains(evaluated.GetErrorReport(), expected);
            Assert.AreEqual(errorIndex, evaluated.ErrorIndex, evaluated.GetErrorReport());
        }

        private static string Shorten (string text) => text.Length > 80 ? text.Substring(0, 77) + "..." : text;
    }
}
