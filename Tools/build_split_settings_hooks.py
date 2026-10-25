"""Build and disassemble the split cooldown/limit hook templates.

Absolute operands are relocated by the C# loader to the running image/remote
allocation. Internal relative branches are resolved here, then checked with
Capstone. No process access or game-file changes occur during generation.
"""
import hashlib
import json
import struct
from pathlib import Path
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from native_trace import PE

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Shared/Playcall'
EVIDENCE = ROOT / 'Evidence/update-20261001'
TABLE = 0x2000
CAPACITY = 1024
SEQUENCE = 0x1800
OFF_LABEL = 0x1900
DEF_LABEL = 0x1940
DEF_DESCRIPTION = 0x1980
OFF_LIMIT_LABEL = 0x1a00
DEF_LIMIT_LABEL = 0x1a40
DEF_LIMIT_DESCRIPTION = 0x1a80
ENTRY_SIZE = 12
LOOKUP = 0xc00


class Assembler:
    def __init__(self, offset):
        self.offset = offset
        self.data = bytearray()
        self.labels = {}
        self.branches = []
        self.relocations = []

    def emit(self, text):
        self.data.extend(bytes.fromhex(text))

    def label(self, name):
        self.labels[name] = len(self.data) + self.offset

    def branch(self, opcode, target):
        self.emit(opcode)
        pos = len(self.data)
        self.data.extend(bytes(4))
        self.branches.append((pos, target))

    def absolute(self, opcode, kind, offset):
        self.emit(opcode)
        self.relocations.append((self.offset + len(self.data), kind, offset))
        self.data.extend(bytes(8))

    def jump(self, rva):
        self.absolute('ff2500000000', 'image', rva)

    def call_image(self, rva):
        self.absolute('48b8', 'image', rva)
        self.emit('ffd0')

    def finish(self):
        for pos, target in self.branches:
            dest = self.labels[target] if isinstance(target, str) else target
            struct.pack_into('<i', self.data, pos, dest - (self.offset + pos + 4))
        return self.data


