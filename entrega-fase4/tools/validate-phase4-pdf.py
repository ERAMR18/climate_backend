"""Read PDF artifacts and render representative pages for visual inspection."""
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools/pdf-libs'))
import pymupdf
from PIL import Image, ImageDraw
from pypdf import PdfReader

pdf=ROOT/'Documentacion_Climate_Monitoring_Fase_4.pdf'
doc=pymupdf.open(str(pdf))
pages_text=[page.get_text() for page in doc]
outside=[]
blank=[]
for index,page in enumerate(doc):
    if len(page.get_text().strip())<70:
        blank.append(index+1)
    for block in page.get_text('dict')['blocks']:
        if block.get('type')!=0: continue
        for line in block['lines']:
            for span in line['spans']:
                x0,y0,x1,y1=span['bbox']
                if x0 < -1 or y0 < -1 or x1 > page.rect.width+1 or y1 > page.rect.height+1:
                    outside.append({'page':index+1,'bbox':[round(v,2) for v in span['bbox']]})

needles=['4. Arquitectura de extremo','5. OAuth 2.0','11. API de telemetría',
         'Anexo A. Cambiar UUID','Anexo B. Arduino y puente','RF-OAUTH-04 —',
         'Anexo D. Firmware DHT11']
selected=[0,1]
for needle in needles:
    found=[i for i,text in enumerate(pages_text) if needle in text and i>4]
    if found: selected.append(found[0])
selected=list(dict.fromkeys(selected))
previews=ROOT/'docs/validation/pdf-preview'
previews.mkdir(parents=True,exist_ok=True)
thumbs=[]
for index in selected:
    pix=doc[index].get_pixmap(matrix=pymupdf.Matrix(1.25,1.25),alpha=False)
    target=previews/f'fase4-pagina-{index+1:02}.png'
    pix.save(str(target))
    thumbnail=Image.open(target).convert('RGB')
    thumbnail.thumbnail((480,680))
    tile=Image.new('RGB',(500,715),'#dce5eb')
    tile.paste(thumbnail,((500-thumbnail.width)//2,20))
    ImageDraw.Draw(tile).text((15,695),f'Pagina {index+1}',fill='#17324d')
    thumbs.append(tile)
columns=3
sheet=Image.new('RGB',(columns*500,((len(thumbs)+columns-1)//columns)*715),'white')
for index,tile in enumerate(thumbs): sheet.paste(tile,((index%columns)*500,(index//columns)*715))
sheet.save(previews/'contacto-fase4.png')

original=PdfReader(ROOT/'docs/archive/Documentacion_Climate_Monitoring_System_antes_Fase_4.pdf')
combined=PdfReader(ROOT/'Documentacion_Climate_Monitoring_System.pdf')
preserved=all((original.pages[i].extract_text() or '')==(combined.pages[len(doc)+i].extract_text() or '') for i in range(len(original.pages)))
outline_count=len(PdfReader(pdf).outline)
assert not outside, 'Text outside PDF media box'
assert not blank, 'Unexpected empty pages'
assert preserved, 'Previous PDF pages changed'
assert len(combined.pages)==len(original.pages)+len(doc), 'Duplicate/missing annex'
assert outline_count>15, 'Missing PDF navigation bookmarks'
result={'phase4_pages':len(doc),'consolidated_pages':len(combined.pages),
        'text_outside_page':outside,'unexpected_empty_pages':blank,
        'previous_text_preserved':preserved,'navigation_entries_top_level':outline_count,
        'preview_pages':[i+1 for i in selected]}
(ROOT/'docs/validation/phase4-pdf-layout.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
