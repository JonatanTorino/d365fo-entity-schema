# Plan para herramienta de consola (DBML)

## Objetivo
Construir una aplicación de consola que reutilice la lógica del Add-in para generar archivos DBML de tablas de D365FO. La consola debe aceptar los mismos parámetros funcionales que el formulario principal (`ErdForm`), pero enfocada solo en producir DBML y dejar el resultado en archivo o salida estándar.

## Entradas esperadas (equivalentes al formulario)
- `--model <nombre>`: modelo contenedor de todas las tablas.
- `--table <tabla>`: tablas separadas por ',' y que acepte wildcard '*'.
- `--add-inward <tabla>` / `--add-outward <tabla>` / `--add-related <tabla>`: incluye tablas relacionadas entrantes, salientes o todas las relacionadas, para una sola tabla ingresada.
- `--include-non-KeyFields`: Al usar este parámetro incluye todos los campos, sino solo los campos clave (clave primaria, claves foraneas, campos pertenecientes a índices).
- `--include-extensions`: incluir campos de extensiones en la salida DBML.
- `--mark-mandatory`: marcar campos obligatorios con `not null` en DBML.
- `--simplify-types`: convertir EDT a tipos simples.
- `--ignore-staging`: filtrar tablas staging.
- `--ignore-self-references`: filtrar relaciones consigo mismo.
- `--output <ruta>`: archivo de salida; si se omite, se escribe a stdout.

## Suposiciones y dependencias
- El entorno de ejecución dispone de las librerías de metadatos de D365FO (las mismas que consume el Add-in). Puede ejecutarse dentro de un VM de desarrollo o en una instalación local con los ensamblados de Visual Studio/D365FO.
- Se reutilizarán `D365FODataModelProvider` y `DBMLSchemaProvider`, por lo que la consola debe poder obtener un `IMetadataProvider` equivalente al que provee `DataModelProviderFactory` en el Add-in.

## Pasos propuestos
1. **Crear el proyecto**
   - Generar un proyecto de consola .NET en la solución (`Waywo.DbSchema.Console` o similar) y agregarlo a la solución `Waywo.DbSchema.sln`.
   - Referenciar el proyecto `Waywo.DbSchema.AddIn` o extraer las clases reutilizables (`Providers`, `Model`, `Adapters`) a una biblioteca compartida para evitar dependencias innecesarias con Windows Forms.

2. **Inicializar el proveedor de metadatos**
   - Reusar `DataModelProviderFactory` o implementar una variante que cargue el `RuntimeMetadataProvider` apuntando al directorio de metadatos (`MetadataDirectory`, `ModelStore`) en entornos fuera del IDE.
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
   - Ejemplo mínimo #1: `Waywo.DbSchema.Console --model ApplicationSuite --include-non-KeyFields --mark-mandatory > schema.dbml`.
   - Ejemplo mínimo #2: `Waywo.DbSchema.Console --table AxnCore* --include-non-KeyFields --mark-mandatory > schema.dbml`.
   - Ejemplo mínimo #3: `Waywo.DbSchema.Console --model Axxon365Core --table 'AxnCore*,CustTable' --include-non-KeyFields --mark-mandatory > schema.dbml`.
   - Ejemplo ampliado con relaciones #4: `Waywo.DbSchema.Console --model ApplicationSuite --add-related --include-non-KeyFields --include-extensions --simplify-types --ignore-staging --output cust-schema.dbml`.
   - Ejemplo ampliado con relaciones #5: `Waywo.DbSchema.Console --add-related CustTable --include-non-KeyFields --include-extensions --simplify-types --ignore-staging --output cust-schema.dbml`.

7. **Pruebas y validación**
   - Validar que el archivo DBML se abra en dbdiagram.io sin errores de sintaxis.
   - Probar combinaciones de flags para asegurar que las mismas reglas del formulario (por ejemplo, inclusión de campos de extensiones, marcado de obligatorios) se respetan en la consola.
