//
//  ImportTextIntent.swift
//  HostShortcuts (Slice 4 F-27)
//
//  Source-duplicated from PrayerApp.AppIntents/PrayerAppIntents/PrayerAppIntents.swift
//  per Apple DTS's documented workaround for FB14857658 (forums thread 759160).
//  When AppShortcutsProvider lives in the host target, the AppShortcut's
//  declared `intent: ImportTextIntent()` resolves to *this* host-binary type
//  via Swift's mangled name, so iOS dispatches perform() to the HOST'S process
//  on Shortcuts.app tile invocation. The host therefore needs the same real
//  perform() implementation as the extension, not a stub. Byte-identical
//  declarations across both targets keep metadata extraction consistent.
//

import AppIntents
import Foundation

struct ImportTextIntent: AppIntent {
    static let title: LocalizedStringResource = "Save to Prayer"
    static let description = IntentDescription(
        "Save shared text as a new prayer card in Practicing Prayer.")

    static let openAppWhenRun: Bool = true

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
