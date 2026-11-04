namespace AJut.Bench.Benchmarks
{
    using AJut.Text;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Configs;

    // TermFormula parsing once and evaluating many times, for a formula with a table function and steps and for the template
    //  that prints its result. What matters is that evaluating stays small next to whatever produced the values, and that
    //  parsing is cheap enough to do on every keystroke in an editor.
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class TermFormulaBench
    {
        private const string kFormula = "$best = cap(max($scores), 10)\n$best + count($scores == 10)";
        private const string kTemplate =
            "$total from $scores$(count($scores == 0) > count($scores) / 2 ? \", mostly zeros\" : \"\")";

        private TermDeclarations m_declarations;
        private TermFormula m_formula;
        private TermFormula m_template;
        private TermValues m_values;
        private TermValues m_valuesWithResult;

        [GlobalSetup]
        public void Setup ()
        {
            TermFunctions functions = TermExpression.ParseFunctions(
            [
                new TermFunctionSource("cap", ["x", "high"], "min($x, $high)"),
            ]).Value;

            m_declarations = new TermDeclarations(functions).IntList("scores");
            m_formula = TermExpression.ParseFormula(kFormula, m_declarations, eTermKind.Int).Value;
            m_template = TermExpression.ParseTemplate(kTemplate, m_formula.DeclarationsWithResult("total")).Value;
            m_values = m_declarations.CreateValues().Set("scores", new[] { 9, 1, 8, 3, 10, 2, 7, 10 });
            m_valuesWithResult = m_formula.Evaluate(m_values).ValuesWithResult("total");
        }

        // ===========[ Parse ]===================================
        [BenchmarkCategory("Parse"), Benchmark(Description = "Formula")]
        public TermFormula ParseFormula () => TermExpression.ParseFormula(kFormula, m_declarations, eTermKind.Int).Value;

        // ===========[ Evaluate ]===================================
        [BenchmarkCategory("Evaluate"), Benchmark(Description = "Formula")]
        public long EvaluateFormula () => m_formula.Evaluate(m_values).Value.AsInt;

        [BenchmarkCategory("Evaluate"), Benchmark(Description = "Template")]
        public string EvaluateTemplate () => m_template.Evaluate(m_valuesWithResult).Value.AsString;
    }
}
