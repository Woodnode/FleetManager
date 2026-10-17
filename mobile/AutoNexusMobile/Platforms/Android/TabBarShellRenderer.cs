using Android.Content.Res;
using AutoNexusMobile.Services;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Shape;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;

namespace AutoNexusMobile;

/// <summary>
/// Shell Android dont l'onglet actif porte une pastille teintée, comme le lien actif de la barre
/// latérale du web (fond de marque à 16 %). Le thème Material 2 de MAUI ne donne aucune forme à
/// cet indicateur : sans celle-ci, il restait invisible.
/// </summary>
public sealed class TabBarShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
        new TabBarTracker(this, shellItem);
}

internal sealed class TabBarTracker(IShellContext shellContext, ShellItem shellItem)
    : ShellBottomNavViewAppearanceTracker(shellContext, shellItem)
{
    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        ApplyIndicator(bottomView);
    }

    public override void ResetAppearance(BottomNavigationView bottomView)
    {
        base.ResetAppearance(bottomView);
        ApplyIndicator(bottomView);
    }

    private static void ApplyIndicator(BottomNavigationView bottomView)
    {
        var density = bottomView.Resources?.DisplayMetrics?.Density ?? 1;
        bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityLabeled;
        bottomView.ItemTextAppearanceInactive = Resource.Style.TabLabel;
        bottomView.ItemTextAppearanceActive = Resource.Style.TabLabelActive;
        bottomView.ItemActiveIndicatorEnabled = true;
        bottomView.ItemActiveIndicatorColor = ColorStateList.ValueOf(ThemeColors.Get("AccentSoft").ToPlatform());
        bottomView.ItemActiveIndicatorWidth = (int)(60 * density);
        bottomView.ItemActiveIndicatorHeight = (int)(30 * density);
        bottomView.ItemActiveIndicatorShapeAppearance = ShapeAppearanceModel.InvokeBuilder()
            .SetAllCorners(CornerFamily.Rounded, 8 * density)
            .Build();
    }
}
