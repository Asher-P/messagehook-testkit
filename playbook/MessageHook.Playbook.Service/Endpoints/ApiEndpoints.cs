using MessageHook.Playbook.Service.Runs;
using MessageHook.Playbook.Service.Storage;
using Res = Microsoft.AspNetCore.Http.Results; // unique alias; 'Results' clashes with MessageHook.Playbook.Results

namespace MessageHook.Playbook.Service.Endpoints;

public sealed record CreateSuiteRequest(string? Name);

/// <summary>All REST routes for the board UI, grouped under <c>/api</c>.</summary>
public static class ApiEndpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // --- suites ---------------------------------------------------------------------------------
        api.MapGet("/suites", (SuiteStore store) => Res.Ok(store.List()));

        api.MapPost("/suites", async (CreateSuiteRequest request, SuiteStore store) =>
        {
            var suite = new Suite { Name = string.IsNullOrWhiteSpace(request.Name) ? "Untitled suite" : request.Name! };
            return Res.Ok(await store.SaveAsync(suite));
        });

        api.MapGet("/suites/{id}", (string id, SuiteStore store) =>
            store.Get(id) is { } suite ? Res.Ok(suite) : Res.NotFound());

        api.MapPut("/suites/{id}", async (string id, Suite body, SuiteStore store) =>
        {
            if (store.Get(id) is null) return Res.NotFound();
            body.Id = id;
            return Res.Ok(await store.SaveAsync(body));
        });

        api.MapDelete("/suites/{id}", (string id, SuiteStore store) =>
            store.Delete(id) ? Res.NoContent() : Res.NotFound());

        // --- payload stack --------------------------------------------------------------------------
        api.MapPost("/suites/{id}/payloads", async (string id, IFormFile file, SuiteStore store) =>
        {
            if (store.Get(id) is null) return Res.NotFound();
            await using var stream = file.OpenReadStream();
            try
            {
                await store.SavePayloadAsync(id, file.FileName, stream);
            }
            catch (InvalidDataException e)
            {
                return Res.BadRequest(new { error = e.Message });
            }
            return Res.Ok(new { name = Path.GetFileName(file.FileName) });
        }).DisableAntiforgery();

        api.MapGet("/suites/{id}/payloads", (string id, SuiteStore store) =>
            store.Get(id) is null ? Res.NotFound() : Res.Ok(store.ListPayloads(id)));

        api.MapGet("/suites/{id}/payloads/{name}", (string id, string name, SuiteStore store) =>
            store.ReadPayload(id, name) is { } json ? Res.Text(json, "application/json") : Res.NotFound());

        api.MapDelete("/suites/{id}/payloads/{name}", (string id, string name, SuiteStore store) =>
            store.DeletePayload(id, name) ? Res.NoContent() : Res.NotFound());

        // --- validate (broker-free) -----------------------------------------------------------------
        api.MapPost("/suites/{id}/testcases/{caseId}/validate", (string id, string caseId, SuiteStore store, RunManager runs) =>
        {
            var (suite, testCase) = Find(store, id, caseId);
            if (suite is null || testCase is null) return Res.NotFound();
            var errors = runs.Validate(suite, testCase);
            return Res.Ok(new { valid = errors.Count == 0, errors });
        });

        // --- run (streaming NDJSON) -----------------------------------------------------------------
        api.MapPost("/suites/{id}/testcases/{caseId}/run", async (string id, string caseId, HttpContext ctx, SuiteStore store, RunManager runs) =>
        {
            var (suite, testCase) = Find(store, id, caseId);
            if (suite is null || testCase is null) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            await StreamRun(ctx, runs, suite, new[] { testCase });
        });

        api.MapPost("/suites/{id}/run", async (string id, HttpContext ctx, SuiteStore store, RunManager runs) =>
        {
            var suite = store.Get(id);
            if (suite is null) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            await StreamRun(ctx, runs, suite, suite.TestCases);
        });
    }

    private static (Suite? suite, TestCase? testCase) Find(SuiteStore store, string id, string caseId)
    {
        var suite = store.Get(id);
        var testCase = suite?.TestCases.FirstOrDefault(c => c.Id == caseId);
        return (suite, testCase);
    }

    private static async Task StreamRun(HttpContext ctx, RunManager runs, Suite suite, IReadOnlyList<TestCase> cases)
    {
        ctx.Response.ContentType = "application/x-ndjson";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering so events flush live
        await runs.StreamAsync(suite, cases, ctx.Response.Body, ctx.RequestAborted);
    }
}
