# Publicación en Azure for Students

La base PostgreSQL ya funciona en el servidor del profesor. Falta crear el App Service que ejecutará la aplicación web. La cuenta de Azure y su suscripción deben estar disponibles para continuar; todavía no se ha creado ningún recurso de Azure para este proyecto.

## Crear el servicio

En [Azure Portal](https://portal.azure.com), con la cuenta de Azure for Students, abre **App Services → Crear → Aplicación web** y usa estos valores:

| Campo | Valor |
|---|---|
| Suscripción | Tu suscripción Azure for Students |
| Grupo de recursos | `rg-pulperia-user07` |
| Nombre sugerido | `pulperia-user07-andrewshot01` (Azure debe confirmar su disponibilidad) |
| Publicar | Código |
| Pila de entorno | .NET 10 |
| Sistema operativo | Linux |
| Región | Una región admitida por tu suscripción |
| Plan | Nuevo plan `plan-pulperia-user07` |
| Plan de precios | **Free F1** |

Selecciona explícitamente **Free F1** antes de revisar la creación. Si la región o la suscripción no ofrece F1, detén la creación y revisa otra región; no cambies automáticamente a un plan de pago. No agregues una base de datos, ya que se utiliza la del profesor. El [inicio rápido oficial de Microsoft](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore) documenta este despliegue y el [plan gratuito de Linux](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/) tiene límites de uso apropiados para demostraciones académicas.

## Configurar la conexión

En el App Service abre **Variables de entorno → Configuración de la aplicación**. Agrega `ConnectionStrings__Pulperia` con la misma conexión configurada localmente. Incluye `SSL Mode=VerifyFull` y `Search Path=user_07`. La contraseña no debe agregarse al código ni a GitHub.

El secreto local está asociado al proyecto `src/MiPulperia.Web` mediante Secret Manager de .NET. No es necesario ejecutar nuevamente los scripts SQL: las tablas del proyecto ya existen en la nube. Si Azure no puede comunicarse con PostgreSQL, el profesor debe autorizar las IP de salida del App Service en el firewall de su servidor.

La aplicación permite administrar los datos de la pulpería. Antes de dejarla accesible públicamente, configura **Autenticación de App Service** con las cuentas autorizadas del curso, o restringe el acceso a las IP necesarias para la demostración. No abras los mantenimientos a visitantes anónimos de Internet.

## Conectar GitHub Actions

El repositorio incluye dos workflows. `ci.yml` valida el código con PostgreSQL aislado y genera el paquete. `deploy-azure.yml` publica ese mismo paquete después de una validación aprobada de un push a `main`.

Configura en GitHub:

1. Variable de repositorio `AZURE_WEBAPP_NAME`: el nombre exacto del App Service creado.
2. Entorno `azure` y su secreto `AZURE_WEBAPP_PUBLISH_PROFILE`: el perfil obtenido del App Service, si las políticas de la cuenta permiten este método. No lo copies al repositorio.

Si la cuenta tiene deshabilitados los perfiles de publicación, utiliza OIDC desde **Centro de implementación** y adapta el workflow con las credenciales federadas siguiendo la [guía oficial](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions). No desactives una política de la organización para obtener el perfil.

Con la cuenta y el App Service disponibles se debe completar la configuración, ejecutar la publicación y verificar la URL real. La existencia del workflow por sí sola no acredita un despliegue en Azure.
