namespace WebsiteBuilder.Core.Validation;

/// <summary>Raised when a project violates the canonical model contract.</summary>
public sealed class ProjectValidationException : IOException
{
    public ProjectValidationException(string message)
        : base(message)
    {
    }
}
