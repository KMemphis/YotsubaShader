; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
YS0001  | YotsubaShader | Error   | Número de componentes inválido
YS0002  | YotsubaShader | Error   | Índice de componente negativo
YS0003  | YotsubaShader | Error   | Índice de componente repetido
YS0004  | YotsubaShader | Error   | Falta un índice de componente
YS0005  | YotsubaShader | Error   | Componentes con tipos distintos
YS0006  | YotsubaShader | Error   | Tipo de componente no soportado
YS0007  | YotsubaShader | Warning | Miembro del vector sin [VectorComponent]
YS0008  | YotsubaShader | Warning | [VectorComponent] ignorado
YS0009  | YotsubaShader | Warning | [VectorComponent] fuera de un vector
YS0010  | YotsubaShader | Warning | Valor inicial no constante
YS0101  | YotsubaShader | Error   | Compute shader sin tamaño de grupo
YS0102  | YotsubaShader | Error   | Tamaño de grupo con una dimensión a cero
YS0103  | YotsubaShader | Error   | Llamada directa a un punto de entrada
YS0105  | YotsubaShader | Warning | Tamaño de grupo ignorado
YS0401  | YotsubaShader | Error   | Método genérico en un shader
YS0402  | YotsubaShader | Error   | Parámetro ref, in u out en una función auxiliar
YS0403  | YotsubaShader | Error   | Parámetro params en un shader
YS0504  | YotsubaShader | Error   | Creación de objeto sin usar
YS0201 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0202 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0203 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0204 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0205 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0206 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0207 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0209 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0220 | YotsubaShader | Error | InterfaceRulesAnalyzer
YS0223 | YotsubaShader | Error | InterfaceRulesAnalyzer