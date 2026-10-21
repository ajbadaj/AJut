namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TermExpressionTests
    {
        private static readonly TermMatchOptions kMatchCase = new TermMatchOptions { MatchCase = true };
        private static readonly TermMatchOptions kWholeWords = new TermMatchOptions { WholeWords = true };
        private static readonly TermParseOptions kPreserveWhitespace = new TermParseOptions { InnerWhitespace = eTermWhitespace.Preserve };

        // ===========[ Evaluate ]=============================================

        [DataTestMethod]
        [DataRow("code", new[] { "code" }, true)]
        [DataRow("code", new[] { "ui" }, false)]
        [DataRow("code", new string[] { }, false)]
        [DataRow("code & frontend & ui", new[] { "code", "frontend", "ui" }, true)]
        [DataRow("code & frontend & ui", new[] { "code", "ui" }, false)]
        [DataRow("code | frontend | ui", new[] { "ui" }, true)]
        [DataRow("code | frontend | ui", new[] { "backend" }, false)]
        [DataRow("code & (frontend | ui)", new[] { "code", "ui" }, true)]
        [DataRow("code & (frontend | ui)", new[] { "code", "backend" }, false)]
        [DataRow("code & (frontend | ui)", new[] { "ui" }, false)]
        [DataRow("a & (b | (c & !d))", new[] { "a", "c" }, true)]
        [DataRow("a & (b | (c & !d))", new[] { "a", "c", "d" }, false)]
        [DataRow("Code", new[] { "code" }, true)]
        [DataRow("!code", new string[] { }, true)]
        [DataRow("!code", new[] { "code" }, false)]
        [DataRow("code & !wpf", new[] { "code" }, true)]
        [DataRow("code & !wpf", new[] { "code", "wpf" }, false)]
        [DataRow("!(wpf | winui)", new[] { "code" }, true)]
        [DataRow("!(wpf | winui)", new[] { "winui" }, false)]
        [DataRow("!!code", new[] { "code" }, true)]
        [DataRow("code && ui", new[] { "code", "ui" }, true)]
        [DataRow("code && ui", new[] { "code" }, false)]
        [DataRow("code || ui", new[] { "ui" }, true)]
        [DataRow("Mr Toot | Tommy Boy", new[] { "Tommy Boy" }, true)]
        [DataRow("Mr Toot | Tommy Boy", new[] { "Tommy" }, false)]
        [DataRow("\"This & That\" & Others", new[] { "This & That", "Others" }, true)]
        [DataRow("\"This & That\" & Others", new[] { "This", "That", "Others" }, false)]
        [DataRow("Jill Spill | (Ghost & Pilly)", new[] { "Jill Spill" }, true)]
        [DataRow("Jill Spill | (Ghost & Pilly)", new[] { "Ghost", "Pilly" }, true)]
        [DataRow("Jill Spill | (Ghost & Pilly)", new[] { "Ghost", "Jill", "Spill" }, false)]
        public void TermExpression_Evaluate_TestsMembership (string expression, string[] terms, bool expected)
        {
            Assert.AreEqual(expected, MustParse(expression).Evaluate(terms));
        }

        [TestMethod]
        public void TermExpression_Evaluate_MatchCaseComparesExactly ()
        {
            TermExpression expression = MustParse("Code");
            Assert.IsFalse(expression.Evaluate(new[] { "code" }, kMatchCase));
            Assert.IsTrue(expression.Evaluate(new[] { "Code" }, kMatchCase));
        }

        [TestMethod]
        public void TermExpression_Evaluate_AnyCollectionWorks ()
        {
            TermExpression expression = MustParse("code & ui");
            Assert.IsTrue(expression.Evaluate(new List<string> { "code", "ui" }));
            Assert.IsFalse(expression.Evaluate(new List<string> { "code" }));

            // A queue is a collection that can't be indexed, so it gets enumerated
            Assert.IsTrue(expression.Evaluate(new Queue<string>(new[] { "UI", "Code" })));
            Assert.IsFalse(expression.Evaluate(new Queue<string>(new[] { "code" })));
        }

        [TestMethod]
        public void TermExpression_Evaluate_SetsAnswerByTheOptionsNotTheirOwnComparer ()
        {
            TermExpression expression = MustParse("Code");

            // Sets that compare the way the options ask
            Assert.IsTrue(expression.Evaluate(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "code" }));
            Assert.IsTrue(expression.Evaluate(new HashSet<string>(StringComparer.Ordinal) { "Code" }, kMatchCase));
            Assert.IsTrue(expression.Evaluate(new HashSet<string> { "Code" }, kMatchCase));
            Assert.IsFalse(expression.Evaluate(new HashSet<string> { "code" }, kMatchCase));

            // Sets that don't still get the options' answer
            Assert.IsTrue(expression.Evaluate(new HashSet<string> { "code" }));
            Assert.IsFalse(expression.Evaluate(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "code" }, kMatchCase));
        }

        [TestMethod]
        public void TermExpression_Evaluate_AsksEachTermOnceAndOnlyWhenReached ()
        {
            var asked = new List<string>();
            bool result = MustParse("a & (b | a) & c").Evaluate(term =>
            {
                asked.Add(term);
                return term == "a";
            });

            Assert.IsFalse(result);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, asked);

            asked.Clear();
            Assert.IsTrue(MustParse("a | b").Evaluate(term =>
            {
                asked.Add(term);
                return true;
            }));
            CollectionAssert.AreEqual(new[] { "a" }, asked);

            asked.Clear();
            Assert.IsFalse(MustParse("a & b").Evaluate(term =>
            {
                asked.Add(term);
                return false;
            }));
            CollectionAssert.AreEqual(new[] { "a" }, asked);
        }

        [TestMethod]
        public void TermExpression_Evaluate_CallerDecidesWhatTermsMean ()
        {
            // ':' and '>' aren't reserved, so these are ordinary terms, and HasTrait decides what each one means
            TermExpression query = MustParse("class:bard & (level>3 | name:Jill Spill)");
            CollectionAssert.AreEqual(new[] { "class:bard", "level>3", "name:Jill Spill" }, query.Terms.ToArray());

            var party = new[]
            {
                new Character("Jill Spill", "bard", 1),
                new Character("Ghost", "bard", 5),
                new Character("Pilly", "wizard", 9),
                new Character("Tommy Boy", "bard", 2),
            };

            Character[] found = party.Where(character => query.Evaluate(term => HasTrait(character, term))).ToArray();
            CollectionAssert.AreEqual(new[] { party[0], party[1] }, found);
        }

        [TestMethod]
        public void TermExpression_Evaluate_ManyTermsWorkLikeFew ()
        {
            string[] terms = Enumerable.Range(0, 300).Select(n => $"t{n}").ToArray();
            string anyOfText = String.Join(" | ", terms);
            TermExpression anyOf = MustParse(anyOfText);
            TermExpression allOf = MustParse(String.Join(" & ", terms));

            Assert.AreEqual(300, anyOf.Terms.Count);
            Assert.IsTrue(anyOf.Evaluate("t299"));
            Assert.IsFalse(anyOf.Evaluate("nothing"));
            Assert.IsTrue(allOf.Evaluate(terms));
            Assert.IsFalse(allOf.Evaluate(terms.Take(299).ToArray()));

            // Every term twice, and more of them than fit on the stack, so the evaluation remembers its answers in a rented
            //  buffer. A buffer that kept the last evaluation's answers would still say t299 is present.
            var asked = new List<string>();
            TermExpression twice = MustParse($"({anyOfText}) & ({anyOfText})");
            Assert.IsTrue(twice.Evaluate(term =>
            {
                asked.Add(term);
                return term == "t299";
            }));
            CollectionAssert.AreEqual(terms, asked);
            Assert.IsFalse(twice.Evaluate("nothing"));
            Assert.IsTrue(twice.Evaluate("t150"));
        }

        // ===========[ IsMatch ]==============================================

        [DataTestMethod]
        [DataRow("pill", "Pilly is here", true)]
        [DataRow("pill", "Polly is here", false)]
        [DataRow("ghost & !pilly", "a ghost story", true)]
        [DataRow("ghost & !pilly", "Ghost and Pilly", false)]
        [DataRow("Jill Spill | (Ghost & Pilly)", "That was jill spill's fault", true)]
        [DataRow("Jill Spill | (Ghost & Pilly)", "Jill and Spill", false)]
        [DataRow("Jill Spill | (Ghost & Pilly)", "Pilly saw a ghost", true)]
        [DataRow("(one | two) & (three | four)", "two and four", true)]
        [DataRow("(one | two) & (three | four)", "one and two", false)]
        [DataRow("\"This & That\"", "all of this & that", true)]
        [DataRow("x", "", false)]
        public void TermExpression_IsMatch_FindsTermsAnywhere (string expression, string text, bool expected)
        {
            Assert.AreEqual(expected, MustParse(expression).IsMatch(text));
        }

        [DataTestMethod]
        [DataRow("pill", "Pilly", false)]
        [DataRow("pill", "a pill.", true)]
        [DataRow("pill", "pills, then a pill", true)]
        [DataRow("pill", "pill", true)]
        [DataRow("pill", "pill_box", false)]
        [DataRow("pill", "pill2", false)]
        [DataRow("pill", "pill-box", true)]
        [DataRow("cafe", "cafe\u0301", false)]
        [DataRow("pill", "pill\U0001D400", false)]
        [DataRow("pill", "\U0001D400pill", false)]
        [DataRow("pill box", "a pill box!", true)]
        public void TermExpression_IsMatch_WholeWordsOnlyCountsWholeWords (string expression, string text, bool expected)
        {
            Assert.AreEqual(expected, MustParse(expression).IsMatch(text, kWholeWords));
        }

        [TestMethod]
        public void TermExpression_IsMatch_MatchCaseComparesExactly ()
        {
            TermExpression expression = MustParse("Ghost");
            Assert.IsFalse(expression.IsMatch("a ghost", kMatchCase));
            Assert.IsTrue(expression.IsMatch("a Ghost", kMatchCase));
            Assert.IsFalse(expression.IsMatch("a Ghostly figure", new TermMatchOptions { MatchCase = true, WholeWords = true }));
        }

        [TestMethod]
        public void TermExpression_IsMatch_SearchesSeveralTextsAsOne ()
        {
            TermExpression expression = MustParse("jill & ghost");
            Assert.IsTrue(expression.IsMatch("Jill", "saw a ghost"));
            Assert.IsFalse(expression.IsMatch("Jill"));
            Assert.IsFalse(expression.IsMatch((string)null));
            Assert.IsTrue(expression.IsMatch(new List<string> { "Jill", null, "ghost" }));
        }

        [TestMethod]
        public void TermExpression_IsMatch_FiltersRecordsByTheirFields ()
        {
            var records = new[]
            {
                new SearchRecord("Jill Spill", "Left the gate open"),
                new SearchRecord("Ghost", "Pilly saw it first"),
                new SearchRecord("Ghost", "Nobody saw anything"),
                new SearchRecord("Pilly", "Wrote it down"),
            };

            TermExpression expression = MustParse("Jill Spill | (Ghost & Pilly)");
            SearchRecord[] found = records.Where(r => expression.IsMatch(r.Name, r.Body)).ToArray();

            CollectionAssert.AreEqual(new[] { records[0], records[1] }, found);
        }

        // ===========[ Parse ]================================================

        [DataTestMethod]
        [DataRow("", 0, "The expression is empty.")]
        [DataRow("   ", 0, "The expression is empty.")]
        [DataRow("code &", 5, "'&' at position 6 has nothing after it.")]
        [DataRow("code && || ui", 5, "'&&' at position 6 has nothing after it.")]
        [DataRow("| code", 0, "'|' at position 1 has nothing before it.")]
        [DataRow("(& code)", 1, "'&' at position 2 has nothing before it.")]
        [DataRow("code (ui)", 5, "Missing '&' or '|' before '(' at position 6.")]
        [DataRow("code \"Jill Spill\"", 5, "Missing '&' or '|' before '\"Jill Spill\"' at position 6.")]
        [DataRow("code !ui", 5, "Missing '&' or '|' before '!' at position 6.")]
        [DataRow("(code) ui", 7, "Missing '&' or '|' before 'ui' at position 8.")]
        [DataRow("\"a\" b c", 4, "Missing '&' or '|' before 'b c' at position 5.")]
        [DataRow("()", 0, "The parentheses at position 1 are empty.")]
        [DataRow("code & ()", 7, "The parentheses at position 8 are empty.")]
        [DataRow("(code", 0, "The '(' at position 1 is never closed.")]
        [DataRow("(code & (ui)", 0, "The '(' at position 1 is never closed.")]
        [DataRow("code)", 4, "The ')' at position 5 has no '(' to close.")]
        [DataRow(")", 0, "The ')' at position 1 has no '(' to close.")]
        [DataRow("(code))", 6, "The ')' at position 7 has no '(' to close.")]
        [DataRow("code & --", 7, "'--' at position 8 is not a term: a term needs at least one letter or digit, or quotes around it.")]
        [DataRow("-", 0, "'-' at position 1 is not a term: a term needs at least one letter or digit, or quotes around it.")]
        [DataRow("_", 0, "'_' at position 1 is not a term: a term needs at least one letter or digit, or quotes around it.")]
        [DataRow("-_-", 0, "'-_-' at position 1 is not a term: a term needs at least one letter or digit, or quotes around it.")]
        [DataRow("- -", 0, "'- -' at position 1 is not a term: a term needs at least one letter or digit, or quotes around it.")]
        [DataRow("!", 0, "'!' at position 1 has nothing after it.")]
        [DataRow("code & !", 7, "'!' at position 8 has nothing after it.")]
        [DataRow("!)", 0, "'!' at position 1 has nothing after it.")]
        [DataRow("\"Jill", 0, "The '\"' at position 1 is never closed.")]
        [DataRow("\"Jill\\\"", 0, "The '\"' at position 1 is never closed.")]
        [DataRow("\"\"", 0, "The quotes at position 1 are empty.")]
        [DataRow("code &&& ui", 5, "'&&&' at position 6 is not an operator: use '&'.")]
        [DataRow("code ||| ui", 5, "'|||' at position 6 is not an operator: use '|'.")]
        [DataRow("code & frontend | ui", 16, "'&' and '|' meet without parentheses at position 17. Write it as (code & frontend) | ui or code & (frontend | ui).")]
        [DataRow("code && frontend || ui", 17, "'&&' and '||' meet without parentheses at position 18. Write it as (code & frontend) | ui or code & (frontend | ui).")]
        [DataRow("a | b & c", 6, "'|' and '&' meet without parentheses at position 7. Write it as a | (b & c) or (a | b) & c.")]
        [DataRow("a & b | c && d", 6, "'&' and '|' meet without parentheses at position 7. Write it as (a & b) | (c & d) or a & (b | c) & d.")]
        [DataRow("x & (a & b | c)", 11, "'&' and '|' meet without parentheses at position 12. Write the group at position 5 as ((a & b) | c) or (a & (b | c)).")]
        [DataRow("!a & b | c", 7, "'&' and '|' meet without parentheses at position 8. Write it as (!a & b) | c or !a & (b | c).")]
        [DataRow("a & b | (c", 8, "The '(' at position 9 is never closed.")]
        public void TermExpression_Parse_RefusesWithPositionAndReason (string expression, int errorIndex, string error)
        {
            TermExpression.ParseResult parsed = TermExpression.Parse(expression);
            Assert.IsTrue(parsed.HasErrors, $"'{expression}' parsed as '{parsed.Value}'");
            Assert.AreEqual(errorIndex, parsed.ErrorIndex);
            Assert.AreEqual(error, parsed.Errors.Single());
            Assert.IsNull(parsed.Value);
        }

        [TestMethod]
        public void TermExpression_Parse_NullIsEmpty ()
        {
            TermExpression.ParseResult parsed = TermExpression.Parse(null);
            Assert.AreEqual(0, parsed.ErrorIndex);
            Assert.AreEqual("The expression is empty.", parsed.GetErrorReport());
        }

        [TestMethod]
        public void TermExpression_Parse_ResultWorksLikeAnyResult ()
        {
            TermExpression.ParseResult parsed = TermExpression.Parse("code");
            Assert.IsTrue(parsed);
            Assert.AreEqual(-1, parsed.ErrorIndex);

            TermExpression expression = parsed;
            Assert.AreSame(parsed.Value, expression);

            Assert.IsFalse(TermExpression.Parse("("));
        }

        [DataTestMethod]
        [DataRow("-code")]
        [DataRow("1st")]
        [DataRow("snake_case")]
        [DataRow("x-1")]
        [DataRow("café")]
        [DataRow("C:\\path")]
        [DataRow("C:\\path to\\a file.txt")]
        [DataRow("a.b")]
        [DataRow("#tag")]
        [DataRow("@smoke")]
        [DataRow("rock'n'roll")]
        [DataRow("Mr Toot")]
        public void TermExpression_Parse_BareTermsTakeAnythingUnreserved (string term)
        {
            TermExpression expression = MustParse(term);
            Assert.AreEqual(term, expression.Terms.Single());
            Assert.AreEqual(term, expression.ToString());
        }

        [DataTestMethod]
        [DataRow("code & (frontend | ui | code)", "code, frontend, ui")]
        [DataRow("Code & code", "Code, code")]
        [DataRow("\"code\" | code", "code")]
        [DataRow("!wpf & !(winui | wpf)", "wpf, winui")]
        [DataRow("Mr Toot | Tommy Boy", "Mr Toot, Tommy Boy")]
        [DataRow("Mr  Toot | Mr Toot | Mr\tToot", "Mr Toot")]
        public void TermExpression_Parse_ListsEachTermOnceInOrder (string expression, string expectedTerms)
        {
            Assert.AreEqual(expectedTerms, String.Join(", ", MustParse(expression).Terms));
        }

        [TestMethod]
        public void TermExpression_Parse_InnerWhitespaceCollapsesUnlessPreserved ()
        {
            const string kText = "Mr  Toot | Mr Toot | Mr\tToot";

            TermExpression collapsed = MustParse(kText);
            Assert.AreEqual("Mr Toot", collapsed.Terms.Single());
            Assert.AreEqual("Mr Toot | Mr Toot | Mr Toot", collapsed.ToString());

            TermExpression preserved = MustParse(kText, kPreserveWhitespace);
            CollectionAssert.AreEqual(new[] { "Mr  Toot", "Mr Toot", "Mr\tToot" }, preserved.Terms.ToArray());

            // A term whose whitespace a collapsing parse would change is quoted, so the canonical text reads back the same
            //  either way
            string rendered = preserved.ToString();
            Assert.AreEqual("\"Mr  Toot\" | Mr Toot | \"Mr\tToot\"", rendered);
            CollectionAssert.AreEqual(preserved.Terms.ToArray(), MustParse(rendered).Terms.ToArray());
            CollectionAssert.AreEqual(preserved.Terms.ToArray(), MustParse(rendered, kPreserveWhitespace).Terms.ToArray());

            // The ends of a bare term are trimmed either way, and a quoted term keeps everything
            Assert.AreEqual("Mr  Toot", MustParse("   Mr  Toot   ", kPreserveWhitespace).Terms.Single());
            Assert.AreEqual(" padded ", MustParse("\" padded \"").Terms.Single());
        }

        [TestMethod]
        public void TermExpression_Parse_NestsUpToTheLimit ()
        {
            int limit = TermParseOptions.kDefaultMaxDepth;
            string atTheLimit = new string('(', limit) + "a" + new string(')', limit);
            Assert.AreEqual("a", MustParse(atTheLimit).ToString());

            string pastTheLimit = "(" + atTheLimit + ")";
            TermExpression.ParseResult parsed = TermExpression.Parse(pastTheLimit);
            Assert.AreEqual(limit, parsed.ErrorIndex);
            Assert.AreEqual($"Parentheses nest more than {limit} deep at position {limit + 1}.", parsed.GetErrorReport());
        }

        [TestMethod]
        public void TermExpression_Parse_MaxDepthCanBeSet ()
        {
            var shallow = new TermParseOptions { MaxDepth = 2 };
            Assert.IsTrue(TermExpression.Parse("((a))", shallow));

            TermExpression.ParseResult parsed = TermExpression.Parse("(((a)))", shallow);
            Assert.AreEqual(2, parsed.ErrorIndex);
            Assert.AreEqual("Parentheses nest more than 2 deep at position 3.", parsed.GetErrorReport());
        }

        [TestMethod]
        public void TermExpression_Parse_RefusesOptionsOutOfRange ()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => TermExpression.Parse("a", new TermParseOptions { MaxDepth = -1 }));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => TermExpression.Parse("a", new TermParseOptions { InnerWhitespace = (eTermWhitespace)7 })
            );
        }

        // ===========[ Rendering ]============================================

        [DataTestMethod]
        [DataRow("code&&(frontend||ui)", "code & (frontend | ui)")]
        [DataRow("code&(frontend|ui)", "code & (frontend | ui)")]
        [DataRow("  code   &   ui  ", "code & ui")]
        [DataRow("((code))", "code")]
        [DataRow("((code)) & (ui & a)", "code & ui & a")]
        [DataRow("(a | b) | (c | d)", "a | b | c | d")]
        [DataRow("a & (b | (c & d))", "a & (b | (c & d))")]
        [DataRow("a && b && c", "a & b & c")]
        [DataRow("a || b", "a | b")]
        [DataRow("!(a)", "!a")]
        [DataRow("! a", "!a")]
        [DataRow("!!a", "a")]
        [DataRow("!(!a)", "a")]
        [DataRow("!(a & b) & c", "!(a & b) & c")]
        [DataRow("!(a & (b & c))", "!(a & b & c)")]
        [DataRow("a & !(b & c)", "a & !(b & c)")]
        [DataRow("\"a\" & a", "a & a")]
        [DataRow("\"code\"", "code")]
        [DataRow("\"Jill Spill\" | (Ghost & Pilly)", "Jill Spill | (Ghost & Pilly)")]
        [DataRow("Mr   Toot|Tommy Boy", "Mr Toot | Tommy Boy")]
        [DataRow("\"This & That\" & Others", "\"This & That\" & Others")]
        [DataRow("\"say \\\"hi\\\"\"", "\"say \\\"hi\\\"\"")]
        [DataRow("\"back\\\\slash here\"", "back\\slash here")]
        [DataRow("\"C:\\path\"", "C:\\path")]
        [DataRow("\"-\"", "\"-\"")]
        [DataRow("\" padded \"", "\" padded \"")]
        [DataRow("café & naïve", "café & naïve")]
        public void TermExpression_ToString_RendersTheCanonicalForm (string expression, string expected)
        {
            string rendered = MustParse(expression).ToString();
            Assert.AreEqual(expected, rendered);

            // The canonical form parses back to itself
            Assert.AreEqual(rendered, MustParse(rendered).ToString());
        }

        [TestMethod]
        public void TermExpression_ToString_UnescapesQuotedTerms ()
        {
            Assert.AreEqual("say \"hi\"", MustParse("\"say \\\"hi\\\"\"").Terms.Single());
            Assert.AreEqual("back\\slash", MustParse("\"back\\\\slash\"").Terms.Single());
            Assert.AreEqual("C:\\path", MustParse("\"C:\\path\"").Terms.Single());
        }

        // ===========[ MapTerms ]=============================================

        [TestMethod]
        public void TermExpression_MapTerms_RoundTripsThroughIds ()
        {
            string[] names = { "code", "frontend", "user interface", "legacy" };
            Dictionary<string, string> idByName = names.ToDictionary(
                name => name,
                name => Guid.NewGuid().ToString(),
                StringComparer.OrdinalIgnoreCase
            );
            Dictionary<string, string> nameById = idByName.ToDictionary(entry => entry.Value, entry => entry.Key);

            TermExpression byName = MustParse("code & (frontend | user interface) & !legacy");
            TermExpression byId = byName.MapTerms(name => idByName[name]);

            CollectionAssert.AreEqual(names.Select(name => idByName[name]).ToArray(), byId.Terms.ToArray());
            string code = idByName["code"];
            string frontend = idByName["frontend"];
            string userInterface = idByName["user interface"];
            string legacy = idByName["legacy"];
            Assert.AreEqual($"{code} & ({frontend} | {userInterface}) & !{legacy}", byId.ToString());
            Assert.AreEqual(byName.ToString(), byId.MapTerms(id => nameById[id]).ToString());

            // Ids render without quotes, so they parse back the same way too
            Assert.AreEqual(byId.ToString(), MustParse(byId.ToString()).ToString());
        }

        [TestMethod]
        public void TermExpression_MapTerms_FoldsTermsThatMapTogether ()
        {
            TermExpression folded = MustParse("Code | (code & !CODE)").MapTerms(term => term.ToLowerInvariant());
            Assert.AreEqual("code", folded.Terms.Single());
            Assert.AreEqual("code | (code & !code)", folded.ToString());
            Assert.IsTrue(folded.Evaluate("code"));
            Assert.IsFalse(folded.Evaluate("ui"));
        }

        [TestMethod]
        public void TermExpression_MapTerms_MapsEachTermOnce ()
        {
            var mapped = new List<string>();
            MustParse("a & (a | b) & !b").MapTerms(term =>
            {
                mapped.Add(term);
                return term.ToUpperInvariant();
            });

            CollectionAssert.AreEqual(new[] { "a", "b" }, mapped);
        }

        [TestMethod]
        public void TermExpression_MapTerms_QuotesTermsThatNeedIt ()
        {
            var replacements = new Dictionary<string, string> { ["a"] = "this & that", ["b"] = "-", ["c"] = "two words" };
            TermExpression mapped = MustParse("a & b & c").MapTerms(term => replacements[term]);

            Assert.AreEqual("\"this & that\" & \"-\" & two words", mapped.ToString());
            CollectionAssert.AreEqual(mapped.Terms.ToArray(), MustParse(mapped.ToString()).Terms.ToArray());
        }

        [TestMethod]
        public void TermExpression_MapTerms_RefusesEmptyTerms ()
        {
            TermExpression expression = MustParse("a & b");
            Assert.ThrowsException<ArgumentException>(() => expression.MapTerms(term => null));
            Assert.ThrowsException<ArgumentException>(() => expression.MapTerms(term => String.Empty));
        }

        // ===========[ Helpers ]==============================================

        private static TermExpression MustParse (string text, TermParseOptions options = default)
        {
            TermExpression.ParseResult parsed = TermExpression.Parse(text, options);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            return parsed.Value;
        }

        // Terms here are "class:<class>", "name:<name>", or "level><number>"
        private static bool HasTrait (Character character, string term)
        {
            const string kLevelAbove = "level>";
            if (term.StartsWith(kLevelAbove, StringComparison.Ordinal))
            {
                return character.Level > Int32.Parse(term.Substring(kLevelAbove.Length));
            }

            int colon = term.IndexOf(':');
            if (colon < 0)
            {
                return false;
            }

            string value = term.Substring(colon + 1);
            return term.Substring(0, colon) switch
            {
                "name" => String.Equals(character.Name, value, StringComparison.OrdinalIgnoreCase),
                "class" => String.Equals(character.Class, value, StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }

        private record SearchRecord (string Name, string Body);

        private record Character (string Name, string Class, int Level);
    }
}
