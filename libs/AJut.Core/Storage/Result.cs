﻿namespace AJut.Storage
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// A state-based work result structure that allows you to track success and failure, as well as failure messaging
    /// </summary>
    public class Result
    {
        protected bool m_isInErrorState;
        private List<string> m_errors = new List<string>();

        public Result ()
        {
            this.Errors = m_errors.AsReadOnly();
        }

        public Result (Result takeErrorsFrom) : this()
        {
            m_errors.AddRange(takeErrorsFrom.Errors);
            m_isInErrorState = true;
        }

        public ReadOnlyCollection<string> Errors { get; }
        public bool HasErrors => m_isInErrorState;

        public void AddError (string error)
        {
            m_errors.Add(error);
            m_isInErrorState = true;
        }

        public Result AddErrorsFrom (Result other)
        {
            m_errors.AddRange(other.Errors);
            return this;
        }

        public static Result Success () => new Result();

        public static Result Error (string error = null)
        {
            var result = new Result
            {
                m_isInErrorState = true
            };

            if (error != null)
            {
                result.AddError(error);
            }

            return result;
        }

        public static Result ErrorJoin (params Result[] errors)
        {
            Result error = new Result();
            errors.SelectMany(e => e.Errors).ForEach(e => error.AddError(e));
            return error;
        }

        /// <summary>
        /// Generates a simple user-displayable text report of the errors list
        /// </summary>
        public string GetErrorReport (string separator = "\n")
        {
            return this.HasErrors ? string.Join(separator, this.Errors) : null;
        }

        public Result<T> ToErrorOf<T> ()
        {
            return new Result<T>(this);
        }

        public static implicit operator bool (Result result) => !result.HasErrors;

        public override string ToString ()
        {
            return this ? "<Successful Result>" : this.GetErrorReport();
        }
    }

    /// <summary>
    /// A state-based work result structure that allows you to track success and data for that success - or failure, as well as failure messaging
    /// </summary>
    public class Result<T> : Result
    {
        public T Value { get; init; }

        public Result (T value = default)
        {
            this.Value = value;
        }

        public Result (Result takeErrorsFrom, T value = default) : base(takeErrorsFrom)
        {
            this.Value = value;
        }


        public static Result<T> Success (T value) => new Result<T>(value);

        public static new Result<T> Error (string error = null)
        {
            var result = new Result<T>
            {
                m_isInErrorState = true
            };

            if (error != null)
            {
                result.AddError(error);
            }

            return result;
        }

        public static Result<T> ErrorJoin (params Result<T>[] errors)
        {
            Result<T> error = new Result<T>();
            errors.SelectMany(e => e.Errors).ForEach(e => error.AddError(e));
            return error;
        }

        public static implicit operator Result<T> (T successValue) => Result<T>.Success(successValue);
        public static implicit operator T (Result<T> successValue) => successValue.Value;
        public static implicit operator bool (Result<T> result) => !result.HasErrors;

        public override string ToString ()
        {
            return this ? $"<Successful Result: {this.Value}>" : this.GetErrorReport();
        }
    }
}
