# TD-9 — Dark Mode Color Audit
## Static `{StaticResource}` color usages that bypass `AppThemeBinding`

**Completed:** 2026-03-17
**Method:** `grep` across all XAML files for TextColor/BackgroundColor/Stroke using
`{StaticResource ...}` without an `AppThemeBinding` wrapper; each hit reviewed for
context (element type, surrounding background, dark-mode behavior).

---

## Summary

Most hits are intentional fixed colors (semantic action buttons, always-dark or always-light
surfaces). Two are genuine low-contrast risks in dark mode, one is borderline.

---

## ❌ Genuine contrast risks — fix in a follow-up pass

### 1. "Add prayer" link inside card body — PrayerCardsPage.xaml:151
```xml
<Label Text="+ Add prayer" TextColor="{StaticResource Primary}" FontAttributes="Italic" />
```
- **Dark mode background:** Card uses `PrayerCardBorder` → `BackgroundColor = DeepEarth (#2D3428)` in dark
- **Primary** = `#6B7D5A` on `DeepEarth #2D3428` → contrast ratio ≈ **2.85:1** (WCAG AA fail)
- **Fix:** `TextColor="{AppThemeBinding Light={StaticResource Primary}, Dark={StaticResource PrimaryDark}}"`
- `PrimaryDark = #A8B896` on DeepEarth → contrast ≈ **5.2:1** ✅

### 2. Privacy Policy link — Settings.xaml:80
```xml
<Label Text="Privacy Policy" TextColor="{StaticResource Primary}" TextDecorations="Underline" />
```
- **Dark mode background:** Page background is dark (OS default or OffBlack)
- **Primary** = `#6B7D5A` on dark page → contrast ratio ≈ **3.18:1** (below AA for normal 14pt text)
- **Fix:** Same `AppThemeBinding Light=Primary, Dark=PrimaryDark` pattern

---

## ⚠️ Borderline — flag, watch in practice

### 3. Answered date badge — PrayerDetailPage.xaml:39
```xml
<Label TextColor="{StaticResource SuccessGreen}" FontAttributes="Bold" FontSize="13" />
```
- `SuccessGreen = #4CAF50` on dark page background → contrast ≈ **4.08:1**
- Below WCAG AA (4.5:1) for normal 13pt text; passes large-text threshold (3:1)
- **Mitigated by** `FontAttributes="Bold"` — bold text has higher effective contrast
- **Fix if desired:** `AppThemeBinding Light=SuccessGreen, Dark=<brighter green ~#66BB6A>`

---

## ✅ Intentionally fixed — not issues

| File | Usage | Reason OK |
|------|-------|-----------|
| OnboardingBanner.xaml | `Gray200`/`Gray300` text | Always on `Tertiary` (#3F4A34) dark bg — light gray on dark green is fine in both modes |
| OnboardingBanner.xaml | `WarmGold` step indicator | Accent on always-dark banner — intentional |
| OnboardingCompletePopup.xaml | `Secondary` bg, `Tertiary`/`Gray500` text | Popup is always-light themed (`Secondary` bg = #E8EDE3); contrast within popup is correct |
| OnboardingWelcomePopup.xaml | Same as above | Same reasoning |
| PrayerCardsPage.xaml (swipe) | `WarmGold`/`Primary`/`DangerRed` bg | Swipe action buttons — semantic action colors, brand-intentional |
| PrayerListPage.xaml (toggles) | `Primary` bg on selected state | Selected toggle button — intentional brand state indicator |
| PrayerTimePage.xaml:134 | `Primary` bg on button | CTA button — intentional |
| MainPage.xaml:21 | `Primary` bg | Hero/action button — intentional |
| Styles.xaml | `DangerRed`/`White`, `WarmGold`, `SuccessGreen` | Named semantic styles — intentional |
| All files | `BoolToMutedColor` converter | Already theme-aware (reads `Gray400`/`OffBlack` from Resources at runtime) |

---

## Recommended fix order

1. **Settings.xaml:80** — quick one-liner, high visibility (Privacy Policy is always rendered)
2. **PrayerCardsPage.xaml:151** — inside card body, appears on every card
3. **PrayerDetailPage.xaml:39** — lower priority; bold mitigates, only visible on answered prayers
