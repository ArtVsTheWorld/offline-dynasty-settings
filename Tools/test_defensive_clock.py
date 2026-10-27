"""Execute the active selection initializer, native countdown and emitted writers in this process only."""
import ctypes as C,struct,sys,json,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];EV=ROOT/('Evidence/game-settings-'+(sys.argv[1] if len(sys.argv)>1 else '0.2.1'))
sys.path.insert(0,str(ROOT/'Tools'))
from native_trace import PE
capture=os.environ.get('CFB_BASELINE_IMAGE')
if not capture:raise SystemExit('Set CFB_BASELINE_IMAGE to your local unmodified decrypted PE capture.')
pe=PE(capture)
k=C.WinDLL('kernel32',use_last_error=True)
k.VirtualAlloc.argtypes=[C.c_void_p,C.c_size_t,C.c_uint32,C.c_uint32];k.VirtualAlloc.restype=C.c_void_p
B=0x140000000;D=0x200000000;M=0x210000000
assert k.VirtualAlloc(B,0x10000000,0x2000,4)==B
assert k.VirtualAlloc(D,0x1000,0x3000,0x40)==D
assert k.VirtualAlloc(M,0x20000,0x3000,0x40)==M
pages=set()
def commit(a,n=1):
 for page in range(a&~4095,(a+n+4095)&~4095,4096):
  if page not in pages:assert k.VirtualAlloc(page,4096,0x1000,0x40)==page;pages.add(page)
def write(a,b):C.memmove(a,b,len(b))
def i(a,v):write(a,struct.pack('<i',v))
def q(a,v):write(a,struct.pack('<Q',v))
def f(a,v):write(a,struct.pack('<f',v))
def n(a):return C.c_int.from_address(a).value
def fl(a):return C.c_float.from_address(a).value
def h(s):return bytes.fromhex(s)
def movrax(a):return h('48b8')+struct.pack('<Q',a)
def jump(a):return h('ff2500000000')+struct.pack('<Q',a)
for r in [0x35f7d31,0x5ff26a0,0xada5bcc,0xf8abb50,0xd9e3818,0xf3f5d48,0x5f1afa0,0x79a48dd,0xf8572b8,0xe2c73e0,0x27a7910,0x5d02340]:commit(B+r,0x200)
for start,end in [(0x5ff26a0,0x5ff26a9),(0x5f1afa0,0x5f1afbd)]:write(B+start,pe.d[pe.off(start):pe.off(end)])
f(B+0xada5bcc,15)
write(M,(EV/'BuildCombinedDefensiveTimer.bin').read_bytes());write(D,(EV/'BuildData.bin').read_bytes())
manager=M+0x10000;q(B+0xf8abb50,manager)
# This call site is entered with the live selection-clock component in R13.
wrapper=M+0x2000
# mov r13,rcx; tail-jump to the native initialization call. Continuation restores wrapper frame.
write(wrapper,h('41554883ec204c8be9')+jump(B+0x35f7d31))
write(B+0x35f7d42,h('4883c420415dc3'))
Init=C.WINFUNCTYPE(None,C.c_void_p)(wrapper)
Tick=C.WINFUNCTYPE(None,C.c_void_p,C.c_float)(B+0x5f1afa0)
checks=[]
def check(label,ok):assert ok,label;checks.append(label)
def reset():
 C.memset(manager,0,0x80);i(B+0xd9e3818,14);i(B+0xf3f5d48,3);q(B+0xf8abb50,manager)
original=pe.d[pe.off(0x35f7d31):pe.off(0x35f7d42)]
write(B+0x35f7d31,original);reset();Init(manager)
check('actual native initializer loads 15 into both clocks',fl(manager+0x34)==15 and fl(manager+0x38)==15)
write(B+0x35f7d31,jump(M)+b'\x90'*3)
for seconds in [3,5,15,20,30,45,60]:
 reset();i(D+0x880,seconds);Init(manager)
 check(f'{seconds}s initializes active maximum and remaining',fl(manager+0x34)==seconds and fl(manager+0x38)==seconds and n(D+0x8a8)==seconds)
 for elapsed in range(1,seconds+3):
  Tick(manager,1.0)
  check(f'{seconds}s native countdown after {elapsed}s',fl(manager+0x38)==max(0,seconds-elapsed))
 Init(manager);C.c_ubyte.from_address(manager+0x3d).value=1;Tick(manager,2)
 check(f'{seconds}s respects native pause flag',fl(manager+0x38)==seconds)
 C.c_ubyte.from_address(manager+0x3d).value=0;Tick(manager,0.5)
 check(f'{seconds}s native fractional countdown resumes',fl(manager+0x38)==seconds-0.5)
 Init(manager);check(f'{seconds}s fresh snap resets once',fl(manager+0x38)==seconds)
