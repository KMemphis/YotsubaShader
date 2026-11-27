# Servidor de la comunidad

Conecta el foro de Yotsuba Shader Hub con un canal de foro de Discord. El Hub **nunca** habla con Discord ni guarda el token del bot: habla con este servidor.

```
Hub (en el equipo del usuario)  ──HTTPS──▶  este servidor  ──▶  Discord (bot + webhook + OAuth2)
```

- Leer el foro es público. Publicar exige iniciar sesión con Discord y estar en el servidor.
- Las respuestas salen por un webhook con el nombre y el avatar de quien escribe, y con las menciones desactivadas (no pueden hacer ping a nadie).
- **Las reacciones son de solo lectura** en el Hub: Discord solo deja que cada usuario ponga las suyas en Discord.

## Probarlo sin Discord

```bash
Community__Mode=dev ASPNETCORE_URLS=http://127.0.0.1:5080 dotnet run
YOTSUBA_COMMUNITY_URL=http://127.0.0.1:5080 dotnet run --project ../YotsubaShader.Hub
```

El modo `dev` sirve datos inventados y un inicio de sesión falso ("Dev User"). No lo expongas a internet.

## Configurar Discord

1. En <https://discord.com/developers/applications> crea una aplicación.
2. **Bot**: crea el bot, copia el token y activa el intent privilegiado **Message Content**. Sin él, Discord devuelve vacío el texto de los mensajes (también por la API REST).
3. **OAuth2 → Redirects**: añade `https://TU-DOMINIO/auth/callback` (exactamente igual que `Community__PublicUrl` más `/auth/callback`). Con scope `identify`.
4. Invita el bot al servidor con permisos: *View Channels*, *Read Message History*, *Manage Webhooks*, *Send Messages in Threads*, *Create Public Threads*. Limítalo al canal de foro con permisos del canal.
5. Activa el modo desarrollador de Discord y copia los ids del servidor y del canal de foro.
6. En el canal de foro crea las etiquetas que quieras (por ejemplo Help, Showcase, Ideas). Una etiqueta llamada `Solved` marca el hilo como resuelto y no se muestra como categoría.

## Variables de entorno

| Variable | Qué es |
| --- | --- |
| `Community__Mode` | `discord` |
| `Community__PublicUrl` | Dirección https por la que el navegador del usuario llega al servidor |
| `Community__SessionSecret` | Al menos 32 caracteres aleatorios. Firma las sesiones; si cambia, todos tienen que volver a entrar |
| `Community__Discord__BotToken` | Token del bot |
| `Community__Discord__GuildId` | Id del servidor |
| `Community__Discord__ForumChannelId` | Id del canal de foro |
| `Community__Discord__ClientId` / `ClientSecret` | De la pestaña OAuth2 |
| `Community__Discord__InviteUrl` | Se muestra a quien intenta entrar sin estar en el servidor |
| `Community__Discord__StaffRoleIds__0`… | Ids de roles que se muestran como *Maintainer* (igual con `HelperRoleIds`) |

Los secretos van en variables de entorno o en el gestor de secretos del alojamiento, nunca en el repositorio. Si el servidor falta o tiene mal algún valor, no arranca y dice cuál.

Detrás de un proxy inverso en otra máquina, configura `ForwardedHeadersOptions.KnownProxies` para que los límites de uso vean la IP real.

## Cómo apunta el Hub aquí

El Hub usa `YOTSUBA_COMMUNITY_URL` (https, o http solo en localhost). Sin esa variable muestra el foro de ejemplo sin conexión. Todavía no hay un valor por defecto compilado: falta decidir dónde se aloja.

## Inicio de sesión

El Hub no puede recibir la redirección de Discord, así que:

1. Abre `/auth/start?login=ID` en el navegador y espera. `ID` son 32 bytes aleatorios.
2. La página muestra un **código** (derivado de `ID`) que debe coincidir con el que enseña el Hub. Si alguien te manda un enlace de inicio de sesión que él mismo empezó, el código no coincide y puedes cerrarlo.
3. Tras Discord, la sesión queda guardada bajo `ID` y el Hub la recoge una sola vez. Un login terminado o usado no se puede reiniciar.

## Límites

- 240 peticiones por minuto por IP; 10 mensajes por minuto y 3 hilos cada 10 minutos por usuario.
- Mensajes de hasta 4000 caracteres y títulos de hasta 100.
- La búsqueda solo cubre títulos y la primera línea de cada hilo.
- La lista se refresca desde Discord como mucho cada 20 segundos y muestra los 100 hilos más recientes.
