using BuildingBlocks.Shared.Results;
using MediatR;
using Modules.UserModule.Application.DTOs;

namespace Modules.UserModule.Application.Queries;

public sealed record GetMeQuery(string? EmailFromClaims) : IRequest<Result<MeDto>>;
