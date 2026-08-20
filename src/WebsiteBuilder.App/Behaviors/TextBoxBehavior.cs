using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WebsiteBuilder.App.Behaviors;

/// <summary>
/// Attached behaviors for <see cref="TextBox"/>. <see cref="UpdateSourceOnEnterProperty"/>
/// makes pressing Enter commit the Text binding immediately (useful with
/// UpdateSourceTrigger=LostFocus so a value applies without leaving the field).
/// </summary>
public static class TextBoxBehavior
{
    public static readonly DependencyProperty UpdateSourceOnEnterProperty =
        DependencyProperty.RegisterAttached(
            "UpdateSourceOnEnter",
            typeof(bool),
            typeof(TextBoxBehavior),
            new PropertyMetadata(false, OnUpdateSourceOnEnterChanged));

    public static bool GetUpdateSourceOnEnter(DependencyObject element)
        => (bool)element.GetValue(UpdateSourceOnEnterProperty);

    public static void SetUpdateSourceOnEnter(DependencyObject element, bool value)
        => element.SetValue(UpdateSourceOnEnterProperty, value);

    private static void OnUpdateSourceOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        if (e.NewValue is true)
        {
            textBox.KeyDown += OnKeyDown;
        }
        else
        {
            textBox.KeyDown -= OnKeyDown;
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        // Commit the value to the bound source, then re-select for quick re-entry.
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        textBox.SelectAll();
        e.Handled = true;
    }
}
