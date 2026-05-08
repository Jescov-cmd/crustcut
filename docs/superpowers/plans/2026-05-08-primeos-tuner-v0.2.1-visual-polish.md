# PrimeOS Tuner v0.2.1 — Visual Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Take the v0.2.0 app — functional but visually bare — to a premium gaming-grade look. Cyan accent flips to crimson; six animations land; cards get depth, shadow, and hover lift; window gets a Mica backdrop. No feature changes, no behavior changes.

**Architecture:** Pure XAML/control-level work in `PrimeOSTuner.UI`. Adopt WPF-UI 3.0.5 (already in csproj from v0.1) for the Mica window backdrop only — every other change is local to existing files. One new user control (`AnimatedNumber`) for tweening text values.

**Tech Stack:** Same as v0.2 — .NET 8, WPF, CommunityToolkit.Mvvm, Serilog. Newly used: `WPF-UI 3.0.5` (the `FluentWindow` class + `WindowBackdropType` property + `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"`).

---

## Working with this plan

Strict continuation of v0.2. Assumes the v0.2.0 tag is on `main` and the v0.2 file layout exists.

- **Check off** every step as you finish (`[ ]` → `[x]`).
- **Run the exact command shown.** If output differs from "Expected", stop and ask.
- **Commit at the end of every task.** 9 commits total.
- **Visual smoke test, not unit tests, is the source of truth** for most tasks. Every task ends with "launch the app and verify X." Build success alone is not enough — XAML errors often hide until runtime.
- **All 90 existing unit tests must keep passing.** One new test is added in Task 7.

---

## Prerequisites (one-time setup before Task 1)

- [ ] **Confirm v0.2.0 is tagged**

```powershell
git tag --list "v0.2.0"
```

Expected: prints `v0.2.0`. If empty, finish v0.2 first — do not start v0.2.1.

- [ ] **Confirm clean tree on main**

```powershell
cd "C:\Users\jaxso\projects\PC Performance booster"
git status
```

Expected: `nothing to commit, working tree clean` on `main`.

- [ ] **Confirm v0.2 build/tests still pass**

```powershell
dotnet build --nologo --verbosity quiet
dotnet test --filter "Category!=Integration&Category!=Network" --nologo --verbosity quiet
```

Expected: `Build succeeded.` and `Passed!  - Failed:     0, Passed:    90`.

---

## File structure

Only the *new* and *modified* files compared to v0.2 are listed.

```
PrimeOS Tuner/
├── docs/superpowers/
│   ├── specs/
│   │   └── 2026-05-08-primeos-tuner-v0.2.1-visual-polish-design.md  (already committed)
│   └── plans/
│       └── 2026-05-08-primeos-tuner-v0.2.1-visual-polish.md         (this file)
├── src/
│   └── PrimeOSTuner.UI/
│       ├── Theme/
│       │   ├── Colors.xaml         MODIFY — palette flip + new gradient brushes
│       │   └── Styles.xaml         MODIFY — CardBorder hover/shadow + new PrimaryActionButton style
│       ├── Controls/
│       │   ├── AnimatedNumber.xaml         NEW — tweening text helper
│       │   ├── AnimatedNumber.xaml.cs      NEW
│       │   ├── BoostScoreRing.xaml         MODIFY — halo storyboard + AnimatedNumber for the score
│       │   ├── BoostScoreRing.xaml.cs      MODIFY — wire score property to AnimatedNumber
│       │   └── StatCard.xaml               MODIFY — glow filter on sparkline + AnimatedNumber for the value
│       ├── Views/
│       │   ├── DashboardView.xaml          MODIFY — apply PrimaryActionButton on Optimize action (already styled there) and verify card shadow
│       │   ├── OptimizeView.xaml           MODIFY — PrimaryActionButton on Optimize Now
│       │   ├── GameBoostView.xaml          MODIFY — PrimaryActionButton on the three Apply Now buttons
│       │   ├── GameLibraryView.xaml        MODIFY — PrimaryActionButton on + Add Game
│       │   ├── CustomModeView.xaml         MODIFY — PrimaryActionButton on Save
│       │   └── HistoryView.xaml            MODIFY — minor card style refresh (no buttons here)
│       ├── MainWindow.xaml                 MODIFY — switch to ui:FluentWindow, add Mica backdrop, sidebar radial glow, sliding nav indicator
│       ├── MainWindow.xaml.cs              MODIFY — call ShellViewModel.Navigate so SelectedTabIndex stays in sync
│       └── ViewModels/
│           └── ShellViewModel.cs           MODIFY — add SelectedTabIndex computed from ActiveTab
└── src/PrimeOSTuner.Tests/
    └── ViewModels/
        └── ShellViewModelTests.cs          NEW — verifies SelectedTabIndex tracks ActiveTab
```

---

## Task 1: Palette flip — cyan to crimson

**Files:**
- Modify: `src/PrimeOSTuner.UI/Theme/Colors.xaml`

- [ ] **Step 1: Read the current file** so you understand what's there. The relevant block is the `<Color>` definitions and the `<SolidColorBrush>` definitions. Do not reorder them.

- [ ] **Step 2: Replace the Colors.xaml content** with the version below. The diff: `AccentColor` flips from `#00e5c5` to `#ff4d6d`, `Accent2Color` flips from `#6ad7ff` to `#ff8095`, a new `AccentDeepColor` is added (`#d11a3e`), a new `AccentDeepBrush` is added, and a new `AccentGradientBrush` (LinearGradientBrush) is added at the bottom.

