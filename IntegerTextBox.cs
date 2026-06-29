using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TapeSplitterWpf;

/// <summary>Simple numeric up/down that binds cleanly as a WPF control.</summary>
public class IntegerTextBox : Control
{
    static IntegerTextBox() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(IntegerTextBox),
            new FrameworkPropertyMetadata(typeof(IntegerTextBox)));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(IntegerTextBox),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((IntegerTextBox)d).UpdateDisplay()));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(IntegerTextBox), new PropertyMetadata(0));
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(IntegerTextBox), new PropertyMetadata(int.MaxValue));
    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register(nameof(Increment), typeof(int), typeof(IntegerTextBox), new PropertyMetadata(1));

    public int Value     { get => (int)GetValue(ValueProperty);     set => SetValue(ValueProperty, value); }
    public int Minimum   { get => (int)GetValue(MinimumProperty);   set => SetValue(MinimumProperty, value); }
    public int Maximum   { get => (int)GetValue(MaximumProperty);   set => SetValue(MaximumProperty, value); }
    public int Increment { get => (int)GetValue(IncrementProperty); set => SetValue(IncrementProperty, value); }

    private TextBox? _txt;
    private Button?  _up, _dn;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _txt = GetTemplateChild("PART_Text") as TextBox;
        _up  = GetTemplateChild("PART_Up")   as Button;
        _dn  = GetTemplateChild("PART_Down") as Button;

        if (_txt != null)
        {
            _txt.LostFocus    += (_, _) => CommitText();
            _txt.KeyDown      += (_, e) => { if (e.Key == Key.Enter) CommitText(); };
            UpdateDisplay();
        }
        if (_up  != null) _up.Click  += (_, _) => Step(+1);
        if (_dn  != null) _dn.Click  += (_, _) => Step(-1);
    }

    private void Step(int dir)
    {
        Value = Math.Clamp(Value + dir * Increment, Minimum, Maximum);
        UpdateDisplay();
    }

    private void CommitText()
    {
        if (_txt != null && int.TryParse(_txt.Text, out int v))
            Value = Math.Clamp(v, Minimum, Maximum);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_txt != null) _txt.Text = Value.ToString();
    }
}
