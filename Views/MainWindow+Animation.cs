using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class MainWindow
{
    private DispatcherTimer? _cleaningAnimTimer;
    private double _animPhase;
    private readonly RotateTransform _logoRotate = new();
    private readonly ScaleTransform _logoScale = new();

    private void InitLogoAnimation()
    {
        var group = new TransformGroup();
        group.Children.Add(_logoScale);
        group.Children.Add(_logoRotate);
        BrandLogoImage.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        BrandLogoImage.RenderTransform = group;

        _cleaningAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _cleaningAnimTimer.Tick += OnCleaningAnimTick;
    }

    private void UpdateLogoCleaningState()
    {
        var store = PurgeStore.Shared;
        bool cleaning = store.IsCleaning;

        if (cleaning)
        {
            LogoCleaningRing.Visibility = Visibility.Visible;
            LogoCleaningRing.IsActive = true;
            BrandCleanSparkle.Visibility = Visibility.Visible;
            TxtBrandCleaningStatus.Visibility = Visibility.Visible;
            TxtBrandCleaningStatus.Text = string.IsNullOrWhiteSpace(store.CleaningLabel)
                ? Loc.T("Nettoyage...", "Cleaning...")
                : store.CleaningLabel;

            if (_cleaningAnimTimer is { IsEnabled: false })
            {
                _animPhase = 0;
                _cleaningAnimTimer.Start();
            }
        }
        else
        {
            if (_cleaningAnimTimer is { IsEnabled: true })
            {
                _cleaningAnimTimer.Stop();
            }

            LogoCleaningRing.IsActive = false;
            LogoCleaningRing.Visibility = Visibility.Collapsed;
            BrandCleanSparkle.Visibility = Visibility.Collapsed;
            TxtBrandCleaningStatus.Visibility = Visibility.Collapsed;

            _logoRotate.Angle = 0;
            _logoScale.ScaleX = 1;
            _logoScale.ScaleY = 1;
        }
    }

    private void OnCleaningAnimTick(object? sender, object e)
    {
        _animPhase += 0.14;
        _logoRotate.Angle = Math.Sin(_animPhase) * 12.0;
        var scale = 1.0 + (Math.Sin(_animPhase * 2.0) * 0.08);
        _logoScale.ScaleX = scale;
        _logoScale.ScaleY = scale;
    }
}
