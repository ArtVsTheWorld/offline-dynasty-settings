"""Generate offline Dynasty Controller Select bridges. No process or capture access.

Queue on native controller confirmation. Preserve the current snap, then move the
full-team role before the following normal picker initializes. Never call the
forced picker refresh, spend a timeout, or replace shared menu-expression hooks.

Code: queue 0, boundary 0x400, apply 0x800, eligibility 0xc00, trampoline
0xe00. RW telemetry: applied 0x1800, eligibility 0x1818; pending side/seen
live/count/cancel 0x1900..0x190c; owning match 0x1920. -1 means no request.
"""
import struct
from pathlib import Path
from build_split_settings_hooks import Assembler
ROOT=Path(__file__).resolve().parents[1]
def apply(image,remote,original):
 a=Assembler(0)
 a.emit('5356574883ec60488bd9');a.absolute('48b8','original',0);a.emit('ffd04889442420')
 for rva,value in [(0xd9e3818,14),(0xf3f5d48,3)]:
  a.absolute('49ba','image',rva);a.emit('41833a'+bytes([value]).hex());a.branch('0f85','done')
 a.absolute('49ba','image',0xf6379e0);a.emit('4d8b124d85d2');a.branch('0f84','done');a.emit('4180ba8400000000');a.branch('0f85','done')
 a.emit('8bbbf805000083ff01');a.branch('0f87','done')
 # Controller Select currently supports to one user on controller 0.
 for offset in range(0x5fc,0x610,4):
  a.emit('83bb'+struct.pack('<I',offset).hex()+'00');a.branch('0f8c','slot'+str(offset))
  a.emit('83bb'+struct.pack('<I',offset).hex()+'02');a.branch('0f85','done');a.label('slot'+str(offset))
 a.absolute('48be','image',0xf857018);a.emit('488b364885f6');a.branch('0f84','done')
 a.absolute('49ba','image',0xb324050);a.emit('4c3916');a.branch('0f85','done')
 a.emit('4c8d96480100004c8d9e7401000041833a00');a.branch('0f84','found');a.emit('4d87da41833a00');a.branch('0f85','done')
 a.label('found');a.emit('41833bff');a.branch('0f85','done');a.emit('41817b04ff000000');a.branch('0f85','done')
 a.emit('41837a1423');a.branch('0f85','done');a.emit('418b420483f801');a.branch('0f87','done');a.emit('39f8');a.branch('0f84','done')
 # Preserve the full 44-byte role, moving only the current side field.
 a.emit('f3410f6f02f3410f6f4a10498b4220418b5228f30f7f442430f30f7f4c2440488944245089542458')
 a.emit('f3410f6f03f3410f6f4b10498b4320418b5328f3410f7f02f3410f7f4a104989422041895228')
 a.emit('f30f6f442430f30f6f4c2440488b4424508b542458f3410f7f03f3410f7f4b10498943204189532841897b04')
 a.absolute('49ba','image',0xf3d27d0);a.emit('49c702ffffffff41c704ba00000000')
 a.emit('488bce33d2448bc7');a.absolute('48b8','image',0x2eb3e40);a.emit('ffd0')
 a.emit('89bb40060000');a.absolute('49ba','remote',0x1800);a.emit('f041ff0241897a04')
 a.label('done');a.emit('488b4424204883c4605f5e5bc3')
 return a

def finish(a,image,remote,original):
 return a

def common(a,end):
 for rva,value in [(0xd9e3818,14),(0xf3f5d48,3)]:
  a.absolute('49ba','image',rva);a.emit('41833a'+bytes([value]).hex());a.branch('0f85',end)
 a.absolute('49ba','image',0xf6379e0);a.emit('4d8b124d85d2');a.branch('0f84',end);a.emit('4180ba8400000000');a.branch('0f85',end)
def queue(image,remote,original):
 a=Assembler(0);a.emit('5356574883ec20488bd9');common(a,'native')
 a.absolute('49ba','image',0xf547a00);a.emit('4d8b124d85d2');a.branch('0f84','native');a.emit('41803a01');a.branch('0f85','native')
 a.emit('8bbbf805000083ff01');a.branch('0f87','native')
 for offset in range(0x5fc,0x610,4):
  a.emit('83bb'+struct.pack('<I',offset).hex()+'00');a.branch('0f8c','single'+str(offset))
  a.emit('83bb'+struct.pack('<I',offset).hex()+'02');a.branch('0f85','native');a.label('single'+str(offset))
 # Obtain the actual role, not the frontend's pending selection.
 a.absolute('48be','image',0xf857018);a.emit('488b364885f6');a.branch('0f84','native')
 a.absolute('49ba','image',0xb324050);a.emit('4c3916');a.branch('0f85','native')
 a.emit('4c8d964801000041833a00');a.branch('0f84','role');a.emit('4c8d967401000041833a00');a.branch('0f85','native')
 a.label('role');a.emit('41837a1423');a.branch('0f85','native');a.emit('418b720483fe01');a.branch('0f87','native')
 a.absolute('49ba','image',0xf8576b0);a.emit('4d8b124d85d2');a.branch('0f84','native');a.emit('4d8b52104d85d2');a.branch('0f84','native')
 a.absolute('49bb','remote',0x1920);a.emit('4d8913')
 a.absolute('49ba','remote',0x1900);a.emit('90')
 a.emit('41893a41c742040000000039f7');a.branch('0f85','queued')
 a.emit('41c702fffffffff041ff420c');a.branch('e9','restore')
 a.label('queued');a.emit('f041ff4208');a.absolute('49bb','image',0xf8575a0);a.emit('4d8b1b4d85db');a.branch('0f84','restore')
 a.emit('418b83a000000083f803');a.branch('0f84','seen');a.emit('83f804');a.branch('0f85','restore')
 a.label('seen');a.emit('41c7420401000000')
 a.label('restore');a.emit('89b3f805000089b340060000')
 a.absolute('49ba','image',0xf3d27d0);a.emit('49c702ffffffff41c704b200000000')
 a.emit('48c78378060000ffffffffc784b37806000000000000')
 a.label('native');a.emit('488bcb');a.absolute('48b8','original',0);a.emit('ffd04883c4205f5e5bc3')
 return finish(a,image,remote,original)
