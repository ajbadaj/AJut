namespace AJut.Bench.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Text;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;

    // TermExpression against the same check written out by hand, for membership in a handful of tags and for searching a
    //  list of records. The hand written rows are the floor: whatever the expression costs on top of them is the price of
    //  reading the check from text instead of compiling it in.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class TermExpressionBench
    {
        private const string kTagExpression = "code & (frontend | ui) & !legacy";
        private const string kSearchExpression = "jill spill | (ghost & !pilly)";
        private const int kRecordCount = 1000;

        private TermExpression m_tagExpression;
        private TermExpression m_searchExpression;
        private string[] m_tags;
        private HashSet<string> m_tagSet;
        private List<SearchRecord> m_records;

        [GlobalSetup]
        public void Setup ()
        {
            m_tagExpression = TermExpression.Parse(kTagExpression).Value;
            m_searchExpression = TermExpression.Parse(kSearchExpression).Value;
            m_tags = new[] { "code", "backend", "ui", "docs" };
            m_tagSet = new HashSet<string>(m_tags, StringComparer.OrdinalIgnoreCase);
            m_records = BuildRecords();
        }

        // ===========[ Parse ]===================================
        [BenchmarkCategory("Parse"), Benchmark(Description = "Parse")]
        public TermExpression Parse () => TermExpression.Parse(kTagExpression).Value;

        // ===========[ Evaluate ]===================================
        [BenchmarkCategory("Evaluate"), Benchmark(Baseline = true, Description = "By hand")]
        public bool Evaluate_ByHand ()
        {
            return _Has("code") && (_Has("frontend") || _Has("ui")) && !_Has("legacy");

            bool _Has (string tag)
            {
                foreach (string candidate in m_tags)
                {
                    if (String.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [BenchmarkCategory("Evaluate"), Benchmark(Description = "Array")]
        public bool Evaluate_Array () => m_tagExpression.Evaluate(m_tags);

        [BenchmarkCategory("Evaluate"), Benchmark(Description = "Matching HashSet")]
        public bool Evaluate_HashSet () => m_tagExpression.Evaluate(m_tagSet);

        // ===========[ Search ]===================================
        [BenchmarkCategory("Search"), Benchmark(Baseline = true, Description = "By hand")]
        public int Search_ByHand ()
        {
            int found = 0;
            foreach (SearchRecord record in m_records)
            {
                if (_Contains(record, "jill spill") || (_Contains(record, "ghost") && !_Contains(record, "pilly")))
                {
                    ++found;
                }
            }

            return found;

            static bool _Contains (SearchRecord record, string term)
            {
                return record.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || record.Body.Contains(term, StringComparison.OrdinalIgnoreCase);
            }
        }

        [BenchmarkCategory("Search"), Benchmark(Description = "IsMatch")]
        public int Search_IsMatch ()
        {
            int found = 0;
            foreach (SearchRecord record in m_records)
            {
                if (m_searchExpression.IsMatch(record.Name, record.Body))
                {
                    ++found;
                }
            }

            return found;
        }

        [BenchmarkCategory("Search"), Benchmark(Description = "IsMatch, whole words")]
        public int Search_IsMatchWholeWords ()
        {
            var wholeWords = new TermMatchOptions { WholeWords = true };
            string[] fields = new string[2];
            int found = 0;
            foreach (SearchRecord record in m_records)
            {
                fields[0] = record.Name;
                fields[1] = record.Body;
                if (m_searchExpression.IsMatch(fields, wholeWords))
                {
                    ++found;
                }
            }

            return found;
        }

        // ===========[ Helpers ]===================================

        // Records of about 40 words each, drawn from a small vocabulary so every term turns up somewhere. Seeded, so every
        //  run searches the same records.
        private static List<SearchRecord> BuildRecords ()
        {
            const int kSeed = 17;
            const int kWordsPerBody = 40;

            string[] words =
            {
                "ghost", "pilly", "jill", "spill", "gate", "lantern", "orchard", "fog", "kettle", "ledger", "ferry", "moth",
            };

            var random = new Random(kSeed);
            var records = new List<SearchRecord>(kRecordCount);
            for (int recordIndex = 0; recordIndex < kRecordCount; ++recordIndex)
            {
                string name = $"{_Pick()} {_Pick()}";
                string body = String.Join(" ", Enumerable.Range(0, kWordsPerBody).Select(_ => _Pick()));
                records.Add(new SearchRecord(name, body));
            }

            return records;

            string _Pick () => words[random.Next(words.Length)];
        }

        private record SearchRecord (string Name, string Body);
    }
}
