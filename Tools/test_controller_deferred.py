"""Execute release-DLL bridges against fake objects in this Python process only."""
import ctypes as C,struct
from pathlib import Path
import sys
folder=Path(sys.argv[1])
def queue(*args):return (folder/"Queue.bin").read_bytes()
def boundary(*args):return (folder/"Boundary.bin").read_bytes()
def apply(*args):return (folder/"Apply.bin").read_bytes()
k=C.WinDLL('kernel32');k.VirtualAlloc.argtypes=[C.c_void_p,C.c_size_t,C.c_uint32,C.c_uint32];k.VirtualAlloc.restype=C.c_void_p
B=0x140000000;R=0x210000000;D=0x220000000
assert k.VirtualAlloc(B,0x10000000,0x2000,4)==B
assert k.VirtualAlloc(R,8192,0x3000,0x40)==R
assert k.VirtualAlloc(D,0x4000,0x3000,4)==D
pages=set()
for rva in [0xd9e3818,0xf3f5d48,0xf6379e0,0xf547a00,0xf857018,0xf3d27d0,0x2eb3e40,0x4ff5b90,0xf8575a0,0xf8576b0,0xfa53900,0x5d0e950]:
 page=(B+rva)&~4095
 if page not in pages:assert k.VirtualAlloc(page,4096,0x1000,0x40)==page;pages.add(page)
def put(a,b):C.memmove(a,b,len(b))
def n(a,v):put(a,struct.pack('<i',v))
def q(a,v):put(a,struct.pack('<Q',v))
def iv(a):return C.c_int.from_address(a).value
adapter=D;manager=D+0x1000;config=D+0x2000;pause=D+0x2300
put(R+0xc00,bytes.fromhex('ff8190070000488bc1c3'))
put(B+0x2eb3e40,bytes.fromhex('ff810802000089910002000044898104020000c3'))
put(B+0x4ff5b90,bytes.fromhex('ff8190000000c3'))
q(B+0xf6379e0,config);q(B+0xf547a00,pause);q(B+0xf857018,manager);q(manager,B+0xb324050)
n(B+0xd9e3818,14);n(B+0xf3f5d48,3);put(pause,b'\1')
for j in range(1,6):n(adapter+0x5f8+j*4,2 if j==5 else -1)
# Role fields beyond the current-side word must remain byte-identical.
active=bytearray(range(44));struct.pack_into('<ii',active,0,0,1);struct.pack_into('<i',active,0x14,35)
empty=bytearray([0xff]*44);struct.pack_into('<ii',empty,0,-1,255)
put(manager+0x148,bytes(empty)+bytes(active));n(adapter+0x5f8,0);put(B+0xf3d27d0,struct.pack('<ii',0,0))

q(B+0xf8575a0,D+0x3000);q(B+0xf8576b0,D+0x2800);q(D+0x2810,D+0x2a00);q(B+0xfa53900,adapter)
put(B+0x5d0e95f,bytes.fromhex('8991a0000000c3'))
# Begin controlling side 0 with its existing role unchanged.
struct.pack_into('<i',active,4,0);put(manager+0x148,bytes(active)+bytes(empty));n(adapter+0x5f8,0);n(D+0x30a0,1)
put(R,queue(B,R,R+0x900));put(R+0x400,boundary(B,R,R+0x800));put(R+0x800,apply(B,R,R+0x900))
# Apply code occupies 0x800..0xbff; keep the original callback at 0xc00.
put(R+0xc00,bytes.fromhex('ff8190070000488bc1c3'))
put(R,queue(B,R,R+0xc00));put(R+0x800,apply(B,R,R+0xc00))
k.VirtualProtect.argtypes=[C.c_void_p,C.c_size_t,C.c_uint32,C.POINTER(C.c_uint32)];old=C.c_uint32();assert k.VirtualProtect(R,4096,0x20,C.byref(old))
confirm=C.WINFUNCTYPE(C.c_void_p,C.c_void_p)(R);transition=C.WINFUNCTYPE(None,C.c_void_p,C.c_int)(R+0x400)
n(R+0x1900,-1)
n(adapter+0x5f8,1);assert confirm(adapter)==adapter
assert iv(adapter+0x5f8)==0 and iv(R+0x1900)==1 and iv(R+0x1904)==0 and iv(manager+0x208)==0
assert iv(B+0xf3d27d0)==0 and iv(B+0xf3d27d0+4)==-1
transition(D+0x3000,5);assert iv(R+0x1900)==1 and iv(manager+0x208)==0
transition(D+0x3000,3);assert iv(R+0x1904)==1 and iv(manager+0x208)==0
transition(D+0x3000,4);assert iv(R+0x1900)==1 and iv(manager+0x208)==0
transition(D+0x3000,5);assert iv(R+0x1900)==-1 and iv(manager+0x204)==1 and iv(manager+0x208)==1
want=bytearray(active);struct.pack_into('<i',want,4,1)
assert C.string_at(manager+0x148,88)==bytes(empty)+bytes(want)
assert iv(adapter+0x5f8)==1 and iv(adapter+0x640)==1 and iv(adapter+0x678)==-1 and iv(adapter+0x67c)==0
assert iv(B+0xf3d27d0)==-1 and iv(B+0xf3d27d0+4)==0
assert iv(config+0x90)==0 # No forced playcall refresh or timeout.
# Request a return while the picker is open: it must wait for another snap.
n(adapter+0x5f8,0);confirm(adapter);assert iv(adapter+0x5f8)==1 and iv(R+0x1900)==0
transition(D+0x3000,5);assert iv(manager+0x208)==1
transition(D+0x3000,3);transition(D+0x3000,4);transition(D+0x3000,5)
assert iv(manager+0x208)==2 and iv(manager+0x204)==0 and iv(config+0x90)==0
assert C.string_at(manager+0x148,88)==bytes(active)+bytes(empty)
# Cancel a queued change by confirming the current side.
n(adapter+0x5f8,1);confirm(adapter);n(adapter+0x5f8,0);confirm(adapter)
assert iv(R+0x1900)==-1
transition(D+0x3000,3);transition(D+0x3000,5);assert iv(manager+0x208)==2
# A stale request cannot cross to another match.
n(adapter+0x5f8,1);confirm(adapter);q(D+0x2810,D+0x2b00);transition(D+0x3000,3);assert iv(R+0x1900)==-1
# A non-Dynasty or practice confirmation must retain native behavior and never queue.
for address,value in [(B+0xd9e3818,17),(B+0xf3f5d48,4),(config+0x84,1),(pause,0),(adapter+0x5fc,1)]:
 oldvalue=iv(address);n(address,value);n(adapter+0x5f8,1);n(R+0x1900,-1);confirm(adapter)
 assert iv(R+0x1900)==-1 and iv(manager+0x208)==2
 n(address,oldvalue)
print('PASS deferred lineup and picker requests, no switch during current play, both directions before next picker, no forced refresh/timeout, role preservation, captain/cache sync, cancel and stale-match rejection; no game access')
