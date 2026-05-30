using BuildingBlocks.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Http;

/// <summary>
/// Maps a failed <see cref="Result"/> to the correct HTTP status code (CQ1).
///
/// Status is derived from the <b>last dot-segment</b> of <c>Error.Code</c> (e.g.
/// <c>"WorkoutPlan.NotFound" -&gt; "NotFound" -&gt; 404</c>), so the mapping stays correct even if a
/// module-prefixed/dotted code is introduced — the old per-action <c>code == "NotFound"</c> checks
/// silently fell through to 400 for such codes. Today's <c>Error</c> factory emits bare codes, so
/// behaviour for existing codes is unchanged.
///
/// The response body is the bare <c>Error.Message</c> string, identical to the previous per-action
/// returns, so responses remain backward-compatible.
/// </summary>
public static class ResultActionResultExtensions
{
    public static IActionResult ToFailureResult(this Result result, ControllerBase controller)
    {
        var error = result.Error;
        var suffix = LastSegment(error.Code);

        return suffix switch
        {
            "Validation" => controller.BadRequest(error.Message),
            "NotFound" => controller.NotFound(error.Message),
            // Permission/authorization denials map to 403, matching the prior per-controller behavior
            // (these are membership/role denials, not authentication failures).
            "Unauthorized" or "Forbidden" or "AdminOnly" => controller.StatusCode(403, error.Message),
            "Conflict" => controller.Conflict(error.Message),
            _ => controller.BadRequest(error.Message),
        };
    }

    private static string LastSegment(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return code;
        }

        var parts = code.Split('.');
        return parts[^1];
    }
}
