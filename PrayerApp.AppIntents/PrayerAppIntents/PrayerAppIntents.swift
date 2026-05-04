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
    // Slice 4 plan UX-36: label disambiguation. The legacy Action Extension's
    // share-sheet entry already shows as "Practicing Prayer" (CFBundleDisplayName
    // in PrayerApp.ActionExtension/Info.plist). The App Intent must have a
    // visibly different label so the user can tell them apart on iOS 18+ where
    // both surface in the share sheet. Action-shaped "Save to Prayer" pairs
    // naturally with the app-shaped "Practicing Prayer" tile.
    static let title: LocalizedStringResource = "Save to Prayer"
    static let description = IntentDescription(
        "Save shared text as a new prayer card in Practicing Prayer.")

    // Load-bearing: tells iOS to foreground the host app when this intent runs.
    static let openAppWhenRun: Bool = true

    // requestValueDialog only fires when the parameter is unresolved. iOS 18+
    // share-sheet auto-promotion populates `sharedText` from the selected text
    // via the system's text-provider activation BEFORE perform runs, so the
    // dialog is bypassed in that path. Shortcuts.app tile tap and Siri voice
    // (when the user doesn't dictate text) hit this prompt.
    //
    // Avoid `inputConnectionBehavior: .connectToPreviousIntentResult` here —
    // empirically it short-circuits the prompt path when there is no upstream
    // action (standalone tile tap), causing LNContextErrorDomain Code=2001.
    @Parameter(
        title: "Shared Text",
        description: "Text to save as a prayer card.",
        requestValueDialog: IntentDialog("What text should I save?")
    )
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

// AppShortcutsProvider lives in the host target (PrayerApp.HostShortcuts) per
// Apple docs: "You should have only one AppShortcutsProvider for your app."
// F-27 moves it there so the host's Metadata.appintents/ registers
// com.multithreadedllc.prayercards with WFInterchangeAppRegistry. This bundle
// retains ImportTextIntent as the canonical perform() implementation; iOS
// dispatches to the bundle that owns the type identifier.
