# YotsubaShader · Web

Sitio técnico multipágina en español. Identidad basada en el logo aprobado, carmesí y una jerarquía funcional para desarrolladores. Ver `DESIGN.md` para los criterios de diseño.

## Uso local

Desde esta carpeta:

```sh
npm run build
npm test
npm run dev
```

Abrir http://localhost:4173. Python 3 sirve `dist`; no hay dependencias npm. Usa tipografías del sistema, sin fuentes remotas.

## Edición

- `build.mjs`: plantillas, navegación y contenido de las nueve páginas.
- `assets/style.css`: estilos y adaptación móvil.
- `assets/app.js`: menú, copiar código.
- `assets/yotsuba-crimson.png`: logo aprobado.
- `verify.mjs`: comprobación de referencias internas y ausencia de navegación por anclas.

Las rutas tienen HTML propio y se pueden abrir directamente. Todos los enlaces de navegación llevan a páginas; los controles de menú y copiar ejecutan acciones. El servidor estático de producción debe servir índices de directorio y `404.html` para rutas inexistentes.

## Contenido verificado

Revisión del 11 de octubre de 2026: Compiler, Generator, Frontend, IR, Language, Example y Frontend.Tests, además de `docs/SPIRV_GENERATOR.md`.

`dotnet build YotsubaShader.sln --no-restore` falla con CS0234 en `Compiler/YS_Types/SpirVOperation.cs`. La inspección encontró código incompleto en ShaderProcessor y referencias de namespace inconsistentes en ShaderDiscovery y tests. No se modificó el compilador para crear la web. La portada presenta un fragmento de BasicColorShader. No se inventan releases, enlaces a repositorios del proyecto ni paquetes publicados.

El build reconstruye `dist` desde las plantillas y `assets`; no editar la salida generada. La ejecución funciona desde cualquier directorio.
