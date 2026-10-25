"""Read-only capture, compatibility checks and address suggestions for game updates."""
import argparse
import ctypes
import hashlib
import json
import os
import re
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def image(path):
    from native_trace import PE
    return PE(path)


def verify(args):
    pe = image(args.image)
    manifest = json.loads(Path(args.manifest).read_text())
    failures = []
    for item in manifest['regions']:
        rva, size = int(item['rva'], 0), item['length']
        try:
            start = pe.off(rva)
            data = pe.d[start:start + size]
            actual = hashlib.sha256(data).hexdigest()
        except ValueError:
            actual = 'unmapped'
        if actual != item['sha256']:
            failures.append(dict(name=item['name'], rva=item['rva'], length=size, expected=item['sha256'], actual=actual))
    report = dict(image=Path(args.image).name, regions=len(manifest['regions']), failures=failures)
    print(json.dumps(report, indent=2))
    if failures:
        raise SystemExit(1)


def capture(args):
    if os.name != 'nt':
        raise SystemExit('Capture requires 64-bit Windows.')
    from ctypes import wintypes as w
    k = ctypes.WinDLL('kernel32', use_last_error=True)

    class Module(ctypes.Structure):
        _fields_ = [('size', w.DWORD), ('moduleId', w.DWORD), ('pid', w.DWORD), ('globalUses', w.DWORD),
                    ('processUses', w.DWORD), ('base', ctypes.c_void_p), ('imageSize', w.DWORD),
                    ('handle', w.HMODULE), ('name', w.WCHAR * 256), ('path', w.WCHAR * 260)]

    k.CreateToolhelp32Snapshot.argtypes = [w.DWORD, w.DWORD]
    k.CreateToolhelp32Snapshot.restype = w.HANDLE
    k.Module32FirstW.argtypes = [w.HANDLE, ctypes.POINTER(Module)]
    k.Module32NextW.argtypes = [w.HANDLE, ctypes.POINTER(Module)]
    k.CloseHandle.argtypes = [w.HANDLE]
    k.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
    k.OpenProcess.restype = w.HANDLE
    k.ReadProcessMemory.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
    snapshot = k.CreateToolhelp32Snapshot(0x18, args.pid)
    if snapshot == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    module = Module()
    module.size = ctypes.sizeof(module)
    found = None
    try:
        more = k.Module32FirstW(snapshot, ctypes.byref(module))
        while more:
            if module.name.lower() == 'collegefb27.exe':
                found = (module.base, module.imageSize)
                break
            more = k.Module32NextW(snapshot, ctypes.byref(module))
    finally:
        k.CloseHandle(snapshot)
    if not found:
        raise SystemExit('CollegeFB27.exe module was not found in that PID.')
    base, module_size = found
    handle = k.OpenProcess(0x1010, False, args.pid)  # query + read only
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())

    def read(address, length):
        buffer = ctypes.create_string_buffer(length)
        count = ctypes.c_size_t()
        if not k.ReadProcessMemory(handle, address, buffer, length, ctypes.byref(count)) or count.value != length:
            raise OSError(f'Could not read 0x{address:x} ({length} bytes). Wait until the game has loaded.')
        return buffer.raw

    destination = Path(args.output)
    if destination.exists():
        k.CloseHandle(handle)
        raise SystemExit('Output already exists; choose a new filename.')
    try:
        header = read(base, 4096)
        offset = struct.unpack_from('<I', header, 60)[0]
        if header[:2] != b'MZ' or header[offset:offset + 4] != b'PE\0\0':
            raise ValueError('Unexpected module header.')
        optional = offset + 24
        section_count = struct.unpack_from('<H', header, offset + 6)[0]
        section_table = optional + struct.unpack_from('<H', header, offset + 20)[0]
        header_size = struct.unpack_from('<I', header, optional + 60)[0]
        if header_size > 65536 or section_count > 96 or section_table + section_count * 40 > header_size:
            raise ValueError('Unexpected PE layout.')
        header = read(base, header_size)
        sections = []
        total = header_size
        for index in range(section_count):
            at = section_table + index * 40
            name = header[at:at + 8].rstrip(b'\0').decode('ascii')
            virtual_size, rva, size, raw = struct.unpack_from('<IIII', header, at + 8)
            if raw + size > 512 * 1024 * 1024 or rva + size > module_size + 4096:
                raise ValueError('Section outside the expected image bounds.')
            total = max(total, raw + size)
            if name in {'.text', '.rdata', '.pdata'} and size:
                sections.append((name, rva, size, raw))
        output = bytearray(total)
        output[:len(header)] = header
        for name, rva, size, raw in sections:
            for pos in range(0, size, 1024 * 1024):
                count = min(1024 * 1024, size - pos)
                output[raw + pos:raw + pos + count] = read(base + rva + pos, count)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(output)
        print(json.dumps(dict(output=str(destination), pid=args.pid, sections=[s[0] for s in sections], bytes=total)))
    finally:
        k.CloseHandle(handle)


