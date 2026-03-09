# 📋 Backlog - Waywo.DbSchema.AddIn

## 🎯 Product Backlog - Entity Relationship Diagram Tool

Mejoras propuestas para la herramienta de generación de diagramas ERD para Dynamics 365 Finance & Operations.

---

## 🚀 High Priority

### 1. Modo de Búsqueda Dual (Exacta vs. Parcial)
**Estado:** 💡 Propuesta  
**Prioridad:** Alta  
**Complejidad:** Media  

**Descripción:**  
Agregar la opción de elegir entre búsqueda exacta y búsqueda parcial de tablas, en lugar de tener solo búsqueda parcial como comportamiento predeterminado.

**Beneficios:**
- Mayor control para el usuario sobre los resultados de búsqueda
- Búsqueda exacta es más rápida cuando se conoce el nombre completo
- Evita resultados inesperados con nombres muy comunes

**Implementación Sugerida:**
- Agregar un `RadioButton` o `CheckBox` en el formulario con opciones:
  - `[ ] Búsqueda Exacta` 
  - `[x] Búsqueda Parcial` (por defecto)
- Modificar `addButton_Click()` para llamar a:
  - `AddTable(tableTextBox.Text)` si es búsqueda exacta
  - `AddTablesByPartialName(tableTextBox.Text)` si es búsqueda parcial

**Componentes Afectados:**
- `ErdForm.cs` - UI y lógica de evento
- `ErdForm.Designer.cs` - Controles del formulario

---

### 2. Límite Máximo de Resultados con Confirmación
**Estado:** 💡 Propuesta  
**Prioridad:** Alta  
**Complejidad:** Baja  

**Descripción:**  
Implementar un límite configurable de resultados para evitar agregar cientos de tablas accidentalmente con búsquedas muy amplias (ej: "Table", "Trans", "Jur").

**Beneficios:**
- Previene operaciones que consumen mucho tiempo
- Protege contra errores del usuario
- Mejora la experiencia de usuario

**Implementación Sugerida:**
```csharp
// En D365FODataModelProvider.cs
public int MaxSearchResults { get; set; } = 50; // Límite por defecto

public IDataModelProvider AddTablesByPartialName(string partialName)
{
    var matchingTables = new List<string>();
    
    foreach (string key in this.provider.Tables.GetPrimaryKeys())
    {
        if (key.ToUpper().Contains(partialName.ToUpper()))
        {
            matchingTables.Add(key);
            
            if (matchingTables.Count >= MaxSearchResults)
            {
                // Mostrar diálogo de confirmación
                break;
            }
        }
    }
    
    // Si se alcanzó el límite, preguntar al usuario
    if (matchingTables.Count >= MaxSearchResults)
    {
        DialogResult result = MessageBox.Show(
            $"Se encontraron {MaxSearchResults}+ coincidencias. ¿Desea agregar las primeras {MaxSearchResults}?",
            "Demasiados resultados",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );
        
        if (result == DialogResult.No)
            return this;
    }
    
    // Agregar tablas...
}
```

**Configuración Sugerida:**
- Agregar un `NumericUpDown` en el formulario para configurar el límite (10-500)
- Valor por defecto: 50 tablas

**Componentes Afectados:**
- `IDataModelProvider.cs` - Agregar propiedad `MaxSearchResults`
- `D365FODataModelProvider.cs` - Implementar lógica de límite
- `ErdForm.cs` - Bind del control de configuración

---

### 3. Filtro por Tipo de Tabla
**Estado:** 💡 Propuesta  
**Prioridad:** Media  
**Complejidad:** Media  

**Descripción:**  
Permitir filtrar las búsquedas por tipo de tabla (Regular, Staging, View, InMemory, TempDB) para obtener resultados más precisos.

**Beneficios:**
- Excluir tablas de staging automáticamente en búsquedas
- Enfocarse solo en tablas regulares o vistas
- Reducir ruido en los resultados

**Implementación Sugerida:**
```csharp
// Agregar enum para filtros
[Flags]
public enum TableTypeFilter
{
    None = 0,
    Regular = 1,
    Staging = 2,
    View = 4,
    InMemory = 8,
    TempDB = 16,
    All = Regular | Staging | View | InMemory | TempDB
}

// En IDataModelProvider.cs
TableTypeFilter TableFilter { get; set; }

// En D365FODataModelProvider.cs
public IDataModelProvider AddTablesByPartialName(string partialName)
{
    foreach (string key in this.provider.Tables.GetPrimaryKeys())
    {
        if (key.ToUpper().Contains(partialName.ToUpper()))
        {
            AxTable metaData = this.provider.Tables.Read(key);
            
            if (metaData != null && ShouldIncludeTable(metaData))
            {
                // Agregar tabla...
            }
        }
    }
}

private bool ShouldIncludeTable(AxTable table)
{
    if (TableFilter.HasFlag(TableTypeFilter.All))
        return true;
        
    switch (table.TableType)
    {
        case TableType.Regular:
            return TableFilter.HasFlag(TableTypeFilter.Regular);
        // ... otros tipos
    }
}
```