for mode,match in [(13,3),(14,4),(-1,3),(14,0)]:
 reset();i(B+0xd9e3818,mode);i(B+0xf3f5d48,match);i(D+0x880,60);Init(manager)
 check(f'other mode/match {mode}/{match} stays native',fl(manager+0x38)==15)
for bad in [-1,14,16,19,21,44,59,61,999]:
 reset();i(D+0x880,bad);Init(manager);check(f'invalid runtime value {bad} stays native',fl(manager+0x38)==15)
reset();q(B+0xf8abb50,manager+0x100);i(D+0x880,60);Init(manager)
check('foreign clock object untouched',fl(manager+0x38)==15)
# Exercise the real offensive extension hook repeatedly: its obsolete ring previously
# overlapped 0x900..0xcff, corrupting the new defensive-clock descriptor and choices.
clock=M+0x12000;cm=M+0x11000;profile=M+0x9000;pvt=M+0x9100
q(B+0xf8572b8,cm);q(cm+0x20,clock);q(B+0xe2c73e0,profile);q(B+0xe2c73e8,profile)
q(profile,pvt);q(pvt+0x10,M+0x9200);write(M+0x9200,h('b801000000c3'));write(B+0x27a7910,movrax(profile)+h('c3'))
write(M+0x3000,(EV/'BuildDurationUpdated.bin').read_bytes());write(B+0x5d0234f,h('c5fa114928c3'))
Extend=C.WINFUNCTYPE(None,C.c_void_p,C.c_float)(M+0x3000)
i(B+0xd9e3818,14);i(B+0xf3f5d48,3);i(D+0x828,1)
expected=C.string_at(D+0x900,0x700);expected_labels=C.string_at(D+0x700,0x100)
for _ in range(70):Extend(clock,25)
check('offensive reset path exercised 70 times',n(D+0x838)==70 and n(D+0x830)==70 and fl(clock+0x28)==60)
check('offensive reset telemetry cannot overwrite defensive UI',C.string_at(D+0x900,0x700)==expected and C.string_at(D+0x700,0x100)==expected_labels)
# Reproduce the native defensive UI/tick gate. Its enum-1 read previously used
# Offensive Play Clock, so Off sent the hidden (-100) UI value and skipped Tick.
for r in [0x35fd7d3,0x5cf5d60,0xf8abc20,0x2ad8410,0x5d01c30,0x35add00]:commit(B+r,0x200)
oldenum=M+0x1000;enumcode=M+0xa000;gatewrapper=M+0xb000;cache=M+0xc000;pc=M+0xd000;rootmatch=M+0xe000
profilerowner=M+0xe700;profiler=M+0xe800;profvt=M+0xe900;event=M+0xec00;voidfn=M+0xed00;choice=M+0xee00
write(oldenum,pe.d[pe.off(0x5cf5d60):pe.off(0x5cf5d70)])
write(enumcode,(EV/'BuildEnumUpdated.bin').read_bytes())
q(B+0xe2c73f0,cache);q(B+0xf8abc20,profilerowner);q(profilerowner+0xb0,profiler);q(profiler,profvt);q(profvt+0x38,voidfn)
write(voidfn,h('c3'));write(B+0x2ad8410,h('33c0c3'));write(B+0x5d01c30,h('c3'))
write(B+0x35add00,h('418b0049ba')+struct.pack('<Q',event)+h('41890241ff4204c3'))
write(M+0x9200,movrax(choice)+h('8b00c3'))
start,end=0x35fd7d3,0x35fd8b5;write(B+start,pe.d[pe.off(start):pe.off(end)])
# Preserve native nonvolatile registers and use its original RBP/RSP layout.
body=h('5553565741544155415641574881ec18010000488d6c2439c5f829742450c5fa114d6f4c8bf149bf')+struct.pack('<Q',pc)
body+=h('49bd')+struct.pack('<Q',rootmatch)+h('33f648b9')+struct.pack('<Q',cache)+h('ba01000000')+jump(B+start)
write(gatewrapper,body);write(B+end,h('4881c418010000415f415e415d415c5f5e5b5dc3'))
Gate=C.WINFUNCTYPE(None,C.c_void_p,C.c_float)(gatewrapper)
Enum=C.WINFUNCTYPE(C.c_ushort,C.c_void_p,C.c_int)(B+0x5cf5d60)
C.c_ubyte.from_address(pc+0x19).value=1;i(rootmatch+0x1d0,1)
def selection(seconds,off,mode=14,match=3):
 reset();i(D+0x880,seconds);Init(manager);i(choice,off);C.c_ushort.from_address(cache+0x62).value=off
 i(B+0xd9e3818,mode);i(B+0xf3f5d48,match);i(event,-999)
