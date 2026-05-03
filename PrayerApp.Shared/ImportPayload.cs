using System.Text.Json.Serialization;

namespace PrayerApp.Shared;

/// <summary>
/// Wire format for the pending-import.json payload written by the iOS Share
/// Extension and read by the main app. AOT-clean via the source-gen context
/// below — do NOT call JsonSerializer.Serialize&lt;T&gt;(value) without the
/// context; that path uses reflection and trips IL2026/IL3050 under AOT.
/// </summary>
public sealed record ImportPayload(string Raw, string Ts);

/// <summary>
/// Source-generated JsonSerializerContext for ImportPayload.
/// Use ImportPayloadJsonContext.Default.ImportPayload at every serialize and
/// deserialize call site. PropertyNamingPolicy=CamelCase mirrors the JSON
/// wire format produced by the original anonymous-type serialization in
/// slice 3b ({"raw": "...", "ts": "..."}).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ImportPayload))]
public partial class ImportPayloadJsonContext : JsonSerializerContext
{
}
