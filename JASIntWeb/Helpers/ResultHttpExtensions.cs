#pragma warning disable CS8655

using Microsoft.AspNetCore.Http;

namespace JASIntWeb.Helpers;

public static class ResultHttpExtensions
{
    extension (Result result)
    {
        public IResult ToHttpResult(Func<IResult>? onSuccess = null) =>
            result switch
            {
                Success => onSuccess != null ? onSuccess() : Results.Ok(),
                NotFoundError nf => Results.NotFound(new { message = nf.Message }),
                ValidationError ve => Results.BadRequest(new { message = ve.Message, errors = ve.Errors }),
                ConflictError ce => Results.Conflict(new { message = ce.Message }),
                UnauthorizedError => Results.Unauthorized(),
                ForbiddenError => Results.Forbid(),
                CatalogError cat => Results.Json(new { message = cat.Message, errors = cat.ValidationErrors }, statusCode: cat.Code),
                ShopError se => Results.BadRequest(new { message = se.Message }),
                CommerceError ce => Results.BadRequest(new { message = ce.Message }),
                ImageValidationError ie => Results.BadRequest(new { message = ie.Message }),
                UnexpectedError ue => Results.Problem(detail: ue.Message, statusCode: 500),
                Error err => Results.Problem(detail: err.Message, statusCode: err.Code > 0 ? err.Code : 400)
            };
    }

    extension (Task<Result> task)
    {
        public async Task<IResult> ToHttpResultAsync(Func<IResult>? onSuccess = null)
        {
            var result = await task;
            return result.ToHttpResult(onSuccess);
        }
    }

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
                CatalogError cat => Results.Json(new { message = cat.Message, errors = cat.ValidationErrors }, statusCode: cat.Code),
                ShopError se => Results.BadRequest(new { message = se.Message }),
                CommerceError ce => Results.BadRequest(new { message = ce.Message }),
                ImageValidationError ie => Results.BadRequest(new { message = ie.Message }),
                UnexpectedError ue => Results.Problem(detail: ue.Message, statusCode: 500),
                Error err => Results.Problem(detail: err.Message, statusCode: err.Code > 0 ? err.Code : 400)
            };
    }

    extension (Error error)
    {
        public IResult ToHttpResult() =>
            ((Result)error).ToHttpResult();
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
