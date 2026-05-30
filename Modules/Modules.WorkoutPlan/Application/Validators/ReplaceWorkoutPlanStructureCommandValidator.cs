using FluentValidation;
using Modules.WorkoutPlanModule.Application.Commands;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class ReplaceWorkoutPlanStructureCommandValidator : AbstractValidator<ReplaceWorkoutPlanStructureCommand>
{
    public ReplaceWorkoutPlanStructureCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Workouts).NotNull();

        RuleForEach(x => x.Workouts).ChildRules(w =>
        {
            w.RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
            w.RuleFor(x => x.Order).GreaterThanOrEqualTo(1);
            w.RuleFor(x => x.Exercises).NotNull();
            w.RuleForEach(x => x.Exercises).ChildRules(e =>
            {
                e.RuleFor(x => x.ExerciseId).NotEmpty();
                e.RuleFor(x => x.Order).GreaterThanOrEqualTo(1);
                e.RuleFor(x => x.Sets).NotEmpty().WithMessage("Each exercise must have at least one set.");
                e.RuleForEach(x => x.Sets).ChildRules(s =>
                {
                    s.RuleFor(x => x.Order).GreaterThanOrEqualTo(1);
                    s.RuleFor(x => x.RestSeconds).GreaterThanOrEqualTo(0);
                    s.RuleFor(x => x.TargetRpe)
                        .InclusiveBetween(1, 10)
                        .When(x => x.TargetRpe.HasValue);
                    s.RuleFor(x => x.TargetWeightKg)
                        .GreaterThan(0)
                        .When(x => x.TargetWeightKg.HasValue);
                    s.RuleFor(x => x.TargetReps)
                        .GreaterThanOrEqualTo(1)
                        .When(x => x.TargetReps.HasValue);
                    s.RuleFor(x => x.TargetDurationSeconds)
                        .GreaterThanOrEqualTo(1)
                        .When(x => x.TargetDurationSeconds.HasValue);
                });
            });
        });
    }
}