**UI Sugerida:**
- `CheckedListBox` con opciones:
  - ☑ Regular
  - ☑ View
  - ☐ Staging
  - ☐ InMemory
  - ☐ TempDB

**Componentes Afectados:**
- `IDataModelProvider.cs` - Agregar propiedad `TableFilter`
- `D365FODataModelProvider.cs` - Implementar filtrado
- `ErdForm.cs` - UI y binding
- Crear nuevo archivo: `TableTypeFilter.cs` (enum)

---

## 🎨 Medium Priority

### 4. Búsqueda con Wildcards (* y ?)
**Estado:** 💡 Propuesta  
**Prioridad:** Media  
**Complejidad:** Media  

**Descripción:**  
Soportar wildcards estilo SQL para búsquedas más avanzadas:
- `*` = cualquier secuencia de caracteres
- `?` = un solo carácter

**Ejemplos de Búsqueda:**
- `Cust*Table` → `CustTable`, `CustCollectionLetterTable`, etc.
- `Inv??Table` → `InvtTable` (no `InventTable`, tiene 5 letras)
- `*Trans*` → Cualquier tabla que contenga "Trans"

**Beneficios:**
- Búsquedas más precisas
- Poder para usuarios avanzados
- Compatible con convenciones de nombrado de D365

**Implementación Sugerida:**
```csharp
using System.Text.RegularExpressions;

public IDataModelProvider AddTablesByPartialName(string partialName)
{
    // Convertir wildcards a regex
    string regexPattern = "^" + Regex.Escape(partialName)
        .Replace("\\*", ".*")
        .Replace("\\?", ".") + "$";
    
    Regex regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
    
    foreach (string key in this.provider.Tables.GetPrimaryKeys())
    {
        if (regex.IsMatch(key))
        {
            // Agregar tabla...
        }
    }
}
```

**Consideraciones:**
- Mantener compatibilidad hacia atrás (si no hay wildcards, usar `Contains()`)
- Agregar tooltip explicando la sintaxis de wildcards
- Validar patrón antes de ejecutar búsqueda

**Componentes Afectados:**
- `D365FODataModelProvider.cs` - Lógica de matching con regex

---

### 5. Preview de Resultados Antes de Agregar
**Estado:** 💡 Propuesta  
**Prioridad:** Media  
**Complejidad:** Alta  

**Descripción:**  
Mostrar un diálogo con vista previa de las tablas que se agregarán antes de confirmar la operación.

**Beneficios:**
- Usuario puede revisar antes de agregar
- Evita operaciones no deseadas
- Permite deseleccionar tablas específicas

**UI Sugerida:**
```
┌────────────────────────────────────────┐
│ Preview: Agregar Tablas                │
├────────────────────────────────────────┤
│ Se encontraron 15 coincidencias con    │
│ el texto "Cust"                        │
│                                        │
│ ☑ CustTable                            │
│ ☑ CustTrans                            │
│ ☑ CustGroup                            │
│ ☑ CustInvoiceJour                      │
│ ☐ CustCollectionLetterTable (ya existe)│
│ ... (10 más)                           │
│                                        │
│ [Seleccionar Todos] [Deseleccionar]   │
│                                        │
│        [Agregar]        [Cancelar]     │
└────────────────────────────────────────┘
```

**Implementación Sugerida:**
- Crear nuevo formulario: `TableSelectionPreviewForm.cs`
- Usar `CheckedListBox` para mostrar resultados
- Permitir selección/deselección individual

**Componentes Afectados:**
- Crear: `TableSelectionPreviewForm.cs` + `.Designer.cs`
- `ErdForm.cs` - Invocar diálogo de preview

---

### 6. Historial de Búsquedas Recientes
**Estado:** 💡 Propuesta  
**Prioridad:** Baja  
**Complejidad:** Media  

**Descripción:**  
Mantener un historial de las últimas 10-20 búsquedas realizadas para facilitar búsquedas repetitivas.

**Beneficios:**
- Mejora productividad en flujos de trabajo repetitivos
- Reducir escritura redundante

**UI Sugerida:**
- Cambiar `tableTextBox` por `ComboBox` con autocompletado
- Dropdown con últimas búsquedas

**Implementación Sugerida:**
```csharp
public class SearchHistory
{
    private const int MaxHistoryItems = 20;
    private Queue<string> history = new Queue<string>();
    
    public void Add(string searchText)
    {
        if (!history.Contains(searchText))
        {
            if (history.Count >= MaxHistoryItems)
                history.Dequeue();
                
            history.Enqueue(searchText);
        }
    }
    
    public IEnumerable<string> GetHistory() => history.Reverse();
}
```

