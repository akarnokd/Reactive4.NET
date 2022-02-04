namespace Reactive4.NET
{
    /// <summary>
    /// Indicates when an error from the main source should be reported.
    /// </summary>
    public enum ErrorMode
    {
        /// <summary>
        /// Report the error immediately, cancelling the active inner source.
        /// </summary>
        Immediate,
        /// <summary>
        /// Report error after an inner source terminated.
        /// </summary>
        Boundary,
        /// <summary>
        /// Report the error after all sources terminated.
        /// </summary>
        End
    }
}