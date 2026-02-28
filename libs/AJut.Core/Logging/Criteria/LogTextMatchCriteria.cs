namespace AJut
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A <see cref="LogScenarioCriteriaBase"/> that activates when a log line matches a text pattern
    /// a configurable number of times.
    /// </summary>
    public class LogTextMatchCriteria : LogScenarioCriteriaBase
    {
        private long m_currentMatchCount;
        private Regex m_compiledRegex;

        public string SearchText { get; set; }
        public eLogSearch SearchType { get; set; } = eLogSearch.Contains;
        public bool CaseSensitive { get; set; }
        public long RequiredMatchCount { get; set; } = 1;

        public override bool Evaluate (string message, bool isError)
        {
            if (IsMatch(message))
            {
                ++m_currentMatchCount;
                return m_currentMatchCount >= RequiredMatchCount;
            }
            return false;
        }

        public override void InitiateScenario () => m_currentMatchCount = 0;

        public override void Reset ()
        {
            m_currentMatchCount = 0;
            m_compiledRegex = null;
        }

        private bool IsMatch (string message)
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                return false;
            }

            var comparison = CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            return SearchType switch
            {
                eLogSearch.StartsWith => message.StartsWith(SearchText, comparison),
                eLogSearch.EndsWith   => message.EndsWith(SearchText, comparison),
                eLogSearch.Contains   => message.Contains(SearchText, comparison),
                eLogSearch.Regex      => GetOrBuildRegex().IsMatch(message),
                _ => false,
            };
        }

        private Regex GetOrBuildRegex ()
        {
            if (m_compiledRegex == null)
            {
                var options = RegexOptions.Compiled;
                if (!CaseSensitive)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                m_compiledRegex = new Regex(SearchText, options);
            }
            return m_compiledRegex;
        }
    }
}
