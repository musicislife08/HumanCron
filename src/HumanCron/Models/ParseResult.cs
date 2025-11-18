namespace HumanCron.Models;

/// <summary>
/// Result of parsing natural language text
/// Uses discriminated union pattern for type-safe error handling
/// </summary>
public abstract record ParseResult<T>
{
    /// <summary>
    /// Successful parse result containing the parsed value
    /// </summary>
    public sealed record Success(T Value) : ParseResult<T>;

    /// <summary>
    /// Failed parse result containing an error message
    /// </summary>
    public sealed record Error(string Message) : ParseResult<T>;

    // Prevent external inheritance - only Success and Error are valid
    private ParseResult() { }
}
