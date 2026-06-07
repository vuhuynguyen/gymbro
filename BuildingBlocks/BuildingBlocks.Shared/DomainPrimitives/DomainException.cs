namespace BuildingBlocks.Shared.DomainPrimitives;

/// <summary>
/// Thrown by domain entity factories/methods when a business invariant is violated (e.g. a required id
/// is empty, or a value is out of its allowed range).
///
/// <para>
/// Handlers should still prefer returning <c>Result</c> failures for <b>expected</b> business rules so the
/// happy path never throws. This exception is the defensive backstop for invariants that a FluentValidation
/// validator did not pre-check: <c>GlobalExceptionHandler</c> maps it to HTTP 400 (instead of a generic 500),
/// so a bad-input case that slipped past validation still reads as a client error.
/// </para>
///
/// Lives in DomainPrimitives (already imported by every aggregate) so factories can throw it without
/// taking a new dependency.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
