# Mi Pulpería de Barrio

Primer entregable de Programación Avanzada, Universidad Americana, III cuatrimestre de 2026. Fecha indicada por el profesor: **14 de noviembre de 2026**.

Aplicación C# con ASP.NET Core MVC (.NET 10), Razor, jQuery y Entity Framework Core/Npgsql. Base PostgreSQL del profesor: `advanced_programming`, esquema `user_07`. El código referencia explícitamente ese esquema.

**Estado verificado el 8 de noviembre de 2026:** esquema instalado en el servidor del profesor y aplicación conectada con TLS. Se comprobaron las seis páginas principales y el alta, edición y eliminación de registros temporales en los tres catálogos. La conexión de este equipo está en Secret Manager; no hay contraseñas en Git. El código está en GitHub. La aplicación web todavía se ejecuta localmente; falta publicarla en Azure.

**Validación:** 39 pruebas locales aprobadas y [CI aprobada en GitHub Actions](https://github.com/AndrewShot01/Proyecto-ProgramacionAvanzada/actions/runs/42515482001), incluida la generación del paquete de publicación.

**Alcance actual:** se trabaja en el primer entregable y la publicación en Azure se deja para una fase posterior, prevista junto con el segundo entregable. El enunciado exige Azure y CI/CD como condiciones generales, pero no asigna expresamente su fecha a una entrega; posponer la publicación no equivale a una autorización del profesor. Para esta revisión se demuestra la aplicación local conectada a PostgreSQL en la nube.

## Contenido

- Crear, consultar, buscar por nombre, editar y eliminar sucursales, clientes y productos.
- Atributos de producto en JSONB con validación de objeto JSON y vista previa con jQuery.
- Consultas individuales de existencias y reservas a través de dos vistas SQL.
- Seis tablas con datos ficticios, PK, FK, índices únicos, CHECK y triggers de integridad.
- Pruebas de integración contra PostgreSQL real y pruebas HTTP de los mantenimientos.
- Workflows de CI y publicación en Azure, modelo de datos e informe de resultados.

## Configurar la base del profesor

En pgAdmin abre Query Tool sobre **advanced_programming**. Ejecuta, en orden:

Los scripts 001 y 002 **ya se ejecutaron en la base del profesor**. Los pasos siguientes documentan una instalación inicial; no vuelvas a ejecutar 001 sobre los objetos instalados.

1. `database/001_estructura.sql`: una sola vez; crea únicamente objetos `user_07.mp_*` en una transacción.
2. `database/002_datos_demo.sql`: datos ficticios; se puede repetir sin duplicarlos.
3. `database/003_consultas.sql`: consultas para comprobar los registros y las vistas.

La primera ejecución requiere permiso `CREATE` en `user_07`. Si los objetos `mp_*` ya existen, el script falla y revierte la transacción; no elimina ni reemplaza tablas existentes. Las tablas `PRQAutomoviles`, `PRQIngresosAutomoviles` y `PRQParqueos` no se modifican.

La conexión de pgAdmin no se comparte automáticamente con una aplicación C#. Desde PowerShell, en esta carpeta:

```powershell
.\scripts\configurar-conexion.ps1 -Servidor 'HOST_COMPLETO_DEL_PROFESOR' -Usuario 'USUARIO_DE_CONEXION'
dotnet restore
dotnet run --project src/MiPulperia.Web --urls http://localhost:5087
```

El script solicita la contraseña de forma oculta y guarda la cadena en Secret Manager de .NET, fuera del repositorio. El usuario de conexión no necesariamente se llama `user_07`: ese es el esquema. La conexión exige certificado TLS válido (`SSL Mode=VerifyFull`). No copies contraseñas en `appsettings.json` ni en Git.

Abre <http://localhost:5087>. La aplicación no crea ni borra tablas al arrancar. Ante una conexión no disponible muestra un error controlado; el detalle técnico queda en los logs.

## Pruebas

Usa una base **exclusiva para pruebas**. En ella crea primero el esquema con `CREATE SCHEMA user_07;` y ejecuta los scripts 001 y 002. En el servidor del profesor el esquema ya existe y no debe recrearse. Nunca apuntes las pruebas automatizadas a la base compartida del profesor. Las pruebas SQL se revierten y las pruebas web crean y eliminan registros de prueba; algunas pruebas de concurrencia conservan historial en esa base aislada.

```powershell
$env:PULPERIA_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=pulperia_test;Username=postgres;Password=TU_CLAVE_LOCAL'
dotnet test MiPulperia.sln --logger trx
dotnet publish src/MiPulperia.Web -c Release -o artifacts/publish
```

CI crea su propio PostgreSQL 17, instala el esquema, compila, ejecuta pruebas y genera el paquete. No usa credenciales del profesor. La evidencia local está en `docs/evidencias/` y el detalle de cobertura en `docs/entregable-1.md`.

## Publicar en Azure for Students

Esta actividad queda pendiente por decisión del estudiante mientras se completa la revisión del primer entregable. La publicación requiere una suscripción y un App Service del estudiante. Esos recursos todavía deben configurarse en la cuenta correspondiente.

1. Crear un App Service que ejecute .NET 10; habilitar HTTPS. Para una aplicación administrativa, restringir el acceso con App Service Authentication antes de exponerla públicamente.
2. Configurar `ConnectionStrings__Pulperia` como variable de entorno del App Service, con la conexión del profesor y `SSL Mode=VerifyFull`.
3. Autorizar la salida del App Service en el firewall de PostgreSQL, si el profesor lo exige.
4. Configurar la variable de repositorio `AZURE_WEBAPP_NAME` y el secreto del entorno `azure` llamado `AZURE_WEBAPP_PUBLISH_PROFILE`. El perfil se obtiene del App Service; no se guarda en el repositorio. Si la cuenta no permite perfiles de publicación, configurar OIDC siguiendo la documentación oficial y adaptar el workflow.
5. Subir a `main`. `ci.yml` valida; `deploy-azure.yml` publica el artefacto de esa ejecución únicamente cuando CI termina con éxito y la variable de Azure está configurada.

La autenticación de usuarios no forma parte del enunciado. El sitio contiene operaciones administrativas y debe usar una restricción de acceso de Azure al publicarse. No se han creado recursos con costo ni se ha ejecutado una publicación desde esta sesión.

## Documentación

- [Modelo y relaciones](docs/modelo-datos.md).
- [Alcance y pruebas del entregable](docs/entregable-1.md).
- [Informe actualizado en Word](docs/Informe-Primer-Entregable-Actualizado.docx) con análisis, comandos y capturas.
- [Diagrama de base de datos](docs/diagrama-base-datos.png).
- [Creación del App Service y publicación en Azure](docs/publicar-azure.md).
- Scripts reproducibles en `database/`; código en `src/`; pruebas en `tests/`.

Segundo entregable: formularios de ingreso de inventario, reserva y compra, cancelación calendarizada, maestro-detalle, Polly y SignalR. Los triggers son la base de integridad de esos procesos; su interfaz y coordinación no se incluyen en esta fase.

## Referencias técnicas

- [Validación MVC de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0).
- [Proveedor EF Core de Npgsql](https://www.npgsql.org/efcore/index.html).
- [Triggers de PostgreSQL](https://www.postgresql.org/docs/current/sql-createtrigger.html).
- [GitHub Actions para Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions).