`src/PrimeOSTuner.UI/Theme/Colors.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Color x:Key="Bg0Color">#06080c</Color>
    <Color x:Key="Bg1Color">#0b0f17</Color>
    <Color x:Key="Bg2Color">#11161f</Color>
    <Color x:Key="Bg3Color">#1a2030</Color>
    <Color x:Key="LineColor">#1c2333</Color>
    <Color x:Key="Text0Color">#f1f5fb</Color>
    <Color x:Key="Text1Color">#c7cfde</Color>
    <Color x:Key="Text2Color">#8b95a8</Color>
    <Color x:Key="Text3Color">#5a6478</Color>
    <Color x:Key="AccentColor">#ff4d6d</Color>
    <Color x:Key="Accent2Color">#ff8095</Color>
    <Color x:Key="AccentDeepColor">#d11a3e</Color>
    <Color x:Key="GoodColor">#43d27a</Color>
    <Color x:Key="WarnColor">#ffb84d</Color>
    <Color x:Key="DangerColor">#ff6b6b</Color>

    <SolidColorBrush x:Key="Bg0Brush"   Color="{StaticResource Bg0Color}"/>
    <SolidColorBrush x:Key="Bg1Brush"   Color="{StaticResource Bg1Color}"/>
    <SolidColorBrush x:Key="Bg2Brush"   Color="{StaticResource Bg2Color}"/>
    <SolidColorBrush x:Key="Bg3Brush"   Color="{StaticResource Bg3Color}"/>
    <SolidColorBrush x:Key="LineBrush"  Color="{StaticResource LineColor}"/>
    <SolidColorBrush x:Key="Text0Brush" Color="{StaticResource Text0Color}"/>
    <SolidColorBrush x:Key="Text1Brush" Color="{StaticResource Text1Color}"/>
    <SolidColorBrush x:Key="Text2Brush" Color="{StaticResource Text2Color}"/>
    <SolidColorBrush x:Key="Text3Brush" Color="{StaticResource Text3Color}"/>
    <SolidColorBrush x:Key="AccentBrush"      Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="Accent2Brush"     Color="{StaticResource Accent2Color}"/>
    <SolidColorBrush x:Key="AccentDeepBrush"  Color="{StaticResource AccentDeepColor}"/>
    <SolidColorBrush x:Key="GoodBrush"   Color="{StaticResource GoodColor}"/>
    <SolidColorBrush x:Key="WarnBrush"   Color="{StaticResource WarnColor}"/>
    <SolidColorBrush x:Key="DangerBrush" Color="{StaticResource DangerColor}"/>

    <LinearGradientBrush x:Key="AccentGradientBrush" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="{StaticResource AccentColor}"     Offset="0"/>
        <GradientStop Color="{StaticResource AccentDeepColor}" Offset="1"/>
    </LinearGradientBrush>
</ResourceDictionary>
```

- [ ] **Step 3: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

- [ ] **Step 4: Run the app and visually confirm the new color**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected:
- The boost score ring on the Dashboard is now **crimson red** instead of teal-cyan.
- The "PRIMEOS TUNER" sidebar label is crimson.
- The "Apply Now" buttons on Game Boost are still cyan-styled in their literal `Background="{StaticResource AccentBrush}"` references — they will pick up crimson too.
- Close the app when satisfied.

- [ ] **Step 5: Commit**

```powershell
git add src/PrimeOSTuner.UI/Theme/Colors.xaml
git commit -m "Flip accent palette from cyan to crimson"
```

---

## Task 2: WPF-UI Fluent window with Mica backdrop + sidebar radial glow

**Files:**
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml` (root element changes from Window to ui:FluentWindow)
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs` (base class changes from Window to FluentWindow)

WPF-UI is already in `PrimeOSTuner.UI.csproj` (`<PackageReference Include="WPF-UI" Version="3.0.5" />`). The `FluentWindow` class lives in `Wpf.Ui.Controls`.

- [ ] **Step 1: Replace `MainWindow.xaml`** with this version. Diff vs. current: root tag is `<ui:FluentWindow>` instead of `<Window>`, with `WindowBackdropType="Mica"` and `ExtendsContentIntoTitleBar="False"`. The sidebar's `<Border>` gets a `RadialGradientBrush` overlay child for the soft accent glow. A new `Path` element `NavIndicator` is added above the nav buttons but is empty/hidden until Task 7.

`src/PrimeOSTuner.UI/MainWindow.xaml`:

```xml
<ui:FluentWindow x:Class="PrimeOSTuner.UI.MainWindow"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
                 Title="PrimeOS Tuner"
                 Height="780" Width="1240"
                 WindowStartupLocation="CenterScreen"
                 WindowBackdropType="Mica"
                 ExtendsContentIntoTitleBar="False"
                 Background="Transparent">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="220"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Sidebar -->
        <Grid Grid.Column="0">
            <Border Background="{StaticResource Bg1Brush}" BorderBrush="{StaticResource LineBrush}" BorderThickness="0,0,1,0"/>
            <!-- Soft radial accent glow at top of sidebar -->
            <Border IsHitTestVisible="False">
                <Border.Background>
                    <RadialGradientBrush Center="0.5,0.0" GradientOrigin="0.5,0.0" RadiusX="0.9" RadiusY="0.6">
                        <GradientStop Color="#22FF4D6D" Offset="0"/>
                        <GradientStop Color="#00FFFFFF" Offset="1"/>
                    </RadialGradientBrush>
                </Border.Background>
            </Border>

            <DockPanel Margin="12,20" LastChildFill="True">
                <StackPanel DockPanel.Dock="Bottom" Margin="0,12,0,0">
                    <Border Background="{StaticResource Bg2Brush}" CornerRadius="10" Padding="12">
                        <StackPanel>
                            <TextBlock x:Name="WatcherStatusText" Text="{Binding StatusText}" Foreground="{StaticResource Text1Brush}" FontSize="11" FontWeight="Bold"/>
                            <ToggleButton x:Name="WatcherToggle" IsChecked="{Binding IsWatching, Mode=TwoWay}" Margin="0,6,0,0" Content="Toggle"/>
                        </StackPanel>
                    </Border>
                </StackPanel>

                <Grid>
                    <!-- Sliding indicator (positioned in Task 7) -->
                    <Canvas x:Name="NavIndicatorHost" Width="3" HorizontalAlignment="Left" Margin="0,32,0,0" Panel.ZIndex="1">
                        <Border x:Name="NavIndicator" Width="3" Height="30"
                                Canvas.Top="0"
                                CornerRadius="2"
                                Background="{StaticResource AccentGradientBrush}"
                                Visibility="Collapsed">
                            <Border.Effect>
                                <DropShadowEffect Color="{StaticResource AccentColor}" BlurRadius="10" ShadowDepth="0" Opacity="0.7"/>
                            </Border.Effect>
                        </Border>
                    </Canvas>

                    <StackPanel Margin="8,0,0,0">
                        <TextBlock Text="PRIMEOS TUNER" Style="{StaticResource SectionLabel}" Foreground="{StaticResource AccentBrush}" Margin="6,0,0,16"/>
                        <TextBlock Text="NAVIGATION" Style="{StaticResource SectionLabel}" Margin="6,0,0,8"/>
                        <Button Content="&#x2302;  Dashboard"   Tag="Dashboard"   Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                        <Button Content="&#x26A1;  Optimize"    Tag="Optimize"    Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                        <Button Content="&#x1F3AE;  Game Boost" Tag="GameBoost"   Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                        <Button Content="&#x1F4DA;  Library"    Tag="GameLibrary" Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                        <Button Content="&#x2699;  Custom"      Tag="CustomMode"  Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                        <Button Content="&#x26E8;  History"     Tag="History"     Click="NavButton_Click" Style="{StaticResource NavButtonStyle}"/>
                    </StackPanel>
                </Grid>
            </DockPanel>
        </Grid>

        <!-- Page host -->
        <ContentControl Grid.Column="1" x:Name="PageHost" Margin="32,28"/>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 2: Update `MainWindow.xaml.cs`** so the partial class inherits `FluentWindow` instead of `Window`. Replace the file:

`src/PrimeOSTuner.UI/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;
using Wpf.Ui.Controls;

