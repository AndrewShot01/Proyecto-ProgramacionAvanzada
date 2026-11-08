"""Genera el informe con las evidencias reales de docs/evidencias.
Requiere python-docx y Pillow. No consulta ni modifica PostgreSQL.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / 'docs'
QA = ROOT / 'artifacts' / 'informe'
QA.mkdir(parents=True, exist_ok=True)
FONT = Path('C:/Windows/Fonts/arial.ttf')
BOLD = Path('C:/Windows/Fonts/arialbd.ttf')
font = ImageFont.truetype(str(FONT), 25)
bold = ImageFont.truetype(str(BOLD), 29)
im = Image.new('RGB', (1500, 1460), 'white')
draw = ImageDraw.Draw(im)
def box(x, y, w, title, lines):
    h = 67 + len(lines) * 40
    draw.rounded_rectangle((x,y,x+w,y+h), 10, fill='#fafbf8', outline='#9ba99d', width=2)
    draw.rectangle((x+1,y+1,x+w-1,y+57),fill='#254b3b')
    draw.text((x+22,y+13),title,font=bold,fill='white')
    for k,line in enumerate(lines): draw.text((x+22,y+72+k*40),line,font=font,fill='#26382d')
def edge(points, label, pos):
    draw.line(points,fill='#748774',width=4)
    x,y=points[-1]; draw.polygon([(x,y),(x-8,y-17),(x+8,y-17)],fill='#748774')
    draw.text(pos,label,font=font,fill='#345e40')
box(40,40,620,'mp_sucursales',['PK id · integer identity','UQ nombre · varchar(100)','direccion · varchar(250)','telefono · varchar(25)'])
box(830,40,630,'mp_productos',['PK id · integer identity','UQ codigo · varchar(30)','nombre · varchar(120)','precio · numeric(12,2) > 0','atributos · jsonb objeto','maximo_reserva · integer > 0','horas_reserva · numeric > 0','existencia_minima · integer >= 0'])
box(40,520,620,'mp_inventarios',['PK id · integer identity','FK sucursal_id → mp_sucursales','FK producto_id → mp_productos','UQ (sucursal_id, producto_id)'])
box(830,570,630,'mp_clientes',['PK id · integer identity','UQ identificacion · varchar(30)','nombre · varchar(120)','UQ correo · varchar(150)','telefono · varchar(25)'])
box(40,1000,620,'mp_ingresos',['PK id · integer identity','FK inventario_id → mp_inventarios','cantidad · integer > 0','fecha · timestamptz','referencia · varchar(100)'])
box(830,1000,630,'mp_reservas',['PK id · integer identity','FK inventario_id → mp_inventarios','FK cliente_id → mp_clientes','cantidad · integer > 0','precio_unitario · numeric(12,2)','fecha_reserva · timestamptz','fecha_vencimiento · timestamptz','fecha_cierre · timestamptz nullable','estado · varchar(10) con CHECK'])
edge([(350,267),(350,520)],'1 a N',(370,390))
draw.line([(830,250),(745,250),(745,610),(660,610)],fill='#748774',width=4)
draw.polygon([(660,610),(677,602),(677,618)],fill='#748774'); draw.text((675,465),'1 a N',font=font,fill='#345e40')
edge([(350,747),(350,1000)],'1 a N',(370,865))
draw.line([(660,690),(745,690),(745,1120),(830,1120)],fill='#748774',width=4)
draw.polygon([(830,1120),(813,1112),(813,1128)],fill='#748774'); draw.text((675,920),'1 a N',font=font,fill='#345e40')
edge([(1140,837),(1140,1000)],'1 a N',(1160,910))
im.save(DOCS/'diagrama-base-datos.png')

doc = Document()
s = doc.sections[0]
s.page_width=Inches(8.5); s.page_height=Inches(11)
s.top_margin=Inches(.65); s.bottom_margin=Inches(.65); s.left_margin=Inches(.7); s.right_margin=Inches(.7)
for name in ['Normal','Title','Subtitle','Heading 1','Heading 2']:
    st=doc.styles[name]; st.font.name='Arial'; st.font.color.rgb=RGBColor(0,0,0)
doc.styles['Normal'].font.size=Pt(10)
doc.styles['Normal'].paragraph_format.space_after=Pt(7)
doc.styles['Normal'].paragraph_format.line_spacing=1.1
doc.styles['Title'].font.size=Pt(27)
doc.styles['Heading 1'].font.size=Pt(18)
doc.styles['Heading 2'].font.size=Pt(12)
doc.core_properties.title='Primer entregable de Mi Pulpería de Barrio'
doc.core_properties.subject='Análisis de implementación y pruebas de Programación Avanzada'
doc.core_properties.author='Proyecto Programación Avanzada'
foot=s.footer.paragraphs[0]; foot.alignment=2
run=foot.add_run('Mi Pulpería de Barrio  |  '); run.font.size=Pt(8)
fld=OxmlElement('w:fldSimple'); fld.set(qn('w:instr'),'PAGE'); foot._p.append(fld)
def p(text,style=None): return doc.add_paragraph(text,style)
def h(text): doc.add_heading(text,1)
def page(title): doc.add_page_break(); h(title)
def table(headers,rows,widths=None):
    t=doc.add_table(rows=1,cols=len(headers)); t.autofit=False
    for i,v in enumerate(headers): t.rows[0].cells[i].text=v
    for row in rows:
        cells=t.add_row().cells
        for i,v in enumerate(row): cells[i].text=str(v)
    for rid,row in enumerate(t.rows):
        for i,c in enumerate(row.cells):
            if widths: c.width=Inches(widths[i])
            pr=c._tc.get_or_add_tcPr()
            borders=OxmlElement('w:tcBorders')
            for edge_name in ['top','left','bottom','right']:
                e=OxmlElement('w:'+edge_name); e.set(qn('w:val'),'single'); e.set(qn('w:sz'),'4'); e.set(qn('w:color'),'D9D9D9'); borders.append(e)
            pr.append(borders)
            sh=OxmlElement('w:shd'); sh.set(qn('w:fill'),'343A40' if rid==0 else ('F3F4F5' if rid%2 else 'FFFFFF')); pr.append(sh)
            margins=OxmlElement('w:tcMar')
            for side in ['top','left','bottom','right']:
                e=OxmlElement('w:'+side);e.set(qn('w:w'),'95');e.set(qn('w:type'),'dxa');margins.append(e)
            pr.append(margins)
            for para in c.paragraphs:
                para.paragraph_format.space_after=Pt(2)
                for r in para.runs: r.font.size=Pt(9); r.bold=(rid==0); r.font.color.rgb=RGBColor(255,255,255) if rid==0 else RGBColor(0,0,0)
    repeat=OxmlElement('w:tblHeader');t.rows[0]._tr.get_or_add_trPr().append(repeat)
    p('')
def shot(file,caption,bottom=690,width=7.05,crop=True):
    img=Image.open(DOCS/'evidencias'/file)
    if crop: img=img.crop((260,92,1420,min(bottom,img.height)))
    out=QA/file; img.save(out)
    doc.add_picture(str(out),width=Inches(width))
    par=p(caption); par.paragraph_format.space_after=Pt(12)
    for r in par.runs:r.font.size=Pt(9);r.italic=True
def code(text):
    par=p(text); par.paragraph_format.space_after=Pt(10)
    for r in par.runs:r.font.name='Consolas';r.font.size=Pt(8)

p('Universidad Americana', 'Subtitle')
p('Primer entregable de\nMi Pulpería de Barrio','Title')
p('Programación Avanzada · III cuatrimestre de 2026')
p('Profesor Héctor Egérico Fernández Méndez\nFecha de entrega indicada: 14 de noviembre de 2026\nFecha de verificación: 8 de noviembre de 2026')
h('Resultado de la implementación')
p('Se desarrollaron los mantenimientos de sucursales, clientes y productos, las consultas individuales, el modelo PostgreSQL con datos de demostración y las vistas requeridas. Las 39 pruebas automatizadas aprobaron en una base PostgreSQL local aislada. Este informe presenta los escenarios, sus resultados y las evidencias de la aplicación.')
p('El esquema se instaló en la base del profesor y la aplicación se conectó mediante TLS. Se verificaron allí las consultas, la integridad y los mantenimientos. El código está en GitHub y CI aprobó. La aplicación web todavía se ejecuta localmente; falta publicarla en Azure.')
table(['Componente','Estado'],[
('Modelo con constraints, triggers y datos','Implementado y probado localmente'),
('Vistas de existencias y reservas','Implementadas y consultadas'),
('Mantenimientos y consultas individuales','Implementados y probados'),
('CI y despliegue continuo','CI aprobada en GitHub; despliegue pendiente'),
('Base del profesor','Instalada y verificada con TLS'),
('Aplicación en Azure','Publicación pendiente')],[4.6,2.5])
p('Repositorio: github.com/AndrewShot01/Proyecto-ProgramacionAvanzada\nSolución: MiPulperia.sln · C# y ASP.NET Core MVC .NET 10 · jQuery · Npgsql')

page('Modelo de base de datos')
p('Todos los objetos pertenecen a user_07 y llevan el prefijo mp_. Las tablas PRQ existentes no se modifican. Cada relación usa una clave foránea con ON DELETE RESTRICT; las claves primarias son identidades generadas por PostgreSQL.')
doc.add_picture(str(DOCS/'diagrama-base-datos.png'),width=Inches(6.7))
p('PK: clave primaria. FK: clave foránea. UQ: unicidad. Todos los campos son obligatorios salvo fecha_cierre mientras la reserva está activa. Los índices únicos de texto ignoran mayúsculas y espacios exteriores. El archivo modelo-datos.md incluye la versión Mermaid editable.')

page('Reglas y datos de demostración')
table(['Regla','Aplicación en PostgreSQL'],[
('Ingresos de inventario','Solo INSERT; UPDATE, DELETE y TRUNCATE se rechazan.'),
('Reservas nuevas','Inician RESERVADO; se verifica stock, mínimo y máximo acumulado por cliente y producto.'),
('Cambios de reserva','Solo estado: COMPRADO antes del vencimiento o CANCELADO después. Estados finales inmutables.'),
('Cantidad disponible','Ingresos menos reservas activas menos compras; se deriva del historial, sin saldo editable.'),
('Atributos y precios','JSONB debe ser objeto; cantidades y precios positivos; mínimos no negativos.'),
('Concurrencia','Bloqueo de producto para reservas y versión xmin para mantenimientos.'),
('Cambios de límites','No se aceptan máximos menores a reservas existentes ni mínimos superiores al saldo.')],[1.7,5.4])
p('La disponibilidad por sucursal se calcula en mp_v_productos_existencias. La vista mp_v_reservas_clientes reúne cliente, sucursal, producto, cantidad, total, fechas, estado y resultado de compra. Las reservas se muestran de la más reciente a la más antigua.')
table(['Tabla','Registros demo'],[('mp_sucursales','3'),('mp_clientes','3'),('mp_productos','3'),('mp_inventarios','9'),('mp_ingresos','9'),('mp_reservas','2')],[5.3,1.8])
p('Los datos son ficticios. Se ingresan 180 unidades, se reservan dos y se compra una: quedan 177 disponibles y dos reservadas. Los atributos ejemplifican calzado y café. El script de datos se puede repetir sin duplicar ingresos ni reservas.')
p('Se interpretó el máximo por cliente para el mismo producto entre todas las sucursales. El precio y el plazo quedan fijados al reservar. El TODO de penalización de clientes no se implementó porque el enunciado no define su política.')

page('Pruebas automatizadas y comandos')
p('Se usaron .NET 10 y PostgreSQL real, con bases temporales exclusivas para pruebas y demostración. No se ejecutaron pruebas sobre datos del profesor. El resultado fue 39 aprobadas, cero fallidas y cero omitidas.')
code('dotnet test MiPulperia.sln -c Release --logger trx\ndotnet publish src/MiPulperia.Web -c Release -o artifacts/publish\npsql -h 127.0.0.1 -p 5547 -U postgres -d pulperia_demo \\\n  -v ON_ERROR_STOP=1 -f database/004_pruebas_integridad.sql')
table(['Casos','Esperado y observado'],[
('Datos y consultas','Las seis tablas contienen filas; nueve rutas HTTP responden 200.'),
('CRUD de los tres catálogos','Alta, búsqueda, detalle, edición y borrado sin relaciones funcionan.'),
('Edición concurrente y CSRF','Se rechaza la versión antigua; un POST sin token recibe HTTP 400.'),
('Valores inválidos','Se rechazan JSON array, precio negativo, duplicado y FK inexistente.'),
('Historial inmutable','Se rechazan cambios y borrados de ingresos, reservas e inventarios; se protege TRUNCATE.'),
('Integridad referencial','Se rechaza borrar un cliente con reservas, con mensaje en la interfaz.'),
('Estados y vencimiento','No se cancela antes de vencer ni se compra después; finalizar impide cambios posteriores.'),
('Stock y límites','Se rechaza exceder el máximo acumulado y consumir la existencia mínima.'),
('Ingreso y compra','Ingreso aumenta saldo; reserva lo reduce; compra conserva el descuento.'),
('Cancelación vencida','Devuelve unidades disponibles y queda inmutable.'),
('Dos solicitudes simultáneas','Con 5 unidades y mínimo 1, una reserva de 3 se acepta y otra se rechaza; quedan 2.')],[2.0,5.1])
p('Evidencias: pruebas-dotnet.txt, pruebas-sql.txt y resultados/entregable1.trx. El código de cada escenario está en tests/MiPulperia.Tests. El script SQL de comprobación termina en ROLLBACK. Los casos de concurrencia conservan historial únicamente en la base de pruebas.')

page('Mantenimiento de sucursales y clientes')
p('La búsqueda acepta fragmentos del nombre. Cada fila ofrece detalle, edición y eliminación. Las pruebas HTTP ejecutaron el ciclo completo en ambos catálogos; los registros con relaciones se conservan.')
shot('02-sucursales.png','Figura 1. Sucursales con datos ficticios y acciones de mantenimiento.',bottom=570)
shot('03-clientes-busqueda.png','Figura 2. Búsqueda aproximada de Ana; se obtiene el cliente correspondiente.',bottom=480)
p('Prueba de éxito: registrar una sucursal o un cliente con valores válidos. Se espera redirección al listado, mensaje de confirmación y persistencia. Resultado: aprobado. Prueba de error: repetir nombre de sucursal o identificación/correo de cliente. Se espera rechazo por unicidad; la restricción se aplica en PostgreSQL.')

page('Mantenimiento de productos y atributos')
p('Cada producto tiene precio, máximo reservado por cliente, plazo y existencia mínima. Los atributos se almacenan en JSONB y se validan como objeto. jQuery muestra una vista previa sin interpretar el contenido como HTML.')
shot('04-productos.png','Figura 3. Catálogo de productos con sus condiciones de reserva.',bottom=570)
shot('05b-formulario-json.png','Figura 4. Edición de un producto y atributos JSON.',width=4.7,crop=False)
p('Prueba de éxito: crear y editar un producto con JSON válido y precio positivo. Resultado: persistencia y consulta correctas. Los cambios no alteran el precio ni el plazo de reservas anteriores.')

page('Consultas de existencias y reservas')
shot('06b-existencias-filtradas.png','Figura 5. Consulta filtrada de zapatos y cantidades por sucursal.',bottom=760)
shot('07-reservas.png','Figura 6. Reserva activa y compra materializada, ordenadas por fecha descendente.',bottom=640)
p('Se espera que los totales no se multipliquen al relacionar varios ingresos con varias reservas. Los agregados se calculan por separado. Las pruebas verificaron el saldo tras ingresar, reservar, comprar y cancelar por vencimiento. Las fechas de la interfaz se expresan en horario de Costa Rica.')

page('Escenarios de error en la interfaz')
p('Las validaciones de la aplicación complementan las restricciones de PostgreSQL. Ante un error, se conserva el formulario y se muestra el motivo; no se confunde la operación fallida con un registro guardado.')
shot('08-borrado-protegido.png','Figura 7. El intento de borrar un cliente con reservas se rechaza y conserva el historial.',bottom=530)
shot('10-json-invalido.png','Figura 8. Un array JSON se rechaza porque los atributos deben ser un objeto.',bottom=750)
p('La prueba del navegador confirmó ambos mensajes. Las pruebas automatizadas verificaron además el rechazo de una edición con versión desactualizada y de peticiones sin token antifalsificación. No se observaron errores JavaScript durante la revisión; el panel móvil no produjo desbordamiento horizontal a 390 píxeles.')

page('Verificación en la nube y publicación')
p('Se instalaron seis tablas y dos vistas en advanced_programming.user_07, en el servidor del profesor. La conexión se verificó con TLS y certificado válido. Las tablas PRQ se conservaron. La aplicación web se ejecuta localmente con los datos alojados en la nube.')
doc.add_heading('Resultados en el servidor del profesor',2)
p('El script 004 confirmó cinco rechazos de operaciones indebidas y saldos no negativos, con ROLLBACK final. Las seis páginas principales respondieron HTTP 200. El alta, edición y eliminación funcionaron en los tres catálogos; los registros temporales de verificación se eliminaron. Los datos demo conservan 177 unidades disponibles, dos reservadas y una vendida.')
p('Las evidencias adicionales son verificacion-nube.txt, http-nube.json, crud-nube.json y 12-inicio-base-profesor.png. Las 39 pruebas automatizadas completas se ejecutaron en la base local aislada; no se ejecutó esa batería sobre la base compartida.')
doc.add_heading('Conexión e instalación',2)
p('Los scripts 001 y 002 ya se ejecutaron en la base del profesor. No debe repetirse 001 sobre las tablas instaladas. Se requiere CREATE únicamente dentro del esquema user_07, que proporciona el profesor; no se intenta recrear el esquema ni modificar permisos. La aplicación no ejecuta DDL al arrancar.')
code('.\\scripts\\configurar-conexion.ps1 -Servidor HOST -Usuario USUARIO\ndotnet run --project src/MiPulperia.Web --urls http://localhost:5087')
p('La conexión de este equipo ya está guardada en Secret Manager, fuera del repositorio. El usuario y el esquema son user_07. El script permite configurar otro equipo sin escribir contraseñas en archivos del proyecto.')
doc.add_heading('Publicación y CI CD',2)
p('GitHub Actions aprobó la compilación, las pruebas y el empaquetado del código 049ef0a en la ejecución 42515482001. La evidencia está en verificacion-ci.json. deploy-azure.yml publicará el artefacto aprobado cuando se configure el App Service, AZURE_WEBAPP_NAME y las credenciales de despliegue. Todavía no se han creado recursos de Azure.')
p('El App Service debe recibir ConnectionStrings__Pulperia y tener acceso de red al servidor del profesor. Como la interfaz permite administración de datos, debe restringirse el acceso al publicarla. Los permisos de la aplicación deberían excluir cambios de estructura; un propietario o administrador siempre puede modificar sus propios triggers.')
doc.add_heading('Trabajo del segundo entregable',2)
p('Quedan los formularios de ingresos, reservas y compras; el proceso calendarizado de vencimiento; maestro detalle; Polly y SignalR. No se ofrece cancelación manual en la interfaz. La separación de permisos del proceso calendarizado se completa con esa fase. La penalización de clientes sigue pendiente de una política definida por el profesor.')
doc.add_heading('Referencias técnicas',2)
p('Enunciado del curso: documento suministrado por el estudiante.\nMicrosoft Learn: learn.microsoft.com/aspnet/core/mvc/models/validation\nNpgsql: npgsql.org/efcore\nPostgreSQL: postgresql.org/docs/current/sql-createtrigger.html\nAzure: learn.microsoft.com/azure/app-service/deploy-github-actions')
for element in [doc._element, doc.styles.element]:
    for border in list(element.xpath('.//w:pBdr')): border.getparent().remove(border)
doc.save(DOCS/'Informe-Primer-Entregable-Actualizado.docx')
print(DOCS/'Informe-Primer-Entregable-Actualizado.docx')
