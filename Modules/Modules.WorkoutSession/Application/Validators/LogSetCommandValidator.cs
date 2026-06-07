using FluentValidation;
using Modules.WorkoutSessionModule.Application.Commands;

namespace Modules.WorkoutSessionModule.Application.Validators;

public sealed class LogSetCommandValidator : AbstractValidator<LogSetCommand>
{
    public LogSetCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ExerciseId).NotEmpty();
        RuleFor(x => x.SetNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.SetType).IsInEnum();
        RuleFor(x => x.Rpe).InclusiveBetween(1, 10).When(x => x.Rpe.HasValue);
        RuleFor(x => x.WeightKg).GreaterThan(0).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.Reps).GreaterThanOrEqualTo(1).When(x => x.Reps.HasValue);
        RuleFor(x => x.DurationSeconds).GreaterThanOrEqualTo(1).When(x => x.DurationSeconds.HasValue);
        RuleFor(x => x.DistanceM).GreaterThanOrEqualTo(1).When(x => x.DistanceM.HasValue);
        RuleFor(x => x.RestSeconds).GreaterThanOrEqualTo(0).When(x => x.RestSeconds.HasValue);
    }
}
