namespace SevenZipWrapper;

/// <summary>
/// The exception that is thrown when a 7-Zip operation fails.
/// </summary>
public sealed class SevenZipException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SevenZipException"/> class.
    /// </summary>
    public SevenZipException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SevenZipException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SevenZipException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SevenZipException"/> class with a specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SevenZipException(string message, Exception innerException) : base(message, innerException)
    {
    }
}