using System;

namespace EasyIpc
{
    public class Result
    {
        public static Result<T> Empty<T>()
        {
            return new Result<T>(true, default);
        }

        public static Result Fail(string error)
        {
            return new Result(false, error);
        }
        public static Result Fail(Exception ex)
        {
            return new Result(false, null, ex);
        }
        public static Result Fail(Exception ex, string error)
        {
            return new Result(false, error, ex);
        }

        public static Result<T> Fail<T>(string error)
        {
            return new Result<T>(false, default, error);
        }

        public static Result<T> Fail<T>(Exception ex)
        {
            return new Result<T>(false, default, exception: ex);
        }

        public static Result<T> Fail<T>(Exception ex, string message)
        {
            return new Result<T>(false, default, message, ex);
        }

        public static Result Ok()
        {
            return new Result(true);
        }

        public static Result<T> Ok<T>(T value)
        {
            return new Result<T>(true, value, null);
        }


        public Result(bool isSuccess, string error = null, Exception exception = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Exception = exception;
        }

        public bool IsSuccess { get; private set; }

        public string Error { get; private set; }

        public Exception Exception { get; private set; }


    }

    public class Result<T>
    {
        public Result(bool isSuccess, T value, string error = null, Exception exception = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Value = value;
            Exception = exception;
        }


        public bool IsSuccess { get; private set; }

        public string Error { get; private set; }

        public Exception Exception { get; private set; }

        public T Value { get; private set; }
    }
}
