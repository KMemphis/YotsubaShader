# Diagnósticos de YotsubaShader

Generado por `scripts/generate-rule-docs.py` desde `ShaderRules.cs`.

| ID | Título | Severidad | Área |
|---|---|---|---|
| [YS0001](YS0001.md) | Número de componentes inválido | Error | Tipos de GPU |
| [YS0002](YS0002.md) | Índice de componente negativo | Error | Tipos de GPU |
| [YS0003](YS0003.md) | Índice de componente repetido | Error | Tipos de GPU |
| [YS0004](YS0004.md) | Falta un índice de componente | Error | Tipos de GPU |
| [YS0005](YS0005.md) | Componentes con tipos distintos | Error | Tipos de GPU |
| [YS0006](YS0006.md) | Tipo de componente no soportado | Error | Tipos de GPU |
| [YS0007](YS0007.md) | Miembro del vector sin [VectorComponent] | Warning | Tipos de GPU |
| [YS0008](YS0008.md) | [VectorComponent] ignorado | Warning | Tipos de GPU |
| [YS0009](YS0009.md) | [VectorComponent] fuera de un vector | Warning | Tipos de GPU |
| [YS0010](YS0010.md) | Valor inicial no constante | Warning | Tipos de GPU |
| [YS0011](YS0011.md) | Tipo sin representación en la GPU | Error | Tipos de GPU |
| [YS0100](YS0100.md) | Punto de entrada no válido | Error | Puntos de entrada |
| [YS0101](YS0101.md) | Compute shader sin tamaño de grupo | Error | Puntos de entrada |
| [YS0102](YS0102.md) | Tamaño de grupo inválido | Error | Puntos de entrada |
| [YS0103](YS0103.md) | Llamada directa a un punto de entrada | Error | Puntos de entrada |
| [YS0104](YS0104.md) | Tamaño de grupo demasiado grande | Error | Puntos de entrada |
| [YS0105](YS0105.md) | Tamaño de grupo ignorado | Warning | Puntos de entrada |
| [YS0106](YS0106.md) | Varias etapas en un método | Error | Puntos de entrada |
| [YS0107](YS0107.md) | Parámetro por referencia en un punto de entrada | Error | Puntos de entrada |
| [YS0108](YS0108.md) | Punto de entrada duplicado | Error | Puntos de entrada |
| [YS0201](YS0201.md) | Built-in no válido en esta etapa | Error | Interfaz entre etapas |
| [YS0202](YS0202.md) | Built-in en el sentido equivocado | Error | Interfaz entre etapas |
| [YS0203](YS0203.md) | Tipo incorrecto para el built-in | Error | Interfaz entre etapas |
| [YS0204](YS0204.md) | Built-in repetido | Error | Interfaz entre etapas |
| [YS0205](YS0205.md) | Location duplicada | Error | Interfaz entre etapas |
| [YS0206](YS0206.md) | Atributos de interfaz en conflicto | Error | Interfaz entre etapas |
| [YS0207](YS0207.md) | Miembro de la interfaz sin slot | Error | Interfaz entre etapas |
| [YS0208](YS0208.md) | bool en la interfaz | Error | Interfaz entre etapas |
| [YS0209](YS0209.md) | Location en un compute shader | Error | Interfaz entre etapas |
| [YS0210](YS0210.md) | Render target fuera de rango | Error | Interfaz entre etapas |
| [YS0211](YS0211.md) | Índice de location negativo | Error | Interfaz entre etapas |
| [YS0212](YS0212.md) | Semántica en una salida de fragment | Error | Interfaz entre etapas |
| [YS0213](YS0213.md) | Array de interfaz sin longitud | Error | Interfaz entre etapas |
| [YS0214](YS0214.md) | Recurso en la interfaz | Error | Interfaz entre etapas |
| [YS0215](YS0215.md) | Entrada de fragment sin salida de vertex | Error | Interfaz entre etapas |
| [YS0216](YS0216.md) | Tipos distintos entre etapas | Error | Interfaz entre etapas |
| [YS0217](YS0217.md) | Descriptor en la interfaz | Error | Interfaz entre etapas |
| [YS0218](YS0218.md) | Tipo no válido en la interfaz | Error | Interfaz entre etapas |
| [YS0219](YS0219.md) | Built-in no soportado todavía | Error | Interfaz entre etapas |
| [YS0220](YS0220.md) | Struct de interfaz anidado | Error | Interfaz entre etapas |
| [YS0221](YS0221.md) | Vertex shader sin posición | Warning | Interfaz entre etapas |
| [YS0301](YS0301.md) | Recurso sin binding | Error | Recursos |
| [YS0302](YS0302.md) | Binding repetido | Error | Recursos |
| [YS0303](YS0303.md) | Binding negativo | Error | Recursos |
| [YS0304](YS0304.md) | Registro HLSL de otro tipo | Error | Recursos |
| [YS0305](YS0305.md) | Buffer que no es struct | Error | Recursos |
| [YS0306](YS0306.md) | Recurso declarado mal | Error | Recursos |
| [YS0307](YS0307.md) | Tipo no válido en un buffer | Error | Recursos |
| [YS0308](YS0308.md) | Escritura en un buffer de solo lectura | Error | Recursos |
| [YS0309](YS0309.md) | Location en un recurso | Error | Recursos |
| [YS0310](YS0310.md) | Campo sin significado en la GPU | Error | Recursos |
| [YS0311](YS0311.md) | Array sin tamaño en mitad del buffer | Error | Recursos |
| [YS0401](YS0401.md) | Método genérico en un shader | Error | Métodos |
| [YS0402](YS0402.md) | Parámetro ref, in u out en una función auxiliar | Error | Métodos |
| [YS0403](YS0403.md) | Parámetro params en un shader | Error | Métodos |
| [YS0404](YS0404.md) | Recursión | Error | Métodos |
| [YS0405](YS0405.md) | Llamada virtual | Error | Métodos |
| [YS0406](YS0406.md) | Método sin traducción a la GPU | Error | Métodos |
| [YS0407](YS0407.md) | Método async o iterador | Error | Métodos |
| [YS0501](YS0501.md) | Sentencia no soportada | Error | C# sin traducción |
| [YS0502](YS0502.md) | Expresión no soportada | Error | C# sin traducción |
| [YS0503](YS0503.md) | Tipo no soportado en el shader | Error | C# sin traducción |
| [YS0504](YS0504.md) | Creación de objeto sin usar | Error | C# sin traducción |
| [YS0505](YS0505.md) | Campo estático mutable | Error | C# sin traducción |
| [YS0506](YS0506.md) | switch sin caso por defecto | Error | C# sin traducción |
| [YS0507](YS0507.md) | Array no soportado | Error | C# sin traducción |
| [YS0508](YS0508.md) | Intrínseca no soportada con estos argumentos | Error | C# sin traducción |
| [YS0601](YS0601.md) | Operación solo de fragment | Error | Uso por etapa |
| [YS0602](YS0602.md) | Operación solo de compute | Error | Uso por etapa |
| [YS0603](YS0603.md) | Escritura en una entrada | Error | Uso por etapa |
| [YS0604](YS0604.md) | Atómica sobre una variable local | Error | Uso por etapa |
| [YS0701](YS0701.md) | Tipo no portable | Warning | Portabilidad |
| [YS0702](YS0702.md) | Built-in no portable | Warning | Portabilidad |
| [YS0703](YS0703.md) | Intrínseca no portable | Warning | Portabilidad |
| [YS0704](YS0704.md) | Límite de hardware no portable | Warning | Portabilidad |
| [YS0705](YS0705.md) | Recurso no portable | Warning | Portabilidad |
| [YS0706](YS0706.md) | Layout de buffer distinto entre APIs | Warning | Portabilidad |
| [YS0707](YS0707.md) | Operación con derivadas en control de flujo no uniforme | Warning | Portabilidad |
| [YS0708](YS0708.md) | NaN e infinito no fiables | Warning | Portabilidad |
| [YS0709](YS0709.md) | División entera por cero | Warning | Portabilidad |
| [YS0710](YS0710.md) | No disponible en Godot | Warning | Portabilidad |
| [YS0711](YS0711.md) | Característica no disponible en la API declarada | Warning | Portabilidad |
| [YS0712](YS0712.md) | Versión de API gráfica sin backend | Error | Portabilidad |
| [YS0713](YS0713.md) | Uniform buffer demasiado grande | Warning | Portabilidad |
