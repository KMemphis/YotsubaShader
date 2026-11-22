# Prompt: añadir la guía de Godot a learn.yotsubashader.com

Copia todo lo que hay debajo de la línea y pégalo a la otra IA. Va completo: no necesita el contexto de esta conversación.
Junto a este archivo hay una captura (`godot-plasma.png`) que la guía puede usar.

---

## Tu tarea

Añade **Godot** a la sección **Usar en engines existentes** del sitio de aprendizaje de Yotsuba Shader (`Web/learn/`).
Esa sección ya tiene guías de MonoGame, KNI y FNA. Falta Godot. Tienes que crear la guía en **español e inglés**, registrar el engine
en el sitio y comprobar que el sitio compila.

Puedes verlo en local en `http://localhost:4174/es/engines/` (servidor de `npm run dev`, ya suele estar corriendo).

## Reglas del repositorio (obligatorias)

El repositorio tiene un `AGENTS.md` en la raíz. Resumen de lo que te toca respetar:

- **Solo puedes modificar archivos dentro de `Web/`.** Todo lo que cambies va dentro de `Web/learn/`. No toques nada fuera de `Web/`
  (ni `Core/`, `Plugins/`, `Examples/`, `README.md` de la raíz, `.sln`...).
- **No modifiques** `LICENSE`, `NOTICE`, cabeceras de licencia ni `assets/` de la raíz.
- No hagas commits, push ni pull requests. Deja los cambios sin commitear.
- No generes audio (`tts.mjs` necesita una clave de API que no tienes). Las páginas funcionan sin audio.
- La documentación debe separar **lo que funciona hoy** de **lo que está previsto**. No afirmes nada que no esté en la sección
  "Hechos verificados".
- Al terminar, di exactamente qué archivos creaste o editaste.

## Cómo está hecho el sitio (léelo antes de escribir)

Lee `Web/learn/README.md`. Lo que necesitas saber:

- Es un sitio estático generado desde Markdown. `cd Web/learn && npm run build` escribe `dist/` y **falla si hay enlaces rotos o falta
  el frontmatter**. `npm run dev` sirve en `http://localhost:4174` y reconstruye al cambiar.
