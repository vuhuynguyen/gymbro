using BuildingBlocks.Shared.Errors;
using BuildingBlocks.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Http;

/// <summary>
/// Maps a failed <see cref="Result"/> to the correct HTTP status code (CQ1).
///
/// The status is taken from <see cref="Error.Type"/> — the error's category — <b>not</b> from the text
/// of <see cref="Error.Code"/>. This keeps the mapping correct for machine-readable codes such as
/// <c>"DailyLog.Closed"</c> that don't end in a recognised suffix; the previous last-dot-segment
/// convention silently mapped those to 400.
///
/// The response body is the bare <c>Error.Message</c> string, identical to the previous per-action
/// returns, so responses remain backward-compatible.
/// </summary>
public static class ResultActionResultExtensions
{
    public static IActionResult ToFailureResult(this Result result, ControllerBase controller)
    {
        var error = result.Error;

        return error.Type switch
        {
            ErrorType.Validation => controller.BadRequest(error.Message),
            ErrorType.NotFound => controller.NotFound(error.Message),
            // Membership/role denials map to 403 (authenticated but not permitted), not 401.
            ErrorType.Unauthorized or ErrorType.Forbidden => controller.StatusCode(403, error.Message),
            ErrorType.Conflict => controller.Conflict(error.Message),
            _ => controller.BadRequest(error.Message),
        };
    }
}
