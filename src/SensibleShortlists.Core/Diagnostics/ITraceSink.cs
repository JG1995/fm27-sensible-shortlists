namespace SensibleShortlists.Core.Diagnostics;

/// <summary>Receives finished trace records, one line each.</summary>
public interface ITraceSink
{
    void Write(string line);
}
