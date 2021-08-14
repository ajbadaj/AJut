namespace AJut.Application
{
    using System;
    using System.Linq;

    public class Parameter
    {
        public string Name { get; private set; }
        public string Value { get; private set; }
        public string Problem { get; private set; }

        public Parameter (string name, string value, string problem = null)
        {
            this.Name = name;
            this.Value = value;
            this.Problem = problem;
        }

        public override string ToString ()
        {
            if (this.Problem != null)
            {
                return $"{this.Name} was passed as \"{this.Value}\" causing {this.Problem}";
            }

            return $"{this.Name} was passed as \"{this.Value}\"";
        }
    }

    public class BadParametersException : Exception
    {
        public BadParametersException (string location, params Parameter[] parametersAndProblems)
            : base($"Issue with parameters passed in '{location}'.\n\tParams:\n\t{string.Join("\n\t", parametersAndProblems.Select(p => p.ToString()))}")
        {
        }
    }
    public class InvalidSetupException : Exception
    {
        public InvalidSetupException (string className, params string[] setupIssues)
            : base($"Error: Class '{className}' was setup with the following issues:\n{String.Join("\n\t", setupIssues)}")
        { }
    }

    /// <summary>
    /// An exception which is formatted "You just cant {message}"
    /// </summary>
    public class YouCantDoThatException : Exception
    {
        /// <summary>
        /// An exception which is formatted "You just cant {message}"
        /// </summary>
        public YouCantDoThatException (string whatYouCantDo)
            : base($"You just can't {whatYouCantDo}")
        { }
    }

    public class ThisWillNeverHappenButICantReturnWithoutDoingSomethingException : Exception
    {
        public ThisWillNeverHappenButICantReturnWithoutDoingSomethingException() : base("This was never supposed to happen") { }
    }

    public class NonExistantEnumException<TEnum> : Exception
    {
        public NonExistantEnumException (int value) 
            : base($"Enum '{typeof(TEnum).Name}' does not have a value assigned to {value}.") { }
    }
}