using GroupDocs.Signature.Logging;

namespace Demo.DigitalSigningOptions;

/// <summary>
/// Collects the messages GroupDocs.Signature writes, so they can be counted per level.
/// </summary>
/// <remarks>
/// Implements <see cref="ILogger"/>, the interface GroupDocs.Signature calls for
/// errors, warnings and trace messages. Pass an instance to <c>SignatureSettings</c> to
/// route the library's messages into your own logging system, such as Serilog, NLog or
/// Application Insights, instead of the console. Which messages arrive is decided by
/// <c>SignatureSettings.LogLevel</c>, which takes effect since version 26.9.
/// </remarks>
public sealed class CollectingLogger : ILogger
{
    public int Errors { get; private set; }

    public int Warnings { get; private set; }

    public int Traces { get; private set; }

    public List<string> WarningMessages { get; } = new();

    /// <summary>
    /// Receives an error message and the exception that caused it.
    /// </summary>
    /// <remarks>
    /// GroupDocs.Signature calls this for unrecoverable problems, for example a
    /// document that cannot be opened. It is called only when <c>LogLevel</c> includes
    /// <c>LogLevel.Error</c>. The log level only filters what is logged: it does not
    /// change which exceptions are thrown to your code.
    /// </remarks>
    public void Error(string message, Exception exception)
    {
        Errors++;
    }

    /// <summary>
    /// Receives a warning message.
    /// </summary>
    /// <remarks>
    /// GroupDocs.Signature calls this when an operation succeeds but the result may not
    /// be what you expect, for example when a document is signed with an expired
    /// certificate that <c>AllowExpired</c> permits. It is called only when
    /// <c>LogLevel</c> includes <c>LogLevel.Warning</c>. The message is kept in
    /// <see cref="WarningMessages"/>.
    /// </remarks>
    public void Warning(string message)
    {
        Warnings++;
        WarningMessages.Add(message);
    }

    /// <summary>
    /// Receives a trace message that describes a step of the process.
    /// </summary>
    /// <remarks>
    /// GroupDocs.Signature calls this for each step of an operation, such as loading
    /// the document or starting to sign. It is called only when <c>LogLevel</c>
    /// includes <c>LogLevel.Trace</c>; leave traces out in production to keep the log
    /// small.
    /// </remarks>
    public void Trace(string message)
    {
        Traces++;
    }
}
