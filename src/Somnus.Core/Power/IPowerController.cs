namespace Somnus.Core.Power;

/// <summary>
/// Performs power actions. This is the only way Somnus changes the power state of the PC
/// (rule R-01): Debug builds and <c>--dry-run</c> use an implementation that only logs.
/// </summary>
public interface IPowerController
{
    /// <summary>
    /// Puts the PC to sleep. On Windows the call returns after the PC wakes up again.
    /// </summary>
    /// <exception cref="PowerActionFailedException">Windows refused the request.</exception>
    void Sleep();

    /// <summary>
    /// Hibernates the PC. On Windows the call returns after the PC resumes.
    /// </summary>
    /// <exception cref="PowerActionFailedException">Windows refused the request.</exception>
    void Hibernate();

    /// <summary>
    /// Starts shutting down the PC. The call returns as soon as Windows accepted the request.
    /// </summary>
    /// <param name="forceCloseApps">
    /// <see langword="true"/> closes running apps without asking them, so unsaved data is lost;
    /// <see langword="false"/> lets apps with unsaved work stop the shutdown.
    /// </param>
    /// <exception cref="PowerActionFailedException">Windows refused the request.</exception>
    void Shutdown(bool forceCloseApps);
}
