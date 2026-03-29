# F-13 — iOS Native Field Styling

**Status:** Completed (session 18)

## Context
Entry and Editor fields use a single global style that was designed with Android in mind. On iOS, the result feels un-native — gray backgrounds on Editor, no focus feedback, placeholder colors that are too subtle. Goal: add a `Focused` VSM state that uses the app's Primary/Secondary palette as subtle visual feedback on both platforms, and fix placeholder colors to be more readable.

**All changes are in `Styles.xaml` only — no handler customization.**

**Implementation notes:** OnPlatform backgrounds were skipped — all controls stay Transparent since `InputBorder` owns the visual container. Scope expanded to include SearchBar/SearchHandler placeholder consistency. Picker/DatePicker/TimePicker reviewed and confirmed consistent.

---

## Files
- `PrayerApp/Resources/Styles/Styles.xaml`

---

## Entry Style Changes

```xaml
<Style TargetType="Entry">
    <Setter Property="TextColor"
            Value="{AppThemeBinding Light={StaticResource Black}, Dark={StaticResource White}}" />
    <!-- iOS: transparent (native look); Android: light gray fill -->
    <Setter Property="BackgroundColor">
        <Setter.Value>
            <OnPlatform x:TypeArguments="Color">
                <On Platform="iOS"     Value="Transparent" />
                <On Platform="Android" Value="{StaticResource Gray100}" />
            </OnPlatform>
        </Setter.Value>
    </Setter>
    <Setter Property="FontFamily"  Value="OpenSansRegular" />
    <Setter Property="FontSize"    Value="14" />
    <!-- Gray400 → more readable placeholder without being prominent -->
    <Setter Property="PlaceholderColor"
            Value="{AppThemeBinding Light={StaticResource Gray400}, Dark={StaticResource Gray500}}" />
    <Setter Property="MinimumHeightRequest" Value="44" />
    <Setter Property="MinimumWidthRequest"  Value="44" />
    <Setter Property="VisualStateManager.VisualStateGroups">
        <VisualStateGroupList>
            <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="Normal" />
                <VisualState x:Name="Focused">
                    <VisualState.Setters>
                        <!-- Subtle app-palette tint on focus -->
                        <Setter Property="BackgroundColor"
                                Value="{AppThemeBinding Light={StaticResource Secondary},
                                                        Dark={StaticResource Gray800}}" />
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name="Disabled">
                    <VisualState.Setters>
                        <Setter Property="TextColor"
                                Value="{AppThemeBinding Light={StaticResource Gray300},
                                                        Dark={StaticResource Gray600}}" />
                    </VisualState.Setters>
                </VisualState>
            </VisualStateGroup>
        </VisualStateGroupList>
    </Setter>
</Style>
```

---

## Editor Style Changes

```xaml
<Style TargetType="Editor">
    <Setter Property="TextColor"
            Value="{AppThemeBinding Light={StaticResource Black}, Dark={StaticResource White}}" />
    <!-- iOS: transparent; Android: light gray fill -->
    <Setter Property="BackgroundColor">
        <Setter.Value>
            <OnPlatform x:TypeArguments="Color">
                <On Platform="iOS"     Value="Transparent" />
                <On Platform="Android" Value="{StaticResource Gray100}" />
            </OnPlatform>
        </Setter.Value>
    </Setter>
    <Setter Property="FontFamily"  Value="OpenSansRegular" />
    <Setter Property="FontSize"    Value="14" />
    <Setter Property="PlaceholderColor"
            Value="{AppThemeBinding Light={StaticResource Gray400}, Dark={StaticResource Gray500}}" />
    <Setter Property="MinimumHeightRequest" Value="44" />
    <Setter Property="MinimumWidthRequest"  Value="44" />
    <Setter Property="VisualStateManager.VisualStateGroups">
        <VisualStateGroupList>
            <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="Normal" />
                <VisualState x:Name="Focused">
                    <VisualState.Setters>
                        <Setter Property="BackgroundColor"
                                Value="{AppThemeBinding Light={StaticResource Secondary},
                                                        Dark={StaticResource Gray800}}" />
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name="Disabled">
                    <VisualState.Setters>
                        <Setter Property="TextColor"
                                Value="{AppThemeBinding Light={StaticResource Gray300},
                                                        Dark={StaticResource Gray600}}" />
                    </VisualState.Setters>
                </VisualState>
            </VisualStateGroup>
        </VisualStateGroupList>
    </Setter>
</Style>
```

---

## Notes
- `Secondary` = `#E8EDE3` (light sage-beige) — provides a warm, on-brand focus tint in light mode
- `Gray800` = dark background — provides a slightly lighter dark-mode focus contrast
- The Android underline indicator is rendered natively and is not affected by these changes
- No inline overrides exist on any Entry/Editor in the views — all inherit the global style

---

## Verification
1. Open a prayer request on iOS (light mode) → tap the title field → background shifts to a soft sage-beige tint
2. Open same on iOS (dark mode) → tap title → background shifts to Gray800
3. Open on Android → no visual regression (gray fill, native underline on focus unchanged)
4. Placeholder text visible on all platforms with the new Gray400 value