def map_functions(args):
    from capstone import Cs, CS_ARCH_X86, CS_MODE_64
    old, new = image(args.baseline), image(args.candidate)
    manifest = json.loads(Path(args.manifest).read_text())
    targets = sorted({int(item['rva'], 0) for item in manifest['regions']} |
                     {int(value, 0) for value in manifest['references']})
    cs = Cs(CS_ARCH_X86, CS_MODE_64)
    cs.detail = True
    text = next(section for section in new.sections if section[0] == '.text')
    results, functions = [], {}
    derived = {}
    for rva in targets:
        section = next((s for s in old.sections if s[1] <= rva < s[1] + s[2]), None)
        if not section or section[0] != '.text':
            continue
        start, end = old.function(rva)
        if start not in functions:
            matches, used = [], 0
            old_instructions = []
            for limit in (32, 64, 128, 256, 512):
                instructions = list(cs.disasm(old.d[old.off(start):old.off(start) + min(limit, end - start)], start))
                pattern = bytearray()
                mask = []
                for instruction in instructions:
                    blob = instruction.bytes
                    excluded = set()
                    if 'rip' in instruction.op_str:
                        excluded.update(range(instruction.disp_offset, instruction.disp_offset + instruction.disp_size))
                    if instruction.mnemonic.startswith('j') or instruction.mnemonic == 'call':
                        excluded.update(range(instruction.imm_offset, instruction.imm_offset + instruction.imm_size))
                    pattern.extend(blob)
                    mask.extend(index not in excluded for index in range(len(blob)))
                if len(pattern) < 5:
                    break
                expression = b''.join(re.escape(bytes([value])) if keep else b'.' for value, keep in zip(pattern, mask))
                search = re.compile(expression, re.S)
                lo, hi = max(text[1], start - args.window), min(text[1] + text[4], start + args.window)
                if lo >= hi:
                    matches = []
                else:
                    raw = text[3] + lo - text[1]
                    matches = [lo + match.start() for match in search.finditer(new.d[raw:raw + hi - lo])]
                used, old_instructions = len(pattern), instructions
                if len(matches) == 1:
                    break
            functions[start] = (matches, used)
            if len(matches) == 1:
                mapped = matches[0]
                new_instructions = list(cs.disasm(new.d[new.off(mapped):new.off(mapped) + used], mapped))
                for before, after in zip(old_instructions, new_instructions):
                    if before.mnemonic != after.mnemonic or before.size != after.size:
                        break
                    if 'rip' in before.op_str:
                        left, right = before.address + before.size + before.disp, after.address + after.size + after.disp
                    elif before.mnemonic in ('call', 'jmp') and before.imm_size:
                        left, right = before.operands[0].imm, after.operands[0].imm
                    else:
                        continue
                    if 0x1000 <= left < 0x13000000 and 0x1000 <= right < 0x13000000:
                        derived.setdefault(hex(left), set()).add(hex(right))
        matches, used = functions[start]
        results.append(dict(old=hex(rva), function=hex(start), signatureBytes=used,
                            candidates=[hex(address + rva - start) for address in matches],
                            status='review unique match' if len(matches) == 1 else 'ambiguous' if matches else 'not found'))
    report = dict(baseline=Path(args.baseline).name, candidate=Path(args.candidate).name,
                  warning='Suggestions only. Review instructions, globals, vtables and jump tables; do not apply a blanket RVA delta.',
                  functions=results, references={key: sorted(value) for key, value in derived.items()})
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    Path(args.output).write_text(json.dumps(report, indent=2) + '\n')
    print(f'Wrote {len(results)} address suggestions to {args.output}')


def fixtures(args):
    pe = image(args.image)
    manifest = json.loads(Path(args.manifest).read_text())
    chunks = []
    for item in manifest['regions']:
        at = pe.off(int(item['rva'], 0))
        data = pe.d[at:at + item['length']]
        if hashlib.sha256(data).hexdigest() != item['sha256']:
            raise SystemExit(f'Baseline mismatch at {item["rva"]}; fixtures were not written.')
        chunks.append(data)
    (ROOT / 'Tools/compatibility-reference.bin').write_bytes(b''.join(chunks))
    calls = b''.join(pe.d[pe.off(rva):pe.off(rva) + 5] for rva in [0x35ff6ef, 0x7b7e964])
    (ROOT / 'Tools/callsheet-call-reference.bin').write_bytes(calls)
    print('Generated local compatibility fixtures. These files are gitignored.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    command = sub.add_parser('capture')
    command.add_argument('--pid', type=int, required=True)
    command.add_argument('--output', required=True)
    command.set_defaults(run=capture)
    for name, function in [('verify', verify), ('fixtures', fixtures)]:
        command = sub.add_parser(name)
        command.add_argument('image')
        command.add_argument('--manifest', default=str(ROOT / 'docs/native-layout.json'))
        command.set_defaults(run=function)
    command = sub.add_parser('map')
    command.add_argument('baseline')
    command.add_argument('candidate')
    command.add_argument('--manifest', default=str(ROOT / 'docs/native-layout.json'))
    command.add_argument('--output', required=True)
    command.add_argument('--window', type=lambda value: int(value, 0), default=0x100000)
    command.set_defaults(run=map_functions)
    command = sub.add_parser('disasm')
    command.add_argument('image')
    command.add_argument('start', type=lambda value: int(value, 0))
    command.add_argument('--length', type=lambda value: int(value, 0), default=0x100)
    command.set_defaults(run=lambda args: print(image(args.image).disasm(args.start, args.start + args.length)))
    args = parser.parse_args()
    args.run(args)


if __name__ == '__main__':
    main()
