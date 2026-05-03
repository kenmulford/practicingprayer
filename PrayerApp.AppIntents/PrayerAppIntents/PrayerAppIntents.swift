//
//  PrayerAppIntents.swift
//  PrayerAppIntents
//
//  Slice 4 M1 — App Intent for share-sheet auto-launch on iOS 18+.
//  iOS 18+ auto-promotes AppIntents that declare a String @Parameter +
//  openAppWhenRun=true into the share menu. Tapping the entry foregrounds
//  the host app (PrayerApp) with the shared text. M3 wires perform() to
//  write the payload to the shared App Group container so the host app's
//  AppGroupImportOrchestrator can pick it up on activation.
//
//  References:
//   - https://developer.apple.com/documentation/appintents/appintent/openappwhenrun
//   - https://developer.apple.com/documentation/appintents/appshortcutsprovider
//   - WWDC25 §244 "Get to know App Intents"

import AppIntents
import Foundation

struct ImportTextIntent: AppIntent {
    static let title: LocalizedStringResource = "Save to Practicing Prayer"
    static let description = IntentDescription(
        "Save shared text as a new prayer card in Practicing Prayer.")

    // Load-bearing: tells iOS to foreground the host app when this intent runs.
    static let openAppWhenRun: Bool = true

    @Parameter(title: "Shared Text",
               inputConnectionBehavior: .connectToPreviousIntentResult)
    var sharedText: String

    static var parameterSummary: some ParameterSummary {
        Summary("Save \(\.$sharedText) to Practicing Prayer")
    }

    @MainActor
    func perform() async throws -> some IntentResult {
        try AppGroupWriter().writePendingImport(rawText: sharedText)
        return .result()
    }
}

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
