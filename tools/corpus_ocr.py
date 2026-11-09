"""Optional offline import: pip install pymupdf; Tesseract with eng language on PATH."""
import argparse,concurrent.futures,json,subprocess,tempfile
from pathlib import Path
import fitz
parser=argparse.ArgumentParser()
parser.add_argument('pdf');parser.add_argument('output');parser.add_argument('--workers',type=int,default=4)
args=parser.parse_args();pdf=Path(args.pdf).resolve()
with fitz.open(pdf) as source:count=len(source)
def read_page(index):
 with fitz.open(pdf) as doc:
  page=doc[index];text=page.get_text(sort=True).strip();ocr=''
  if len(text)<120:
   with tempfile.TemporaryDirectory() as temp:
    path=Path(temp)/'page.png';page.get_pixmap(matrix=fitz.Matrix(1.65,1.65)).save(path)
    result=subprocess.run(['tesseract',str(path),'stdout','-l','eng','--psm','11'],text=True,capture_output=True,timeout=60,check=True)
    ocr=result.stdout.strip()
  return {'page':index+1,'text':text,'ocrText':ocr,'source':'text+ocr' if ocr else 'text'}
with concurrent.futures.ThreadPoolExecutor(max_workers=max(1,min(args.workers,8))) as pool:pages=list(pool.map(read_page,range(count)))
output=Path(args.output);output.parent.mkdir(parents=True,exist_ok=True)
temporary=output.with_suffix('.tmp')
temporary.write_text(json.dumps({'documentId':'on-air-manual','title':'On-Air CMS User Manual','pages':pages},ensure_ascii=False,indent=2),encoding='utf-8')
temporary.replace(output)
print(f'Imported {count} pages; {sum(bool(p["ocrText"]) for p in pages)} OCR pages. Review OCR before deployment.')
