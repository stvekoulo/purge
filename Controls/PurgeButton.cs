using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Theme;

namespace PurgeWin.Controls;

public enum ButtonRole { Primary, Secondary, Destructive, Quiet, Soft, Ghost }
public enum ButtonSize { Small, Compact, Regular, Large }

public enum PurgeButtonVariant { Primary, Secondary, Destructive, Ghost, Soft }
public enum PurgeButtonSize { Small, Compact, Regular, Large }

/// Port of PurgeButtonStyle: every action is a pill. Hover, pressed,
/// disabled (0.45 opacity), and active spinner (IsBusy) come from here.
public sealed class PurgeButton : Button
{
    private const string PillTemplateXaml = """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         TargetType="Button">
          <Border Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}"
                  CornerRadius="{TemplateBinding CornerRadius}"
                  Padding="{TemplateBinding Padding}">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="6">
              <ProgressRing x:Name="BusySpinner" Width="13" Height="13"
                            IsActive="False" Visibility="Collapsed"
                            Foreground="{TemplateBinding Foreground}"/>
              <ContentPresenter Content="{TemplateBinding Content}"
                                ContentTemplate="{TemplateBinding ContentTemplate}"
                                Foreground="{TemplateBinding Foreground}"
                                HorizontalAlignment="Center"
                                VerticalAlignment="Center"/>
            </StackPanel>
          </Border>
        </ControlTemplate>
        """;

    private static ControlTemplate? _pillTemplate;
    private ProgressRing? _busySpinner;

    public static readonly DependencyProperty RoleProperty = DependencyProperty.Register(
        nameof(Role), typeof(ButtonRole), typeof(PurgeButton),
        new PropertyMetadata(ButtonRole.Secondary, (d, _) => ((PurgeButton)d).Refresh()));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(PurgeButtonVariant), typeof(PurgeButton),
        new PropertyMetadata(PurgeButtonVariant.Secondary, (d, e) =>
        {
            var pb = (PurgeButton)d;
            pb.Role = (PurgeButtonVariant)e.NewValue switch
            {
                PurgeButtonVariant.Primary => ButtonRole.Primary,
                PurgeButtonVariant.Destructive => ButtonRole.Destructive,
                PurgeButtonVariant.Ghost => ButtonRole.Quiet,
                PurgeButtonVariant.Soft => ButtonRole.Soft,
                _ => ButtonRole.Secondary,
            };
        }));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(PurgeButtonSize), typeof(PurgeButton),
        new PropertyMetadata(PurgeButtonSize.Regular, (d, _) => ((PurgeButton)d).ApplySize()));

    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy), typeof(bool), typeof(PurgeButton),
        new PropertyMetadata(false, (d, _) => ((PurgeButton)d).OnBusyChanged()));

    public ButtonRole Role { get => (ButtonRole)GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public PurgeButtonVariant Variant { get => (PurgeButtonVariant)GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public PurgeButtonSize Size { get => (PurgeButtonSize)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    private bool _hover;
    private bool _pressed;

    public PurgeButton()
    {
        Template = _pillTemplate ??= (ControlTemplate)XamlReader.Load(PillTemplateXaml);
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        ApplySize();
        Loaded += (_, _) => Refresh();
        ActualThemeChanged += (_, _) => Refresh();
        IsEnabledChanged += (_, _) => Refresh();
        PointerEntered += (_, _) => { _hover = true; Refresh(); };
        PointerExited += (_, _) => { _hover = false; _pressed = false; Refresh(); };
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => { _pressed = true; Refresh(); }), true);
        AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => { _pressed = false; Refresh(); }), true);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _busySpinner = GetTemplateChild("BusySpinner") as ProgressRing;
        UpdateBusyUi();
    }

    private void OnBusyChanged()
    {
        UpdateBusyUi();
        Refresh();
    }

    private void UpdateBusyUi()
    {
        if (_busySpinner is not null)
        {
            _busySpinner.IsActive = IsBusy;
            _busySpinner.Visibility = IsBusy ? Visibility.Visible : Visibility.Collapsed;
        }
        IsHitTestVisible = !IsBusy;
    }

    private void ApplySize()
    {
        (Height, FontSize, Padding) = Size switch
        {
            PurgeButtonSize.Small or PurgeButtonSize.Compact => (26d, 12d, new Thickness(11, 0, 11, 0)),
            PurgeButtonSize.Large => (38d, 15d, new Thickness(20, 0, 20, 0)),
            _ => (30d, 13d, new Thickness(14, 0, 14, 0)),
        };
        CornerRadius = new CornerRadius(Height / 2);
    }

    public void Refresh()
    {
        Opacity = IsBusy ? 0.95 : (IsEnabled ? 1.0 : 0.45);
        var state = _pressed ? "Pressed" : _hover ? "Hover" : "";
        string fill, label;
        string? border = null;
        switch (Role)
        {
            case ButtonRole.Primary:
                fill = "ActionPrimary" + state; label = "OnActionPrimary"; break;
            case ButtonRole.Destructive:
                fill = "ActionDestructive" + state; label = "OnActionPrimary"; break;
            case ButtonRole.Quiet or ButtonRole.Ghost:
                fill = state == "" ? "" : "FillSecondary" + (state == "Pressed" ? "Hover" : ""); label = "TextPrimary"; break;
            case ButtonRole.Soft:
                fill = "FillSecondary" + state; label = "TextPrimary"; break;
            default:
                fill = "FillSecondary" + state; label = "TextPrimary"; border = "BorderStrong"; break;
        }
        var fg = Role == ButtonRole.Destructive
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : ThemeBrushes.Get(this, label);

        Background = fill == "" ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : ThemeBrushes.Get(this, fill);
        Foreground = fg;
        BorderBrush = border is null ? null : ThemeBrushes.Get(this, border);
        BorderThickness = new Thickness(border is null ? 0 : 1);
        if (_busySpinner is not null) _busySpinner.Foreground = fg;
        PropagateForeground(Content as DependencyObject, fg);
    }

    private static void PropagateForeground(DependencyObject? node, Brush fg)
    {
        if (node is null) return;
        if (node is TextBlock tb) tb.Foreground = fg;
        else if (node is IconElement icon) icon.Foreground = fg;
        else if (node is Panel panel)
        {
            foreach (var child in panel.Children) PropagateForeground(child, fg);
        }
    }
}
