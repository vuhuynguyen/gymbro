using BuildingBlocks.Infrastructure.Persistence.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.WorkoutPlanModule.Application.Abstractions;
using Modules.WorkoutPlanModule.Entities;

namespace Modules.WorkoutPlanModule.Infrastructure.Persistence;

public sealed class PlanAssignmentRepository(DbContext context)
    : Repository<PlanAssignment>(context), IPlanAssignmentRepository
{
}