def build():
    pe = PE(str(EVIDENCE / 'live-code.pe.bin'))
    out = bytearray(TABLE + CAPACITY * ENTRY_SIZE)
    out[0x1804:0x1808] = b'SPL2'
    relocations = []
    sections = []

    def add(name, asm):
        data = asm.finish()
        if len(data) > 0x600:
            raise ValueError('hook exceeded its reserved region')
        out[asm.offset:asm.offset + len(data)] = data
        relocations.extend(asm.relocations)
        sections.append((name, asm.offset, len(data)))

    # Leaf lookup: EDX league ID; RAX entry pointer or zero (unconfigured).
    # Only RAX/R10/R11 and flags are clobbered. A published nonzero ID is
    # written after its value, so a reader never observes a partial new entry.
    a = Assembler(LOOKUP)
    a.emit('85d2')
    a.branch('0f84', 'missing')
    a.absolute('49ba', 'remote', TABLE)
    a.emit('41bb' + struct.pack('<I', CAPACITY).hex())
    a.label('loop')
    a.emit('413912')
    a.branch('0f84', 'found')
    a.emit('4983c20c41ffcb')
    a.branch('0f85', 'loop')
    a.label('missing')
    a.emit('33c0c3')
    a.label('found')
    a.emit('498bc2c3')
    add('lookup', a)

    # Replace both native calls with four rows only for the
    # offline response (AL == 0). EBX remains the native dynamic row index.
    # Stack clones borrow option pointers only for synchronous serialization;
    # the original descriptor is never changed or freed by this extension.
    a = Assembler(0)
    a.emit('84c0')
    a.branch('0f85', 'online')
    a.emit('4881ec0003000048897c24304889742438')
    a.emit('488b8718040000488b80400100004805303a00004989c2')
    for offset in (0x40, 0xf0):
        a.emit('488dbc24' + struct.pack('<I', offset).hex())
        a.emit('498bf2b916000000f348a5')
    a.emit('4981c2b0000000')  # Native limit descriptor immediately follows CD.
    for offset in (0x1a0, 0x250):
        a.emit('488dbc24' + struct.pack('<I', offset).hex())
        a.emit('498bf2b916000000f348a5')
    a.emit('488b7c2430488b742438')
    a.absolute('48b8', 'remote', OFF_LABEL)
    a.emit('4889442450')
    a.absolute('48b8', 'remote', DEF_LABEL)
    a.emit('4889842400010000')
    a.absolute('48b8', 'remote', DEF_DESCRIPTION)
    a.emit('4889842418010000')
    a.absolute('48b8', 'remote', OFF_LIMIT_LABEL)
    a.emit('48898424b0010000')
    a.absolute('48b8', 'remote', DEF_LIMIT_LABEL)
    a.emit('4889842460020000')
    a.absolute('48b8', 'remote', DEF_LIMIT_DESCRIPTION)
    a.emit('4889842478020000')
    a.absolute('49ba', 'image', 0xf40e998)
    a.emit('418b12')
    a.branch('e8', LOOKUP)
    a.emit('4885c0')
    a.branch('0f84', 'inherited')
    a.emit('8b500483faff')
    a.branch('0f84', 'limit_value')
    a.emit('8994244c010000')  # defensive CD clone +0x5c
    a.label('limit_value')
    a.emit('8b500883faff')
    a.branch('0f84', 'inherited')
    a.emit('899424ac020000')  # defensive limit clone +0x5c
    a.label('inherited')
    for offset, key in ((0x40, 0xa), (0xf0, 0xf0), (0x1a0, 0xb), (0x250, 0xf1)):
        a.emit('4c8d8c24' + struct.pack('<I', offset).hex())
        a.emit('41b8' + struct.pack('<I', key).hex())
        a.emit('895c2420488bd6488bcf')
        a.call_image(0x799bfb0)
        a.emit('ffc3')
    a.emit('4881c400030000')
    a.jump(0x7993f38)
    a.label('online')
    a.emit(pe.d[pe.off(0x7993ee0):pe.off(0x7993ef0)].hex())
    a.jump(0x7993ef0)
    add('settings rows', a)

    # Intercept only private keys 0xf0/0xf1. All normal keys replay the original
    # dispatch including its relocated conditional branch. No save field is
    # repurposed for the defense setting.
    a = Assembler(0x600)
    a.emit('3df0000000')
    a.branch('0f84', 'cooldown')
    a.emit('3df1000000')
    a.branch('0f85', 'normal')
    a.branch('e9', 'limit')
    for name, maximum, field in [('cooldown', 10, 4), ('limit', 20, 8)]:
        a.label(name)
        a.emit('488b06')
        a.absolute('49b8', 'image', 0xac87ec0)
        a.emit('418bd7488bceff5018')
        a.emit('83f8' + bytes([maximum]).hex())
        a.branch('0f87', 'done')
        a.emit('8bc8')
        a.absolute('49ba', 'image', 0xf40e998)
        a.emit('418b1285d2')
        a.branch('0f84', 'done')
        a.absolute('49ba', 'remote', TABLE)
        a.emit('41bb' + struct.pack('<I', CAPACITY).hex())
        a.label(name + '_slot')
        a.emit('413912')
        a.branch('0f84', name + '_store')
        a.emit('41833a00')
        a.branch('0f84', name + '_new')
        a.emit('4983c20c41ffcb')
        a.branch('0f85', name + '_slot')
        a.branch('e9', 'done')
        a.label(name + '_new')
        a.emit('41c74204ffffffff41c74208ffffffff')
        a.emit('41894a' + bytes([field]).hex())
        a.emit('418912')  # Publish ID only after both fields are initialized.
        a.branch('e9', 'publish')
        a.label(name + '_store')
        a.emit('41894a' + bytes([field]).hex())
        a.branch('e9', 'publish')
    a.label('publish')
    a.absolute('49ba', 'remote', SEQUENCE)
    a.emit('f041ff02')
    a.label('done')
    a.jump(0x79a4a1d)
    a.label('normal')
    # Runtime relocation chooses the verified Play Clock writer, when present,
    # or this hook's original native dispatch. EAX is preserved for chaining.
    native_dispatch = a.offset + len(a.data) + 14
    a.absolute('ff2500000000', 'writer', native_dispatch)
    a.emit('83c0fe3ddb000000')
    a.branch('0f86', 'dispatch')
    a.jump(0x79a4a1d)
    a.label('dispatch')
    a.jump(0x79a2271)
    add('settings writer', a)

    # Defense getter: substitute independently configured cooldown/limit
    # values only in the verified local offline Dynasty match context. The
    # settings response is released during matches and cannot gate gameplay.
    # Stored -1 inherits native; configured zero becomes native disabled -1.
    a = Assembler(0x900)
    a.absolute('49ba', 'image', 0xd89d818)
    a.emit('41833a0e')
    a.branch('0f85', 'fallback')
    a.absolute('49ba', 'image', 0xf2afd48)
    a.emit('41833a03')
    a.branch('0f85', 'fallback')
    a.absolute('49ba', 'image', 0xf40e998)
    a.emit('418b12')
    a.emit('4883ec28')  # Win64 shadow space and call alignment.
    a.branch('e8', LOOKUP)
    a.emit('4883c4284885c0')
    a.branch('0f84', 'fallback')
    a.emit('448b5004448b5808')
    a.emit('8b81100d00004183faff')
    a.branch('0f84', 'write_cooldown')
    a.emit('418bc285c0baffffffff0f44c2')
    a.label('write_cooldown')
    a.emit('4189008b81140d00004183fbff')
    a.branch('0f84', 'write_limit')
    a.emit('418bc385c0baffffffff0f44c2')
    a.label('write_limit')
    a.jump(0x2e93faa)
    a.label('fallback')
    a.emit('8b81100d00004189008b81140d0000')
    a.jump(0x2e93faa)
    add('defense getter', a)

    for offset, text in [(OFF_LABEL, 'Offensive Playcall Cooldown'),
                         (DEF_LABEL, 'Defensive Playcall Cooldown'),
                         (DEF_DESCRIPTION, 'Plays required before a user can call the same defensive play again. 0 disables defensive cooldown.'),
                         (OFF_LIMIT_LABEL, 'Offensive Playcall Limit'),
                         (DEF_LIMIT_LABEL, 'Defensive Playcall Limit'),
                         (DEF_LIMIT_DESCRIPTION, 'Maximum times a user can call the same defensive play in a game. 0 disables defensive playcall limits.')]:
        encoded = text.encode('utf8') + b'\0'
        out[offset:offset + len(encoded)] = encoded
    OUT.mkdir(exist_ok=True)
    # Verify complete instructions separately from the inline absolute-jump
    # data. Relocate a copy to plausible image/remote addresses for readable QA.
    relocated = bytearray(out)
    for pos, kind, offset in relocations:
        struct.pack_into('<Q', relocated, pos, (0x140000000 if kind == 'image' else 0x200000000) + offset)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    asm_lines = []
    for name, start, size in sections:
        asm_lines.append(f'\n{name} [{start:x}, {start + size:x})')
        pos = start
        while pos < start + size:
            if relocated[pos:pos + 6] == bytes.fromhex('ff2500000000'):
                dest = struct.unpack_from('<Q', relocated, pos + 6)[0]
                asm_lines.append(f'{0x200000000 + pos:012x} jmp absolute {dest:x}')
                pos += 14
            else:
                ins = next(md.disasm(relocated[pos:start + size], 0x200000000 + pos, count=1), None)
                if not ins:
                    raise ValueError('invalid hook instruction at ' + hex(pos))
                if ins.mnemonic.startswith('j') or ins.mnemonic == 'call':
                    if ins.operands[0].type == 2:
                        target = ins.operands[0].imm - 0x200000000
                        if target != LOOKUP and not start <= target < start + size:
                            raise ValueError('relative branch leaves hook region')
                asm_lines.append(f'{ins.address:012x} {ins.bytes.hex():30} {ins.mnemonic} {ins.op_str}')
                pos += ins.size
    ordered = sorted(sections, key=lambda section: section[1])
    for (_, start, size), (_, next_start, _) in zip(ordered, ordered[1:]):
        if start + size > next_start:
            raise ValueError('overlapping generated hooks')
    (EVIDENCE / 'split-settings-generated-hooks.asm').write_text('\n'.join(asm_lines))
    raw = out.hex()
    chunks = ['            "' + raw[i:i + 160] + '"' for i in range(0, len(raw), 160)]
    cs = '''// Generated by Tools/build_split_settings_hooks.py; inspect matching evidence ASM.
using System;
namespace OfflinePlaycall {
    internal static class SplitHookCode {
        internal const int TableOffset = 0x2000, Capacity = 1024, EntrySize = 12,
            AllocationSize = 0x5000, SequenceOffset = 0x1800;
        internal static byte[] Create(long image, long remote, long previousWriter) {
            byte[] code = Hex(
''' + ' +\n'.join(chunks) + ');\n'
    for pos, kind, offset in relocations:
        expression = f'{"image" if kind == "image" else "remote"} + 0x{offset:x}L'
        if kind == 'writer':
            expression = f'previousWriter != 0 ? previousWriter : remote + 0x{offset:x}L'
        cs += f'            Array.Copy(BitConverter.GetBytes({expression}), 0, code, 0x{pos:x}, 8);\n'
    cs += '''            return code;
        }
        private static byte[] Hex(string s) {
            byte[] b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }
    }
}
'''
    (OUT / 'SplitHookCode.cs').write_text(cs)
    regions = [(0x2e93f70, 0x3e), (0x4f96880, 0xc4), (0x2e8c2d0, 0x338), (0x8456330, 3), (0x7993bd0, 7), (0x7ee2090, 0x45), (0x3009c00, 0x10)]
    manifest = {'sections': sections, 'relocations': relocations,
                'regions': [{'rva': r, 'size': size,
                             'sha256': hashlib.sha256(pe.d[pe.off(r):pe.off(r) + size]).hexdigest()}
                            for r, size in regions],
                'sites': [{'rva': r, 'remoteOffset': o, 'original': pe.d[pe.off(r):pe.off(r) + size].hex()}
                          for r, o, size in [(0x7993ee0, 0, 16), (0x79a2263, 0x600, 14), (0x2e93f9b, 0x900, 15)]]}
    (OUT / 'hooks-manifest.json').write_text(json.dumps(manifest, indent=2))
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    raise SystemExit('Template input only. Run build_game_settings_menu.py for the combined menu; review Shared/Playcall/SplitHookCode.cs directly for native updates.')
