using FluentValidation;
using Modules.ExerciseModule.Application.Commands;
using Modules.ExerciseModule.Entities;

namespace Modules.ExerciseModule.Application.Validators;

public class CreateExerciseCommandValidator
    : AbstractValidator<CreateExerciseCommand>
{
    public CreateExerciseCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);

        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(s => Enum.TryParse<ExerciseType>(s, true, out _))
            .WithMessage("Invalid exercise type.");

        RuleFor(x => x.MovementType)
            .NotEmpty()
            .Must(s => Enum.TryParse<MovementType>(s, true, out _))
            .WithMessage("Invalid movement type.");

        RuleFor(x => x.Difficulty)
            .NotEmpty()
            .Must(s => Enum.TryParse<DifficultyLevel>(s, true, out _))
            .WithMessage("Invalid difficulty.");

        RuleFor(x => x.Equipment)
            .NotEmpty()
            .Must(s => Enum.TryParse<Equipment>(s, true, out _))
            .WithMessage("Invalid equipment.");

        RuleFor(x => x.EstimatedCaloriesBurn)
            .GreaterThanOrEqualTo(0)
            .When(x => x.EstimatedCaloriesBurn.HasValue);

        RuleFor(x => x.AverageDurationSeconds)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AverageDurationSeconds.HasValue);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));

        RuleFor(x => x.Muscles)
            .NotEmpty()
            .WithMessage("At least one muscle is required.");

        RuleFor(x => x.Muscles)
            .Must(m => m.Any(x => x.IsPrimary))
            .WithMessage("At least one muscle must be marked as primary.")
            .When(x => x.Muscles.Count > 0);

        RuleFor(x => x.Muscles)
            .Must(m => m
                .GroupBy(x => x.Muscle.Trim(), StringComparer.OrdinalIgnoreCase)
                .All(g => g.Count() == 1))
            .WithMessage("Duplicate muscle groups are not allowed.")
            .When(x => x.Muscles.Count > 0);

        RuleForEach(x => x.Muscles).ChildRules(child =>
        {
            child.RuleFor(m => m.Muscle)
                .NotEmpty()
                .Must(s => Enum.TryParse<MuscleGroup>(s, true, out _))
                .WithMessage("Invalid muscle group.");
        });

        When(x => x.Instructions != null, () =>
        {
            RuleFor(x => x.Instructions!)
                .Must(list => list.Count <= 100)
                .WithMessage("At most 100 instruction steps are allowed.");

            RuleForEach(x => x.Instructions!).MaximumLength(1000);
        });

        When(x => x.Tags != null, () =>
        {
            RuleFor(x => x.Tags!)
                .Must(list => list.Count <= 50)
                .WithMessage("At most 50 tags are allowed.");

            RuleForEach(x => x.Tags!).MaximumLength(50);
        });

        When(x => x.Media != null, () =>
        {
            RuleFor(x => x.Media!)
                .Must(m => m.Count <= 50)
                .WithMessage("At most 50 media items are allowed.");

            RuleForEach(x => x.Media!).ChildRules(child =>
            {
                child.RuleFor(m => m.Url).MaximumLength(500);
                child.RuleFor(m => m.Type)
                    .Must(t => string.Equals(t, "Image", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(t, "Video", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("Media type must be Image or Video.");
            });
        });

        When(x => x.Warnings != null, () =>
        {
            RuleFor(x => x.Warnings!)
                .Must(list => list.Count <= 50)
                .WithMessage("At most 50 warnings are allowed.");

            RuleForEach(x => x.Warnings!).MaximumLength(500);
        });
    }
}
