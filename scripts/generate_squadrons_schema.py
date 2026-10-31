"""Generate the command inventory and native inspection schema; run from repository root."""
from pathlib import Path
import sys,re,json,csv,hashlib,io
sys.path.insert(0,str(Path('scripts').resolve()))
from squadrons_native_schema import inspect_native,FIELDS
root=Path('data/schemas/squadrons')
known=Path('src/Heimdall.Squadrons/SquadronsKnownConcepts.cs').read_text()
concepts=[]
for scope,names in re.findall(r'Add\(result, "([^"]+)", """(.*?)"""\);',known,re.S):
 concepts.extend(scope+'.Concept'+name for name in names.split())
source=Path('src/Heimdall.Squadrons/Data/JoystickTemplates_v4')
model,_=inspect_native(source.read_text())
assert concepts and len(concepts)==len(set(concepts))
assert model['bindings']
assert set(concepts)=={r['concept'] for r in model['bindings']}
def decode(f):
 a,b=f['axis'],f['button']
 if (a,b) in {(8,174),(10,174),(9,174),(11,174)}:
  return {8:'JOY_X +',10:'JOY_X -',9:'JOY_Y -',11:'JOY_Y +'}[a]
 for key,code in [('JOY_SLIDER1',14),('JOY_SLIDER2',16),('JOY_Z',40),('JOY_RX',42),('JOY_RY',44),('JOY_RZ',46)]:
  if (a,b)==(26,code):return key+' +'
  if (a,b)==(26,code+1):return key+' -'
 if a==26:
  for n in range(1,129):
   if b==(n+21 if n<=18 else n+45):return 'JOY_BTN'+str(n)
  if 48<=b<=63:return 'JOY_POV'+str((b-48)//4+1)+'_'+['U','D','L','R'][(b-48)%4]
 return 'UNKNOWN'
rows=[]
for b in model['bindings']:
 rows.append({'concept':b['concept'],'index':b['index'],**b['fields'],
              'adapterInput':decode(b['fields']),
              'adapterType':'Axis' if decode(b['fields']).endswith((' +',' -')) else 'Hat' if 'POV' in decode(b['fields']) else 'Button' if 'BTN' in decode(b['fields']) else 'Unknown',
              'evidenceClass':'bundled-nonzero-identifier' if b['fields']['identifier'] else 'bundled-zero-identifier-mixed-observed-and-generalized',
              'gameAcceptance':'unverified'})
labels=dict(re.findall(r'\["([^"\n]+)"\]\s*=\s*"([^"\n]+)"',known))
commands=[]
for c in sorted(concepts):
 rs=[r for r in rows if r['concept']==c]
 name=re.sub(r'(?<=[a-z])(?=[A-Z])',' ',c.split('.Concept',1)[1])
 commands.append({'concept':c,'displayName':labels.get(c,name),'context':c.split('.')[0],
                  'adapterTypes':sorted({r['adapterType'] for r in rs}),
                  'records':[{'index':r['index'],'fields':{k:r[k] for k in FIELDS},'adapterInput':r['adapterInput'],'adapterType':r['adapterType'],
                              'evidenceClass':r['evidenceClass'],'gameAcceptance':r['gameAcceptance']} for r in rs]})
inventory={'schemaVersion':1,'scope':'Current HEIMDALL catalog; not a claim of complete game coverage.',
 'source':str(source),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'catalogSha256':hashlib.sha256(Path('src/Heimdall.Squadrons/SquadronsKnownConcepts.cs').read_bytes()).hexdigest(),
 'codecSha256':hashlib.sha256(Path('src/Heimdall.Squadrons/SquadronsInputCodec.cs').read_bytes()).hexdigest(),
 'commandCount':len(commands),'recordCount':len(rows),'gameAcceptance':'unverified','commands':commands}
(root/'command-inventory.v1.json').write_text(json.dumps(inventory,indent=2)+'\n')
with (root/'command-parameters.v1.csv').open('w',newline='') as out:
 writer=csv.DictWriter(out,fieldnames=['concept','index',*FIELDS,'adapterInput','adapterType','evidenceClass','gameAcceptance'],lineterminator='\n');writer.writeheader();writer.writerows(rows)
lines=['# Squadrons command and parameter reference','',
f'Generated from the current adapter catalog and bundled records. **{len(commands)} known commands; {len(rows)} joystick record variants. Game acceptance is unverified.**',
'','Every value below is a bundled descriptor, not an instruction to activate that binding. `deviceid` and input codes are placeholders until assignment. Multiple rows under a concept are separate variants; their indices and identifiers are not interchangeable. Keyboard, mouse, gamepad and unknown imported records are described by the export schema, but this bundle does not supply their complete command defaults.',
'', 'Zero-identifier rows include both observed and generalized metadata; per-record independent provenance is not yet available. Nonzero identifiers are copied bundle values whose generation rule remains unknown. See the [export contract](../design/squadrons-native-export-schema.md).','']
for command in commands:
 lines.extend(['## '+command['concept'],'','| Index | altbutton | axis | button | deviceid | identifier | modifier | negate | type | Adapter interpretation |','| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |'])
 for record in command['records']:
  lines.append('| '+' | '.join(str(v) for v in [record['index'],*(record['fields'][k] for k in FIELDS),record['adapterInput']])+' |')
 lines.append('')
Path('docs/reference/squadrons-command-parameters.md').write_text('\n'.join(lines))
integer={'type':'integer','minimum':-(2**31),'maximum':2**31-1}
schema={'$schema':'https://json-schema.org/draft/2020-12/schema',
 'title':'Squadrons native file inspection model v1','description':'Parsed view of native text. Schema validity does not prove Squadrons acceptance. Native game exports remain text, not JSON.',
 'type':'object','additionalProperties':False,'required':['format','inputDataVersion','settings','devices','bindings'],
 'properties':{'format':{'const':'squadrons-native-inspection-v1'},'inputDataVersion':{'enum':[4,None]},
 'settings':{'type':'object','additionalProperties':{'type':'string','pattern':'^[^\\r\\n\\u0000]*$'}},
 'devices':{'type':'array','items':{'type':'object','additionalProperties':False,'required':['slot','name'],
 'properties':{'slot':{'type':'integer','minimum':1,'maximum':10,'description':'Current adapter range; not a verified game limit.'},'name':{'type':'string','pattern':'^[^\\r\\n\\u0000]*$'}}}},
 'bindings':{'type':'array','items':{'type':'object','additionalProperties':False,'required':['concept','index','fields'],
 'properties':{'concept':{'type':'string','minLength':1,'pattern':'^[^\\s]+$'},'index':{'type':'integer','minimum':0,'maximum':2**31-1},
 'fields':{'type':'object','required':list(FIELDS),'properties':{k:dict(integer) for k in FIELDS},'additionalProperties':dict(integer)}}}}},
 'allOf':[{'if':{'properties':{'inputDataVersion':{'type':'null'}}},'then':{'properties':{'bindings':{'maxItems':0}}}}]}
(root/'native-export-inspection.v1.schema.json').write_text(json.dumps(schema,indent=2)+'\n')
print('Generated',len(commands),'commands,',len(rows),'complete parameter rows and inspection schema.')
