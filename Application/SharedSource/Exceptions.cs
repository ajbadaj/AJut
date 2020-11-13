namespace AJut.Application
{
    using System;
    using System.Text;
    using AJut.Text;

    public class Parameter
	{
		public string Name { get; private set; }
		public string Value { get; private set; }
		public string Problem { get; private set; }

        public Parameter(string name, string value, string problem = null)
        {
            this.Name = name;
            this.Value = value;
            this.Problem = problem;
        }

		public override string  ToString()
		{
			if( Problem != null )
				return "\t{0} was passed as \"{1}\" causing {1}".ApplyFormatArgs(Name, Value, Problem);
			return "\t{0} was passed as \"{1}\"".ApplyFormatArgs(Name,Value);
		}
	}
	public class BadParametersException : Exception
	{
		public BadParametersException(string location, params Parameter[] parametersAndProblems)
			: base(string.Format("Bad parameters passed to '{0}'.\n\tParams: {1}", location, ConcatParams(parametersAndProblems) ) )
		{
        }

		static string ConcatParams(Parameter[] paramsAndProblems)
		{
			StringBuilder sb = new StringBuilder();
			foreach (Parameter paramProblem in paramsAndProblems)
			{
				sb.Append(paramProblem.ToString());
			}
			return sb.ToString();
		}
	}
	public class InvalidSetupException : Exception
	{
		public InvalidSetupException(string className, params string[] setupIssues)
			: base(string.Format("Error: Class '{0}' was setup with the following issues:\n{1}", className, String.Join("\n\t", setupIssues)))
		{ }
	}
	public class YouCantDoThatException : Exception
	{
		public YouCantDoThatException(string whatYouCantDo)
			: base(string.Format("You just can't {0}", whatYouCantDo))
		{ }
	}
	public class NonExistantEnumException<TEnum> : Exception
	{
		public NonExistantEnumException(int value) : base(string.Format("Enum '{0}' does not have a value that evaluates to {1}.", typeof(TEnum).Name, value)) { }
	}
}