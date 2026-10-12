# Identificador de YotsubaFramework en SPIR-V

## Resumen

La palabra `Generator` del encabezado de un módulo SPIR-V no contiene un nombre como texto. Es un valor de 32 bits dividido en dos partes:

```text
31                              16 15                               0
+--------------------------------+----------------------------------+
| Tool ID asignado por Khronos   | Versión definida por el proyecto |
+--------------------------------+----------------------------------+
```

- Los 16 bits superiores identifican la herramienta que generó el módulo.
- Los 16 bits inferiores contienen una versión o cualquier valor que decida el proveedor de la herramienta.
- El valor completo puede ser `0`; no modifica la semántica del módulo SPIR-V.

Actualmente `SpirVCompiler` escribe `0` en este campo. Por eso una herramienta como `spirv-dis` puede mostrar el generador reservado con el ID cero en lugar de `YotsubaFramework`.

## Implementación

El valor puede construirse de esta forma:

```csharp
private const ushort GeneratorToolId = 0;
private const ushort GeneratorVersion = 1;

private static uint GeneratorWord =>
    ((uint)GeneratorToolId << 16) | GeneratorVersion;
```

Al construir el encabezado del módulo:

```csharp
moduleHeader.AddRange(BitConverter.GetBytes(GeneratorWord));
```

El encabezado completo conserva este orden:

```csharp
moduleHeader.AddRange(BitConverter.GetBytes(0x07230203u));   // Magic number
moduleHeader.AddRange(BitConverter.GetBytes(0x00010600u));   // Versión SPIR-V 1.6
moduleHeader.AddRange(BitConverter.GetBytes(GeneratorWord)); // Generador
moduleHeader.AddRange(BitConverter.GetBytes((uint)nextID));  // Bound
moduleHeader.AddRange(BitConverter.GetBytes(0u));            // Schema reservado
```

## Registro oficial de YotsubaFramework

Para que las versiones actualizadas de SPIRV-Tools puedan mostrar el nombre `YotsubaFramework`, el proyecto debe reservar un Tool ID en el registro oficial de Khronos:

- Registro: <https://github.com/KhronosGroup/SPIRV-Headers/blob/main/include/spirv/spir-v.xml>
- Especificación del encabezado: <https://registry.khronos.org/SPIR-V/specs/unified1/SPIRV.html#_physical_layout_of_a_spir_v_module_and_instruction>

El proceso es:

1. Consultar el elemento `<unused>` del registro para encontrar el primer ID disponible.
2. Crear un fork de `KhronosGroup/SPIRV-Headers`.
3. Añadir una entrada contigua con el proveedor, el nombre de la herramienta y un contacto.
4. Incrementar el valor `start` de `<unused>`.
5. Enviar un pull request a Khronos.
6. Usar el ID en YotsubaFramework solamente después de que la reserva sea aceptada.

Ejemplo ilustrativo, donde `NN` debe sustituirse por el ID realmente asignado:

```xml
<id value="NN"
    vendor="Nombre del proveedor"
    tool="YotsubaFramework"
    comment="Contacto y URL del repositorio"/>
```

Después de la asignación oficial:

```csharp
private const ushort GeneratorToolId = NN;
private const ushort GeneratorVersion = 1;
```

No se debe escoger un ID privado o copiar el ID de otra herramienta. Dos generadores que usen el mismo ID producirían información de procedencia ambigua.

## Versiones de la herramienta

Los 16 bits inferiores pertenecen a YotsubaFramework. Una estrategia sencilla es incrementarlos para cada versión incompatible del generador binario:

```csharp
private const ushort GeneratorVersion = 1;
```

Este número no es la versión de SPIR-V. La versión de SPIR-V continúa almacenada por separado en la segunda palabra del encabezado, por ejemplo `0x00010600` para SPIR-V 1.6.

## Validación

Después de modificar el encabezado:

```bash
dotnet run --project YotsubaShader.Example/YotsubaShader.Example.csproj
spirv-val "mi modulo spirv.spv"
spirv-dis "mi modulo spirv.spv" -o -
```

`spirv-val` debe seguir aceptando el módulo. El nombre nuevo solo aparecerá en `spirv-dis` cuando la versión instalada de SPIRV-Tools incluya el registro actualizado de Khronos.
