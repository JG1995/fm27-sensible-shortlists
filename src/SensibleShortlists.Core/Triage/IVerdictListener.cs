namespace SensibleShortlists.Core.Triage;

/// <summary>Something that wants to know where each inbox message stands with the triage.</summary>
public interface IVerdictListener
{
    /// <summary>The message has been listed for the first time and is being asked about.</summary>
    void Awaiting(long message);

    /// <summary>The message has its verdict. It gets one per loaded career.</summary>
    void Judged(long message, Verdict verdict);

    /// <summary>
    /// Another career was loaded. Its messages reuse the same numbers, so everything heard so far
    /// is about other messages.
    /// </summary>
    void Forget();
}
