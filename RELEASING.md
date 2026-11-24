# Publicar en NuGet

Los releases estables usan una etiqueta `vX.Y.Z` cuyo commit pertenece a `main`. El
workflow [`.github/workflows/release.yml`](.github/workflows/release.yml) se inicia al
publicar el GitHub Release. Primero crea y prueba los 19 paquetes, instala el
analizador y el CLI en consumidores limpios, y ejecuta el CLI autocontenido en seis
runners (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`).
Cada runner compila el ejemplo a SPIR-V, HLSL, GLSL, MSL, MGFX, KNI y FNA. Solo si
terminan todas esas comprobaciones, se envían los paquetes a nuget.org y se adjuntan
los seis archivos al Release. Los ejecutables arrancan sin el runtime .NET; para
compilar un `.csproj` necesitan un SDK .NET compatible.

## Preparación de la cuenta

La variable de GitHub `NUGET_USER` del repositorio es `YotsubaShader`. El job de
publicación usa el environment `nuget` y `NuGet/login@v1` para obtener una credencial
temporal mediante OIDC. En nuget.org, el propietario de los paquetes debe configurar
una política de Trusted Publishing que vincule la cuenta `YotsubaShader` al repositorio
`KMemphis/YotsubaShader`, el workflow `release.yml` y el environment `nuget`. Esta
política se configura en la cuenta de nuget.org antes del primer Release. No se
almacena una API key en GitHub.

## Dependencia ShadowDusk

El submódulo usa el fork `KMemphis/YotsubaShader-ShadowDusk`. Sus cuatro paquetes
`YotsubaShader.ShadowDusk.*` se empaquetan junto con los paquetes de este repositorio
con la misma versión. El fork sigue a ShadowDusk upstream (`kaltinril/ShadowDusk`):
cada actualización se fusiona en una rama del fork y se enlaza el submódulo antes del
Release. El DXC fijado (1.9.2607) de Linux (x64 y ARM64) y macOS sale del paquete
`MonoGame.Tool.Dxc`, y el de Windows de los releases de upstream. Solo los binarios
ARM64 de vkd3d (Linux y Windows) se restauran desde los GitHub Releases del fork;
`tools/restore.*` comprueba sus SHA-256 y los cargadores comprueban la identidad
nativa antes de usarlos. El verificador del Release exige que los seis RID incluyan
los binarios requeridos, con los hashes de `scripts/verify_release.py`: si cambia
la versión de DXC o de vkd3d, hay que actualizarlos allí.

El CI (`ci.yml`, job `plugin-smoke`) construye los plugins FNA, MonoGame y KNI desde
el commit y compila un shader a todos sus destinos en los seis RID, antes de que
existan en NuGet (`scripts/stage_plugins.py`).

Si un Release se publicó accidentalmente como preliminar, primero cámbialo a
estable. Reejecutar el workflow omitido conserva el evento original y lo vuelve
a omitir. Para reintentar el mismo Release estable sin mover la etiqueta ni crear
otro Release, ejecuta manualmente `release.yml` desde `main` con el input
`tag=vX.Y.Z`. El workflow comprueba el Release y construye desde ese tag.

Para comprobar los paquetes localmente, con los binarios nativos ya restaurados:

```sh
bash ThirdParty/ShadowDusk/tools/restore.sh
bash scripts/pack_release.sh 0.1.0 /tmp/yotsuba-nupkg
python3 scripts/verify_release.py packages /tmp/yotsuba-nupkg 0.1.0
```