namespace PrimeOSTuner.UI;

public partial class MainWindow : FluentWindow
{
    private readonly ShellViewModel _shellVm;

    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm)
    {
        InitializeComponent();
        _shellVm = vm;
        DataContext = vm;
        var bottomBlock = (FrameworkElement)FindName("WatcherStatusText");
        if (bottomBlock?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

        ShowTab("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowTab(tab);
    }

    private void ShowTab(string tab)
    {
        _shellVm.NavigateCommand.Execute(tab);
        var sp = ((App)Application.Current).Host.Services;
        PageHost.Content = tab switch
        {
            "Dashboard"    => sp.GetRequiredService<DashboardView>(),
            "Optimize"     => sp.GetRequiredService<OptimizeView>(),
            "GameBoost"    => sp.GetRequiredService<GameBoostView>(),
            "GameLibrary"  => sp.GetRequiredService<GameLibraryView>(),
            "CustomMode"   => sp.GetRequiredService<CustomModeView>(),
            "History"      => sp.GetRequiredService<HistoryView>(),
            _ => new TextBlock
            {
                Text = $"{tab} (placeholder)",
                FontSize = 22,
                Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
            }
        };
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

If you see "The type or namespace 'FluentWindow' could not be found", the WPF-UI namespace import isn't resolving. Confirm `<PackageReference Include="WPF-UI" Version="3.0.5" />` is in `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj` (it should be from v0.1) and run `dotnet restore`.

- [ ] **Step 4: Run the app and verify**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected:
- On Windows 11: window has the **Mica backdrop** (translucent, blurred wallpaper showing through). On Windows 10 it falls back to a solid background — that's fine.
- The sidebar has a faint **crimson radial glow at the top** (subtle, not aggressive).
- All 6 nav tabs still work; clicking switches `PageHost.Content`.
- Close the app.

- [ ] **Step 5: Commit**

```powershell
git add src/PrimeOSTuner.UI/MainWindow.xaml src/PrimeOSTuner.UI/MainWindow.xaml.cs
git commit -m "Adopt WPF-UI FluentWindow with Mica backdrop and sidebar accent glow"
```

---

## Task 3: Card style overhaul — gradient + drop shadow + hover lift

**Files:**
- Modify: `src/PrimeOSTuner.UI/Theme/Styles.xaml`

The `CardBorder` style gets a vertical gradient background, a drop shadow, and a hover trigger. The `HeaderText` and `SectionLabel` styles are tightened. Existing `NavButtonStyle` is unchanged.

- [ ] **Step 1: Replace `Styles.xaml`** entirely:

`src/PrimeOSTuner.UI/Theme/Styles.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource Bg0Brush}"/>
        <Setter Property="Foreground" Value="{StaticResource Text0Brush}"/>
        <Setter Property="FontFamily" Value="Segoe UI"/>
        <Setter Property="FontSize" Value="13"/>
    </Style>

    <Style TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource Text1Brush}"/>
    </Style>

    <Style x:Key="HeaderText" TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource Text0Brush}"/>
        <Setter Property="FontSize" Value="24"/>
        <Setter Property="FontWeight" Value="Bold"/>
    </Style>

    <Style x:Key="SectionLabel" TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource Text3Brush}"/>
        <Setter Property="FontSize" Value="11"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="TextOptions.TextRenderingMode" Value="ClearType"/>
        <Setter Property="Margin" Value="0,0,0,6"/>
    </Style>

    <!-- A flat (non-hover) card. Used internally; views should prefer CardBorder. -->
    <Style x:Key="FlatCardBorder" TargetType="Border">
        <Setter Property="BorderBrush" Value="{StaticResource LineBrush}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="CornerRadius" Value="14"/>
        <Setter Property="Padding" Value="18"/>
        <Setter Property="Background">
            <Setter.Value>
                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                    <GradientStop Color="{StaticResource Bg2Color}" Offset="0"/>
                    <GradientStop Color="{StaticResource Bg1Color}" Offset="1"/>
                </LinearGradientBrush>
            </Setter.Value>
        </Setter>
        <Setter Property="Effect">
            <Setter.Value>
                <DropShadowEffect Color="Black" BlurRadius="20" ShadowDepth="4" Opacity="0.35"/>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Default card style: same look as FlatCard plus hover lift + accent border + glow. -->
    <Style x:Key="CardBorder" TargetType="Border" BasedOn="{StaticResource FlatCardBorder}">
        <Setter Property="RenderTransformOrigin" Value="0.5,0.5"/>
        <Setter Property="RenderTransform">
            <Setter.Value>
                <TranslateTransform Y="0"/>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                <Trigger.EnterActions>
                    <BeginStoryboard>
                        <Storyboard>
                            <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.Y)"
                                             To="-3" Duration="0:0:0.18"/>
                            <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)"
                                             To="32" Duration="0:0:0.18"/>
                            <ColorAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.Color)"
                                            To="#FF4D6D" Duration="0:0:0.18"/>
                            <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.Opacity)"
                                             To="0.45" Duration="0:0:0.18"/>
                        </Storyboard>
                    </BeginStoryboard>
                </Trigger.EnterActions>
                <Trigger.ExitActions>
                    <BeginStoryboard>
                        <Storyboard>
                            <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.Y)"
                                             To="0" Duration="0:0:0.18"/>
                            <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)"
                                             To="20" Duration="0:0:0.18"/>
                            <ColorAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.Color)"
                                            To="Black" Duration="0:0:0.18"/>
                            <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.Opacity)"
                                             To="0.35" Duration="0:0:0.18"/>
                        </Storyboard>
                    </BeginStoryboard>
                </Trigger.ExitActions>
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="NavButtonStyle" TargetType="Button">
        <Setter Property="HorizontalContentAlignment" Value="Left"/>
        <Setter Property="Padding" Value="14,11"/>
        <Setter Property="Margin" Value="0,2"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Foreground" Value="{StaticResource Text1Brush}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="bg" Background="{TemplateBinding Background}" CornerRadius="10" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bg" Property="Background" Value="#10FFFFFF"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- PrimaryActionButton — used for Apply Now / Optimize Now / + Add Game / Save -->
    <Style x:Key="PrimaryActionButton" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource AccentGradientBrush}"/>
        <Setter Property="Foreground" Value="White"/>
        <Setter Property="FontWeight" Value="Bold"/>
        <Setter Property="FontSize" Value="13"/>
        <Setter Property="Padding" Value="18,8"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid>
                        <Border x:Name="OuterGlow" CornerRadius="10" Background="Transparent">
                            <Border.Effect>
                                <DropShadowEffect Color="#FF4D6D" BlurRadius="0" ShadowDepth="0" Opacity="0.7"/>
                            </Border.Effect>
                        </Border>
                        <Border x:Name="bg" Background="{TemplateBinding Background}" CornerRadius="10" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <EventTrigger RoutedEvent="Loaded">
                            <BeginStoryboard>
                                <Storyboard RepeatBehavior="Forever">
                                    <DoubleAnimation Storyboard.TargetName="OuterGlow"
                                                     Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)"
                                                     From="0" To="22" Duration="0:0:1.0"/>
                                    <DoubleAnimation Storyboard.TargetName="OuterGlow"
                                                     Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.Opacity)"
                                                     From="0.7" To="0" Duration="0:0:1.0"/>
                                    <DoubleAnimation Storyboard.TargetName="OuterGlow"
                                                     Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)"
                                                     BeginTime="0:0:1.0" From="22" To="0" Duration="0:0:1.0"/>
                                </Storyboard>
                            </BeginStoryboard>
                        </EventTrigger>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="bg" Property="Background" Value="{StaticResource AccentBrush}"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.5"/>
                            <Setter TargetName="OuterGlow" Property="Visibility" Value="Collapsed"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

- [ ] **Step 3: Run + verify hover lift**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected:
- Cards on the **Dashboard** (Currently Active Profile panel if visible, the StatCards) and on **Game Boost** (3 mode cards) should now have a subtle gradient background and a drop shadow.
- Hovering a card should make it **lift slightly upward** (~3 px) and gain a **crimson border + glow**. The lift should look smooth, not snappy.
- Cards should look unchanged on the Dashboard score area until Task 5 wraps the score in animations.

If hovering a card doesn't trigger the lift, the issue is likely the trigger animating a `RenderTransform` that doesn't start as a `TranslateTransform`. The setter in the style provides one — confirm Styles.xaml was replaced fully.

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.UI/Theme/Styles.xaml
git commit -m "Add card hover lift, drop shadow, and PrimaryActionButton pulse style"
```

---

## Task 4: AnimatedNumber control

**Files:**
- Create: `src/PrimeOSTuner.UI/Controls/AnimatedNumber.xaml`
- Create: `src/PrimeOSTuner.UI/Controls/AnimatedNumber.xaml.cs`

A `UserControl` that wraps a `TextBlock`. It exposes a `TargetValue` (double) dependency property. When the value changes, it tweens an internal "current" value over 800 ms with cubic-out easing, and the TextBlock updates to the integer-rounded current value on every animation tick.

WPF can't tween `TextBlock.Text` directly (it's a string), so we tween a backing `double` exposed as a separate dependency property `DisplayValue`, and bind the `Text` to it through a converter that rounds to int.

For simplicity, we use the code-behind approach: subscribe to `TargetValue` changes and start a `DoubleAnimation` on `DisplayValue` programmatically.

- [ ] **Step 1: Create the XAML**

`src/PrimeOSTuner.UI/Controls/AnimatedNumber.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Controls.AnimatedNumber"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Name="Root">
    <TextBlock x:Name="Label"
               Foreground="{Binding Foreground, ElementName=Root}"
               FontSize="{Binding FontSize, ElementName=Root}"
               FontWeight="{Binding FontWeight, ElementName=Root}"
               HorizontalAlignment="Center"
               VerticalAlignment="Center"/>
</UserControl>
```

- [ ] **Step 2: Create the code-behind**

`src/PrimeOSTuner.UI/Controls/AnimatedNumber.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace PrimeOSTuner.UI.Controls;

public partial class AnimatedNumber : UserControl
{
    public static readonly DependencyProperty TargetValueProperty = DependencyProperty.Register(
        nameof(TargetValue), typeof(double), typeof(AnimatedNumber),
        new PropertyMetadata(0.0, OnTargetValueChanged));

    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(double), typeof(AnimatedNumber),
        new PropertyMetadata(0.0, OnDisplayValueChanged));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(AnimatedNumber),
        new PropertyMetadata("0"));

    public double TargetValue
    {
        get => (double)GetValue(TargetValueProperty);
        set => SetValue(TargetValueProperty, value);
    }

    public double DisplayValue
    {
        get => (double)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public AnimatedNumber()
    {
        InitializeComponent();
        Label.Text = "0";
    }

    private static void OnTargetValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AnimatedNumber)d;
        var from = ctrl.DisplayValue;
        var to = (double)e.NewValue;
        if (from == to) return;

        var anim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(800),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) => ctrl.DisplayValue = to;
        ctrl.BeginAnimation(DisplayValueProperty, anim);
    }

    private static void OnDisplayValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (AnimatedNumber)d;
        var v = (double)e.NewValue;
        ctrl.Label.Text = v.ToString(ctrl.Format);
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

(No visual smoke test yet — the control is unused. Tasks 5 and 8 wire it into BoostScoreRing and StatCard.)

- [ ] **Step 4: Commit**

```powershell
git add src/PrimeOSTuner.UI/Controls/AnimatedNumber.xaml src/PrimeOSTuner.UI/Controls/AnimatedNumber.xaml.cs
git commit -m "Add AnimatedNumber user control for tweening text values"
```

---

## Task 5: BoostScoreRing — animated halo + count-up

**Files:**
- Modify: `src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml`
- Modify: `src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml.cs`

Add a `Path` element behind the existing arc that draws a translucent gradient sweep. A `Storyboard` rotates it forever. Replace the score `TextBlock` with an `AnimatedNumber`.

- [ ] **Step 1: Read the current BoostScoreRing.xaml.cs** (do NOT replace it yet — note how the `Score` dependency property is wired and how the arc geometry is set on update). Keep that logic intact in Step 3.

- [ ] **Step 2: Replace `BoostScoreRing.xaml`**

`src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Controls.BoostScoreRing"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:c="clr-namespace:PrimeOSTuner.UI.Controls"
             Width="120" Height="120">
    <Grid>
        <!-- Animated halo: full ring with a soft gradient that rotates -->
        <Ellipse Width="120" Height="120" RenderTransformOrigin="0.5,0.5">
            <Ellipse.Stroke>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="#00FF4D6D" Offset="0"/>
                    <GradientStop Color="#80FF4D6D" Offset="0.5"/>
                    <GradientStop Color="#00FF4D6D" Offset="1"/>
                </LinearGradientBrush>
            </Ellipse.Stroke>
            <Ellipse.StrokeThickness>14</Ellipse.StrokeThickness>
            <Ellipse.Effect>
                <BlurEffect Radius="6"/>
            </Ellipse.Effect>
            <Ellipse.RenderTransform>
                <RotateTransform x:Name="HaloRotate" Angle="0"/>
            </Ellipse.RenderTransform>
            <Ellipse.Triggers>
                <EventTrigger RoutedEvent="Loaded">
                    <BeginStoryboard>
                        <Storyboard RepeatBehavior="Forever">
                            <DoubleAnimation Storyboard.TargetName="HaloRotate"
                                             Storyboard.TargetProperty="Angle"
                                             From="0" To="360" Duration="0:0:6"/>
                        </Storyboard>
                    </BeginStoryboard>
                </EventTrigger>
            </Ellipse.Triggers>
        </Ellipse>

        <!-- Static background ring -->
        <Ellipse Width="120" Height="120" Stroke="{StaticResource LineBrush}" StrokeThickness="10"/>

        <!-- Live arc that fills based on Score -->
        <Path x:Name="Arc" StrokeThickness="10" StrokeStartLineCap="Round" StrokeEndLineCap="Round">
            <Path.Stroke>
                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                    <GradientStop Color="{StaticResource AccentColor}" Offset="0"/>
                    <GradientStop Color="{StaticResource Accent2Color}" Offset="1"/>
                </LinearGradientBrush>
            </Path.Stroke>
            <Path.Effect>
                <DropShadowEffect Color="#FF4D6D" BlurRadius="14" ShadowDepth="0" Opacity="0.7"/>
            </Path.Effect>
        </Path>

        <!-- Animated score readout -->
        <c:AnimatedNumber x:Name="ScoreText"
                          FontSize="36" FontWeight="Black"
                          Foreground="{StaticResource Text0Brush}"
                          HorizontalAlignment="Center" VerticalAlignment="Center"/>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Replace `BoostScoreRing.xaml.cs`** so the existing `Score` DP forwards to `ScoreText.TargetValue` instead of writing the TextBlock directly:

`src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PrimeOSTuner.UI.Controls;

public partial class BoostScoreRing : UserControl
{
    public static readonly DependencyProperty ScoreProperty = DependencyProperty.Register(
        nameof(Score), typeof(int), typeof(BoostScoreRing),
        new PropertyMetadata(0, OnScoreChanged));

    public int Score
    {
        get => (int)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public BoostScoreRing()
    {
        InitializeComponent();
        UpdateArc(0);
    }

    private static void OnScoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (BoostScoreRing)d;
        var v = (int)e.NewValue;
        ring.ScoreText.TargetValue = v;
        ring.UpdateArc(v);
    }

    private void UpdateArc(int score)
    {
        const double radius = 55;
        const double cx = 60;
        const double cy = 60;
        var fraction = Math.Clamp(score, 0, 100) / 100.0;
        var angle = fraction * 360.0;
        if (angle <= 0)
        {
            Arc.Data = null;
            return;
        }
        var rad = (angle - 90) * Math.PI / 180.0;
        var endX = cx + radius * Math.Cos(rad);
        var endY = cy + radius * Math.Sin(rad);
        var isLargeArc = angle > 180;

        var fig = new System.Windows.Media.PathFigure
        {
            StartPoint = new Point(cx, cy - radius),
            IsClosed = false
        };
        fig.Segments.Add(new System.Windows.Media.ArcSegment
        {
            Point = new Point(endX, endY),
            Size = new Size(radius, radius),
            SweepDirection = System.Windows.Media.SweepDirection.Clockwise,
            IsLargeArc = isLargeArc
        });

        var geom = new System.Windows.Media.PathGeometry();
        geom.Figures.Add(fig);
        Arc.Data = geom;
    }
}
```

(If the v0.1 `BoostScoreRing.xaml.cs` had the same `UpdateArc` logic, this is essentially a copy plus the `ScoreText.TargetValue = v` line and removing direct `ScoreText.Text` writes. If your v0.1 file used a different geometry approach, keep the geometry intact and only update the score readout.)

- [ ] **Step 4: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

- [ ] **Step 5: Run + verify**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected on the Dashboard:
- Score number **counts up from 0** to its current value over ~0.8 sec when the dashboard first appears.
- A **soft crimson halo** rotates slowly behind the score arc (one full rotation every 6 seconds).
- The arc itself glows softly thanks to the new DropShadowEffect.
- Resize the window to confirm nothing layout-breaks.
- Close the app.

- [ ] **Step 6: Commit**

```powershell
git add src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml src/PrimeOSTuner.UI/Controls/BoostScoreRing.xaml.cs
git commit -m "Add rotating halo and count-up animation to BoostScoreRing"
```

---

## Task 6: PrimaryActionButton — apply across all "Apply Now" buttons

**Files:**
- Modify: `src/PrimeOSTuner.UI/Views/OptimizeView.xaml`
- Modify: `src/PrimeOSTuner.UI/Views/GameBoostView.xaml`
- Modify: `src/PrimeOSTuner.UI/Views/GameLibraryView.xaml`
- Modify: `src/PrimeOSTuner.UI/Views/CustomModeView.xaml`

Find the existing primary action buttons in each view and add `Style="{StaticResource PrimaryActionButton}"`. Remove their inline `Background`/`Foreground`/`FontWeight`/`Padding` since the style supplies them.

- [ ] **Step 1: OptimizeView.xaml** — find the "Optimize Now" Button (or whatever the primary action is on this view; in v0.2 it was a button labelled "Optimize Now" or similar). Add `Style="{StaticResource PrimaryActionButton}"` to it. Remove any inline `Background`, `Foreground`, `BorderThickness`, `FontWeight`, and `Padding` it has — let the style supply them. Keep `Click=` and `Content=` as-is.

If the view has multiple buttons, only the **single primary action** gets `PrimaryActionButton`. Per-row "Preview"/"Apply" buttons stay as default Buttons.

- [ ] **Step 2: GameBoostView.xaml** — there are exactly three Apply buttons (one in each mode card). Apply `Style="{StaticResource PrimaryActionButton}"` to all three. Remove inline `Background`, `Foreground`, `FontWeight`, `BorderThickness`, `Padding` from all three.

- [ ] **Step 3: GameLibraryView.xaml** — apply `Style="{StaticResource PrimaryActionButton}"` to the "+ Add Game" button. Remove its inline styling.

- [ ] **Step 4: CustomModeView.xaml** — apply `Style="{StaticResource PrimaryActionButton}"` to the "Save Custom Mode" button. Remove its inline styling.

- [ ] **Step 5: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

- [ ] **Step 6: Run + verify**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected:
- The primary action buttons on **Optimize**, **Game Boost** (×3), **Game Library** (Add Game), **Custom Mode** (Save) all share the same style — gradient crimson background, white bold text, gentle outward pulse-glow every ~2 seconds.
- Hovering brightens them slightly. Disabling them (e.g. while applying) hides the pulse.
- Other buttons (per-row Preview/Apply, Cancel) are unchanged.
- Close the app.

- [ ] **Step 7: Commit**

```powershell
git add src/PrimeOSTuner.UI/Views/OptimizeView.xaml src/PrimeOSTuner.UI/Views/GameBoostView.xaml src/PrimeOSTuner.UI/Views/GameLibraryView.xaml src/PrimeOSTuner.UI/Views/CustomModeView.xaml
git commit -m "Apply PrimaryActionButton style to all primary action buttons"
```

---

## Task 7: Sliding nav indicator + ShellViewModel SelectedTabIndex

**Files:**
- Modify: `src/PrimeOSTuner.UI/ViewModels/ShellViewModel.cs`
- Modify: `src/PrimeOSTuner.UI/MainWindow.xaml.cs`
- Create: `src/PrimeOSTuner.Tests/ViewModels/ShellViewModelTests.cs`

The `NavIndicator` Border placeholder in MainWindow.xaml (added in Task 2) gets positioned via `Canvas.SetTop` to align with the currently selected nav button. We compute its target position from the active tab name.

- [ ] **Step 1: Write the failing test**

`src/PrimeOSTuner.Tests/ViewModels/ShellViewModelTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.UI.ViewModels;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class ShellViewModelTests
{
    [Fact]
    public void SelectedTabIndex_starts_at_zero_for_default_dashboard_tab()
    {
        var vm = new ShellViewModel();
        vm.SelectedTabIndex.Should().Be(0);
    }

    [Theory]
    [InlineData("Dashboard", 0)]
    [InlineData("Optimize", 1)]
    [InlineData("GameBoost", 2)]
    [InlineData("GameLibrary", 3)]
    [InlineData("CustomMode", 4)]
    [InlineData("History", 5)]
    public void SelectedTabIndex_tracks_ActiveTab(string tab, int expectedIndex)
    {
        var vm = new ShellViewModel();
        vm.NavigateCommand.Execute(tab);
        vm.SelectedTabIndex.Should().Be(expectedIndex);
    }

    [Fact]
    public void SelectedTabIndex_returns_zero_for_unknown_tab()
    {
        var vm = new ShellViewModel();
        vm.NavigateCommand.Execute("Unknown");
        vm.SelectedTabIndex.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run, watch fail**

```powershell
dotnet test --filter FullyQualifiedName~ShellViewModelTests --nologo --verbosity quiet
```

Expected: `Failed:    8` (or build error — `SelectedTabIndex` doesn't exist yet).

- [ ] **Step 3: Update ShellViewModel.cs** to add `SelectedTabIndex`

`src/PrimeOSTuner.UI/ViewModels/ShellViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrimeOSTuner.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private static readonly string[] TabOrder =
    {
        "Dashboard", "Optimize", "GameBoost", "GameLibrary", "CustomMode", "History"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTabIndex))]
    private string _activeTab = "Dashboard";

    public int SelectedTabIndex
    {
        get
        {
            var idx = Array.IndexOf(TabOrder, ActiveTab);
            return idx < 0 ? 0 : idx;
        }
    }

    [RelayCommand]
    private void Navigate(string tab) => ActiveTab = tab;
}
```

- [ ] **Step 4: Run, watch pass**

```powershell
dotnet test --filter FullyQualifiedName~ShellViewModelTests --nologo --verbosity quiet
```

Expected: `Passed!  - Failed:     0, Passed:     8` (or 8 passed from the data theory expansion).

- [ ] **Step 5: Wire the indicator** — modify `MainWindow.xaml.cs` so when `ShowTab` is called it animates the indicator to the right vertical position. Replace the file:

`src/PrimeOSTuner.UI/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.UI.Views;
using Wpf.Ui.Controls;

