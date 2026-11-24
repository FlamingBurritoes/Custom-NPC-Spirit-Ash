"""Build compact catalog data from WitchyBND XML and matching stock PARAMs."""
from pathlib import Path
from struct import unpack_from
import re
import sys
import xml.etree.ElementTree as ET

source = Path(sys.argv[1])
target = Path(__file__).resolve().parents[1] / 'src/SpiritCreator.Core/Catalog'
target.mkdir(exist_ok=True)

def table(name, size):
    xml = ET.parse(source / (name + '.param.xml'))
    b = (source / (name + '.param')).read_bytes()
    names = {int(r.get('id')): r.get('paramdexName', '') for r in xml.findall('rows/row')}
    for i in range(unpack_from('<H', b, 10)[0]):
        id, _, offset, _ = unpack_from('<iIQQ', b, 64 + i * 24)
        yield id, names.get(id, ''), b[offset:offset+size]

def write(name, rows):
    rows = list(rows)
    (target / (name + '.tsv')).write_text(''.join('\t'.join(map(str, r)) + '\n' for r in rows), encoding='utf-8')
    print(name, len(rows))

def i32(b, o): return unpack_from('<i', b, o)[0]
def u16(b, o): return unpack_from('<H', b, o)[0]
def named(n): return n and not n.startswith('[') and not re.search(r'\b(unused|dummy|test|debug)\b', n, re.I)

w = {i: (n,b) for i,n,b in table('EquipParamWeapon', 664)}
curves = {i for i,_,_ in table('ReinforceParamWeapon',128)}
slots = [1,3,5,7,9,11,13,14,15,16,17,19,21,23,24,25,27,28,29,31,35,37,39,41,50,51,53,55,56,57,-1,61,65,67,69,87,88,89,90,91,92,93,94,95]
def weapons():
    for i,(n,b) in w.items():
        t = u16(b,422)
        if i < 1000000 or i % 10000 or not named(n) or t not in slots:
            continue
        max_level = 0
        for level in range(1,26):
            off = 96 + level*4 if level < 16 else 592 + (level-16)*4
            if i32(b,off) != i or u16(b,218)+level not in curves: break
            max_level = level
        mask = 1
        if b[577] == 2 and not b[265]&128:
            for a in range(1,13):
                v = w.get(i+a*100)
                if v and i32(v[1],96) == i: mask |= 1<<a
        yield i,n,*b[242:246],b[405],t,max_level,mask,slots.index(t) if b[577]==2 else -1, int(bool(b[261]&128)),int(bool(b[262]&2))

write('Weapons',weapons())
write('Ammo',((i,n,u16(b,422)) for i,(n,b) in w.items() if i%10000==0 and named(n) and u16(b,422) in (81,83,85,86)))
write('Armor', ((i,n,b[214]) for i,n,b in table('EquipParamProtector',416)
                if named(n) and b[214]<4 and b[220]&(1<<(b[214]+1))))
write('Talismans', ((i,n,i32(b,4)) for i,n,b in table('EquipParamAccessory',96)
                     if named(n) and b[49]==2 and i32(b,4)>0))
goods = list(table('EquipParamGoods',176))
spell_goods = {i for i,n,b in goods if b[62] in (5,16,17,18) and n.startswith(('[Sorcery] ','[Incantation] '))}
write('Spells', ((i,n.split('] ',1)[1],b[34],b[35],b[14],i32(b,116),b[38]) for i,n,b in table('Magic',168)
                if i in spell_goods and n.startswith(('[Sorcery] ','[Incantation] ')) and 'unused' not in n.lower()))
write('Ashes', ((i,n[12:],i32(b,24),b[56:62].hex(),u16(b,48)&8191) for i,n,b in table('EquipParamGem',96)
                if n.startswith('Ash of War: ') and i>=10000 and u16(b,4)>0 and u16(b,48)&1))
write('Items', ((i, 'NPC Crimson Flask' if i==50201 else 'NPC Crimson Flask (strong)' if i==50203 else n,
                min(u16(b,58),3 if i in (50201,50203) else 10),i32(b,124),i32(b,4),b[63])
                for i,n,b in goods if named(n) and i not in (2001202,2001203) and b[72]&128 and b[63] in (1,2)
                and u16(b,58)>0 and i32(b,124)//1000000 in (10,20,30,31)))
