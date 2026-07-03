using System;

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
    /// Failed parse result containing an error message and, optionally, the
    /// original exception that caused the failure (for logging/tracing —
    /// the Message itself is always a clean, stable string safe to show
    /// directly to a user).
    /// </summary>
    public sealed record Error(string Message, Exception? Exception = null) : ParseResult<T>;

    // Prevent external inheritance - only Success and Error are valid
    private ParseResult() { }
}
