import re

with open("C:/repos/PrayerApp/uitest-phase1.log", "rb") as f:
    raw = f.read()
content = raw.decode("utf-16-le", errors="ignore")

blocks = re.split(
    r"(?=\[xUnit\.net [^\]]+\]\s+PrayerApp\.UITests\.Tests\.[^\s]+\s+\[FAIL\])", content
)

rows = []
for b in blocks:
    hdr = re.match(
        r"\[xUnit\.net [^\]]+\]\s+PrayerApp\.UITests\.Tests\.([^\s]+)\s+\[FAIL\]", b
    )
    if not hdr:
        continue
    name = hdr.group(1)
    em = re.search(r"Error Message:\s*(.*?)(?=Stack Trace:|$)", b, re.DOTALL)
    err_raw = em.group(1).strip() if em else "<none>"
    err = re.sub(r"\s+", " ", err_raw)[:200]
    tl = re.search(
        r"at PrayerApp\.UITests\.Tests\.\S+\(\) in \S+\\([^\\]+\.cs):line (\d+)", b
    )
    hl_matches = re.findall(r"at PrayerApp\.UITests\.Helpers\.AppExtensions\.(\w+)", b)
    helper = hl_matches[-1] if hl_matches else "-"
    test_loc = f"{tl.group(1)}:{tl.group(2)}" if tl else "?"
    rows.append((name, test_loc, helper, err))

seen = {}
for n, t, h, e in rows:
    seen.setdefault(n, (t, h, e))

for n in sorted(seen):
    t, h, e = seen[n]
    print(f"{n}")
    print(f"  loc={t}  helper={h}")
    print(f"  err={e}")
    print()
