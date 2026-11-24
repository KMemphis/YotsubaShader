; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
YS0001 | YotsubaShader | Error | Número de componentes inválido
YS0002 | YotsubaShader | Error | Índice de componente negativo
YS0003 | YotsubaShader | Error | Índice de componente repetido
YS0004 | YotsubaShader | Error | Falta un índice de componente
YS0005 | YotsubaShader | Error | Componentes con tipos distintos
YS0006 | YotsubaShader | Error | Tipo de componente no soportado
YS0007 | YotsubaShader | Warning | Miembro del vector sin [VectorComponent]
YS0008 | YotsubaShader | Warning | [VectorComponent] ignorado
YS0009 | YotsubaShader | Warning | [VectorComponent] fuera de un vector
YS0010 | YotsubaShader | Warning | Valor inicial no constante
YS0011 | YotsubaShader | Error | Tipo sin representación en la GPU
YS0100 | YotsubaShader | Error | Punto de entrada no válido
YS0101 | YotsubaShader | Error | Compute shader sin tamaño de grupo
YS0102 | YotsubaShader | Error | Tamaño de grupo inválido
YS0103 | YotsubaShader | Error | Llamada directa a un punto de entrada
YS0104 | YotsubaShader | Error | Tamaño de grupo demasiado grande
YS0105 | YotsubaShader | Warning | Tamaño de grupo ignorado
YS0106 | YotsubaShader | Error | Varias etapas en un método
YS0107 | YotsubaShader | Error | Parámetro por referencia en un punto de entrada
YS0108 | YotsubaShader | Error | Punto de entrada duplicado
YS0201 | YotsubaShader | Error | Built-in no válido en esta etapa
YS0202 | YotsubaShader | Error | Built-in en el sentido equivocado
YS0203 | YotsubaShader | Error | Tipo incorrecto para el built-in
YS0204 | YotsubaShader | Error | Built-in repetido
YS0205 | YotsubaShader | Error | Location duplicada
YS0206 | YotsubaShader | Error | Atributos de interfaz en conflicto
YS0207 | YotsubaShader | Error | Miembro de la interfaz sin slot
YS0208 | YotsubaShader | Error | bool en la interfaz
YS0209 | YotsubaShader | Error | Location en un compute shader
YS0210 | YotsubaShader | Error | Render target fuera de rango
YS0211 | YotsubaShader | Error | Índice de location negativo
YS0212 | YotsubaShader | Error | Semántica en una salida de fragment
YS0213 | YotsubaShader | Error | Array de interfaz sin longitud
YS0214 | YotsubaShader | Error | Recurso en la interfaz
YS0215 | YotsubaShader | Error | Entrada de fragment sin salida de vertex
YS0216 | YotsubaShader | Error | Tipos distintos entre etapas
YS0217 | YotsubaShader | Error | Descriptor en la interfaz
YS0218 | YotsubaShader | Error | Tipo no válido en la interfaz
YS0219 | YotsubaShader | Error | Built-in no soportado todavía
YS0220 | YotsubaShader | Error | Struct de interfaz anidado
YS0221 | YotsubaShader | Warning | Vertex shader sin posición
YS0301 | YotsubaShader | Error | Recurso sin binding
YS0302 | YotsubaShader | Error | Binding repetido
YS0303 | YotsubaShader | Error | Binding negativo
YS0304 | YotsubaShader | Error | Registro HLSL de otro tipo
YS0305 | YotsubaShader | Error | Buffer que no es struct
YS0306 | YotsubaShader | Error | Recurso declarado mal
YS0307 | YotsubaShader | Error | Tipo no válido en un buffer
YS0308 | YotsubaShader | Error | Escritura en un buffer de solo lectura
YS0309 | YotsubaShader | Error | Location en un recurso
YS0310 | YotsubaShader | Error | Campo sin significado en la GPU
YS0311 | YotsubaShader | Error | Array sin tamaño en mitad del buffer
YS0401 | YotsubaShader | Error | Método genérico en un shader
YS0402 | YotsubaShader | Error | Parámetro ref, in u out en una función auxiliar
YS0403 | YotsubaShader | Error | Parámetro params en un shader
YS0404 | YotsubaShader | Error | Recursión
YS0405 | YotsubaShader | Error | Llamada virtual
YS0406 | YotsubaShader | Error | Método sin traducción a la GPU
YS0407 | YotsubaShader | Error | Método async o iterador
YS0501 | YotsubaShader | Error | Sentencia no soportada
YS0502 | YotsubaShader | Error | Expresión no soportada
YS0503 | YotsubaShader | Error | Tipo no soportado en el shader
YS0504 | YotsubaShader | Error | Creación de objeto sin usar
YS0505 | YotsubaShader | Error | Campo estático mutable
YS0506 | YotsubaShader | Error | switch sin caso por defecto
YS0507 | YotsubaShader | Error | Array no soportado
YS0508 | YotsubaShader | Error | Intrínseca no soportada con estos argumentos
YS0601 | YotsubaShader | Error | Operación solo de fragment
YS0602 | YotsubaShader | Error | Operación solo de compute
YS0603 | YotsubaShader | Error | Escritura en una entrada
YS0604 | YotsubaShader | Error | Atómica sobre una variable local
YS0701 | YotsubaShader.Portability | Warning | Tipo no portable
YS0702 | YotsubaShader.Portability | Warning | Built-in no portable
YS0703 | YotsubaShader.Portability | Warning | Intrínseca no portable
YS0704 | YotsubaShader.Portability | Warning | Límite de hardware no portable
YS0705 | YotsubaShader.Portability | Warning | Recurso no portable
YS0706 | YotsubaShader.Portability | Warning | Layout de buffer distinto entre APIs
YS0707 | YotsubaShader.Portability | Warning | Operación con derivadas en control de flujo no uniforme
YS0708 | YotsubaShader.Portability | Warning | NaN e infinito no fiables
YS0709 | YotsubaShader | Warning | División entera por cero
YS0710 | YotsubaShader.Portability | Warning | No disponible en Godot
YS0711 | YotsubaShader.Portability | Warning | Característica no disponible en la API declarada
YS0712 | YotsubaShader | Error | Destino sin backend
