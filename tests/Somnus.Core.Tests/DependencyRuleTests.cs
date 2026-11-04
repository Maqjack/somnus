using System.Reflection;
using System.Runtime.Versioning;

namespace Somnus.Core.Tests;

/// <summary>
/// Guards the dependency rule of docs/ARCHITECTURE.md: Core depends on nothing project-internal
/// and never on WPF, WinForms or other Windows-only code.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] ForbiddenReferences =
    [
        "Somnus.Presentation",
        "Somnus.Windows",
        "Somnus",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Windows.Forms",
    ];

    private static Assembly CoreAssembly => Assembly.Load("Somnus.Core");

    [Fact]
    public void Core_DoesNotReferenceOtherSomnusProjectsOrUiFrameworks()
    {
        var references = CoreAssembly.GetReferencedAssemblies().Select(name => name.Name);

        Assert.Empty(references.Intersect(ForbiddenReferences));
    }

    [Fact]
    public void Core_IsPlatformNeutral()
    {
        Assert.Null(CoreAssembly.GetCustomAttribute<SupportedOSPlatformAttribute>());
    }
}
