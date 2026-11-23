namespace WebTestPdpa.Api.Contracts;

/// <summary>🔄 ProblemDetails + extension code</summary>
public static class ApiErrors
{
    public static IResult Problem(int status, string code, string? title = null, IDictionary<string, object?>? extra = null)
    {
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = status, Title = title ?? code };
        pd.Extensions["code"] = code;
        if (extra is not null)
        {
            foreach (var kv in extra) pd.Extensions[kv.Key] = kv.Value;
        }
        return Results.Json(pd, statusCode: status, contentType: "application/problem+json");
    }
}
