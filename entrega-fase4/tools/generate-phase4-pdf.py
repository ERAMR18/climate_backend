"""Build reproducible Phase 4 PDF and retain the previous general PDF as history.

Only explicitly selected documentation/source files are rendered. Private values are
read solely to verify they do not occur in extracted PDF text; never printed.
"""
from __future__ import annotations

import hashlib
import html
import json
import os
from pathlib import Path
import re
import shutil
import sys
import textwrap

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools/pdf-libs'))
from pypdf import PdfReader, PdfWriter
from reportlab.graphics import renderSVG
from reportlab.graphics.shapes import Drawing, Line, Polygon, Rect, String
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate, Frame, KeepTogether, NextPageTemplate, PageBreak,
    PageTemplate, Paragraph, Spacer, Table, TableStyle, XPreformatted,
)
from reportlab.platypus.tableofcontents import TableOfContents

DOCS = ROOT / 'docs'
OUT = ROOT / 'Documentacion_Climate_Monitoring_Fase_4.pdf'
GENERAL = ROOT / 'Documentacion_Climate_Monitoring_System.pdf'
ARCHIVE = DOCS / 'archive/Documentacion_Climate_Monitoring_System_antes_Fase_4.pdf'
DATE = '4 de noviembre de 2026'
PAGE_W, PAGE_H = A4
LEFT, RIGHT, TOP, BOTTOM = 49, 49, 57, 47
WIDTH = PAGE_W - LEFT - RIGHT
INK = colors.HexColor('#17324d')
ACCENT = colors.HexColor('#087d89')
MUTED = colors.HexColor('#52657a')
PALE = colors.HexColor('#edf4f8')


def setup_fonts():
    font_dir = Path(os.environ.get('WINDIR', 'C:/Windows')) / 'Fonts'
    for name, filename in [('Body', 'segoeui.ttf'), ('BodyBold', 'segoeuib.ttf'),
                           ('BodyItalic', 'segoeuii.ttf'), ('Mono', 'consola.ttf'), ('MonoBold', 'consolab.ttf')]:
        pdfmetrics.registerFont(TTFont(name, str(font_dir / filename)))
    pdfmetrics.registerFontFamily('Body', normal='Body', bold='BodyBold', italic='BodyItalic', boldItalic='BodyBold')
    pdfmetrics.registerFontFamily('Mono', normal='Mono', bold='MonoBold', italic='Mono', boldItalic='MonoBold')


def make_styles():
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle('BodyPhase4', fontName='Body', fontSize=9.3, leading=14, textColor=INK,
                              spaceAfter=6.5, splitLongWords=True, allowWidows=False, allowOrphans=False))
    styles.add(ParagraphStyle('SmallPhase4', parent=styles['BodyPhase4'], fontSize=7.8, leading=11, spaceAfter=3))
    styles.add(ParagraphStyle('CodePhase4', fontName='Mono', fontSize=7.2, leading=10.3,
                              textColor=INK, spaceAfter=0, allowWidows=True, allowOrphans=True))
    for level, size in [(1, 18), (2, 12.5), (3, 10.5)]:
        styles.add(ParagraphStyle(f'H{level}Phase4', fontName='BodyBold', fontSize=size,
                                  leading=size + 5, textColor=INK if level == 1 else ACCENT,
                                  spaceBefore=13 if level > 1 else 0, spaceAfter=9, keepWithNext=True))
    styles.add(ParagraphStyle('BulletPhase4', parent=styles['BodyPhase4'], leftIndent=12,
                              firstLineIndent=-10, bulletIndent=0))
    styles.add(ParagraphStyle('CoverTitlePhase4', fontName='BodyBold', fontSize=33, leading=40,
                              textColor=INK, spaceAfter=20))
    styles.add(ParagraphStyle('CoverSubPhase4', fontName='Body', fontSize=15, leading=22,
                              textColor=ACCENT, spaceAfter=18))
    return styles


