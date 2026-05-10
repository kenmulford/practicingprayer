internal static class UnicodeChars
{
    public const char LeftSingleQuote  = '\u2018';   // '
    public const char RightSingleQuote = '\u2019';   // '
    public const char LeftDoubleQuote  = '\u201C';   // "
    public const char RightDoubleQuote = '\u201D';   // "
    public const char EnDash           = '\u2013';
    public const char EmDash           = '\u2014';
    public const char Ellipsis         = '\u2026';
    public const char NonBreakingSpace = '\u00A0';
    public const char ZeroWidthSpace   = '\u200B';
    public const char ByteOrderMark    = '\uFEFF';
    public const char Bullet           = '\u2022';
    // …add as we discover regressions
}
