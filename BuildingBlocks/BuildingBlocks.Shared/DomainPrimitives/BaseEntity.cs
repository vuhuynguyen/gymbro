namespace BuildingBlocks.Shared.DomainPrimitives;

public abstract class BaseEntity : Entity
{
    public Guid Id { get; protected set; }
    
    public Guid? TenantId { get; protected set; }
    
    public DateTimeOffset CreatedOnUtc { get; set; }

    public Guid? CreatedBy { get; protected set; }

    public DateTimeOffset? ModifiedOnUtc { get; set; }

    public Guid? ModifiedBy { get; set; }
    
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedOnUtc { get; set; }

    protected BaseEntity()
    {
        // EF Core
    }
}