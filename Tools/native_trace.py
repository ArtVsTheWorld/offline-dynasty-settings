"""Static PE string cross-references and bounded disassembly."""
from pathlib import Path
import struct,re,bisect,sys,os
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86 import X86_OP_MEM,X86_REG_RIP
CAPTURE=os.environ.get('CFB_BASELINE_IMAGE')
class PE:
    def __init__(self,path=None):
        path=path or CAPTURE
        if not path:raise ValueError('Provide a local decrypted PE image or set CFB_BASELINE_IMAGE.')
        self.d=Path(path).read_bytes(); d=self.d; pe=struct.unpack_from('<I',d,60)[0]
        opt=pe+24; self.base=struct.unpack_from('<Q',d,opt+24)[0]
        table=opt+struct.unpack_from('<H',d,pe+20)[0]
        self.sections=[]
        for i in range(struct.unpack_from('<H',d,pe+6)[0]):
            p=table+40*i; size,rva,rawsize,off=struct.unpack_from('<IIII',d,p+8)
            self.sections.append((d[p:p+8].rstrip(b'\0').decode(),rva,size,off,rawsize))
        pr,ps=struct.unpack_from('<II',d,opt+112+24)
        self.funcs=[struct.unpack_from('<III',d,self.off(pr)+i)[:2] for i in range(0,ps,12)]
        self.starts=[a for a,b in self.funcs]
    def off(self,rva):
        for _,v,s,o,n in self.sections:
            if v<=rva<v+n: return o+rva-v
        raise ValueError(hex(rva))
    def rva(self,off):
        for _,v,s,o,n in self.sections:
            if o<=off<o+n:return v+off-o
        raise ValueError(hex(off))
    def function(self,rva):
        i=bisect.bisect_right(self.starts,rva)-1
        return self.funcs[i] if i>=0 and self.funcs[i][1]>rva else (rva,rva+128)
    def disasm(self,start,end):
        cs=Cs(CS_ARCH_X86,CS_MODE_64);cs.detail=True;lines=[]
        for i in cs.disasm(self.d[self.off(start):self.off(end-1)+1],start):
            note=''
            for op in i.operands:
                if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:
                    dest=i.address+i.size+op.mem.disp;note+=f' ; RVA {dest:x}'
                    try:
                        off=self.off(dest);s=self.d[off:off+180].split(b'\0')[0]
                        if s and all(32<=b<127 for b in s):note+=' '+s.decode()
                    except ValueError:pass
            lines.append(f'{i.address:08x} {i.bytes.hex():30} {i.mnemonic:8} {i.op_str}{note}')
        return '\n'.join(lines)
    def refs(self,targets):
        result=[]
        for name,v,s,o,n in self.sections:
            if name!='.text':continue
            code=self.d[o:o+n]
            for m in re.finditer(rb'[\x48-\x4f][\x8b\x8d\x89][\x05\x0d\x15\x1d\x25\x2d\x35\x3d]',code):
                p=m.start(); dest=v+p+7+struct.unpack_from('<i',code,p+3)[0]
                if dest in targets:result.append((v+p,dest,self.function(v+p)))
        return result

if __name__=='__main__':
    pe=PE()
    if len(sys.argv)>1:
        start=int(sys.argv[1],0); end=int(sys.argv[2],0) if len(sys.argv)>2 else pe.function(start)[1]
        print(pe.disasm(start,end))
    else:
        targets={pe.rva(m.start()):m.group().decode() for m in re.finditer(rb'[ -~]{6,}',pe.d) if re.search(rb'play.?call.*(limit|cooldown)|play.*cooldown',m.group(),re.I)}
        for address,dest,(a,b) in pe.refs(targets): print(hex(address),targets[dest],hex(dest),'function',hex(a),hex(b))
