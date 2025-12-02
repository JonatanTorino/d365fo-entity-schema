# Plan para herramienta de consola (DBML)

## Objetivo
Construir una aplicación de consola que reutilice la lógica del Add-in para generar archivos DBML de tablas de D365FO. La consola debe aceptar los mismos parámetros funcionales que el formulario principal (`ErdForm`), pero enfocada solo en producir DBML y dejar el resultado en archivo o salida estándar.

## Entradas esperadas (equivalentes al formulario)
- `--model <nombre>`: modelo seleccionado en el combo de modelos.
- `--table <tabla>` (repetible): tablas iniciales introducidas manualmente.
- `--add-from-model`: agrega todas las tablas del modelo indicado.
- `--add-inward <tabla>` / `--add-outward <tabla>` / `--add-related`: incluye tablas relacionadas entrantes, salientes o todas las relacionadas, respectivamente.
- `--include-standard` / `--include-extensions`: incluir campos estándar o de extensiones en la salida DBML.
- `--mark-mandatory`: marcar campos obligatorios con `not null` en DBML.
- `--simplify-types`: convertir EDT a tipos simples.
- `--ignore-staging` / `--ignore-self-references`: filtrar tablas staging o relaciones consigo mismo.
- `--output <ruta>`: archivo de salida; si se omite, se escribe a stdout.

## Suposiciones y dependencias
- El entorno de ejecución dispone de las librerías de metadatos de D365FO (las mismas que consume el Add-in). Puede ejecutarse dentro de un VM de desarrollo o en una instalación local con los ensamblados de Visual Studio/D365FO.
- Se reutilizarán `D365FODataModelProvider` y `DBMLSchemaProvider`, por lo que la consola debe poder obtener un `IMetadataProvider` equivalente al que provee `DataModelProviderFactory` en el Add-in.

## Pasos propuestos
1. **Crear el proyecto**
   - Generar un proyecto de consola .NET en la solución (`Waywo.DbSchema.Console` o similar).
   - Referenciar el proyecto `Waywo.DbSchema.AddIn` o extraer las clases reutilizables (`Providers`, `Model`, `Adapters`) a una biblioteca compartida para evitar dependencias innecesarias con Windows Forms.

2. **Inicializar el proveedor de metadatos**
   - Reusar `DataModelProviderFactory` cuando se ejecute dentro de Visual Studio o implementar una variante que cargue el `RuntimeMetadataProvider` apuntando al directorio de metadatos (`MetadataDirectory`, `ModelStore`) en entornos fuera del IDE.
   - Instanciar `IDataModelProvider dataProvider = new D365FODataModelProvider(metadataProvider);` y `var dbmlProvider = new DBMLSchemaProvider(dataProvider);`.

3. **Parsear argumentos**
   - Usar `System.CommandLine` o un parser simple para mapear las opciones anteriores a propiedades del proveedor: `SimplifyTypes`, `IgnoreStaging`, `IgnoreSelfReferences`, `StandardFields`, `ExtensionFields`, `MarkMandatory`, y `Model`.
   - Validar que exista al menos una tabla o la opción `--add-from-model`; en caso contrario, mostrar ayuda.

4. **Construir el conjunto de tablas**
   - Para cada `--table`, invocar `dataProvider.AddTable`.
   - Si se indicó `--add-from-model`, llamar a `dataProvider.AddTablesFromModel(model)`.
   - Procesar `--add-inward`, `--add-outward` o `--add-related` en el orden indicado, usando los métodos del proveedor para completar las colecciones.

5. **Generar y escribir el DBML**
   - Ejecutar `dataProvider.GenerateDataModel()` seguido de `var dbml = dbmlProvider.GetSchema();`.
   - Si `--output` está presente, escribir el contenido al archivo (creando directorios si es necesario); de lo contrario, imprimir en consola.

6. **Experiencia de uso y ejemplos**
   - Ejemplo mínimo: `Waywo.DbSchema.Console --model ApplicationSuite --table CustTable --include-standard --mark-mandatory > schema.dbml`.
   - Ejemplo ampliado con relaciones: `Waywo.DbSchema.Console --model ApplicationSuite --table CustTable --add-related --include-standard --include-extensions --simplify-types --ignore-staging --output cust-schema.dbml`.

7. **Pruebas y validación**
   - Validar que el archivo DBML se abra en dbdiagram.io sin errores de sintaxis.
   - Probar combinaciones de flags para asegurar que las mismas reglas del formulario (por ejemplo, inclusión de campos de extensiones, marcado de obligatorios) se respetan en la consola.
