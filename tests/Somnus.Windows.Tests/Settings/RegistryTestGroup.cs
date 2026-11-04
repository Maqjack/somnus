namespace Somnus.Windows.Tests.Settings;

/// <summary>
/// The tests that write test keys under HKEY_CURRENT_USER\Software\Somnus.Tests; they run one after
/// another, because each removes the shared parent key once it is empty, which must not happen while
/// another one writes under it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RegistryTestGroup
{
    /// <summary>The collection name.</summary>
    public const string Name = "Registry";
}
