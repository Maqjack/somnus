namespace Somnus.Core.Power;

/// <summary>
/// Thrown when Windows refuses to perform a power action.
/// </summary>
public sealed class PowerActionFailedException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public PowerActionFailedException()
        : base("The power action failed.")
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">What went wrong, for the log.</param>
    public PowerActionFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and the underlying error.</summary>
    /// <param name="message">What went wrong, for the log.</param>
    /// <param name="innerException">The error reported by the platform.</param>
    public PowerActionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
