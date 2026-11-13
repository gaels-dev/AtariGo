# Base de datos para la jugabilidad

La jugabilidad amplía las tablas existentes `Games` y `GameEvents`. No requiere tablas
adicionales. `Moves` conserva las jugadas y `GamePlayers` las capturas y resultados por jugador.
El código usa exclusivamente ese esquema; no hay selección de diseños antiguos al arrancar.

## Corrección de una BD que ya ejecutó la actualización anterior

Usar **un solo archivo: `Update-Gameplay.sql` de esta versión**. Su contenido sustituye al
script anterior con el mismo nombre. No volver a ejecutar una copia antigua del archivo.

1. Detener el servidor Atari Go y respaldar la BD.
2. Abrir `Update-Gameplay.sql`, seleccionar la BD de Atari Go y ejecutar el archivo completo.
3. Ejecutar la comprobación del esquema y arrancar la versión actualizada del servidor.

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d AtariGoDB -i .\scripts\database\Update-Gameplay.sql
dotnet run --project .\AtariGo.Server -- --check-database
```

Adaptar servidor y nombre de BD a la instalación. También se puede ejecutar el archivo
completo desde SQL Server Management Studio, sin instalar `sqlcmd`.
Se requiere una cuenta con permisos para modificar el esquema. Los permisos de la aplicación
continúan sobre las tablas existentes: en particular, necesita consultar e insertar `GameEvents`
y consultar/actualizar `Games`.

El archivo realiza todo en una transacción:

- Añade las columnas e índices necesarios a las tablas existentes.
- Traslada el tablero completo, historial de posiciones, versión, estado y vencimiento
  desde la estructura anterior. No reinicia los turnos ni los relojes de partidas recuperables.
- Traslada las solicitudes y respuestas guardadas a `GameEvents`, asociándolas al
  `GamePlayerId` que ya representa al participante. No necesita conservar otro identificador
  de jugador en texto.
- Reutiliza el evento original de una jugada o rendición cuando existe; conserva su ID y fecha.
  Las respuestas sin evento previo se incorporan como eventos. Los rechazos anteriores no
  tenían fecha propia guardada: esos eventos reciben la fecha de la migración.
- Comprueba los datos mediante restricciones y un índice único por partida, jugador y acción.
- Elimina `GameProgresses` y `GameActionReceipts`, incluidos sus índices y restricciones.

Cualquier error revierte todo, incluidas las modificaciones de esquema: no se eliminan las
fuentes si falla una copia o validación. Se puede volver a ejecutar el archivo después de
una migración correcta sin duplicar acciones ni reiniciar partidas.
Los nombres de las estructuras anteriores solo se conservan en este migrador y en las pruebas
que reproducen su instalación para comprobar que se eliminan correctamente.

El mismo archivo también admite una BD anterior a la jugabilidad. Solo prepara automáticamente
partidas activas sin movimientos, con 120 segundos para conectar a ambos jugadores. Si hay
movimientos sin un tablero recuperable, devuelve el error 51001 y revierte la operación.
No reconstruye por suposición una partida antigua. Los resultados históricos se conservan.
Los errores 51002 a 51006 indican datos de origen inválidos, identidades sin correspondencia
u otro conflicto de migración; tampoco modifican la BD parcialmente.

## Columnas del esquema final

| Tabla | Columna nueva | Finalidad |
| --- | --- | --- |
| `Games` | `BoardStateJson` (`nvarchar(max)`, nullable) | Tablero, historial de posiciones y datos del motor. |
| `Games` | `StateVersion` (`bigint`, valor inicial 0) | Versión de concurrencia del estado confirmado. |
| `Games` | `TurnDeadlineUtc` (`datetime2(3)`, nullable) | Plazo oficial del turno o de conexión inicial. |
| `GameEvents` | `ActionId` (`varchar(64)`, nullable) | Identificador de acción para reconocer reintentos. |
| `GameEvents` | `ActionRequestJson` (`nvarchar(max)`, nullable) | Solicitud original para detectar reutilización con otros datos. |
| `GameEvents` | `ActionResultJson` (`nvarchar(max)`, nullable) | Respuesta original para recuperar una confirmación perdida. |

Se reutilizan `Games.State`, `Games.EndReason`, `Games.CurrentTurnGamePlayerId`,
`GameEvents.GamePlayerId` y las relaciones existentes. No se almacena otra bandera terminal
ni un segundo estado/plazo dentro del JSON del tablero. El índice
`IX_Games_State_TurnDeadline` permite buscar vencimientos sin recorrer documentos JSON.
`UX_GameEvents_Player_Action` impide duplicar una acción; solo afecta a eventos con `ActionId`.
Los eventos generales, sin solicitud del jugador, conservan sus columnas de acción en NULL.

El JSON del tablero corresponde a `GameState`, un objeto de dominio, no a un mensaje gRPC.
Evita agregar tablas para cada intersección o posición histórica. La configuración de EF se
mantiene en `GameEntityConfiguration` y `GameEventEntityConfiguration`; no hay entidades,
DbSets ni mapeos de tablas auxiliares.

`GameTimeSeconds` se conserva como compatibilidad histórica, sin limitar la partida.
`TurnTimeSeconds` debe ser positivo. Cada jugador recibe ese tiempo completo en cada turno;
el vencimiento cambia el turno sin declarar un perdedor.

## Instalación nueva

`Create-AtariGo.sql` crea directamente el esquema final, sin estructuras transitorias.
Se usa únicamente al instalar desde cero. Crea `AtariGoDB`, las tablas base, sus índices y
restricciones, y el rol `Player` de la tabla `Roles`; no crea cuentas ni contraseñas SQL.
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
la conexión privada de la aplicación. Comprueban instalación nueva, repetición del script,
actualización desde ambos esquemas anteriores, conservación de estados y respuestas,
reutilización de eventos originales y rollback completo ante datos que no se puedan migrar.
Las otras pruebas de persistencia usan SQLite aislado y las de dúplex HTTP/2 usan loopback.
Estas comprobaciones no sustituyen una prueba LAN entre computadoras.