def boundary(image,remote,applyAddress):
 a=Assembler(0);a.emit('5356574883ec30488bd98bf2');common(a,'replay')
 a.absolute('49ba','image',0xf8575a0);a.emit('49391a');a.branch('0f85','replay')
 a.absolute('49ba','remote',0x1900);a.emit('418b3a83ff01');a.branch('0f87','replay')
 a.absolute('49bb','image',0xf8576b0);a.emit('4d8b1b4d85db');a.branch('0f84','cancel');a.emit('4d8b5b10')
 a.absolute('48b8','remote',0x1920);a.emit('4c3918');a.branch('0f85','cancel')
 a.emit('83fe03');a.branch('0f85','check');a.emit('41c7420401000000');a.branch('e9','replay')
 a.label('check');a.emit('83fe05');a.branch('0f85','replay');a.emit('41837a0401');a.branch('0f85','replay')
 a.emit('8bc741bbfffffffff0450fb11a');a.branch('0f85','replay');a.emit('41c7420400000000')
 a.absolute('48b9','image',0xfa53900);a.emit('488b094885c9');a.branch('0f84','replay')
 a.emit('89b9f805000048c78178060000ffffffffc784b97806000000000000');a.absolute('48b8','apply',0);a.emit('ffd0');a.branch('e9','replay')
 a.label('cancel');a.emit('41c702ffffffff41c7420400000000')
 a.label('replay');a.emit('488bcb8bd64883c4305f5e5b')
 # Exact untouched entry instructions, with no RIP-relative operands.
 a.emit('48895c241048896c24184889742420');a.jump(0x5d0e95f)
 return a

def eligibility():
 a=Assembler(0);common(a,'native')
 a.absolute('49ba','image',0xf547a00);a.emit('4d8b124d85d2');a.branch('0f84','native')
 a.emit('41803a01');a.branch('0f85','native')
 # Hide our Dynasty extension outside the supported single full-team controller.
 a.absolute('49ba','image',0xfa53900);a.emit('4d8b124d85d2');a.branch('0f84','native')
 for offset in range(0x5fc,0x610,4):
  a.emit('4183ba'+struct.pack('<I',offset).hex()+'00');a.branch('0f8c','single'+str(offset))
  a.emit('4183ba'+struct.pack('<I',offset).hex()+'02');a.branch('0f85','native');a.label('single'+str(offset))
 a.absolute('49ba','image',0xf857018);a.emit('4d8b124d85d2');a.branch('0f84','native')
 a.absolute('49bb','image',0xb324050);a.emit('4d391a');a.branch('0f85','native')
 a.emit('4183ba4801000000');a.branch('0f84','team0')
 a.emit('4183ba7401000000');a.branch('0f85','native');a.emit('4183ba8801000023');a.branch('0f85','native');a.branch('e9','value')
 a.label('team0');a.emit('4183ba5c01000023');a.branch('0f85','native')
 a.label('value')
 a.absolute('49ba','remote',0x1818);a.emit('f041ff02ba01000000');a.absolute('48b8','image',0x9130550);a.emit('ffe0')
 a.label('native');a.absolute('ff2500000000','remote',0xe00)
 return a

def generate():
 # Arguments here only identify relocation symbols; no code executes.
 blocks=[('Queue',0,queue(0,0,0)),('Boundary',0x400,boundary(0,0,0)),('Apply',0x800,apply(0,0,0)),('Eligibility',0xc00,eligibility())]
 lines=['// Generated by Tools/build_controller_select.py.','using System;','namespace DynastyPlayClock {',' public sealed partial class DynastyPlayClockAction {']
 for name,offset,a in blocks:
  data=a.finish();assert len(data)<=0x400,(name,len(data))
  lines+=['  private static byte[] BuildController'+name+'(long image,long remote,long original) {','   byte[] code=Hex("'+data.hex()+'");']
  for pos,kind,add in a.relocations:
   expr={'image':'image','remote':'remote','original':'original','apply':'remote+0x800L'}[kind]
   lines.append('   Array.Copy(BitConverter.GetBytes('+expr+'+0x'+format(add,'x')+'L),0,code,0x'+format(pos,'x')+',8);')
  lines+=['   return code;','  }']
 lines+=[' }','}']
 (ROOT/'Plugin/ControllerSelectCode.cs').write_text('\n'.join(lines)+'\n')
 print('Generated Controller Select: '+', '.join(name+'='+str(len(a.finish())) for name,_,a in blocks))
if __name__=='__main__':generate()