**Persistencia:**
- Guardar en `Settings` de la aplicación
- Serializar como JSON en archivo local

**Componentes Afectados:**
- `ErdForm.cs` - Cambiar `TextBox` a `ComboBox`
- Crear: `SearchHistory.cs`

---

## 💡 Nice to Have

### 7. Búsqueda por Campos/Columnas
**Estado:** 💡 Propuesta  
**Prioridad:** Baja  
**Complejidad:** Alta  

**Descripción:**  
Permitir buscar tablas que contengan campos específicos (ej: todas las tablas con campo "CustomerId").

**Caso de Uso:**
- Encontrar todas las tablas que referencian `CustAccount`
- Identificar tablas con campos de auditoría (`CreatedDateTime`, `ModifiedBy`)

**Implementación Sugerida:**
```csharp
public IDataModelProvider AddTablesByFieldName(string fieldName)
{
    foreach (string tableName in this.provider.Tables.GetPrimaryKeys())
    {
        AxTable axTable = this.provider.Tables.Read(tableName);
        
        if (axTable.Fields.Any(f => 
            f.Name.ToUpper().Contains(fieldName.ToUpper())))
        {
            // Agregar tabla...
        }
    }
}
```

**Componentes Afectados:**
- `IDataModelProvider.cs` - Nuevo método
- `D365FODataModelProvider.cs` - Implementación
- `ErdForm.cs` - Nuevo botón "Buscar por Campo"

---

### 8. Exportar Lista de Tablas a CSV/Excel
**Estado:** 💡 Propuesta  
**Prioridad:** Baja  
**Complejidad:** Baja  

**Descripción:**  
Exportar la lista actual de tablas seleccionadas a un archivo CSV o Excel para documentación.

**Formato Sugerido:**
```csv
Table Name,Description,Table Group,Cache Lookup,Field Count
CustTable,Customer master data,Main,FoundAndEmpty,45
CustTrans,Customer transactions,Transaction,NotInTTS,32
```

**Implementación Sugerida:**
```csharp
private void exportButton_Click(object sender, EventArgs e)
{
    SaveFileDialog saveDialog = new SaveFileDialog();
    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
    
    if (saveDialog.ShowDialog() == DialogResult.OK)
    {
        ExportTablesToCSV(saveDialog.FileName);
    }
}

private void ExportTablesToCSV(string filePath)
{
    using (StreamWriter writer = new StreamWriter(filePath))
    {
        writer.WriteLine("Table Name,Description,Group,Cache");
        
        foreach (var tableName in controller.DataModelProvider.Tables)
        {
            AxTable axTable = provider.Tables.Read(tableName);
            writer.WriteLine($"{axTable.Name},{axTable.DeveloperDocumentation},...");
        }
    }
}
```

**Componentes Afectados:**
- `ErdForm.cs` - Nuevo botón "Exportar Lista"

---

### 9. Búsqueda por Prefijo de Modelo
**Estado:** 💡 Propuesta  
**Prioridad:** Baja  
**Complejidad:** Baja  

**Descripción:**  
Filtrar búsquedas por prefijo de modelo/módulo (ej: solo tablas que empiecen con "WHs" para Warehouse).

**Prefijos Comunes en D365:**
- `Cust*` - Customer
- `Vend*` - Vendor
- `Invent*` - Inventory
- `WHs*` - Warehouse
- `Ledger*` - General Ledger

**Implementación Sugerida:**
- Dropdown con prefijos predefinidos
- Opción "Custom" para ingresar prefijo manualmente

---

### 10. Modo "Smart Search" con IA
**Estado:** 💭 Idea  
**Prioridad:** Muy Baja  
**Complejidad:** Muy Alta  

**Descripción:**  
Integrar búsqueda semántica usando IA para interpretar consultas en lenguaje natural.

**Ejemplos:**
- "Todas las tablas relacionadas con ventas" → `SalesTable`, `SalesLine`, `SalesParmTable`, etc.
- "Tablas de inventario con dimensiones" → `InventTable`, `InventDim`, `InventTrans`

**Tecnologías:**
- Azure OpenAI / Semantic Kernel
- Embeddings de nombres y descripciones de tablas
- Búsqueda por similitud vectorial

**Consideraciones:**
- Requiere conexión a internet
- Costos de API
- Privacidad de metadatos

---

## 🐛 Bugs / Technical Debt

### 11. Mejorar Rendimiento en Búsquedas
**Estado:** 💡 Propuesta  
**Prioridad:** Media  
**Complejidad:** Media  

**Descripción:**  
Optimizar búsquedas en proyectos grandes con miles de tablas usando indexación o caché.

