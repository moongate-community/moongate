#!/usr/bin/env python3
"""Generate independent wire vectors from an existing POL checkout (requires g++)."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

pol = Path(sys.argv[1]).resolve()
crypt = pol / 'pol-core/pol/crypt'
with tempfile.TemporaryDirectory(prefix='moongate-pol-reference-') as scratch:
    root = Path(scratch)
    (root / 'pol/crypt').mkdir(parents=True)
    (root / 'clib').mkdir()
    for name in ('logincrypt', 'twofish', 'blowfish', 'md5', 'cryptkey'):
        for ext in ('.h', '.cpp'):
            (root / 'pol/crypt' / (name + ext)).write_bytes((crypt / (name + ext)).read_bytes())
    (root / 'fmt').mkdir()
    (root / 'fmt/format.h').write_text('#include <type_traits>\nnamespace fmt { template<class T> constexpr auto underlying(T x) { return static_cast<std::underlying_type_t<T>>(x); } }\n')
    base = (crypt / 'cryptbase.h').read_text()
    (root / 'pol/crypt/cryptbase.h').write_text('#pragma once\n' + base[base.index('#define CRYPT_AUTO_VALUE'):base.index('#include "clib/network/sockets.h"')])
    (root / 'clib/passert.h').write_text('#include <cassert>\n#define passert_r(c,m) assert(c)\n')
    (root / 'clib/clib.h').write_text('#include <strings.h>\n#include <cstdlib>\n#define strnicmp strncasecmp\n')
    (root / 'clib/logfacility.h').write_text('#define POLLOG_ERRORLN(...) ((void)0)\n')
    (root / 'reference.cpp').write_text(r'''
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <cstring>
#include "pol/crypt/cryptkey.h"
#include "pol/crypt/logincrypt.h"
#include "pol/crypt/twofish.h"
#include "pol/crypt/blowfish.h"
#include "pol/crypt/md5.h"
using namespace Pol::Crypt;
void emit(const char* label, const std::vector<unsigned char>& data) {
    printf("%s ", label);
    for (auto b : data) printf("%02X", b);
    printf("\n");
}
void login(LoginCrypt& c, int type, std::vector<unsigned char>& b) {
    if (type == CRYPT_NOCRYPT) return;
    if (type == CRYPT_OLD_BLOWFISH) c.Decrypt_Old(b.data(), b.data(), b.size());
    else if (type == CRYPT_1_25_36) c.Decrypt_1_25_36(b.data(), b.data(), b.size());
    else c.Decrypt(b.data(), b.data(), b.size());
}
int main(int argc, char** argv) {
    TCryptInfo info; CalculateCryptKeys(argv[1], info);
    unsigned int seed = strtoul(argv[2], nullptr, 10);
    unsigned char seedBytes[] = {(unsigned char)(seed>>24),(unsigned char)(seed>>16),(unsigned char)(seed>>8),(unsigned char)seed};
    LoginCrypt l; l.Init(seedBytes, info.uiKey1, info.uiKey2);
    std::vector<unsigned char> bytes(50000);
    for (int i=0;i<bytes.size();i++) bytes[i] = (unsigned char)i;
    auto loginBytes = bytes; login(l, info.eType, loginBytes); emit("login",loginBytes);
    TwoFish t; t.Init(seedBytes); MD5Crypt m; m.Init(t.subData3,256);
    BlowFish b; b.Init(); auto game = bytes;
    // POL sockets cap calls below MAXBUFFER; preserve that contract over long streams.
    for (int start=0;start<game.size();start+=4096) {
        int size = std::min(4096, (int)game.size()-start);
        if (info.eType >= CRYPT_BLOWFISH_TWOFISH) t.Decrypt(game.data()+start,game.data()+start,size);
        if (info.eType >= CRYPT_OLD_BLOWFISH && info.eType <= CRYPT_BLOWFISH_TWOFISH) b.Decrypt(game.data()+start,game.data()+start,size);
    }
    emit("game",game);
    auto send=bytes;
    if(info.eType==CRYPT_TWOFISH) m.Encrypt(send.data(),send.data(),send.size());
    emit("send",send);
    std::vector<unsigned char> lp(62,0); lp[0]=0x80; memcpy(lp.data()+1,"fixture",7); memcpy(lp.data()+31,"example",7);
    l.Init(seedBytes,info.uiKey1,info.uiKey2); login(l,info.eType,lp); emit("loginPacket",lp);
    std::vector<unsigned char> gp(65,0); gp[0]=0x91; memcpy(gp.data()+1,seedBytes,4); memcpy(gp.data()+5,"fixture",7); memcpy(gp.data()+35,"example",7);
    // Invert the original decryptor byte-by-byte to produce a valid client handshake.
    b.Init();
    if(info.eType>=CRYPT_OLD_BLOWFISH && info.eType<=CRYPT_BLOWFISH_TWOFISH) {
        for(auto& plain:gp) for(int v=0;v<256;v++) {
            auto trial=b; unsigned char in=v,out=0; trial.Decrypt(&in,&out,1);
            if(out==plain) { plain=v; b=trial; break; }
        }
    }
    if(info.eType>=CRYPT_BLOWFISH_TWOFISH) { t.Init(seedBytes); t.Decrypt(gp.data(),gp.data(),gp.size()); }
    emit("gameLogin",gp);
}
''')
    subprocess.run(['g++','-std=c++20','-O0','-I',str(root),str(root/'reference.cpp'), *map(str,(root/'pol/crypt').glob('*.cpp')),'-o',str(root/'reference')],check=True)
    rows=[]
    for version in ('none','1.25.35','1.25.36','1.26.0','2.0.0x','2.0.3','7.0.117.0','67.0.117.0'):
        for seed in (0x12345678,0x01020304):
            result=subprocess.check_output([str(root/'reference'),version,str(seed)],text=True)
            values={k:bytes.fromhex(v) for k,v in (line.split() for line in result.splitlines())}
            row={'Version':version,'Seed':seed}
            for key in ('login','game','send'):
                row[key+'Hashes']={str(n):hashlib.sha256(values[key][:n]).hexdigest().upper() for n in (64,256,257,21036,21037,50000)}
            row['LoginPacket']=values['loginPacket'].hex().upper()
            row['GameLogin']=values['gameLogin'].hex().upper()
            rows.append(row)
    output=Path(__file__).with_name('pol-vectors.json')
    output.write_text(json.dumps({'PolCommit':subprocess.check_output(['git','-C',str(pol),'rev-parse','HEAD'],text=True).strip(),'Vectors':rows},indent=2)+'\n')
    print(output)
