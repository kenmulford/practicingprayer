//
//  AppGroupWriter.swift
//  PrayerAppIntents
//
//  Slice 4 M3 — write pending-import.json to the App Group container so the
//  host MAUI app's AppGroupImportOrchestrator picks it up on Window.Activated.
//
//  Wire-format mirror of PrayerApp.ActionExtension/ShareViewController.cs:223-259.
//  Both extensions emit byte-identical NFC-normalized payloads so the host
//  parser is single-source-of-truth.
//
//  Token vocabulary and breadcrumb format match PrayerApp.Shared/AppGroupBreadcrumbLog.cs
//  exactly. DO NOT diverge — the main app truncates this log on pickup.
//

import Foundation
import Darwin

enum IntentError: Error, LocalizedError {
    case appGroupUnavailable
    case payloadTooLarge(byteCount: Int)
    case writeFailed(underlying: Error?)

    var errorDescription: String? {
        switch self {
        case .appGroupUnavailable:
            return "Couldn't reach Practicing Prayer's storage. Reinstall the app to fix sharing."
        case .payloadTooLarge:
            return "That prayer is too long to save from the share sheet. Try a shorter selection."
        case .writeFailed:
            return "Couldn't save the prayer right now. Try again."
        }
    }
}

struct AppGroupWriter {
    static let appGroupID = "group.com.multithreadedllc.prayercards"
    static let payloadFileName = "pending-import.json"
    static let logFileName = "import-log.txt"
    static let maxPayloadBytes = 256 * 1024

    func writePendingImport(rawText: String) throws {
        guard let containerURL = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: AppGroupWriter.appGroupID) else {
            // No container at all — can't even breadcrumb. Nothing to do but throw.
            throw IntentError.appGroupUnavailable
        }

        // Apply NFC at the bridge boundary. Notes can deliver NFD; .NET preserves
        // bytes verbatim. Without normalization here, "é" composed and "é"
        // decomposed produce two PrayerRequest records (Slice 4 plan QA bug 1).
        // The C# Share Extension applies the same normalization symmetrically.
        let normalized = rawText.precomposedStringWithCanonicalMapping
        let byteCount = normalized.utf8.count

        if byteCount > AppGroupWriter.maxPayloadBytes {
            appendBreadcrumb(containerURL: containerURL, byteCount: -1, token: "io-fail")
            throw IntentError.payloadTooLarge(byteCount: byteCount)
        }

        let payload = WirePayload(raw: normalized, ts: Self.iso8601String())
        let json: Data
        do {
            let encoder = JSONEncoder()
            // Stable byte output for the golden fixture; host parser is order-
            // insensitive (System.Text.Json source-gen matches keys by name)
            // so determinism is for fixture stability only.
            encoder.outputFormatting = [.sortedKeys]
            json = try encoder.encode(payload)
        } catch {
            appendBreadcrumb(containerURL: containerURL, byteCount: -1, token: "io-fail")
            throw IntentError.writeFailed(underlying: error)
        }

        let payloadURL = containerURL.appendingPathComponent(AppGroupWriter.payloadFileName)
        do {
            // .atomic = sibling temp + fsync + rename. Mirrors C#'s NSData.Save(url, atomically: true).
            // A crash mid-write leaves the prior file intact.
            try json.write(to: payloadURL, options: [.atomic])
        } catch {
            appendBreadcrumb(containerURL: containerURL, byteCount: -1, token: "io-fail")
            throw IntentError.writeFailed(underlying: error)
        }

        appendBreadcrumb(containerURL: containerURL, byteCount: json.count, token: "write-ok")
    }

    // MARK: - Internals

    private struct WirePayload: Encodable {
        let raw: String
        let ts: String
    }

    private static func iso8601String() -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        return formatter.string(from: Date())
    }

    /// Appends one line to import-log.txt using POSIX O_APPEND for cross-process
    /// atomicity with the main app's `FileMode.Append` writer (see
    /// PrayerApp.Shared/AppGroupBreadcrumbLog.cs:20-26 for the C# guarantee).
    /// Foundation's FileHandle does NOT expose O_APPEND — it truncates on
    /// `forWritingTo:` and `seekToEndOfFile()` is a TOCTOU race against the
    /// main app's truncation via `File.Move`. Use Darwin.open directly.
    /// Best-effort; failure to log must never break the import path.
    private func appendBreadcrumb(containerURL: URL, byteCount: Int, token: String) {
        let line = "\(Self.iso8601String()) \(byteCount < 0 ? "-" : String(byteCount)) \(token)\n"
        guard let bytes = line.data(using: .ascii) else { return }
        let logPath = containerURL.appendingPathComponent(AppGroupWriter.logFileName).path

        let fd = logPath.withCString { cPath in
            Darwin.open(cPath, O_WRONLY | O_APPEND | O_CREAT, 0o644)
        }
        guard fd >= 0 else { return }
        defer { Darwin.close(fd) }

        _ = bytes.withUnsafeBytes { buffer -> Int in
            guard let baseAddress = buffer.baseAddress else { return 0 }
            return Darwin.write(fd, baseAddress, buffer.count)
        }
    }
}
