using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SpiritCreator.Desktop;

public sealed class AttributeInput : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    // Let the user temporarily clear the text to type a replacement. The live
    // level and requirement checks still use 10 until a new value is entered.
    protected override decimal? OnCoerceValue(decimal? value) => value ?? NpcAttributes.Minimum;

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        RestoreBlank();
        base.OnLostFocus(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            RestoreBlank();
        base.OnKeyDown(e);
    }

    private void RestoreBlank()
    {
        if (string.IsNullOrWhiteSpace(Text))
            SetCurrentValue(TextProperty, NpcAttributes.Minimum.ToString(NumberFormat ?? CultureInfo.CurrentCulture.NumberFormat));
    }
}