write(B+0x5cf5d60,jump(oldenum));selection(60,0);Gate(manager,1)
check('reproduces old Off bug: native UI hidden and timer never ticks',n(event)==-100 and fl(manager+0x38)==60)
selection(60,2);Gate(manager,1)
check('reproduces old Off At Playcall bug: native UI hidden and timer never ticks',n(event)==-100 and fl(manager+0x38)==60)
selection(60,1);Gate(manager,1)
check('baseline On enters actual native countdown/UI path',n(event)==60 and fl(manager+0x38)==59)
write(B+0x5cf5d60,jump(enumcode))
for extended in [0,1]:
 i(D+0x828,extended)
 for off in [0,1,2]:
  for seconds in [3,5,15,20,30,45,60]:
   selection(seconds,off);before=n(D+0x8c8);Gate(manager,0.5)
   check(f'off={off} extended={extended} defense={seconds}: native countdown and visible number',fl(manager+0x38)==seconds-0.5 and n(event)==seconds)
   Gate(manager,1)
   check(f'off={off} extended={extended} defense={seconds}: countdown continues normally',fl(manager+0x38)==seconds-1.5 and n(event)==seconds-1)
   check(f'off={off} extended={extended} defense={seconds}: saved offense value unchanged',Enum(cache,1)==off and C.c_ushort.from_address(cache+0x62).value==off and n(D+0x8c4)==off and n(D+0x8c8)==before+(0 if off==1 else 2))
for extended in [0,1]:
 i(D+0x828,extended)
 for off in [0,1,2]:
  selection(0,off);before=n(D+0x8c8)
  check(f'defense Off with offense {off}/{extended}: positive internal sentinel, not immediate expiry',fl(manager+0x38)==15 and n(D+0x8a8)==0)
  Gate(manager,30);Gate(manager,300)
  check(f'defense Off with offense {off}/{extended}: no countdown, hidden UI, no expiry',fl(manager+0x38)==15 and n(event)==-100)
  check(f'defense Off with offense {off}/{extended}: offensive setting remains intact',Enum(cache,1)==off and C.c_ushort.from_address(cache+0x62).value==off and n(D+0x8c8)==before+(0 if off==0 else 2))
selection(15,0);Gate(manager,1)
check('switching back from defensive Off restores timed countdown next snap',fl(manager+0x38)==14 and n(event)==15)
for mode,match in [(13,3),(14,4)]:
 selection(0,1,mode,match);before=n(D+0x8c8);Gate(manager,0.5)
 check(f'defensive Off setting does not disable other modes {mode}/{match}',fl(manager+0x38)==14.5 and n(event)==15 and n(D+0x8c8)==before)

for mode,match in [(13,3),(14,4),(-1,3),(14,0)]:
 selection(60,0,mode,match);f(manager+0x38,60);before=n(D+0x8c8);Gate(manager,1)
 check(f'defensive gate remains native outside offline Dynasty {mode}/{match}',n(event)==-100 and fl(manager+0x38)==60 and n(D+0x8c8)==before)
