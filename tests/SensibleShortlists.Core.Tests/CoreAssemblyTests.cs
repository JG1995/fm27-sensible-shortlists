using SensibleShortlists.Core.Diagnostics;

namespace SensibleShortlists.Core.Tests;

public class CoreAssemblyTests
{
    private static readonly string[] ForbiddenPrefixes =
    {
        "BepInEx", "0Harmony", "HarmonyLib", "Il2Cpp", "UnityEngine", "Unity.", "FM.", "SI.",
    };

    // The core must run without the game. A reference to any of these would tie it to the game process.
    [Fact]
    public void The_core_references_no_game_or_mod_loader_assembly()
    {
        var references = typeof(InteropTraceSession).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(
            references,
            name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
