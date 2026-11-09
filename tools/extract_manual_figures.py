from pathlib import Path
import fitz,json,collections,re,hashlib
root=Path(__file__).resolve().parents[1]/'src/ManualChatbot'
doc=fitz.open(root/'Knowledge/manual.pdf')
out=root/'Knowledge/figures';out.mkdir(exist_ok=True)
counts=collections.Counter(i['xref'] for p in doc for i in p.get_image_info(xrefs=True))
manifest=[]
def title(p):
 lines=[x.strip() for x in p.get_text().splitlines() if x.strip()]
 return lines[0] if lines else ''
def key(t):return re.sub(r'[^a-z]','',t.lower())
def labels(p):
 return [(fitz.Rect(b[:4]),b[4].strip().replace('\n',' ')) for b in p.get_text('blocks') if len(b)>4 and len(b[4])<200 and ('•' in b[4] or not re.match(r'^(?:[A-Z] ){3}',b[4]))]
for n,page in enumerate(doc):
 infos=[i for i in page.get_image_info(xrefs=True) if counts[i['xref']]<20 and i['width']>=180 and i['height']>=100]
 seen=set()
 for i in sorted(infos,key=lambda i:(round(i['bbox'][1]/30),i['bbox'][0])):
  rect=fitz.Rect(i['bbox']) & page.rect
  if rect.is_empty or rect.width<65 or rect.height<50 or tuple(rect) in seen:continue
  seen.add(tuple(rect))
  ident='fig-'+hashlib.sha256(f'{n+1}:{tuple(rect)}'.encode()).hexdigest()[:12]
  zoom=min(2.5,1600/max(rect.width,rect.height))
  pix=page.get_pixmap(matrix=fitz.Matrix(zoom,zoom),clip=rect,alpha=False)
  from PIL import Image
  img=Image.frombytes('RGB',[pix.width,pix.height],pix.samples);img.save(out/(ident+'.webp'),'WEBP',quality=88)
  candidate=labels(page)
  def distance(b):
   r=b[0];dx=max(rect.x0-r.x1,r.x0-rect.x1,0);dy=max(rect.y0-r.y1,r.y0-rect.y1,0)
   return dx*dx+dy*dy
  cap=min(candidate,key=distance)[1] if candidate else 'Manual screenshot'
  cap=cap.replace('•','').strip();cap=cap if 3<len(cap)<140 else 'Manual screenshot'
  pages=[n+1]
  if n>0:
   current=key(title(page));previous=key(title(doc[n-1]))
   # Explicit visual continuation: link to the preceding explanation, not arbitrary neighbors.
   base=re.sub(r'(visualview|image|view)\d*$','',current)
   if ('visualview' in current or 'image' in current) and (len(base)<5 or previous.startswith(base) or base.startswith(previous)):
    pages.append(n)
  manifest.append({'id':ident,'page':n+1,'relatedPages':pages,'caption':cap,'fileName':ident+'.webp'})
raw=(root/'Knowledge/manual.pages.json').read_bytes()
(root/'Knowledge/figures.json').write_text(json.dumps({'corpusHash':hashlib.sha256(raw).hexdigest().upper(),'images':manifest},indent=2))
print('Exported',len(manifest),'screenshots; MB',round(sum(f.stat().st_size for f in out.glob('*.webp'))/1e6,2))
print([m for m in manifest if m['page']==14])
