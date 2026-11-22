using JASIntWeb.Helpers;
using JASIntWeb.Services;

namespace JASIntWeb.Tests.Unit;

public class ResultTests
{
    [Test]
    public async Task Result_ExhaustivePatternMatching_MatchesCorrectBranches()
    {
        Result<string> success = "apple";
        Result<string> notFound = Error.NotFound("Item not found");
        Result<string> validation = Error.Validation("Invalid format", new Dictionary<string, string[]> { ["title"] = ["Required"] });
        Result<string> genericError = Error.Failure("Server failed", 500);

        // Exhaustive matching without discard _
        string successResult = success switch
        {
            string s => $"Found: {s}",
            Error e => $"Error: {e.Message}"
        };
        await successResult.Should().BeEqualTo("Found: apple");

        string notFoundResult = notFound switch
        {
            string s => $"Found: {s}",
            NotFoundError nf => $"404: {nf.Message}",
            Error e => $"Other: {e.Message}"
        };
        await notFoundResult.Should().BeEqualTo("404: Item not found");

        string validationResult = validation switch
        {
            string s => $"Found: {s}",
            ValidationError ve => $"Validation failed: {ve.Errors["title"][0]}",
            Error e => $"Other: {e.Message}"
        };
        await validationResult.Should().BeEqualTo("Validation failed: Required");

        string genericResult = genericError switch
        {
            string s => $"Found: {s}",
            Error e => $"Code {e.Code}: {e.Message}"
        };
        await genericResult.Should().BeEqualTo("Code 500: Server failed");
    }

    [Test]
    public async Task FunctionalCombinators_MapAndBind_TransformValuesCorrectly()
    {
        Result<int> initial = 10;

        // Map
        var doubled = initial.Map(x => x * 2);
        await doubled.IsSuccess.Should().BeTrue();
        await doubled.ValueOrDefault().Should().BeEqualTo(20);

        // Bind
        var parsed = doubled.Bind(x => x > 15 ? Result<string>.Success($"Value: {x}") : Result<string>.Failure("Too small"));
        await parsed.IsSuccess.Should().BeTrue();
        await parsed.ValueOrDefault().Should().BeEqualTo("Value: 20");

        // Bind with failure
        var failed = doubled.Bind<string>(_ => Error.Validation("Blocked"));
        await failed.IsFailure.Should().BeTrue();
        await (failed.Value as Error)!.Message.Should().BeEqualTo("Blocked");

        // Ensure
        var ensuredOk = initial.Ensure(x => x > 5, Error.Validation("Must be > 5"));
        await ensuredOk.IsSuccess.Should().BeTrue();

        var ensuredFail = initial.Ensure(x => x > 100, Error.Validation("Must be > 100"));
        await ensuredFail.IsFailure.Should().BeTrue();
    }

    [Test]
    public async Task LinqQueryComprehension_ComposesMultipleResults()
    {
        Result<int> a = 5;
        Result<int> b = 10;
        Result<int> c = 3;

        // Functional LINQ comprehension with extension members
        var sum =
            from x in a
            from y in b
            from z in c
            select x + y + z;

        await sum.IsSuccess.Should().BeTrue();
        await sum.ValueOrDefault().Should().BeEqualTo(18);

        // Short-circuit on failure
        Result<int> failed = Error.Validation("Calculation error");
        var failedSum =
            from x in a
            from y in failed
            from z in c
            select x + y + z;

        await failedSum.IsFailure.Should().BeTrue();
        await (failedSum.Value as Error)!.Message.Should().BeEqualTo("Calculation error");
    }

    [Test]
    public async Task AsyncCombinators_WorkSeamlessly()
    {
        Result<string> initial = "jas-laptop";

        var mapped = await initial.MapAsync(async val =>
        {
            await Task.Yield();
            return val.ToUpperInvariant();
        });

        await mapped.IsSuccess.Should().BeTrue();
        await mapped.ValueOrDefault().Should().BeEqualTo("JAS-LAPTOP");

        var bound = await mapped.BindAsync(async val =>
        {
            await Task.Yield();
            return val.StartsWith("JAS")
                ? Result<string>.Success($"Verified: {val}")
                : Result<string>.Failure("Invalid prefix");
        });

        await bound.IsSuccess.Should().BeTrue();
        await bound.ValueOrDefault().Should().BeEqualTo("Verified: JAS-LAPTOP");
    }

    [Test]
    public async Task CourierLogistics_TrackingResultUnion_PatternMatchesExhaustively()
    {
        var courier = new CourierLogisticsService();

        var tracked = courier.ResolveTracking("Steadfast Courier", "ST12345");
        string trackedMessage = tracked switch
        {
            TrackingUrl url => $"Tracking via {url.CourierName}: {url.Url}",
            Untracked u => $"Untracked: {u.Reason}"
        };
        await trackedMessage.Should().BeEqualTo("Tracking via Steadfast Courier: https://steadfast.com.bd/t/ST12345");

        var untracked = courier.ResolveTracking("Showroom Pickup", "PICKUP123");
        string untrackedMessage = untracked switch
        {
            TrackingUrl url => $"Tracking via {url.CourierName}: {url.Url}",
            Untracked u => $"Untracked: {u.Reason}"
        };
        await untrackedMessage.Should().BeEqualTo("Untracked: No online tracking page available for courier 'Showroom Pickup'.");
    }
}