def inline(text):
    text = html.escape(text)
    text = re.sub(r'`([^`]+)`', r'<font name="Mono">\1</font>', text)
    text = re.sub(r'\*\*([^*]+)\*\*', r'<b>\1</b>', text)
    def link(match):
        label, target = match.groups()
        if target.startswith(('https://', 'http://')):
            return f'<link href="{target}" color="#087d89">{label}</link>'
        return f'{label} <font color="#52657a">({target})</font>'
    return re.sub(r'\[([^\]]+)\]\(([^)]+)\)', link, text)


def arrow(drawing, x1, y1, x2, y2):
    drawing.add(Line(x1, y1, x2, y2, strokeColor=ACCENT, strokeWidth=1.4))
    if abs(x2-x1) > abs(y2-y1):
        direction = 1 if x2 > x1 else -1
        points = [x2, y2, x2-direction*6, y2+3.5, x2-direction*6, y2-3.5]
    else:
        direction = 1 if y2 > y1 else -1
        points = [x2, y2, x2-3.5, y2-direction*6, x2+3.5, y2-direction*6]
    drawing.add(Polygon(points, fillColor=ACCENT, strokeColor=ACCENT))


def architecture():
    drawing = Drawing(WIDTH, 400)
    w, h = 144, 48
    x1, x2, x3 = 0, (WIDTH-w)/2, WIDTH-w
    def box(x, y, title, subtitle):
        drawing.add(Rect(x, y, w, h, rx=7, ry=7, fillColor=PALE, strokeColor=colors.HexColor('#bbd0db')))
        drawing.add(String(x+w/2, y+29, title, textAnchor='middle', fontName='BodyBold', fontSize=10, fillColor=INK))
        drawing.add(String(x+w/2, y+12, subtitle, textAnchor='middle', fontName='Body', fontSize=8.2, fillColor=MUTED))
    box(x1, 342, 'Arduino Uno', 'Simulación o DHT11')
    box(x2, 342, 'Puente en Windows', 'Serial + .NET 10')
    box(x3, 342, 'Gateway VPS', 'Dominio HTTPS')
    box(x2, 254, 'Cola SQLite', 'UUID estable y reintentos')
    box(x3, 254, 'Monitoring', 'Dispositivo / sensor / fuente')
    box(x3, 166, 'SQL Server', 'Lecturas + despacho pendiente')
    box(x2, 166, 'Alert Service', 'Reglas y deduplicación')
    box(x1, 166, 'SignalR existente', 'Evento + recuperación REST')
    box(x1, 78, 'Dashboard Angular', 'Actual / histórico / origen')
    box(x1, 0, 'Auth NestJS', 'Usuarios, roles y JWT')
    box(x2, 0, 'Google / GitHub', 'OIDC / OAuth 2.0')
    arrow(drawing,x1+w,366,x2,366)
    arrow(drawing,x2+w,366,x3,366)
    arrow(drawing,x2+w/2,342,x2+w/2,302)
    arrow(drawing,x3+w/2,342,x3+w/2,302)
    arrow(drawing,x3+w/2,254,x3+w/2,214)
    arrow(drawing,x3,190,x2+w,190)
    arrow(drawing,x2,190,x1+w,190)
    arrow(drawing,x1+w/2,166,x1+w/2,126)
    arrow(drawing,x1+w/2,78,x1+w/2,48)
    arrow(drawing,x1+w,24,x2,24)
    return drawing


