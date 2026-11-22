#pragma warning disable CS8655

using Microsoft.AspNetCore.Http;

namespace JASIntWeb.Helpers;

public static class ResultHttpExtensions
{
    extension <T>(Result<T> result)
    {
        public IResult ToHttpResult(Func<T, IResult>? onSuccess = null) =>
            result switch
            {
                T val => onSuccess != null ? onSuccess(val) : Results.Ok(val),
                NotFoundError nf => Results.NotFound(new { message = nf.Message }),
                ValidationError ve => Results.BadRequest(new { message = ve.Message, errors = ve.Errors }),
                ConflictError ce => Results.Conflict(new { message = ce.Message }),
                UnauthorizedError => Results.Unauthorized(),
                ForbiddenError => Results.Forbid(),
                UnexpectedError ue => Results.Problem(detail: ue.Message, statusCode: 500),
                Error err => Results.Problem(detail: err.Message, statusCode: err.Code > 0 ? err.Code : 400)
            };
    }

    extension <T>(Task<Result<T>> resultTask)
    {
        public async Task<IResult> ToHttpResultAsync(Func<T, IResult>? onSuccess = null)
        {
            var result = await resultTask;
            return result.ToHttpResult(onSuccess);
        }
    }
}
