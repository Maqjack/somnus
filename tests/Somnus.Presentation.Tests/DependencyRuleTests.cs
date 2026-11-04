using System.Reflection;
using System.Runtime.Versioning;

namespace Somnus.Presentation.Tests;

/// <summary>
/// Guards the dependency rule of docs/ARCHITECTURE.md: Presentation may use Core,
/// but never WPF, WinForms, P/Invoke adapters or the app itself.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly string[] ForbiddenReferences =
    [
        "Somnus.Windows",
        "Somnus",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Windows.Forms",
    ];

    private static Assembly PresentationAssembly => Assembly.Load("Somnus.Presentation");

    [Fact]
    public void Presentation_DoesNotReferenceAdaptersOrUiFrameworks()
    {
        var references = PresentationAssembly.GetReferencedAssemblies().Select(name => name.Name);

        Assert.Empty(references.Intersect(ForbiddenReferences));
    }

    [Fact]
    public void Presentation_IsPlatformNeutral()
    {
        Assert.Null(PresentationAssembly.GetCustomAttribute<SupportedOSPlatformAttribute>());
    }
}
