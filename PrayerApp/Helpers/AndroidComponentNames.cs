namespace PrayerApp.Helpers;

/// <summary>
/// Canonical Android component names shared between the app's host-side code and
/// the UITest harness.
/// </summary>
/// <remarks>
/// .NET-for-Android registers managed activities under a JNI-mangled class name
/// of the form <c>crc&lt;hash&gt;.&lt;TypeName&gt;</c>, where the hash is derived
/// from the assembly + namespace. Any external code that needs to address the
/// activity by component name (debug-only shims, UITest `am start -n` calls,
/// Appium driver capabilities) must use the JNI-mangled name, not the C# fully
/// qualified name.
///
/// Centralising the constant here eliminates the maintenance hazard of two
/// independent string literals drifting apart when the toolchain regenerates
/// the CRC hash (e.g. on a MAUI version bump or namespace refactor).
///
/// This file is non-platform (no Android types referenced) so it compiles
/// cleanly under all target frameworks. It is linked into
/// <c>PrayerApp.UITests.csproj</c> so the same const is visible to test code.
/// </remarks>
public static class AndroidComponentNames
{
    /// <summary>
    /// JNI-mangled class name for <c>PrayerApp.MainActivity</c>. Verified by:
    /// <c>adb shell dumpsys package com.multithreadedllc.prayercards | grep MainActivity</c>.
    /// If a MAUI / .NET-for-Android toolchain bump changes the CRC, update this
    /// single constant — both the app-side shim and the UITest harness pick it up.
    /// </summary>
    public const string MainActivity = "crc6425c6d21f3599989c.MainActivity";
}
