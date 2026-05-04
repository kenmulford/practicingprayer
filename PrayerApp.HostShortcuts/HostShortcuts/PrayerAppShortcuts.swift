//
//  PrayerAppShortcuts.swift
//  HostShortcuts (metadata-only static library — Slice 4 F-27)
//
//  Single AppShortcutsProvider for the app, declared in the host target so
//  WFInterchangeAppRegistry can resolve com.multithreadedllc.prayercards.
//  Apple docs: "You should have only one AppShortcutsProvider for your app."
//  The corresponding declaration in PrayerApp.AppIntents has been removed.
//

import AppIntents

struct PrayerAppShortcuts: AppShortcutsProvider {
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: ImportTextIntent(),
            phrases: [
                "Save to \(.applicationName)",
                "New prayer card in \(.applicationName)",
                "Add a prayer to \(.applicationName)"
            ],
            shortTitle: "Save to Prayer",
            systemImageName: "hands.and.sparkles.fill")
    }
}
