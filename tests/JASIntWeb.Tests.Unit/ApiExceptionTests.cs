using JASIntWeb.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace JASIntWeb.Tests.Unit;

public class ApiExceptionTests
{
    [Test]
    public async Task ApiFailure_ReturnsTraceableProblemWithoutExceptionDetails()
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "safe-trace" };
        context.Request.Path = "/api/cart";
        context.Response.Body = new MemoryStream();
        var middleware = new ApiExceptionMiddleware(_ => throw new InvalidOperationException("Password=private-secret"),
            NullLogger<ApiExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        await context.Response.StatusCode.Should().BeEqualTo(500);
        await context.Response.Headers.CacheControl.ToString().Should().BeEqualTo("no-store");
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var problem = System.Text.Json.JsonDocument.Parse(body);
        await string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()).Should().BeFalse();
        await body.Contains("private-secret").Should().BeFalse();
        await body.Contains("InvalidOperationException").Should().BeFalse();
    }
}
