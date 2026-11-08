# Análisis del primer entregable

## Resultado

Se implementaron los mantenimientos de sucursales, clientes y productos, sus consultas individuales, seis tablas PostgreSQL con datos ficticios, restricciones, triggers y dos vistas. La validación local utiliza PostgreSQL real y pruebas HTTP de la aplicación MVC. El diseño completo está en [modelo-datos.md](modelo-datos.md).

La implementación se instaló y verificó en el esquema `user_07` de `advanced_programming`, en el servidor PostgreSQL del profesor. La aplicación se conecta mediante TLS con verificación del certificado. No se modificaron las tablas PRQ existentes. La publicación de la aplicación en Azure, la subida del código a GitHub y la ejecución remota de CI/CD siguen pendientes.

## Requisitos y estado

| Requisito | Resultado local | Evidencia |
|---|---|---|
| Diagrama con PK y FK | Implementado | modelo-datos.md y diagrama-base-datos.png |
| Constraints y triggers | Verificados | database/001_estructura.sql y pruebas SQL |
| Datos en todas las tablas | 3 sucursales, 3 clientes, 3 productos, 9 inventarios, 9 ingresos y 2 reservas | database/002_datos_demo.sql |
| Vistas | Existencias y reservas completas | database/003_consultas.sql y pantallas 06–07 |
| Mantenimiento sucursales | Alta, consulta, búsqueda, cambio y borrado | Prueba HTTP y pantalla 02 |
| Mantenimiento clientes | Alta, consulta, búsqueda, cambio y borrado | Prueba HTTP y pantalla 03 |
| Mantenimiento productos | CRUD, reglas de reserva, JSONB y vista previa jQuery | Prueba HTTP y pantallas 04–05 |
| Integración en base del profesor | Instalada y verificada con TLS | verificacion-nube.txt, http-nube.json y crud-nube.json |
| CI/CD | Archivos preparados, validación local aprobada; ejecución en GitHub pendiente | .github/workflows |
| Publicación Azure | Pendiente de recursos y credenciales de despliegue | README |

## Pruebas reproducibles

Ejecutadas inicialmente el 7 de noviembre y verificadas de nuevo el 8 de noviembre de 2026 sobre bases temporales locales `pulperia_test`, `pulperia_install_test` y `pulperia_demo`, puerto 5547. La base demo se mantuvo separada de los datos de pruebas. No se utilizaron datos reales de clientes.

```powershell
dotnet build MiPulperia.sln
dotnet test MiPulperia.sln --logger 'trx;LogFileName=entregable1.trx' --results-directory docs/evidencias/resultados
dotnet publish src/MiPulperia.Web -c Release -o artifacts/publish
```

La variable `PULPERIA_TEST_CONNECTION` debe apuntar a una base exclusiva para pruebas. Antes de probar, instalar 001 y 002. El archivo `database/004_pruebas_integridad.sql` permite repetir cinco comprobaciones SQL y una consulta de saldos con `ROLLBACK` final.

| Escenario | Resultado esperado y observado |
|---|---|
| Crear, consultar, editar y eliminar cada catálogo | Se guarda, se encuentra por nombre, se actualiza y se elimina cuando no tiene relaciones. |
| Formulario desactualizado | Se rechaza la segunda edición con mensaje de concurrencia. |
| POST sin token antifalsificación | HTTP 400; ningún cambio. |
| JSON inválido o array | Se muestra validación; no se guarda. |
| Sucursal duplicada con otras mayúsculas y espacios | Índice único rechaza la operación. |
| Precio negativo y FK inexistente | CHECK y FK rechazan la operación. |
| Borrar cliente con reservas | Se conserva el registro y se explica la relación existente. |
| Modificar, borrar o truncar ingresos | Trigger rechaza las operaciones. |
| Reasignar inventario y borrar o truncar reservas | Trigger rechaza las operaciones. |
| Modificar cantidad reservada o reabrir compra | Trigger rechaza las operaciones. |
| Cancelar antes de vencer o comprar después de vencer | Trigger rechaza la transición. |
| Ingresar, reservar y comprar | El ingreso aumenta disponibilidad; reservar reduce disponible; comprar conserva el descuento. |
| Cancelar después de vencer | Se libera disponibilidad y el registro queda inmutable. |
| Exceder máximo acumulado en otra sucursal | Se rechaza considerando las reservas del cliente para ese producto. |
| Reservar por debajo de la existencia mínima | Se rechaza; no hay saldo negativo. |
| Dos reservas simultáneas de 3 unidades con stock 5 y mínimo 1 | Solo una se acepta; quedan 2 disponibles. |
| Reducir máximos o elevar mínimos incompatibles con saldos | El trigger del producto rechaza la modificación. |
| Cargar páginas y formularios | HTTP 200 en las nueve rutas comprobadas. |

El archivo TRX contiene el resultado detallado de las **39 pruebas automatizadas aprobadas**. El navegador comprobó además las consultas, la vista previa JSON, el error de borrado y el diseño móvil a 390 píxeles sin desbordamiento horizontal de la página. Las tablas conservan desplazamiento interno en pantallas pequeñas.

En el servidor del profesor se ejecutó además el script de integridad 004, con sus cinco rechazos esperados y saldos no negativos; finalizó con ROLLBACK. La aplicación respondió HTTP 200 en inicio, sucursales, clientes, productos, existencias y reservas. Se comprobó mediante la interfaz el alta, edición y eliminación de un registro temporal de cada catálogo; esos registros de verificación quedaron eliminados. Las 39 pruebas automatizadas completas se mantuvieron en la base local aislada.

## Interpretación de los resultados

En la base demo hay 180 unidades ingresadas: dos reservadas y una comprada. Por tanto, la disponibilidad global es 177 y la reserva activa es 2. La vista de reservas muestra tanto una compra materializada como una reserva pendiente. La cancelación vencida se verifica en la prueba automatizada con un plazo breve, sin alterar el reloj del servidor.

Los errores de negocio se presentan en español. Las restricciones se ejecutan también en PostgreSQL y no dependen de jQuery. Las claves únicas y relaciones impiden duplicados y pérdida del historial. La aplicación detecta modificaciones concurrentes mediante `xmin`.

## Pendientes y segundo entregable

La base del profesor ya tiene las tablas, datos, vistas y triggers, y la conexión local está configurada fuera de Git. Para cerrar la entrega en el ambiente solicitado falta subir los archivos al repositorio, verificar el pipeline y publicar la aplicación en Azure. La aplicación aún se ejecuta localmente aunque sus datos estén en la nube.

Para el segundo entregable faltan los formularios de ingreso de inventario, reserva y compra, el proceso calendarizado que cancela reservas vencidas, consultas maestro-detalle, circuitos con Polly y notificaciones SignalR. No existe cancelación manual en la interfaz. La separación de permisos del rol calendarizado debe configurarse con ese proceso. El TODO de penalización necesita una política del profesor; no se incorporó una regla inventada.

Los triggers no pueden impedir que el propietario del esquema o un administrador altere o deshabilite el propio trigger. La aplicación desplegada debe usar permisos limitados y acceso restringido, como se explica en el README.
