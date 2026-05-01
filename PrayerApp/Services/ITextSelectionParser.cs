using PrayerApp.Models;

namespace PrayerApp.Services;

public interface ITextSelectionParser
{
    ParseResult Parse(string rawText);
}
