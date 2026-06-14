using BuildingBlocks.Shared.Results;
using FluentValidation;
using MediatR;

namespace Modules.UserModule.Application.Commands;

/// <summary>Sets the current user's IANA time-zone — the authoritative anchor for their day/week boundaries.
/// Null/blank clears it.</summary>
public sealed record SetMyTimeZoneCommand(string? TimeZoneId) : IRequest<Result>;

public sealed class SetMyTimeZoneCommandValidator : AbstractValidator<SetMyTimeZoneCommand>
{
    public SetMyTimeZoneCommandValidator()
    {
        RuleFor(x => x.TimeZoneId)
            .Must(tz => string.IsNullOrWhiteSpace(tz) || TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
            .WithMessage("Unknown time-zone id (use an IANA id such as 'America/Toronto').");
    }
}