class Phase4Doc(BaseDocTemplate):
    def __init__(self, filename):
        super().__init__(str(filename), pagesize=A4, leftMargin=LEFT, rightMargin=RIGHT,
                         topMargin=TOP, bottomMargin=BOTTOM, title='Climate Monitoring System — Fase 4',
                         author='Climate Monitoring System', subject='OAuth, VPS, Arduino, puente y operación')
        frame = Frame(LEFT, BOTTOM, WIDTH, PAGE_H-TOP-BOTTOM, leftPadding=0, rightPadding=0,
                      topPadding=0, bottomPadding=0)
        self.addPageTemplates(PageTemplate(id='Main', frames=[frame], onPage=self.decorate))

    def beforeDocument(self):
        self.heading_index = 0

    def decorate(self, canvas, doc):
        if doc.page == 1:
            canvas.setFillColor(ACCENT)
            canvas.rect(0, PAGE_H-13, PAGE_W, 13, stroke=0, fill=1)
            return
        canvas.saveState()
        canvas.setStrokeColor(colors.HexColor('#d2dfe8'))
        canvas.line(LEFT, PAGE_H-37, PAGE_W-RIGHT, PAGE_H-37)
        canvas.setFont('Body', 8)
        canvas.setFillColor(MUTED)
        canvas.drawString(LEFT, PAGE_H-28, 'Climate Monitoring System  |  Fase 4')
        canvas.drawRightString(PAGE_W-RIGHT, PAGE_H-28, 'Edición técnica y operativa')
        canvas.line(LEFT, 33, PAGE_W-RIGHT, 33)
        canvas.drawString(LEFT, 20, DATE)
        canvas.drawRightString(PAGE_W-RIGHT, 20, f'Página {doc.page}')
        canvas.restoreState()

    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph) and hasattr(flowable, 'toc_level'):
            key = f'phase4-section-{self.heading_index}'
            self.heading_index += 1
            text = flowable.getPlainText()
            self.canv.bookmarkPage(key)
            level = flowable.toc_level
            self.canv.addOutlineEntry(text, key, level=level, closed=True)
            self.notify('TOCEntry', (level, html.escape(text), self.page, key))


def heading(story, text, level, styles, toc=True):
    para = Paragraph(inline(text), styles[f'H{min(level+1,3)}Phase4'])
    if toc and level <= 1:
        para.toc_level = level
    story.append(para)


def safe_split_positions(line):
    positions=[]
    quote=None
    escaped=False
    for i, char in enumerate(line):
        if escaped:
            escaped=False
            continue
        if char in ('\\', '`'):
            escaped=True
            continue
        if quote:
            if char == quote:
                quote=None
        elif char in ("'", '"'):
            quote=char
        elif char == ' ':
            positions.append(i)
    return positions


def formatted_code(lines, language):
    maximum=108
    result=[]
    shell=language.lower() in ('powershell','bash','sh')
    for original in lines:
        line=original.expandtabs(4)
        if len(line)<=maximum:
            result.append(line)
            continue
        if shell and not line.lstrip().startswith('#'):
            remaining=line
            while len(remaining)>maximum:
                spaces=[i for i in safe_split_positions(remaining) if 22<i<maximum-3]
                if not spaces: break
                cut=spaces[-1]
                result.append(remaining[:cut] + (' `' if language.lower()=='powershell' else ' \\'))
                remaining='  '+remaining[cut+1:]
            result.append(remaining)
        else:
            result.extend(textwrap.wrap(line, maximum, subsequent_indent='  ', break_long_words=True,
                                        break_on_hyphens=False, replace_whitespace=False, drop_whitespace=False) or [''])
    return result


