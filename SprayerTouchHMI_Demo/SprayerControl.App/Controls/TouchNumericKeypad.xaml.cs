using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SprayerControl.App.Controls;

public partial class TouchNumericKeypad : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TouchNumericKeypad),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceText));

    public static readonly DependencyProperty ConfirmCommandProperty = DependencyProperty.Register(
        nameof(ConfirmCommand), typeof(ICommand), typeof(TouchNumericKeypad));

    public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(
        nameof(CancelCommand), typeof(ICommand), typeof(TouchNumericKeypad));

    public TouchNumericKeypad() => InitializeComponent();

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? ConfirmCommand
    {
        get => (ICommand?)GetValue(ConfirmCommandProperty);
        set => SetValue(ConfirmCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    private static object CoerceText(DependencyObject _, object baseValue) => Normalize(baseValue as string ?? string.Empty);

    private void OnKeyClick(object sender, RoutedEventArgs e)
    {
        string key = ((FrameworkElement)sender).Tag as string ?? string.Empty;
        Text = key switch
        {
            "backspace" => Text.Length == 0 ? string.Empty : Text[..^1],
            "clear" => string.Empty,
            "." when Text.Contains('.', StringComparison.Ordinal) => Text,
            "." => Text + key,
            _ when key.Length == 1 && char.IsAsciiDigit(key[0]) => Text + key,
            _ => Text
        };
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (ConfirmCommand?.CanExecute(Text) == true)
        {
            ConfirmCommand.Execute(Text);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (CancelCommand?.CanExecute(Text) == true)
        {
            CancelCommand.Execute(Text);
        }
    }

    private static string Normalize(string value)
    {
        bool decimalSeen = false;
        var valid = new System.Text.StringBuilder();
        foreach (char character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                valid.Append(character);
            }
            else if (character == '.' && !decimalSeen)
            {
                valid.Append(character);
                decimalSeen = true;
            }
        }

        return valid.ToString();
    }
}
