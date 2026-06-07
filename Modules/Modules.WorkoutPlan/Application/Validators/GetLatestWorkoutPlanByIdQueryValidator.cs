using FluentValidation;
using Modules.WorkoutPlanModule.Application.Queries;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class GetLatestWorkoutPlanByIdQueryValidator : AbstractValidator<GetLatestWorkoutPlanByIdQuery>
{
    public GetLatestWorkoutPlanByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
