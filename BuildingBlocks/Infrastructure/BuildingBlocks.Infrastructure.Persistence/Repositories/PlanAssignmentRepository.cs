using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public sealed class PlanAssignmentRepository(AppDbContext context)
    : Repository<PlanAssignment>(context), IPlanAssignmentRepository
{
}
