# Base de datos para la jugabilidad

## Verificación del inicio de sesión por correo

El MFA no agrega tablas ni columnas y no requiere ejecutar una actualización SQL.
Reutiliza `AuthenticationCodes` con el tipo `TwoFactor`, `Credentials.FailedAttempts`,
`Credentials.LockedUntil`, `Sessions` y la consulta de sanciones. La recuperación conserva
el tipo `PasswordReset`. `Create-AtariGo.sql` mantiene el esquema anterior al MFA.

Los intentos pendientes se conservan temporalmente en memoria de un único proceso del
servidor, con un límite de 10000 (`LoginVerification:MaximumPendingLogins`) y vencimiento.
El código protegido y su consumo se guardan en SQL. Reiniciar el servidor cancela los
intentos pendientes; el jugador debe volver a proporcionar sus credenciales. Este diseño
requiere que todas las etapas de un intento lleguen al mismo proceso del servidor.

Cambiar o recuperar la contraseña consume los códigos `TwoFactor` pendientes dentro de la
transacción de actualización. Esto impide completar o reenviar un acceso iniciado con la
contraseña anterior, incluso si su estado temporal todavía está en memoria.

La verificación reutiliza el SMTP y `PasswordRecovery:HmacKey` configurados con
`scripts/Configure-RecoveryEmail.ps1`. El cliente y el servidor deben actualizarse juntos.
La ausencia de correo o de una clave válida impide el acceso registrado, sin omitir el código.

Para probar: iniciar el servidor y el cliente actualizados, introducir las credenciales y
confirmar el código recibido. El lobby solo se abre después de confirmar el código. Un
reenvío sustituye el código anterior y respeta la espera indicada.

## Actualización de perfil y avatares

`Update-ProfileAvatars.sql` actualiza la BD existente para guardar `Profiles.AvatarId`
en lugar de la imagen binaria. Asigna el avatar `1` a todos los perfiles existentes
y crea el perfil predeterminado para las cuentas que no lo tenían. También ajusta
`Users.UserName` a `NVARCHAR(15)` y conserva la unicidad y la restricción de nombres
reservados. Si hay un nombre de más de 15 caracteres o un nombre reservado, detiene
la actualización y revierte la transacción; se deben corregir esos datos antes de
repetirla. Se puede ejecutar de nuevo después de una actualización correcta.

Detener el servidor, respaldar la BD y ejecutar el script completo antes de iniciar
la versión que usa `AvatarId`. El archivo `Create-AtariGo.sql` ya contiene el esquema
para instalaciones nuevas y no se ejecuta sobre una BD existente. La aplicación no
actualiza el esquema automáticamente.

La jugabilidad amplía las tablas existentes `Games` y `GameEvents`. No requiere tablas
adicionales. `Moves` conserva las jugadas y `GamePlayers` las capturas y resultados por jugador.
El código usa exclusivamente ese esquema; no hay selección de diseños antiguos al arrancar.

## Desbloqueo de avatares

`Update-AvatarUnlocks.sql` amplía la restricción de `Profiles.AvatarId` de 1–5 a 1–20
en una BD existente que ya tiene `AvatarId`. No modifica los perfiles guardados y puede
ejecutarse de nuevo. Respaldar la BD y detener el servidor antes de aplicarlo. Las
instalaciones nuevas ya reciben el rango 1–20 desde `Create-AtariGo.sql`. El servidor
cuenta las victorias en partidas completadas antes de permitir un avatar del 6 al 20:
el avatar 6 requiere 5 victorias y cada avatar siguiente requiere 5 más.

## Actualización a movimientos relacionales

