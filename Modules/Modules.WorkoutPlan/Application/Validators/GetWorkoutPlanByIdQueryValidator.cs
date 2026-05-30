using FluentValidation;
using Modules.WorkoutPlanModule.Application.Queries;

namespace Modules.WorkoutPlanModule.Application.Validators;

public sealed class GetWorkoutPlanByIdQueryValidator : AbstractValidator<GetWorkoutPlanByIdQuery>
{
    public GetWorkoutPlanByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
