# Visión y arquitectura de d365fo-entity-schema

## Propósito de la aplicación
- **Qué hace**: complemento de Visual Studio 2022 para Dynamics 365 Finance and Operations (F&O) que genera esquemas entidad-relación en formato DBML y documentación tipo wiki a partir del metamodelo de tablas de D365FO.
- **Problema que resuelve**: ayuda a exploradores y desarrolladores a entender dependencias entre tablas sin salir del IDE, permitiendo seleccionar tablas específicas o derivarlas de relaciones existentes.
- **Casos de uso clave**: generación rápida de diagramas DBML renderizables en servicios como dbdiagram.io, documentación de tablas y relaciones para revisiones de diseño, y análisis de impacto a partir de tablas base o modelos completos.

## Arquitectura lógica
La solución se organiza como una extensión con separación entre UI, controladores y proveedores de datos/esquemas:

1. **Presentación (Windows Forms)**
   - `ErdForm` contiene el formulario principal con controles para seleccionar modelo, agregar tablas manualmente o por relación, y configurar opciones como incluir campos estándar o de extensiones, marcar obligatorios, simplificar tipos EDT, o ignorar staging/self references.
   - Las acciones del usuario delegan en un controlador para mantener la lógica fuera de la UI.

2. **Controlador**
   - `ErdController` coordina entre la UI y los proveedores. Recibe instancias de `IDataModelProvider`, `DBMLSchemaProvider` y `WIKISchemaProvider`, y usa `EnvDTE` para abrir o actualizar documentos dentro de Visual Studio.
   - Expone métodos `GetDBML` y `GetWIKI` que generan el esquema y lo insertan en un documento del IDE, así como `UseActiveDocument` para reutilizar tablas definidas en un DBML abierto.

3. **Proveedores de datos y esquemas**
   - **Datos**: `D365FODataModelProvider` accede al metamodelo de D365FO vía `IMetadataProvider` y transforma tablas y relaciones en un `DataModel` interno (clases en `Waywo.DbSchema.Model`). Implementa operaciones para agregar tablas iniciales, sumar relaciones entrantes/salientes o incluir todas las relacionadas, y opciones de filtrado (staging, self references, simplificación de tipos).
   - **Esquemas**: `DBMLSchemaProvider` y `WIKISchemaProvider` consumen el `DataModel` para renderizar la salida. `DBMLSchemaProvider` prioriza claves y relaciones, permite incluir campos estándar y de extensiones, y anota propiedades y descripciones.
   - **Factory**: `DataModelProviderFactory` abstrae la obtención del `IMetadataProvider` desde los servicios de Visual Studio para construir `D365FODataModelProvider` listo para usar dentro del IDE.

4. **Adaptadores y utilitarios**
   - Adaptadores en `Waywo.DbSchema.AddIn.Adapters` convierten elementos del metamodelo AX (tablas, relaciones, extensiones) en el modelo interno consumido por los proveedores de esquema.
   - La carpeta `Framework` agrupa componentes reutilizables para la integración con Visual Studio y el manejo de menús/acciones del Add-in.

## Flujo de trabajo de la extensión
1. El Add-in se carga desde el menú Dynamics 365 en Visual Studio y abre `ErdForm`.
2. El usuario selecciona un modelo o añade tablas manualmente; opcionalmente amplía la selección con relaciones entrantes, salientes o todas.
3. El formulario sincroniza las opciones con el controlador (incluyendo flags de campos, tipos y filtrado).
4. El controlador solicita al proveedor de datos generar el `DataModel`, luego invoca el proveedor de esquema correspondiente (DBML o wiki).
5. El resultado se inserta en un documento nuevo o existente dentro del IDE, listo para visualizar con herramientas externas como dbdiagram.io.

## Diseño y consideraciones
- **Experiencia IDE-first**: la integración con `EnvDTE` y servicios de metadatos de Visual Studio evita dependencias externas y mantiene el flujo dentro del entorno de D365FO.
- **Extensibilidad**: la separación proveedor/UI permite reutilizar `D365FODataModelProvider` y `DBMLSchemaProvider` en otros hosts (por ejemplo, aplicaciones de consola) mientras se mantenga acceso al `IMetadataProvider`.
- **Seguridad de datos**: el Add-in opera sobre metadatos locales del entorno de desarrollo; no realiza llamadas externas ni persiste información sensible.
- **Limitaciones conocidas**: la generación depende de que los modelos y tablas existan en el metadato local; opciones como ignorar staging o autoincluir relaciones se aplican en memoria y no alteran la base de datos real.