**Problemas Actuales:**
- `GetPrimaryKeys()` itera todas las tablas en cada búsqueda
- Sin caché de metadatos ya leídos
- Sin búsqueda asíncrona (UI se congela)

**Soluciones Propuestas:**
```csharp
// 1. Caché de nombres de tabla
private Dictionary<string, AxTable> tableCache = new Dictionary<string, AxTable>();

// 2. Búsqueda asíncrona
private async Task<List<string>> SearchTablesAsync(string partialName)
{
    return await Task.Run(() => {
        // Lógica de búsqueda pesada
    });
}

// 3. Indexación con Trie o Hash Set
private HashSet<string> tableNamesIndex;

private void BuildIndex()
{
    tableNamesIndex = new HashSet<string>(
        provider.Tables.GetPrimaryKeys(),
        StringComparer.OrdinalIgnoreCase
    );
}
```

**Componentes Afectados:**
- `D365FODataModelProvider.cs` - Optimizaciones
- `ErdForm.cs` - UI asíncrona con `async/await`

---

### 12. Agregar Tests Unitarios
**Estado:** ⚠️ Deuda Técnica  
**Prioridad:** Alta  
**Complejidad:** Alta  

**Descripción:**  
Implementar suite de tests unitarios para validar funcionalidad de búsqueda y prevención de regresiones.

**Tests Sugeridos:**
```csharp
[TestClass]
public class D365FODataModelProviderTests
{
    [TestMethod]
    public void AddTablesByPartialName_WithValidText_AddsMatchingTables()
    {
        // Arrange
        var provider = CreateMockProvider();
        var dataModel = new D365FODataModelProvider(provider);
        
        // Act
        dataModel.AddTablesByPartialName("Cust");
        
        // Assert
        Assert.IsTrue(dataModel.Tables.Contains("CustTable"));
        Assert.IsTrue(dataModel.Tables.Contains("CustTrans"));
    }
    
    [TestMethod]
    public void AddTablesByPartialName_AvoidsDuplicates()
    {
        // Arrange & Act
        dataModel.AddTable("CustTable");
        dataModel.AddTablesByPartialName("Cust");
        
        // Assert
        Assert.AreEqual(1, dataModel.Tables.Count(t => t == "CustTable"));
    }
}
```

**Framework:**
- MSTest o NUnit
- Moq para mocks de `IMetadataProvider`

**Componentes Afectados:**
- Crear proyecto: `Waywo.DbSchema.Tests`

---

## 📊 Métricas de Éxito

### KPIs Propuestos
- ⏱️ Tiempo promedio de búsqueda < 2 segundos
- 🎯 Tasa de satisfacción del usuario con resultados > 85%
- 🐛 Bugs reportados en búsqueda < 5 por release
- 📈 Uso de búsqueda parcial vs. exacta (telemetría)

---

## 🗓️ Roadmap Sugerido

### Sprint 1 (Fundamentos)
- ✅ Búsqueda parcial (COMPLETADO)
- [ ] Límite máximo de resultados (#2)
- [ ] Tests unitarios básicos (#12)

### Sprint 2 (Usabilidad)
- [ ] Modo de búsqueda dual (#1)
- [ ] Filtro por tipo de tabla (#3)
- [ ] Preview de resultados (#5)

### Sprint 3 (Avanzado)
- [ ] Wildcards (#4)
- [ ] Optimización de rendimiento (#11)
- [ ] Historial de búsquedas (#6)

### Sprint 4 (Nice to Have)
- [ ] Búsqueda por campos (#7)
- [ ] Exportar a CSV (#8)
- [ ] Búsqueda por prefijo (#9)

### Futuro (Investigación)
- [ ] Smart Search con IA (#10)

---

## 📝 Notas

- **Compatibilidad:** Todas las propuestas deben mantener compatibilidad con .NET Framework 4.8
- **Testing:** Probar en proyectos grandes de D365 (1000+ tablas)
- **Documentación:** Actualizar README.md con nuevas funcionalidades
- **UX:** Mantener simplicidad y no sobrecargar la UI

---

**Última actualización:** 2025-01-21  
**Mantenedores:** JonatanTorino  
**Contribuciones:** Bienvenidas vía Pull Request

---

## 🤝 Contribuir

Para trabajar en cualquiera de estas propuestas:

1. Crear un issue en GitHub referenciando el número de propuesta
2. Comentar en el issue para "reclamar" la tarea
3. Crear un branch: `feature/backlog-{número}-descripción`
4. Implementar con tests
5. Crear Pull Request

**Ejemplo:**
```bash
git checkout -b feature/backlog-2-limite-resultados
# Implementar...
git commit -m "feat: agregar límite máximo de resultados (closes #XX)"
git push origin feature/backlog-2-limite-resultados
```
