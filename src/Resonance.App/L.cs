using System.Windows.Markup;
using Resonance.Core.Localization;

namespace Resonance.App;

/// <summary>XAML text lookup: <c>Text="{local:L nc.title}"</c>. Language changes apply on restart.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class L(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
