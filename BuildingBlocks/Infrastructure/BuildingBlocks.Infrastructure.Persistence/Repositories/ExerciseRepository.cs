using Modules.ExerciseModule.Application.Abstractions;
using Modules.ExerciseModule.Entities;

namespace BuildingBlocks.Infrastructure.Persistence.Repositories;

public class ExerciseRepository(AppDbContext context) : Repository<Exercise>(context), IExerciseRepository;