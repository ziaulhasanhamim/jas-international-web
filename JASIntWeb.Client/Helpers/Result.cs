#pragma warning disable CS8655

using System.Diagnostics.CodeAnalysis;

namespace JASIntWeb.Helpers;

public class Error
{
    public string Message { get; init; } = "";
    public int Code { get; init; } = 400;
    public Dictionary<string, string[]>? ValidationErrors { get; init; }

    public Error() { }

    public Error(string message, int code = 400, Dictionary<string, string[]>? validationErrors = null)
    {
        Message = message;
        Code = code;
        ValidationErrors = validationErrors;
    }

    public static Error Failure(string message, int code = 400) => new(message, code);
    public static NotFoundError NotFound(string message = "Resource not found.") => new(message);
    public static ValidationError Validation(string message, Dictionary<string, string[]>? errors = null) => new(message, errors);
    public static ConflictError Conflict(string message) => new(message);
    public static UnauthorizedError Unauthorized(string message = "Unauthorized.") => new(message);
    public static ForbiddenError Forbidden(string message = "Forbidden.") => new(message);
    public static UnexpectedError Unexpected(string message, Exception? inner = null) => new(message, inner);
}

public sealed class NotFoundError(string message = "Resource not found.") : Error(message, 404);

public sealed class ValidationError(string message, Dictionary<string, string[]>? errors = null) : Error(message, 400, errors)
{
    public Dictionary<string, string[]> Errors => ValidationErrors ?? [];
}

public sealed class ConflictError(string message) : Error(message, 409);

public sealed class UnauthorizedError(string message = "Unauthorized.") : Error(message, 401);

public sealed class ForbiddenError(string message = "Forbidden.") : Error(message, 403);

public sealed class UnexpectedError(string message, Exception? inner = null) : Error(message, 500)
{
    public Exception? InnerException { get; } = inner;
}

public readonly record struct Success(string Message = "")
{
    public static readonly Success Ok = new("");
}

public union Result(Success, Error)
{
    public bool IsSuccess => Value is Success;
    public bool IsFailure => Value is Error;

    public bool TryGetError([NotNullWhen(true)] out Error? error)
    {
        if (Value is Error err)
        {
            error = err;
            return true;
        }

        error = null;
        return false;
    }

    public R Match<R>(Func<R> onSuccess, Func<Error, R> onError) =>
        this switch
        {
            JASIntWeb.Helpers.Success => onSuccess(),
            Error err => onError(err)
        };

    public Result Tap(Action onSuccess)
    {
        if (Value is Success) onSuccess();
        return this;
    }

    public Result TapError(Action<Error> onError)
    {
        if (Value is Error err) onError(err);
        return this;
    }

    public static Success Success => Success.Ok;
    public static Result Failure(Error error) => error;
    public static Result Failure(string message, int code = 400) => new Error(message, code);
}

public union Result<T>(T, Error)
{
    public bool IsSuccess => Value is T;
    public bool IsFailure => Value is Error;

    public T? ValueOrDefault(T? fallback = default) => Value is T val ? val : fallback;

    public bool TryGet([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out Error? error)
    {
        if (Value is T val)
        {
            value = val;
            error = null;
            return true;
        }

        value = default;
        error = Value as Error ?? new Error("Unknown error state.");
        return false;
    }

    public Result<U> Map<U>(Func<T, U> mapper) =>
        this switch
        {
            T val => mapper(val),
            Error err => err
        };

    public Result<U> Bind<U>(Func<T, Result<U>> binder) =>
        this switch
        {
            T val => binder(val),
            Error err => err
        };

    public Result Bind(Func<T, Result> binder) =>
        this switch
        {
            T val => binder(val),
            Error err => err
        };

    public R Match<R>(Func<T, R> onSuccess, Func<Error, R> onError) =>
        this switch
        {
            T val => onSuccess(val),
            Error err => onError(err)
        };

    public Result<T> Tap(Action<T> onSuccess)
    {
        if (Value is T val) onSuccess(val);
        return this;
    }

    public Result<T> TapError(Action<Error> onError)
    {
        if (Value is Error err) onError(err);
        return this;
    }

    public Result<T> Ensure(Func<T, bool> predicate, Error error)
    {
        if (Value is T val && !predicate(val))
        {
            return error;
        }
        return this;
    }

    public static Result<T> Success(T value) => value;
    public static Result<T> Failure(Error error) => error;
    public static Result<T> Failure(string message, int code = 400) => new Error(message, code);
}

public static class ResultExtensions
{
    extension (Result source)
    {
        public Result Bind(Func<Result> binder) =>
            source switch
            {
                Success => binder(),
                Error err => err
            };

