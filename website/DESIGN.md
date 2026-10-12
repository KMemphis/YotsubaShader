# YotsubaShader — dirección de producto

## Público y necesidades
Desarrolladores C# interesados en programación gráfica. Tareas: entender la arquitectura, leer los ejemplos existentes y comprobar la madurez del proyecto. La interfaz no anuncia un SDK estable, descargas ni un compilador web que no existen.

## Identidad
El logo aprobado y el carmesí #A71C33 son las señales de marca. Fondo blanco para lectura, superficies grises para agrupar recursos, cabecera #202124 y código oscuro. Ámbar se reserva para limitaciones y avisos. Sin ilustraciones decorativas, gradientes, animaciones de entrada ni publicidad vacía.

Tipografía nativa del sistema y monoespaciada para código. Escala: 40 px de título de portada, 34 px de título de página, 24 px de sección, 15–16 px de lectura. Espaciado basado en múltiplos de 4 px. Radios de 3 px, sin sombras.

## Arquitectura de información
- Inicio: resumen, código real, acceso a recursos, tabla de componentes y estado.
- Aprender: requisitos y recorridos propuestos.
- Lenguaje: etapas y atributos, con advertencias sobre la API cambiante.
- Documentación: responsabilidades de los componentes.
- Herramientas: generador, constructor e inspección SPIR-V.
- Ejemplos: listado y páginas propias de color y módulo magenta.
- Estado: resultados y bloqueos de la revisión técnica.

Navegación principal constante. Las páginas interiores incorporan navegación lateral; en móvil es una fila desplazable de enlaces. Breadcrumbs sitúan los ejemplos dentro de su página padre. Todos los enlaces llevan a rutas completas: no hay navegación por anclas.

## Componentes
Acción principal carmesí, enlace secundario textual, lista de recursos, tabla, bloque de código con copia, aviso de limitación. Sin formularios, loaders, controles ni contadores simulados. Copiar da confirmación accesible; si falla, selecciona el código e informa. Menú móvil con aria-expanded y Escape. Botón de salto al contenido para teclado. Respeta movimiento reducido.

## Referencias de criterio
Carbon: retícula, jerarquía y contenido técnico. Primer: orientación mediante navegación activa y breadcrumbs.
- https://www.carbondesignsystem.com/building-blocks/foundations/2x-grid/overview
- https://primer.style/product/ui-patterns/navigation/

## Verificación
Nueve rutas en 1440, 900, 390 y 320 px; referencias locales; ausencia de anclas; un H1 por página; menú móvil, Escape, copia y salto al contenido. Revisión visual de portada y arquitectura. Estas comprobaciones no equivalen a una auditoría WCAG completa.

## Portada de presentación
La portada usa el principio de Vulkan de mostrar contenido gráfico antes de la información técnica. La imagen es Enuma, la ilustración facilitada por el usuario. Se conserva su composición vertical completa, sin recortar los personajes, tanto en escritorio como en móvil. El panel lateral comunica el interés de explorar shaders desde C# y propone abrir ejemplos. No se atribuye la imagen a YotsubaShader ni se anuncia una cadena de compilación completa.

Secuencia: presentación visual → accesos a recursos → explicación con código real → ejemplos → arquitectura y estado → acceso final al shader de color. La documentación mantiene su diseño de lectura. En móvil, mensaje e imagen se apilan para conservar legibilidad y un encuadre completo. Sin carrusel automático, efectos decorativos ni funcionalidades simuladas.
