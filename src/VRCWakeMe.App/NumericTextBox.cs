using System.Windows;
using System.Windows.Input;

namespace VRCWakeMe.App;

internal static class NumericTextBox
{
    public static void Attach(System.Windows.Controls.TextBox box, Action onCommitted)
    {
        InputMethod.SetIsInputMethodEnabled(box, false);
        box.PreviewTextInput += (_, e) =>
        {
            e.Handled = !IsDigits(e.Text);
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Space)
            {
                e.Handled = true;
            }
        };
        System.Windows.DataObject.AddPastingHandler(box, (_, e) =>
        {
            if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text) ||
                !IsDigits(e.DataObject.GetData(System.Windows.DataFormats.Text) as string ?? ""))
            {
                e.CancelCommand();
            }
        });
        box.LostFocus += (_, _) => onCommitted();
    }

    public static int Read(System.Windows.Controls.TextBox box, int fallback, int min, int max)
    {
        if (!int.TryParse(box.Text, out var value))
        {
            box.Text = fallback.ToString();
            return fallback;
        }

        var clamped = Math.Clamp(value, min, max);
        box.Text = clamped.ToString();
        return clamped;
    }

    private static bool IsDigits(string text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return text.Length > 0;
    }
}