def code_block(story, lines, language, styles):
    lines=formatted_code(lines,language)
    longest=max((pdfmetrics.stringWidth(line,'Mono',7.2) for line in lines),default=1)
    code_style=ParagraphStyle('CodeFit',parent=styles['CodePhase4'])
    if longest>WIDTH-18:
        code_style.fontSize=max(5.4,7.2*(WIDTH-18)/longest)
        code_style.leading=code_style.fontSize+3
    for start in range(0,len(lines),32):
        block=lines[start:start+32]
        if start:
            story.append(Paragraph('Código (continuación)',styles['SmallPhase4']))
        pre=XPreformatted(html.escape('\n'.join(block)),code_style)
        table=Table([[pre]],colWidths=[WIDTH],hAlign='LEFT')
        table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,-1),colors.HexColor('#f0f4f7')),
                                   ('BOX',(0,0),(-1,-1),.4,colors.HexColor('#ccdae3')),
                                   ('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),
                                   ('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]))
        story.extend([table,Spacer(1,8)])


def table_rows(story, lines, styles):
    rows=[[cell.strip() for cell in line.strip().strip('|').split('|')] for line in lines]
    rows=[row for row in rows if not all(re.fullmatch(r':?-{3,}:?',cell.strip()) for cell in row)]
    count=max(map(len,rows))
    if count>5:
        headers=rows[0]
        for row in rows[1:]:
            heading(story,row[0]+' — '+row[1],2,styles,toc=False)
            data=[]
            for index,value in enumerate(row):
                data.append([Paragraph(inline(headers[index]),styles['SmallPhase4']),Paragraph(inline(value),styles['SmallPhase4'])])
            table=Table(data,colWidths=[112,WIDTH-112],hAlign='LEFT')
            table.setStyle(TableStyle([('BACKGROUND',(0,0),(0,-1),PALE),('VALIGN',(0,0),(-1,-1),'TOP'),
                                       ('BOX',(0,0),(-1,-1),.35,colors.HexColor('#c4d6e0')),
                                       ('INNERGRID',(0,0),(-1,-1),.2,colors.HexColor('#d8e3e9')),
                                       ('LEFTPADDING',(0,0),(-1,-1),6),('RIGHTPADDING',(0,0),(-1,-1),6)]))
            story.extend([table,Spacer(1,9)])
        return
    if count==2:
        widths=[WIDTH*.31,WIDTH*.69]
    elif count==3:
        widths=[WIDTH*.26,WIDTH*.38,WIDTH*.36]
    else:
        widths=[WIDTH/count]*count
    data=[]
    for index,row in enumerate(rows):
        style=ParagraphStyle('TableCell',parent=styles['SmallPhase4'],textColor=colors.white if index==0 else INK,
                             fontName='BodyBold' if index==0 else 'Body')
        data.append([Paragraph(inline(value),style) for value in row]+['']*(count-len(row)))
    table=Table(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
    table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),INK),('VALIGN',(0,0),(-1,-1),'TOP'),
                               ('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#f2f6f9')]),
                               ('GRID',(0,0),(-1,-1),.25,colors.HexColor('#c9d8e1')),
                               ('LEFTPADDING',(0,0),(-1,-1),6),('RIGHTPADDING',(0,0),(-1,-1),6),
                               ('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6)]))
    story.extend([table,Spacer(1,9)])


def markdown(story,path,styles,annex_title=None):
    text=path.read_text(encoding='utf-8-sig')
    if annex_title:
        story.append(PageBreak())
        heading(story,annex_title,0,styles)
    lines=text.splitlines()
    i=0
    first_major=True
    while i<len(lines):
        line=lines[i].rstrip()
        if line.startswith('# '):
            i+=1
            continue
        if line.startswith('```'):
            language=line[3:].strip()
            code=[]
            i+=1
            while i<len(lines) and not lines[i].startswith('```'):
                code.append(lines[i]); i+=1
            code_block(story,code,language,styles)
            i+=1
            continue
        if not line.strip():
            i+=1
            continue
        if line.startswith('|'):
            table=[]
            while i<len(lines) and lines[i].startswith('|'):
                table.append(lines[i]); i+=1
            table_rows(story,table,styles)
            continue
        if line.startswith('![') and 'fase4-arquitectura.svg' in line:
            story.extend([architecture(),Spacer(1,9)])
            i+=1
            continue
        match=re.match(r'^(#{2,5})\s+(.+)',line)
        if match:
            level=len(match[1])-2+(1 if annex_title else 0)
            if level==0:
                if not first_major:
                    story.append(PageBreak())
                first_major=False
            heading(story,match[2],level,styles)
            i+=1
            continue
        if line.startswith('- '):
            story.append(Paragraph('• '+inline(line[2:]),styles['BulletPhase4']))
        elif re.match(r'^\d+\.\s',line):
            story.append(Paragraph(inline(line),styles['BulletPhase4']))
        else:
            story.append(Paragraph(inline(line),styles['BodyPhase4']))
        i+=1


def private_values():
    values=set()
    for path in [ROOT/'backend/.env.phase3.local',ROOT/'tools/Climate.ArduinoBridge/device-token.local']:
        if not path.is_file(): continue
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            if path.name=='device-token.local':
                value=line.strip()
                if len(value)>20: values.add(value)
            elif '=' in line:
                key,value=line.split('=',1)
                if re.search(r'PASSWORD|SECRET|SIGNING_KEY|API_KEY|TOKEN',key) and not key.endswith('SHA256'):
                    value=value.strip().strip('"\'')
                    if len(value)>20: values.add(value)
    return values


def audit_pdf(path,secrets):
    reader=PdfReader(path)
    text='\n'.join(page.extract_text() or '' for page in reader.pages)
    compact=re.sub(r'\s+','',text)
    if any(value in text or re.sub(r'\s+','',value) in compact for value in secrets):
        raise RuntimeError('Private credential detected in PDF; delivery blocked without printing its value.')
    if re.search(r'gh[pousr]_[A-Za-z0-9]{25,}',text):
        raise RuntimeError('Potential provider token detected in PDF; inspect privately.')
    return len(reader.pages),text


def build_pdf():
    setup_fonts()
    styles=make_styles()
    figure_dir=DOCS/'figures'
    figure_dir.mkdir(exist_ok=True)
    renderSVG.drawToFile(architecture(),str(figure_dir/'fase4-arquitectura.svg'))
    story=[Spacer(1,68),Paragraph('CLIMATE MONITORING SYSTEM',styles['CoverSubPhase4']),
           Paragraph('Fase 4',styles['CoverTitlePhase4']),
           Paragraph('Documentación técnica<br/>y guía de operación',styles['CoverTitlePhase4']),
           Paragraph('OAuth / OIDC · VPS · Arduino Uno<br/>DHT11 · Puente C# · Dashboard en tiempo real',styles['CoverSubPhase4']),
           Spacer(1,20),Paragraph(DATE,styles['BodyPhase4']),
           Paragraph('Edición consolidada con guías completas de cambio de UUID y puesta en marcha desde otra PC.',styles['BodyPhase4']),
           Spacer(1,20),Paragraph('Estado transparente: pruebas locales verificadas, operación VPS reportada por el usuario y pendientes externos diferenciados. Sin credenciales reales.',styles['SmallPhase4']),PageBreak()]
    heading(story,'Contenido',0,styles,toc=False)
    toc=TableOfContents()
    toc.levelStyles=[ParagraphStyle('TOC0',fontName='Body',fontSize=9.4,leading=14,leftIndent=0,spaceBefore=5,textColor=INK),
                     ParagraphStyle('TOC1',fontName='Body',fontSize=8.5,leading=12,leftIndent=14,spaceBefore=2,textColor=MUTED)]
    toc.dotsMinLevel=0
    story.extend([toc,PageBreak()])
    markdown(story,DOCS/'FASE_4_DOCUMENTACION_COMPLETA.md',styles)
    markdown(story,DOCS/'GUIA_ARDUINO_CAMBIAR_SENSOR_UUID.md',styles,'Anexo A. Cambiar UUID y configurar la asociación en el VPS')
    markdown(story,DOCS/'GUIA_ARDUINO_NUEVA_PC.md',styles,'Anexo B. Arduino y puente desde una PC Windows nueva')
    markdown(story,DOCS/'FASE_4_MATRIZ_REQUISITOS.md',styles,'Anexo C. Matriz completa de requisitos')
    story.append(PageBreak())
    heading(story,'Anexo D. Firmware DHT11 físico',0,styles)
    story.append(Paragraph('Código fuente incluido para consulta. Compilado para Arduino Uno; la temperatura utiliza source ARDUINO. Copiar/cargar desde el archivo .ino original para conservar el texto exacto.',styles['BodyPhase4']))
    code_block(story,(ROOT/'backend/lecturas-sensor/lecturas-sensor.ino').read_text(encoding='utf-8-sig').splitlines(),'cpp',styles)
    story.append(PageBreak())
    heading(story,'Anexo E. Firmware simulado y referencia LM35',0,styles)
    story.append(Paragraph('SIMULATE_SENSOR=1 produce datos de prueba sin sensor físico. Con valor0 se requiere el cableado y referencia ADC del LM35.',styles['BodyPhase4']))
    code_block(story,(ROOT/'hardware/arduino/temperature-sensor/temperature-sensor.ino').read_text(encoding='utf-8-sig').splitlines(),'cpp',styles)
    story.append(PageBreak())
    heading(story,'Anexo F. Regeneración y evidencias',0,styles)
    story.append(Paragraph('Los comandos largos del PDF pueden mostrarse en varias líneas con continuaciones de shell. El Markdown fuente conserva los comandos completos editables. No leer ni publicar archivos privados para regenerar.',styles['BodyPhase4']))
    code_block(story,['python -m pip install --target tools/pdf-libs -r tools/requirements-phase4-pdf.txt',
                      'python tools/phase4-doc-matrix.py','python tools/generate-phase4-pdf.py'],'powershell',styles)
    story.append(Paragraph('El PDF general preserva sus páginas previas desde docs/archive/Documentacion_Climate_Monitoring_System_antes_Fase_4.pdf y agrega esta edición una sola vez. Volver a ejecutar no duplica el anexo. Para revisar avance: Get-Content .\\FASE_4_PROGRESO.md.',styles['BodyPhase4']))
    Phase4Doc(OUT).multiBuild(story,maxPasses=5)
    secrets=private_values()
    page_count,text=audit_pdf(OUT,secrets)
    required=['OAuth','DHT11','ARDUINO_SOURCE','SQLite','GitHub','Anexo A','Anexo B','RF-OAUTH-05','RNF-OAUTH-02']
    if not all(term in text for term in required):
        raise RuntimeError('Required Phase 4 content missing in extracted PDF.')
    if 'Placeholder for table of contents' in text:
        raise RuntimeError('Table of contents was not resolved.')
    if GENERAL.exists() and not ARCHIVE.exists():
        audit_pdf(GENERAL,secrets)
        ARCHIVE.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(GENERAL,ARCHIVE)
    writer=PdfWriter()
    baseline_pages=0
    writer.append(str(OUT),outline_item='Fase 4 — edición completa y Arduino')
    if ARCHIVE.exists():
        baseline_pages=len(PdfReader(ARCHIVE).pages)
        writer.append(str(ARCHIVE),outline_item='Anexo histórico — Fase 3 conservada')
    writer.add_metadata({'/Title':'Climate Monitoring System — Fases anteriores y Fase 4 completa',
                         '/Author':'Climate Monitoring System','/Subject':'OAuth, VPS, Arduino, puente y dashboard; sin secretos',
                         '/Phase4Edition':'2026-11-04'})
    temp=GENERAL.with_suffix('.phase4.tmp.pdf')
    with temp.open('wb') as stream: writer.write(stream)
    writer.close()
    combined_pages,_=audit_pdf(temp,secrets)
    os.replace(temp,GENERAL)
    validation={'edition':'2026-11-04','phase4_pages':page_count,'previous_pages_preserved':baseline_pages,
                'consolidated_pages':combined_pages,'previous_pages_start':page_count+1,
                'required_sections_present':True,'private_values_detected':False,
                'sources':['docs/FASE_4_DOCUMENTACION_COMPLETA.md','docs/GUIA_ARDUINO_CAMBIAR_SENSOR_UUID.md',
                           'docs/GUIA_ARDUINO_NUEVA_PC.md','docs/FASE_4_MATRIZ_REQUISITOS.md'],
                'outputs':{path.name:{'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
                           for path in [OUT,GENERAL]}}
    (DOCS/'validation').mkdir(exist_ok=True)
    (DOCS/'validation/phase4-pdf.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:validation[k] for k in ['phase4_pages','previous_pages_preserved','consolidated_pages','private_values_detected']},ensure_ascii=False))


if __name__=='__main__':
    build_pdf()
