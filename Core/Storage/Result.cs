namespace AJut.Storage
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class Result
    {
        private List<string> m_errors = new List<string>();

        public Result()
        {
            this.Errors = m_errors.AsReadOnly();
        }

        public Result (Result takeErrorsFrom) : this()
        {
            m_errors.AddRange(takeErrorsFrom.Errors);
        }

        public ReadOnlyCollection<string> Errors { get; }
        public bool HasErrors => this.Errors.Count > 0;

        public void AddError (string error)
        {
            m_errors.Add(error);
        }

        public Result AddErrorsFrom (Result other)
        {
            m_errors.AddRange(other.Errors);
            return this;
        }

        public static Result Error (string error)
        {
            var result = new Result();
            result.AddError(error);
            return result;
        }

        /// <summary>
        /// Generates a simple user-displayable text report of the errors list
        /// </summary>
        public string GetErrorReport (string separator = "\n")
        {
            return this.HasErrors ? string.Join(separator, this.Errors) : null;
        }

        public static implicit operator bool (Result result) => !result.HasErrors;

    }

    public class Result<T> : Result
    {
        public T Value { get; set; }

        public Result (T value = default)
        {
            this.Value = value;
        }

        public Result (Result takeErrorsFrom, T value = default) : base(takeErrorsFrom)
        {
            this.Value = value;
        }

        public static new Result<T> Error (string error)
        {
            var result = new Result<T>();
            result.AddError(error);
            return result;
        }
    }
}
