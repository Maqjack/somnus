using System.Windows.Data;
using System.Windows.Markup;
using Somnus.Presentation.Localization;

namespace Somnus.App.Localization;

/// <summary>
/// XAML shortcut for a localized string that follows language changes:
/// <c>Text="{l:Tr Main_Start}"</c>. The key must exist in <c>Strings.resx</c>; a test checks every use.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
internal sealed class TrExtension : MarkupExtension
{
    /// <summary>Initializes a new instance; set <see cref="Key"/> before use.</summary>
    public TrExtension()
    {
    }

    /// <summary>Initializes a new instance for a key.</summary>
    /// <param name="key">The resource key.</param>
    public TrExtension(string key) => Key = key;

    /// <summary>Gets or sets the resource key.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