namespace PrimeOSTuner.UI;

public partial class MainWindow : FluentWindow
{
    // Vertical pixels per nav button slot. Each NavButton has Padding="14,11" + Margin="0,2"
    // so its outer height is roughly 44 px; the indicator slot is 36 px (button height minus margin).
    private const double SlotHeight = 44;
    private const double IndicatorOffset = 4; // small inset from button top

    private readonly ShellViewModel _shellVm;

    public MainWindow(ShellViewModel vm, WatcherStatusViewModel watcherVm)
    {
        InitializeComponent();
        _shellVm = vm;
        DataContext = vm;
        var bottomBlock = (FrameworkElement)FindName("WatcherStatusText");
        if (bottomBlock?.Parent is FrameworkElement parent)
            parent.DataContext = watcherVm;

        ShowTab("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tab }) ShowTab(tab);
    }

    private void ShowTab(string tab)
    {
        _shellVm.NavigateCommand.Execute(tab);

        var sp = ((App)Application.Current).Host.Services;
        PageHost.Content = tab switch
        {
            "Dashboard"    => sp.GetRequiredService<DashboardView>(),
            "Optimize"     => sp.GetRequiredService<OptimizeView>(),
            "GameBoost"    => sp.GetRequiredService<GameBoostView>(),
            "GameLibrary"  => sp.GetRequiredService<GameLibraryView>(),
            "CustomMode"   => sp.GetRequiredService<CustomModeView>(),
            "History"      => sp.GetRequiredService<HistoryView>(),
            _ => new TextBlock
            {
                Text = $"{tab} (placeholder)",
                FontSize = 22,
                Foreground = (System.Windows.Media.Brush)FindResource("Text0Brush")
            }
        };

        var idx = _shellVm.SelectedTabIndex;
        var targetTop = idx * SlotHeight + IndicatorOffset;

        NavIndicator.Visibility = Visibility.Visible;
        var anim = new DoubleAnimation
        {
            To = targetTop,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        Storyboard.SetTarget(anim, NavIndicator);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(Canvas.Top)"));
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }
}
```

- [ ] **Step 6: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors.

- [ ] **Step 7: Run + verify**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected:
- A **glowing crimson bar** appears on the left edge of the sidebar, aligned with the currently active nav button.
- Clicking another tab **slides the bar** smoothly to its new position over ~280 ms.
- Verify all 6 tabs work; the bar should land approximately on each button's vertical center.
- If the bar visibly misaligns on some buttons, adjust `SlotHeight` or `IndicatorOffset` constants in MainWindow.xaml.cs to match the actual rendered button heights and recommit.

- [ ] **Step 8: Commit**

```powershell
git add src/PrimeOSTuner.UI/ViewModels/ShellViewModel.cs src/PrimeOSTuner.UI/MainWindow.xaml.cs src/PrimeOSTuner.Tests/ViewModels/ShellViewModelTests.cs
git commit -m "Add sliding nav indicator with ShellViewModel.SelectedTabIndex"
```

---

## Task 8: StatCard — glowing sparkline + AnimatedNumber for value

**Files:**
- Modify: `src/PrimeOSTuner.UI/Controls/StatCard.xaml`

Wrap the existing `lvc:CartesianChart` in a `Border` with a `DropShadowEffect` so the line softly glows. Replace the value `TextBlock` with an `AnimatedNumber`. The chart's series stroke color and width are set in code-behind in v0.1, so we don't touch them in XAML.

- [ ] **Step 1: Replace `StatCard.xaml`**

`src/PrimeOSTuner.UI/Controls/StatCard.xaml`:

```xml
<UserControl x:Class="PrimeOSTuner.UI.Controls.StatCard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:c="clr-namespace:PrimeOSTuner.UI.Controls"
             xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF">
    <Border Style="{StaticResource CardBorder}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            <TextBlock x:Name="NameText" Grid.Row="0" Style="{StaticResource SectionLabel}"/>
            <c:AnimatedNumber x:Name="ValueAnimated" Grid.Row="1"
                              FontSize="28" FontWeight="Bold"
                              HorizontalAlignment="Left"
                              Foreground="{StaticResource Text0Brush}"/>
            <TextBlock x:Name="SubTextBlock" Grid.Row="2" FontSize="11" Foreground="{StaticResource Text3Brush}"/>
            <Border Grid.Row="3" Margin="0,8,0,0">
                <Border.Effect>
                    <DropShadowEffect Color="#FF4D6D" BlurRadius="12" ShadowDepth="0" Opacity="0.55"/>
                </Border.Effect>
                <lvc:CartesianChart x:Name="Spark" Height="40"
                                    AnimationsSpeed="0:0:0.3"
                                    TooltipPosition="Hidden"/>
            </Border>
        </Grid>
    </Border>
</UserControl>
```

- [ ] **Step 2: Update `StatCard.xaml.cs`** (the code-behind from v0.1) to push numeric updates into `ValueAnimated.TargetValue` instead of `ValueTextBlock.Text`. Read the existing file first; the `ValueText` dependency property handler is what needs to change.

The v0.1 code-behind has a property setter that wrote the value to a TextBlock. Find that line — typically:

```csharp
private static void OnValueChanged(...)
{
    var card = (StatCard)d;
    card.ValueTextBlock.Text = ...;
}
```

Replace it with logic that:
- Tries to parse the new string as a `double`. If it parses, set `card.ValueAnimated.TargetValue = parsed` and `card.ValueAnimated.Format = "0"` (or `"0.0"` if your stat values have one decimal).
- If parsing fails (e.g. "78%"), fall back to setting `card.ValueAnimated` text via a non-animated path. Simplest: add a public helper `void SetTextLiteral(string text)` to AnimatedNumber that just sets `Label.Text` and call that for non-numeric strings.

In practice, all current StatCards write integer strings. So if your existing v0.1 StatCard code-behind already converts to a number internally, just hand that number to `TargetValue`. If it forwards a raw string with units like "82%" or "12.4 ms", strip the units to a `double` first.

Pseudocode for the change (adapt to the actual v0.1 names you find in the file):

```csharp
private static void OnValueTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    var card = (StatCard)d;
    var text = (string)e.NewValue;
    var digits = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
    if (double.TryParse(digits, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var num))
    {
        card.ValueAnimated.Format = digits.Contains('.') ? "0.0" : "0";
        card.ValueAnimated.TargetValue = num;
    }
}
```

If your v0.1 file uses a different DP name for the value (e.g. `Value` instead of `ValueText`), adapt accordingly. The principle: the value DP setter should call `ValueAnimated.TargetValue` rather than writing a TextBlock. Remove any direct `ValueTextBlock.Text =` lines (the TextBlock no longer exists).

- [ ] **Step 3: Build**

```powershell
dotnet build --nologo --verbosity quiet 2>&1 | findstr /C:"error" /C:"Build succeeded"
```

Expected: 0 errors. If you get "ValueTextBlock does not exist," the code-behind still references the old XAML element — fix it.

- [ ] **Step 4: Run + verify**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Expected on the Dashboard:
- The CPU/RAM/GPU StatCard values **count up smoothly** when they change (every couple of seconds as the LiveCharts2 sample stream updates).
- The sparkline lines now have a soft **crimson glow** around them.
- Card hover lift still works (Task 3).
- Close the app.

- [ ] **Step 5: Commit**

```powershell
git add src/PrimeOSTuner.UI/Controls/StatCard.xaml src/PrimeOSTuner.UI/Controls/StatCard.xaml.cs
git commit -m "Add animated value count-up and glowing sparkline to StatCard"
```

---

## Task 9: Visual smoke test + tag v0.2.1.0

This task has no code changes — it's the gate before tagging.

- [ ] **Step 1: Full build + tests**

```powershell
dotnet build --nologo --verbosity quiet
dotnet test --filter "Category!=Integration&Category!=Network" --nologo --verbosity quiet
```

Expected: `Build succeeded.` and `Passed!  - Failed:     0, Passed:    98` (90 prior + 8 new ShellViewModelTests data theory rows).

- [ ] **Step 2: Publish a fresh build**

```powershell
dotnet publish src/PrimeOSTuner.UI -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/v0.2.1 --nologo --verbosity quiet
```

- [ ] **Step 3: Run the app and walk through this visual checklist**

```powershell
"publish\v0.2.1\PrimeOSTuner.UI.exe"
```

Confirm each item:
- [ ] Window opens with **Mica backdrop** on Win11 (translucent wallpaper showing through).
- [ ] **Crimson** is the dominant accent — score ring, sidebar label, action buttons.
- [ ] **Sliding indicator** glides between sidebar tabs as you click them.
- [ ] **Sidebar radial glow** at the top is visible but subtle.
- [ ] Boost score ring **counts up from 0** when Dashboard first loads.
- [ ] A **rotating halo** is visible behind the score ring (slow, ~6 sec rotation).
- [ ] StatCards on Dashboard show **animated value count-up** when stats change.
- [ ] StatCard sparklines **glow softly** in crimson.
- [ ] Mode cards on **Game Boost** lift up + glow on hover.
- [ ] Game tiles on **Game Library** lift up + glow on hover.
- [ ] All "Apply Now" buttons (Optimize, Game Boost ×3, Library Add Game, Custom Save) **pulse softly** every ~2 sec when idle.
- [ ] Hovering an Apply button brightens it; the pulse continues.
- [ ] Disabling an Apply button (e.g. mid-apply) stops the pulse.
- [ ] All 6 tabs render without exception. Click each one.
- [ ] Close the app cleanly.

If any item fails, **stop and write a fix task before proceeding** — do not tag a broken build.

- [ ] **Step 4: Tag v0.2.1.0**

```powershell
git tag -a v0.2.1.0 -m "v0.2.1.0 — visual polish (crimson palette, Mica backdrop, six animations)"
git tag --list "v0.*"
git log --oneline -1
```

Expected: tag list shows `v0.1.0`, `v0.2.0`, `v0.2.1.0`. `git log` shows the most recent commit on `main`.

- [ ] **Step 5: Update memory**

Append to `C:\Users\jaxso\.claude\projects\C--Users-jaxso-projects-PC-Performance-booster\memory\project_primeos_tuner.md` Status section: `v0.2.1.0 shipped 2026-05-08 (visual polish — crimson accent, Mica backdrop, 6 animations).`

---

## Done

After Task 9 you have:
- A polished, animated, crimson-accented Hone-style PC tuner UI.
- All 6 animations: halo, count-up, pulse, hover lift, sliding nav, glowing sparklines.
- Mica backdrop on Win11 with graceful Win10 fallback.
- 98 unit tests passing (90 prior + 8 new).
- v0.2.1.0 tagged.

## Out of scope (deferred)

- Custom fonts (Inter, Manrope) — needs licensing/loading work.
- Theme switcher (light mode, alternate accent presets).
- Replacing buttons/inputs/scrollbars with WPF-UI variants beyond the window backdrop.
- Bigger structural redesign (different sidebar layout, settings page, etc.).

## Risks recap

- **WPF-UI 3.0.5 API:** Confirmed properties used: `WindowBackdropType`, `ExtendsContentIntoTitleBar`, namespace `xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"`. If WPF-UI changes their API in a future version, the FluentWindow base class and these props are the most likely break points.
- **Mica fallback:** WPF-UI handles fallback to a plain dark window on Win10 automatically; no code path changes needed.
- **StatCard code-behind ambiguity:** Task 8 Step 2 has to adapt to whatever the v0.1 StatCard code-behind looked like — it's the only task that can't paste verbatim. The principle (replace direct TextBlock writes with AnimatedNumber.TargetValue) is unambiguous; the implementer must read the existing file first.
- **Indicator misalignment:** Task 7 Step 7 calls out that `SlotHeight` constants may need fine-tuning by 1-2 px after visual inspection. If misaligned, adjust and recommit before tagging.