- Idiomas `en` y `es`, con los mismos nombres de archivo: `content/<idioma>/engines/<id>.md`.
- Frontmatter de cada capítulo: `title`, `summary`, `order`, y opcionalmente `status: planned` (muestra la etiqueta "Planificado").
- Markdown soportado: `##`/`###` (alimentan "En esta página"), tablas, imágenes (`![alt](../../assets/engines/x.png)`, archivos en
  `content/assets/engines/`), enlaces entre capítulos (`[texto](../get-started/setup.md)`, se validan en el build), bloques de código
  con ` ```csharp title="Ruta/Archivo.cs" `, y avisos `> [!NOTE]`, `> [!TIP]`, `> [!WARNING]`, `> [!IMPORTANT]`, `> [!PLANNED]`.
- La fila de engines de la página de inicio sale de `ENGINES` en `Web/learn/site.config.mjs`. Cada engine necesita su
  `content/<idioma>/engines/<id>.md`.

**Modelos a imitar** (léelos enteros, sobre todo el de FNA, que es el más completo):

- `Web/learn/content/es/engines/fna.md` y `content/en/engines/fna.md`
- `Web/learn/content/es/engines/index.md` (página de la sección)
- `Web/learn/content/es/engines/unity.md` (ejemplo de página provisional con `status: planned`)

**Estilo de escritura**, igual que el resto del sitio:

- Segunda persona ("vas a hacer", "crea", "ejecuta"), frases cortas, sin relleno ni frases que suenen a texto generado
  ("en el mundo actual", "potente", "sin esfuerzo").
- Explica con **código C# real**, no con tablas de nombres sueltos. Cada bloque de código va seguido de una explicación de qué hace
  cada parte.
- Los nombres de engines y marcas pertenecen a sus propietarios; no uses logos.
- Todo ejemplo de código debe poder copiarse y funcionar. Si no lo has probado, no lo pongas.

## Qué crear y modificar (todo dentro de `Web/learn/`)

1. `content/es/engines/godot.md` y `content/en/engines/godot.md` (mismo nombre, mismo contenido traducido).
   - `order: 7` (va después de Silk.NET, que tiene 6; no renumeres las demás).
   - `title: Godot`, un `summary` de una frase.
   - **Sin** `status: planned` en la página (el plugin funciona), pero ver la nota sobre NuGet más abajo.
2. `site.config.mjs`: añade `{ id: 'godot', name: 'Godot' }` a `ENGINES`, justo después de `fna`.
3. `content/es/get-started/next-steps.md` y `content/en/get-started/next-steps.md`: hay una lista de engines con enlaces (cerca de la
   línea 30, tiene `[Silk.NET](../engines/silknet.md)`). Añade Godot con el mismo formato.
4. `content/assets/engines/godot-plasma.png`: copia la captura `prompt-godot-web/godot-plasma.png` (está en la raíz del repo, junto a
   este prompt). Úsala en la guía con un texto alternativo descriptivo ("Un plasma animado de colores: rosa, amarillo y verde").
5. Opcional: una frase en `content/*/engines/index.md` si la lista de engines se menciona allí. Si no, no lo toques.

## Hechos verificados (puedes afirmarlos)

Todo esto se probó en una máquina real con **Godot 4.7.2 estándar (GDScript, no la versión .NET) en macOS**.

### Qué es

Un plugin del CLI, `YotsubaShader.Extensions.Godot`, que traduce un shader de C# a un archivo **`.gdshader`** (Godot Shading Language).
No produce un binario como MonoGame/KNI/FNA: produce **texto**. Se usa con `yotsuba compile` o `yotsuba add`.

- `dotnet yotsuba compile MisShaders/MisShaders.csproj --target godot --output <carpeta>` escribe, por cada clase de shader:
  `<Espacio.De.Nombres>/<Clase>.spatial.gdshader` y `...canvasitem.gdshader` (el espacio de nombres se convierte en carpetas; por ejemplo
  la clase `Examples.GodotShaders.PlasmaShader` queda en `Examples/GodotShaders/PlasmaShader.spatial.gdshader`).
- `dotnet yotsuba add ... --target godot` genera en `YotsubaShader.g.cs` el método `string ToGodotShader(this MiShader shader, GodotShaderType type)`
  que devuelve el código del `.gdshader` como `string` (no `byte[]`). `GodotShaderType` es un enum con `Spatial` y `CanvasItem`.
- `gdshader` es un alias de `godot` en `--target`.
- Dos tipos de shader de Godot: `spatial` (para `MeshInstance3D`, 3D) y `canvas_item` (2D, `ColorRect`, `Sprite2D`...).

### Cómo se traduce

- `Vertex` pasa a `void vertex()` y `Fragment` a `void fragment()`.
- Las entradas del vértice se enlazan por **semántica**: `[POSITION]` → `VERTEX`, `[NORMAL]`, `[TANGENT]`, `[TEXCOORD(0)]` → `UV`,
  `[TEXCOORD(1)]` → `UV2`, `[COLOR]`. En `canvas_item` solo existen posición, color y el primer `TEXCOORD`.
- Las salidas del vértice que lee el fragmento pasan a `varying`.
- La posición que devuelve el vértice (`SV_Position`) se escribe en `POSITION` (spatial) o `VERTEX` (canvas_item).
  **Es posición en espacio de recorte**: Godot no aplica sus matrices de modelo/vista/proyección a un shader hecho así. Si quieres
  aplicarlas, hay que pasarlas como uniform y multiplicarlas tú.
- El color de salida (`SV_Target`) va a `ALBEDO` y `ALPHA` (spatial) o a `COLOR` (canvas_item).
- Un `[Uniform]` struct se convierte en un `uniform` por cada campo, con el nombre **`<CampoDelShader>_<Miembro>`**. Ejemplo: un campo
  `Tiempo` de tipo `Cronometro` con el dato `Segundos` se llama **`Tiempo_Segundos`** en Godot; un campo `P` con `float4 Tint` se llama
  `P_Tint`. Se envían con `material.set_shader_parameter("Tiempo_Segundos", valor)`.
- Las texturas pasan a `uniform sampler2D <nombre>`. El estado del sampler (filtro, repetición) lo decide Godot con los hints de la
  textura, no el `SamplerState` de C#.
- Los shaders `spatial` se generan con `render_mode unshaded, cull_disabled;`. `unshaded` porque el fragmento ya calcula el color final;
  `cull_disabled` porque un `ShaderMaterial` no puede quitar el *culling* y, al escribir en espacio de recorte, Godot descartaba las caras
  (el rectángulo de pantalla completa **no se veía** sin esto).

### Lo que no se puede traducir (da un error claro, no un archivo roto)

Compute shaders, storage buffers (`StructuredBuffer`, `RWStructuredBuffer`, arrays/listas como buffer), más de un render target
(`SV_Target(1)`...), atómicas y built-ins sin equivalente en Godot. Un shader 3D que lee `NORMAL` no puede ser `canvas_item`: el CLI
avisa de ese archivo y termina con código de salida 1, pero el `.spatial.gdshader` sí se escribe.

### El addon del editor

Carpeta `addons/yotsubashader` (código fuente en `Plugins/YotsubaShader.Extensions.Godot/addon/` del repositorio; el `README.md` que hay
ahí explica los ajustes). Se copia a `res://addons/`, se activa en *Proyecto → Ajustes del proyecto → Plugins* y, desde ese momento:

- **Regenera los `.gdshader` cada vez que pulsas Play** (usa el hook `_build` del editor) y también desde el menú
  *Proyecto → Herramientas → Regenerar shaders de YotsubaShader*. Después refresca el sistema de archivos de Godot.
- Ajustes (en *Ajustes del proyecto → General*, con "Ajustes avanzados" activado, sección `yotsuba_shader`):

  | Ajuste | Qué es | Por defecto |
  | --- | --- | --- |
  | `project` | Ruta al `.csproj` con tus shaders (absoluta o relativa al proyecto de Godot) | vacío |
  | `output_dir` | Dónde se escriben los `.gdshader` | `res://yotsuba_shaders` |
  | `shader_type` | `spatial`, `canvas_item` o `both` | `spatial` |
  | `command` | El comando de yotsuba | `dotnet yotsuba` |
  | `regenerate_on_run` | Regenerar al pulsar Play | activado |

- Si el comando falla, el error sale en el panel de salida de Godot. Un fallo en un tipo de shader que no elegiste (por ejemplo
  `canvas_item` cuando solo usas `spatial`) no se considera error.
- Si Godot se abrió desde el Dock o Finder, puede no encontrar `dotnet`: en ese caso hay que poner la ruta completa de la herramienta en
  `command`.
- `dotnet yotsuba` funciona si la carpeta del proyecto Godot está **dentro** de la carpeta donde se hizo `dotnet new tool-manifest` (el
  tutorial lo hace en `Get Started → Prepara tu proyecto`), porque .NET busca el manifiesto hacia arriba.

### El ejemplo completo, probado

El repositorio tiene un proyecto de Godot que funciona: `Examples/Godot.Example/`. **Es la fuente de verdad del código de la guía**: léelo y
úsalo (no inventes variantes). Archivos:

- `Shaders/Godot.Shaders.csproj` y `Shaders/PlasmaShader.cs` (el shader de C#: un efecto a pantalla completa con un plasma animado y
  un uniform `PlasmaParams P` con `Tint` y `Time`).
- `project.godot` (activa el addon y fija `yotsuba_shader/project`), `main.tscn` (un `MeshInstance3D` con un `QuadMesh` de 2×2, que
  cubre la pantalla porque el vértice pasa la posición tal cual al espacio de recorte, y una `Camera3D`) y `main.gd`
  (carga `res://yotsuba_shaders/Examples/GodotShaders/PlasmaShader.spatial.gdshader` y actualiza `P_Time` y `P_Tint` cada frame).
- Resultado: la captura `godot-plasma.png`.

Todo el flujo se ejecutó de punta a punta: C# → `yotsuba compile --target godot` (plugin instalado desde un feed NuGet) → generador del
addon → Godot renderiza el plasma.

## Lo que NO puedes afirmar (decláralo con un aviso, no lo omitas)

1. **Los shaders del tutorial de Get Started NO funcionan en Godot tal cual.** Declaran las entradas del vértice con
   `[location(0)]`, `[location(1)]` (MonoGame/KNI/FNA las traducen a `TextureCoordinate n`). El plugin de Godot las rechaza con un
   error ("`[Location(0)]` no tiene equivalente en Godot. Usa una semántica..."), porque Godot entrega los atributos por nombre
   (`VERTEX`, `UV`...), no por número. Por eso **esta guía no puede ser la continuación del tutorial** como lo son las de MonoGame/KNI/FNA.
   Hay que escribirla con un shader nuevo que use semánticas (`[POSITION]`, `[TEXCOORD(0)]`), el del ejemplo. Explícalo con un
   `> [!IMPORTANT]` al principio ("Esta guía no continúa el tutorial") y di cómo adaptar un shader del tutorial: cambiar
   `[location(0)]` por `[POSITION]` y `[location(1)]` por `[TEXCOORD(0)]`. **No prometas que esa conversión funciona con todos los
   shaders del tutorial**: no se probó.
2. **El paquete todavía no está publicado en NuGet.** El primer `--target godot` instalaría `YotsubaShader.Extensions.Godot` desde
   NuGet, y eso aún no existe. Pon un `> [!PLANNED]` en el paso de instalación diciéndolo, igual que las guías provisionales.
   Tampoco afirmes que el addon se descarga de ningún sitio: hoy se copia desde el repositorio (`Plugins/YotsubaShader.Extensions.Godot/addon/addons/yotsubashader`).
3. **Solo se probó Godot estándar (GDScript) 4.7.2 en macOS.** No se probó Godot .NET, Windows, Linux ni otras versiones de Godot 4.
   Di "probado con Godot 4.7.2" y deja claro que el resto no está comprobado.
4. No se probó pulsar Play dentro del editor con ventana ni el elemento del menú *Herramientas*: se probó el generador del addon con un
   script y que el editor abre el proyecto con el addon activado sin errores. Puedes describir ambos pasos, pero no escribas "probado
   en el editor".
5. No se probaron mallas 3D normales con las matrices de Godot. No expliques cómo pasar una matriz de cámara; solo di que hay que
   hacerlo como uniform si se quiere.

## Estructura sugerida de la guía

1. Introducción corta y la captura del resultado, más el `> [!IMPORTANT]` de que no continúa el tutorial.
2. **Lo que vas a hacer**: un árbol de carpetas (`Tutorial/` con `MisShaders/` y el proyecto de Godot dentro) en bloque ` ```text `.
3. **1. Prepara los shaders**: crear `MisShaders` con el shader del plasma (el código de `Examples/Godot.Example/Shaders/PlasmaShader.cs`,
   adaptado a los nombres del tutorial si lo prefieres, pero **verifica que compila**). Explica el `struct` del uniform y el vértice que
   pasa la posición.
4. **2. Genera el `.gdshader`**: `dotnet yotsuba compile ... --target godot --output ...`, qué archivos salen y cuál es cuál
   (`spatial` / `canvasitem`). Con el `> [!PLANNED]` sobre NuGet.
5. **3. El proyecto de Godot**: la escena (quad de 2×2 + cámara, y por qué ese quad cubre la pantalla), `main.gd` y los uniforms con su
   nombre `<Campo>_<Miembro>`.
6. **4. El addon**: copiarlo, activarlo, ajustes (tabla) y qué pasa al pulsar Play. Aviso del `PATH` en macOS y del manifiesto de
   herramientas.
7. **5. Qué se traduce**: la tabla semántica → built-in de Godot (en C# real: muestra un vértice con `[POSITION]` y `[TEXCOORD(0)]` y el
   `vertex()` resultante).
8. **Lo que no se puede**: compute, storage buffers, varios render targets (con `> [!WARNING]`) y la nota del `cull_disabled`.
9. **Resumen**: qué es propio de Godot (el método `ToGodotShader`/`--target godot`, el addon, los nombres de los uniforms) frente a lo
   que es igual que en las otras guías.

Mantén las dos versiones sincronizadas: mismos encabezados, mismo orden, mismos bloques de código (los comentarios y textos de las
cadenas pueden traducirse).

## Cómo comprobar tu trabajo

```bash
cd Web/learn
npm run build      # debe terminar sin errores: enlaces válidos y frontmatter completo
```

- Abre `http://localhost:4174/es/engines/godot/` y `http://localhost:4174/en/engines/godot/` y comprueba la página, la barra lateral
  (Godot al final de la lista), la imagen y los avisos.
- Comprueba que la fila de engines de la página de inicio (`/es/`, `/en/`) muestra Godot.
- Comprueba que `get-started/next-steps` enlaza a la guía de Godot en los dos idiomas.
- Si copias código C# a la guía, compílalo de verdad antes (puedes usar `Examples/Godot.Example/Shaders` como base).

## Qué entregas

- Los archivos nuevos y editados (lista exacta con rutas).
- Los avisos que pusiste en la guía por lo que no está verificado.
- Cualquier cosa que no hayas podido comprobar.