        public Result<T> Bind<T>(Func<Result<T>> binder) =>
            source switch
            {
                Success => binder(),
                Error err => err
            };

        public async Task<Result> BindAsync(Func<Task<Result>> binder) =>
            source switch
            {
                Success => await binder(),
                Error err => err
            };

        public async Task<Result<T>> BindAsync<T>(Func<Task<Result<T>>> binder) =>
            source switch
            {
                Success => await binder(),
                Error err => err
            };
    }

    extension (Task<Result> task)
    {
        public async Task<Result> BindAsync(Func<Result> binder)
        {
            var result = await task;
            return result.Bind(binder);
        }

        public async Task<Result> BindAsync(Func<Task<Result>> binder)
        {
            var result = await task;
            return await result.BindAsync(binder);
        }

        public async Task<Result<T>> BindAsync<T>(Func<Result<T>> binder)
        {
            var result = await task;
            return result.Bind(binder);
        }

        public async Task<Result<T>> BindAsync<T>(Func<Task<Result<T>>> binder)
        {
            var result = await task;
            return await result.BindAsync(binder);
        }

        public async Task<R> MatchAsync<R>(Func<R> onSuccess, Func<Error, R> onError)
        {
            var result = await task;
            return result.Match(onSuccess, onError);
        }

        public async Task<R> MatchAsync<R>(Func<Task<R>> onSuccess, Func<Error, Task<R>> onError)
        {
            var result = await task;
            return await (result switch
            {
                Success => onSuccess(),
                Error err => onError(err)
            });
        }
    }

    extension <T>(Result<T> source)
    {
        public Result<U> Select<U>(Func<T, U> selector) =>
            source.Map(selector);

        public Result<V> SelectMany<U, V>(Func<T, Result<U>> selector, Func<T, U, V> resultSelector) =>
            source.Bind(t => selector(t).Map(u => resultSelector(t, u)));

        public async Task<Result<U>> MapAsync<U>(Func<T, Task<U>> mapper) =>
            source switch
            {
                T val => await mapper(val),
                Error err => err
            };

        public async Task<Result<U>> BindAsync<U>(Func<T, Task<Result<U>>> binder) =>
            source switch
            {
                T val => await binder(val),
                Error err => err
            };

        public async Task<Result> BindAsync(Func<T, Task<Result>> binder) =>
            source switch
            {
                T val => await binder(val),
                Error err => err
            };
    }

    extension <T>(Task<Result<T>> task)
    {
        public async Task<Result<U>> MapAsync<U>(Func<T, U> mapper)
        {
            var result = await task;
            return result.Map(mapper);
        }

        public async Task<Result<U>> MapAsync<U>(Func<T, Task<U>> mapper)
        {
            var result = await task;
            return await result.MapAsync(mapper);
        }

        public async Task<Result<U>> BindAsync<U>(Func<T, Result<U>> binder)
        {
            var result = await task;
            return result.Bind(binder);
        }

        public async Task<Result<U>> BindAsync<U>(Func<T, Task<Result<U>>> binder)
        {
            var result = await task;
            return await result.BindAsync(binder);
        }

        public async Task<Result> BindAsync(Func<T, Result> binder)
        {
            var result = await task;
            return result switch
            {
                T val => binder(val),
                Error err => err
            };
        }

        public async Task<Result> BindAsync(Func<T, Task<Result>> binder)
        {
            var result = await task;
            return await (result switch
            {
                T val => binder(val),
                Error err => Task.FromResult<Result>(err)
            });
        }

        public async Task<R> MatchAsync<R>(Func<T, R> onSuccess, Func<Error, R> onError)
        {
            var result = await task;
            return result.Match(onSuccess, onError);
        }

        public async Task<R> MatchAsync<R>(Func<T, Task<R>> onSuccess, Func<Error, Task<R>> onError)
        {
            var result = await task;
            return await (result switch
            {
                T val => onSuccess(val),
                Error err => onError(err)
            });
        }
    }
}
