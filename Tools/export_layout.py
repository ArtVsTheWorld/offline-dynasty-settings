"""Export native contracts and referenced RVAs from the current C# source."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def layout():
    files = sorted(list((ROOT / 'Plugin').glob('*.cs')) + list((ROOT / 'Shared').rglob('*.cs')))
    regions = []
    references = {}
    for path in files:
        source = path.read_text(encoding='utf-8-sig')
        relative = path.relative_to(ROOT).as_posix()
        # Settings retains its older layout for reference. Export only the current layout.
        scan = source
        if path.name == 'DynastyPlayClockAction.cs':
            scan = ''  # Current combined layout comes from GameLayouts.cs.
        if path.name == 'GameLayouts.cs':
            scan = source[source.index('new Layout("October 13, 2026 update"'):]
        named = r'new Region\("([^"]+)",\s*(0x[\da-f]+),\s*(0x[\da-f]+|\d+),\s*"([\da-f]{64})"\)'
        plain = r'new Region\((0x[\da-f]+),\s*(0x[\da-f]+|\d+),\s*"([\da-f]{64})"\)'
        for label, rva, size, digest in re.findall(named, scan, re.I):
            regions.append(dict(name=label, source=relative, rva=rva.lower(), length=int(size, 0), sha256=digest.lower()))
        for rva, size, digest in re.findall(plain, scan, re.I):
            regions.append(dict(name=path.stem, source=relative, rva=rva.lower(), length=int(size, 0), sha256=digest.lower()))
        for line, text in enumerate(source.splitlines(), 1):
            for match in re.finditer(r'\b0x([\da-f]+)\b', text, re.I):
                value = int(match.group(1), 16)
                if 0x100000 <= value < 0x13000000:
                    references.setdefault(hex(value), []).append(f'{relative}:{line}')
    defensive = ROOT / 'Plugin/DefensivePlayClock.cs'
    if defensive.exists():
        source = defensive.read_text()
        for field, array in [('DefensiveTimerRva', 'DefensiveTimerOriginal'), ('DefensiveGateRva', 'DefensiveGateOriginal')]:
            rva = re.search(field + r'=(0x[\da-f]+)', source, re.I).group(1)
            data = bytes.fromhex(re.search(array + r'=Hex\("([\da-f]+)"\)', source, re.I).group(1))
            regions.append(dict(name=field, source='Plugin/DefensivePlayClock.cs', rva=rva, length=len(data), sha256=hashlib.sha256(data).hexdigest()))
        data = bytes.fromhex('c5fa10052435db04c3')
        regions.append(dict(name='Defensive duration getter', source='Plugin/DefensivePlayClock.cs', rva='0x5ff26a0', length=len(data), sha256=hashlib.sha256(data).hexdigest()))
    version = re.search(r'PluginVersion\("([^"]+)"\)', (ROOT / 'Plugin/AssemblyInfo.cs').read_text()).group(1)
    return dict(pluginVersion=version, layout='October 13, 2026', wholeExeHashGate=False,
                regions=regions, references=dict(sorted(references.items(), key=lambda item: int(item[0], 16))))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='Fail if the committed manifest differs from source.')
    args = parser.parse_args()
    destination = ROOT / 'docs/native-layout.json'
    result = layout()
    if args.check:
        if not destination.exists() or json.loads(destination.read_text()) != result:
            raise SystemExit('Native layout changed. Run python Tools/export_layout.py and review the diff.')
        print(f'PASS native manifest: {len(result["regions"])} regions, {len(result["references"])} referenced RVAs')
    else:
        destination.write_text(json.dumps(result, indent=2) + '\n')
        print(destination)


if __name__ == '__main__':
    main()
