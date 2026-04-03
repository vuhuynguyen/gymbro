using MediatR;

namespace BuildingBlocks.Application.Messaging;

/// <summary>
/// In-process integration notifications between modules. Define the contract in this assembly;
/// publish from one module and handle in another. Modules must not reference each other's
/// domain or persistence projects.
/// </summary>
public interface ICrossModuleNotification : INotification
{
}
