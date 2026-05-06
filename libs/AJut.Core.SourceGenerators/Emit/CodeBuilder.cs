namespace AJut.Text.AJson.SourceGenerators.Emit
{
    using System;
    using System.Text;

    /// <summary>
    /// Indentation-managing string builder for the emitter. Cheap helper - keeps brace tracking
    /// and indent depth out of the emit code so each Append call reads as the line being emitted.
    /// </summary>
    internal sealed class CodeBuilder
    {
        private readonly StringBuilder m_buffer = new StringBuilder();
        private int m_indent;

        public void AppendLine (string line)
        {
            for (int i = 0; i < m_indent; ++i)
            {
                m_buffer.Append("    ");
            }
            m_buffer.AppendLine(line);
        }

        public void AppendLine ()
        {
            m_buffer.AppendLine();
        }

        public void OpenBrace ()
        {
            this.AppendLine("{");
            ++m_indent;
        }

        public void CloseBrace ()
        {
            --m_indent;
            if (m_indent < 0)
            {
                m_indent = 0;
            }
            this.AppendLine("}");
        }

        public void IndentBlock (Action body)
        {
            ++m_indent;
            body();
            --m_indent;
        }

        public override string ToString () => m_buffer.ToString();
    }
}
