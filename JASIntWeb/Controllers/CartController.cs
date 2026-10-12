using JASIntWeb.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace JASIntWeb.Controllers;
// JSON transport for WebAssembly only. Razor components own all website form handlers.

[ApiController, Route("api/cart"), ServiceFilter(typeof(CartRequestFilter))]
public sealed class CartController(ShoppingService shop, IAntiforgery csrf) : ControllerBase
{
    [HttpGet("security")]
    public AntiforgeryDto Security() => new(csrf.GetAndStoreTokens(HttpContext).RequestToken!);

    [HttpGet("account")]
    public AccountDto Account() => new(User.Identity?.IsAuthenticated == true, User.Identity?.Name);

    [HttpGet("delivery-rates")]
    public DeliveryRatesDto Rates() => shop.Rates;

    [Authorize, HttpGet]
    public Task<CartDto> Get() => shop.GetCartAsync(ShoppingService.UserId(User)!.Value);

    [HttpPost("resolve")]
    public Task<CartDto> Resolve(List<CartLineRequest> items) => shop.ResolveAsync(items);

    [Authorize, HttpPost("merge")]
    public Task<CartDto> Merge(CartMergeRequest request) => shop.MergeAsync(ShoppingService.UserId(User)!.Value, request);

    [Authorize, HttpPost]
    public Task<CartDto> Add(CartLineRequest line) => shop.ChangeAsync(ShoppingService.UserId(User)!.Value, line, true);

    [Authorize, HttpPut]
    public Task<CartDto> Update(CartLineRequest line) => shop.ChangeAsync(ShoppingService.UserId(User)!.Value, line, false);
}

public sealed class CartRequestFilter(IAntiforgery csrf) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try
        {
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
            {
                await csrf.ValidateRequestAsync(context.HttpContext);
            }
            var executed = await next();
            if (executed.Exception is ShopException error)
            {
                executed.ExceptionHandled = true;
                executed.Result = new ConflictObjectResult(new ApiErrorDto(error.Message, null));
            }
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new ApiErrorDto("Your session expired. Try again.", "antiforgery"));
        }
    }
}
