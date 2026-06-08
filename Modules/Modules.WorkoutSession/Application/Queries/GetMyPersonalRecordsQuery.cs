using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.WorkoutSessionModule.Application.DTOs;

namespace Modules.WorkoutSessionModule.Application.Queries;

// Lifetime personal records (best estimated-1RM per lift) across all of the caller's gyms.
public sealed record GetMyPersonalRecordsQuery : IRequest<Result<PersonalRecordListDto>>;