selection(60,0);C.c_ubyte.from_address(manager+0x3d).value=1;Gate(manager,1)
check('independent defensive timer respects native paused/hidden state',n(event)==-100 and fl(manager+0x38)==60)
C.c_ubyte.from_address(manager+0x3d).value=0;Gate(manager,1)
check('independent defensive timer resumes and displays after pause',n(event)==60 and fl(manager+0x38)==59)
selection(60,0);q(B+0xf8abb50,manager+0x100);before=n(D+0x8c8);Gate(manager,1)
check('foreign selection-clock object is not forced on',n(event)==-100 and fl(manager+0x38)==60 and n(D+0x8c8)==before)
selection(60,0);i(choice,99);before=n(D+0x8c8);Gate(manager,1)
check('invalid offensive profile does not force defensive gate',n(event)==-100 and fl(manager+0x38)==60 and n(D+0x8c8)==before)
selection(15,0);Gate(manager,30)
check('independent defensive timer still expires and clamps at zero',fl(manager+0x38)==0)
# Execute the actual new settings writer with native field API stubs.
writer=M+0x4000;wrapper=M+0x5000;adapter=M+0x6000;response=M+0x6500;vt=M+0x7000;source=M+0x8000;svt=M+0x8100;chosen=M+0x8200;online=M+0x8300
write(writer,(EV/'BuildWriterUpdated.bin').read_bytes());q(adapter+0x418,response);q(response,vt);q(vt+0xa10,online)
write(online,h('33c0c3'));q(source,svt);q(svt+0x18,chosen+16);write(chosen+16,movrax(chosen)+h('8b00c3'))
write(wrapper,h('56415641574883ec20488bf1448bfa49be')+struct.pack('<Q',adapter)+h('4489c0')+jump(writer))
write(B+0x79a48dd,h('4883c420415f415e5ec3'))
Writer=C.WINFUNCTYPE(None,C.c_void_p,C.c_int,C.c_int)(wrapper)
i(D+0x828,1)
for seconds in [3,5,15,20,30,45,60,0]:
 i(chosen,seconds);Writer(source,7,0x1002)
 check(f'{seconds}s writer accepts choice without changing old offensive preference',n(D+0x880)==seconds and n(D+0x95c)==seconds and n(D+0x828)==1)
for bad in [-1,14,16,61,999]:
 i(chosen,bad);Writer(source,7,0x1002);check(f'writer rejects {bad}',n(D+0x880)==0)
write(online,h('b801000000c3'));i(chosen,20);Writer(source,7,0x1002);check('online setting write rejected',n(D+0x880)==0)
check('defensive setting has eight choices with Off last',[n(D+0xb00+j*0x80+0x28) for j in range(8)]==[3,5,15,20,30,45,60,0] and C.c_ulonglong.from_address(D+0x980).value==D+0xa40)
check('all defensive labels are intact and Off follows 60 Seconds',[C.string_at(D+0x700+j*0x20).decode() for j in range(8)]==['3 Seconds','5 Seconds','15 Seconds','20 Seconds','30 Seconds','45 Seconds','60 Seconds','Off'])
check('all eight choice and caption pointers resolve without overlapping the heading',all(C.c_ulonglong.from_address(D+0xa00+j*8).value==D+0xb00+j*0x80 and C.c_ulonglong.from_address(D+0xb00+j*0x80+0x30).value==D+0x700+j*0x20 for j in range(8)) and C.string_at(D+0xf00)==b'Defensive Play Clock' and C.string_at(D+0xf30).startswith(b'Time to choose a defensive play'))
check('offensive option captions preserve old numeric values',[n(D+0x180+j*0x80+0x28) for j in range(4)]==[1,2,0,3] and [C.string_at(D+0x600+j*0x40).decode() for j in range(4)]==['On','Off At Playcall','Off','60 Seconds'])
check('renamed offensive clock descriptor',C.string_at(D+0x400)==b'Offensive Play Clock')
(EV/'defensive-clock-validation.json').write_text(json.dumps(dict(passed=len(checks),checks=checks,nativeSelectionInitializerAndCountdownExecuted=True,inGameVerified=False),indent=2))
print(f'PASS {len(checks)} defensive timer, active native countdown, scope, writer and caption checks.')

