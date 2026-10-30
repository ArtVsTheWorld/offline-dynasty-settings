"""Execute release-DLL eligibility with native-ABI stubs; never access the game."""
import ctypes as C,struct
from pathlib import Path
import sys
folder=Path(sys.argv[1])
def build(*args):return (folder/"Eligibility.bin").read_bytes()
k=C.WinDLL('kernel32');k.VirtualAlloc.argtypes=[C.c_void_p,C.c_size_t,C.c_uint32,C.c_uint32];k.VirtualAlloc.restype=C.c_void_p
B=0x140000000;R=0x210000000;D=0x220000000
assert k.VirtualAlloc(B,0x10000000,0x2000,4)==B
assert k.VirtualAlloc(R,8192,0x3000,0x40)==R
assert k.VirtualAlloc(D,0x4000,0x3000,4)==D
pages=set()
for rva in [0xd9eb808,0xf3fddbc,0xf63f9f0,0xf54fa00,0x91364c0,0xfa5b900,0xf85f120]:
 page=(B+rva)&~4095
 if page not in pages:assert k.VirtualAlloc(page,4096,0x1000,0x40)==page;pages.add(page)
def put(a,b):C.memmove(a,b,len(b))
def n(a,v):put(a,struct.pack('<i',v))
def q(a,v):put(a,struct.pack('<Q',v))
# Same output-variant contract as the native bool constructor, independent of game process.
put(B+0x91364c0,bytes.fromhex('8811c7410804000000488bc1c3'))
put(R+0xe00,bytes.fromhex('c60100c7410804000000488bc1c3'))
put(R,build(B,R,R+0xe00));k.VirtualProtect.argtypes=[C.c_void_p,C.c_size_t,C.c_uint32,C.POINTER(C.c_uint32)];old=C.c_uint32();assert k.VirtualProtect(R,4096,0x20,C.byref(old));callback=C.WINFUNCTYPE(C.c_void_p,C.c_void_p,C.c_void_p)(R)
q(B+0xf63f9f0,D+0x100);q(B+0xf54fa00,D+0x200);n(B+0xd9eb808,14);n(B+0xf3fddbc,3);put(D+0x200,b'\1')
q(B+0xfa5b900,D+0x1000);q(B+0xf85f120,D+0x2000);q(D+0x2000,B+0xb32a410)
for j in range(1,6):n(D+0x15f8+j*4,2 if j==5 else -1)
n(D+0x2148,0);n(D+0x215c,35);n(D+0x2174,-1)

def run(expect,label):
 put(D+0x300,b'\xcc'*16);result=callback(D+0x300,0)
 assert result==D+0x300 and C.c_ubyte.from_address(result).value==expect and C.c_int.from_address(result+8).value==4,label
 print('PASS',label)
run(1,'paused offline Dynasty is enabled')
put(D+0x184,b'\1');run(0,'practice is unchanged');put(D+0x184,b'\0')
for mode in [-1,0,17]:n(B+0xd9eb808,mode);run(0,'mode '+str(mode)+' uses original eligibility')
n(B+0xd9eb808,14)
for kind in [0,4,5,6]:n(B+0xf3fddbc,kind);run(0,'match kind '+str(kind)+' uses original eligibility')
n(B+0xf3fddbc,3);put(D+0x200,b'\0');run(0,'unpaused game uses original eligibility');put(D+0x200,b'\1')
q(B+0xf63f9f0,0);run(0,'missing configuration uses original eligibility');q(B+0xf63f9f0,D+0x100)
q(B+0xf54fa00,0);run(0,'missing pause manager uses original eligibility');q(B+0xf54fa00,D+0x200)
n(D+0x15fc,1);run(0,'another user keeps native eligibility');n(D+0x15fc,-1)
n(D+0x215c,1);run(0,'player-locked role keeps native eligibility');n(D+0x215c,35)
run(1,'valid state can be re-enabled');assert C.c_int.from_address(R+0x1818).value==2
print('PASS callback counter and native ABI; no game access')