Para la BD de pruebas con el esquema JSON anterior, detener el servidor y ejecutar
`Update-RelationalGameplay.sql` completo. Seleccionar la instancia correcta; el archivo usa
`AtariGoDB`. Si la instalación tiene otro nombre, ajustar su instrucción `USE`.

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\scripts\database\Update-RelationalGameplay.sql
```

El script aplica en una transacción los cambios de esquema y la sustitución autorizada de
las partidas de prueba. Conserva los IDs de partidas y participantes, las cuentas, sesiones,
reportes y sanciones. Reemplaza los movimientos y eventos anteriores de todas las partidas;
asigna una meta de captura de 1 a sus configuraciones y deja todas completadas con victoria
de negras. Esto también puede cambiar los conteos de victorias utilizados por los avatares.
Cada partida recibe la misma secuencia válida, con coordenadas desde cero:

| Turno | Jugador | Fila | Columna | Capturas |
| --- | --- | --- | --- | --- |
| 1 | Negras | 0 | 1 | 0 |
| 2 | Blancas | 0 | 0 | 0 |
| 3 | Negras | 1 | 0 | 1 |

La tercera jugada captura la ficha blanca de la esquina. `GamePlayers.Result` queda en
`Won` para negras y `Lost` para blancas; sus capturas quedan en 1 y 0. Las fechas de los
movimientos y la finalización se ordenan a partir del inicio de cada partida. Los nuevos
eventos representan el inicio y la finalización por `CaptureGoal`.

Antes de cambiar datos se exige exactamente un participante negro y uno blanco por partida.
Si falta alguno o hay participantes adicionales, se revierte toda la operación. La sustitución
solo se realiza cuando existen columnas JSON antiguas; repetir una actualización completada
no reinicia las partidas ni reemplaza sus movimientos. No es un migrador para conservar
partidas reales: la sustitución de contenido es exclusiva de estos datos de prueba.

`Update-Gameplay.sql` y `Update-Gameplay-Direct.sql` pertenecen al diseño anterior con JSON.
No ejecutarlos sobre el esquema actual. La aplicación no modifica automáticamente el esquema.

## Estructura de la jugabilidad

| Tabla | Columnas vigentes |
| --- | --- |
| `Games` | `GameId`, `RoomId`, `GameConfigurationId`, `GameType`, `State`, `StartedAt`, `FinishedAt`, `EndReason`, `StateVersion`. |
| `GameConfigurations` | `GameConfigurationId`, `CaptureGoal`, `GameTimeSeconds`, `TurnTimeSeconds`. |
| `GamePlayers` | `GamePlayerId`, `GameId`, `UserId`, `GuestName`, `GuestIdentityId`, `StoneColor`, `TurnOrder`, `CapturedStones`, `Result`, `JoinedAt`, `LeftAt`. |
| `Moves` | `MoveId`, `GamePlayerId`, `TurnNumber`, `Row`, `Column`, `PlayedAt`, `CapturedStones`. |
| `GameEvents` | `GameEventId`, `GameId`, `GamePlayerId`, `EventType`, `Description`, `CreatedAt`. |

`Games` pierde `BoardStateJson`, `CurrentTurnGamePlayerId` y `TurnDeadlineUtc`, junto con las
restricciones e índices que los usaban. Conserva `StateVersion` como control de concurrencia
optimista de las escrituras SQL y como versión de las instantáneas enviadas al cliente.
`GameEvents` pierde `ActionId`, `ActionRequestJson`, `ActionResultJson` y su índice de acciones.
Las jugadas aceptadas se escriben únicamente en `Moves`; los eventos conservan transiciones
generales de inicio, finalización y vencimiento de turno.

`Moves.GamePlayerId` determina el participante, su color y la partida mediante `GamePlayers`.
No se duplican esos campos en la jugada. Los turnos omitidos por tiempo producen saltos en
`TurnNumber`; reconstruir la partida respeta esos saltos, las capturas y la repetición de
posiciones mediante el motor del juego. La reconstrucción requiere los movimientos completos
ordenados por turno y las mismas reglas. Un cambio futuro de reglas requiere versionarlas.
El historial de la interfaz conserva su resumen actual sin añadir una pantalla de repetición.

Las tablas `Users`, `Credentials`, `Profiles`, `UserPreferences`, `Sessions`,
`AuthenticationCodes`, `FriendRequests`, `Friendships`, `Rooms`, `Invitations`, `Reports` y
`Sanctions` mantienen su estructura. `Invitations` ya exige mediante
`CK_Invitations_Recipient` un destinatario usuario o correo según `InvitationType`; ese mapeo
se incorpora también a EF Core.

## Estado activo y cierre por reinicio

`GameSessionRegistry` posee el tablero, el historial de posiciones utilizado por las reglas,
los plazos, las pausas, los datos de reconexión y las respuestas de acciones del proceso.
El reloj enumera los vencimientos desde ese registro. La sesión de autenticación sigue en
`Sessions`; no debe confundirse con el estado de una partida ni con su conexión dúplex.

Las operaciones de una partida se serializan mediante `GameCoordinator`. Cada colocación se
confirma en una transacción de EF junto con las capturas y el resultado que corresponda.
Solo después de guardar se actualiza el registro activo y se publican las instantáneas.
Un fallo de escritura conserva el tablero anterior y permite reintentar la misma acción.

Se retienen como máximo 1024 recibos por partida, identificados por participante y `ActionId`.
Dentro de esa ventana se devuelve la respuesta original o `ActionConflict` si cambió el
contenido. Al alcanzar el límite se retira el recibo más antiguo. Una jugada aceptada cuyo
recibo ya salió de la ventana lleva una versión anterior y no puede volver a colocarse.
La recuperación exacta de respuestas antiguas o posteriores a un reinicio no está garantizada.
Los estados terminales y sus recibos se retiran tras una hora sin actividad; los estados
activos se mantienen hasta su finalización. No se establece un máximo global de partidas.

Antes de aceptar conexiones, el servicio de reloj cierra las partidas activas sin estado del
proceso: `Cancelled`, motivo `ServerRestart`, participantes `Abandoned` y ningún ganador.
Conserva los movimientos y capturas ya confirmados. Nunca reanuda una partida perdida a partir
de esos movimientos; estos permiten consultar su tablero registrado posteriormente.
El diseño requiere un único proceso servidor propietario de las partidas.

Al volver al lobby desde una partida terminada, el cliente utiliza `GameplaySession.CloseAsync`
antes de cancelar el token de la pantalla. Se rechazan nuevos envíos, se esperan los ya
admitidos y se completa el envío gRPC con `RequestStream.CompleteAsync()`. El servidor reconoce
el fin del stream de entrada, termina de enviar los eventos en cola y cierra la respuesta.
El cliente espera esa respuesta final antes de liberar la llamada y navegar al lobby.

El cierre tiene un plazo máximo de tres segundos. Si la red no responde, o se cancela la
salida, se interrumpe el transporte y se liberan los recursos sin bloquear la interfaz ni
reconectar la sesión que ya se está cerrando. El cierre ordenado de una partida terminal se
registra como `Information` con el mensaje `Completed game stream closed gracefully`.
Las interrupciones inesperadas conservan sus advertencias. No se modifican el resultado
de la partida, los contratos Protobuf ni el esquema SQL para implementar este cierre.

## Normalización y asuntos pendientes

- `GamePlayers.CapturedStones` es un total materializado de las capturas de `Moves`, mantenido
  en la misma escritura. Se conserva para consultas directas. La redundancia entre relaciones
  no demuestra por sí sola una violación formal de 3FN.
- `GamePlayers.Result` conserva el resultado histórico por participante. No se añade otra
  columna de ganador que repita ese hecho.
- Si `GuestIdentityId` determina siempre `GuestName`, repetir el nombre en varias
  participaciones requiere separar la identidad invitada o justificar la excepción.
- En eventos con participante, `GamePlayerId` determina `GameId`; esa duplicación permanece
  en la estructura histórica de `GameEvents`, protegida por una FK compuesta. La escritura
  nueva de eventos generales no utiliza participante. Resolver el diseño completo queda pendiente.
- `TurnOrder` es redundante si negras siempre empiezan; no se clasifica automáticamente como
  violación de 3FN porque participa en claves alternativas junto con `GameId`.
- `Sanctions.TriggeringReportId` es una clave alternativa única. La repetición del destinatario
  no basta para declarar incumplimiento de 3FN. Falta exigir desde SQL que `Sanctions.UserId`
  coincida con el usuario reportado, aunque el servicio lo asigna correctamente.
- El servicio comprueba que el autor y destinatario de un reporte participaron en la partida;
  las FKs actuales no garantizan esa membresía por sí solas.
- El flujo vigente de invitaciones vive en memoria. Si la tabla `Invitations` se utilizara
  exclusivamente con el creador como remitente, revisar la dependencia `RoomId -> SenderId`.
- `GameTimeSeconds` se conserva por compatibilidad histórica; el reloj usa `TurnTimeSeconds`.
- La unicidad actual de `Moves` es por participante y turno. La exclusión de dos jugadas del
  mismo turno de partida se garantiza por el coordinador y la versión SQL. Una escritura
  directa fuera del servidor necesita respetar esa regla.

Estas observaciones distinguen dependencias funcionales, datos derivados y reglas de
integridad. Una excepción justificada no convierte una tabla en 3FN.

## Instalación nueva

`Create-AtariGo.sql` crea directamente el esquema final, sin estructuras transitorias.
Se usa únicamente al instalar desde cero. Crea `AtariGoDB`, las tablas actuales, sus índices
y restricciones; no crea cuentas de jugador ni contraseñas SQL.
No ejecutar este archivo sobre la BD que se quiere actualizar.

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\scripts\database\Create-AtariGo.sql
```

Los scripts versionados son la referencia de esta implementación. Los archivos originales
externos al repositorio no se modifican. La aplicación no ejecuta migraciones automáticamente.

## Verificación

```powershell
dotnet build .\AtariGo.slnx
dotnet test .\AtariGo.slnx --no-build
```

Las pruebas `SqlServer` usan Windows y SQL Server LocalDB `(localdb)\MSSQLLocalDB`.
Crean bases temporales `AtariGoGameplayTest_...` y las eliminan al terminar, sin utilizar
la conexión privada de la aplicación. Comprueban instalación nueva, sustitución de partidas
de prueba, reconstrucción de sus capturas, repetición del script y rollback ante participantes
incompletos. Las pruebas de persistencia restantes usan SQLite aislado y las de dúplex HTTP/2
usan loopback. Estas comprobaciones no sustituyen una prueba LAN entre computadoras.
