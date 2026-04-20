namespace ProjectX.IAM.Domain.Entities;

public sealed class AuthenticationAuditEntry
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? UserNameOrEmail { get; set; }

    public Guid? ProjectId { get; set; }

    public AuthenticationAuditAction Action { get; set; }

    public AuthenticationAuditOutcome Outcome { get; set; }

    public string? FailureReason { get; set; }

    public string? ClientApplication { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public User? User { get; set; }
}
