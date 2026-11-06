# PROPUESTA DE CONTRATOS GRPC

Tecnologías para la construcción de software

20 DE OCTUBRE DE 2026

MORALES BAIZABAL EMILIANO – AMORES RIVAS GAEL SAMEI

## 1. Propósito, fuentes y alcance

Definir el inventario completo de contratos remotos requerido por el alcance funcional conocido de Atari Go: cuentas, seguridad, perfiles, salas, emparejamiento, reglas y estado de partidas, relaciones sociales, chat, clasificaciones, reportes y moderación. La propuesta es exhaustiva respecto de los 29 casos de uso y las pantallas/modelos identificados, no una afirmación de que todas las funciones ya existen.

La matriz distingue contratos existentes de nuevos, delimita las responsabilidades y establece nombres consistentes para los archivos .proto, las interfaces de aplicación del servidor y los adaptadores gRPC. No incluye código fuente.

| Alcance del documento | Resultado |
| --- | --- |
| Servicios propuestos | 25 servicios organizados por área funcional. |
| Operaciones remotas | 78 operaciones enumeradas en el resumen de contratos. |
| Trazabilidad | 29 casos de uso originales cubiertos en una matriz CU → servicios. |
| Implementación actual | 5 archivos .proto y 9 RPC declaradas en el inventario de referencia (033b4fb). |
| Regla fundamental | Un contrato no es una clase de Entity Framework ni una ViewModel; especifica un límite de comunicación. |

## 2. Resumen de contratos y operaciones

| Servicio .proto | Operaciones |
| --- | --- |
| authentication.proto | Login, StartGuestSession, RefreshSession, ValidateSession |
| registration.proto | RegisterAccount |
| password_recovery.proto | RequestPasswordRecovery, VerifyRecoveryCode, ResetPassword |
| credentials.proto | ChangePassword |
| profile.proto | GetAuthenticatedUserProfile, UpdateAuthenticatedUserProfile, ChangeAuthenticatedUserName, ChangeAuthenticatedUserEmail |
| preferences.proto | GetAuthenticatedUserPreferences, UpdateAuthenticatedUserPreferences |
| friends.proto | SearchUsers, SendFriendRequest, GetIncomingFriendRequests, RespondToFriendRequest, GetPublicProfileByUserId, ListFriendRequestsBySender, CancelFriendRequest |
| presence.proto | GetPresenceByUserIds, WatchPresenceByUserIds |
| friendships.proto | ListFriendshipsByUser, RemoveFriendship |
| rooms.proto | CreatePrivateRoom, GetRoomByCode, ListRoomsByVisibility, UpdateRoomConfiguration, JoinRoom, LeaveRoom, CloseRoom, WatchRoom |
| room_invitations.proto | SendRoomInvitation, ListRoomInvitationsByRecipient, RespondRoomInvitation, CancelRoomInvitation |
| matchmaking.proto | JoinMatchmakingQueue, CancelMatchmaking, WatchMatchmaking |
| game_lifecycle.proto | StartGame, GetGameSummaryByGameId, LeaveGame, SurrenderGame, WatchGameLifecycle |
| game_moves.proto | SubmitMove |
| game_state.proto | GetGameStateByGameId, WatchGameStateByGameId, GetGameEventsByGameId |
| game_history.proto | ListGamesByPlayer, GetGameHistoryByGameId, GetGameReplayByGameId |
| player_statistics.proto | GetPlayerStatistics, GetPublicStatisticsByUserId |
| leaderboard.proto | GetTopPlayers |
| game_chat.proto | SendGameMessage, WatchGameMessages |
| reports.proto | SubmitPlayerReport, ListReportsByReporter, GetReportStatusById |
| report_review.proto | ListReportsByReviewStatus, GetReportById, ReviewReport, CloseReport |
| sanctions.proto | ApplySanction, ListSanctionsByUser, GetSanctionById, RevokeSanction |
| user_administration.proto | ListUsersByRestrictionStatus, GetUserAdministrationByUserId, SuspendUser, RestoreUser |
| notifications.proto | ListNotificationsByRecipient, WatchNotificationsByRecipient, MarkNotificationAsRead |
| session.proto | Connect, Logout |

## 3. Convención definitiva de nombres y capas

| Nivel | Convención | Responsabilidad |
| --- | --- | --- |
| .proto | snake_case.proto; un servicio de negocio por archivo | Define mensajes, enumeraciones y métodos RPC de un área funcional. |
| Servicio Protobuf | PascalCase, sin sufijo Service en nombre del servicio | Entidad RPC lógica que agrupa las operaciones de una sola responsabilidad. |
| Interfaz de aplicación servidor | I&lt;Servicio&gt;Service | Expone casos de uso, sin referencias a ServerCallContext ni mensajes de transporte. |
| Implementación de aplicación | &lt;Servicio&gt;Service | Contiene reglas de aplicación, orquestación y acceso a dependencias. |
| Adaptador de transporte servidor | &lt;Servicio&gt;GrpcService | Traduce entre request/response RPC y servicio de aplicación. |
| Cliente abstracto WPF | I&lt;Servicio&gt;Client | API cliente que reciben los ViewModels mediante inyección de dependencias. |
| Adaptador cliente concreto | Grpc&lt;Servicio&gt;Client | Traduce comandos del cliente a RPC; gestiona plazos y errores de transporte. |
| Persistencia | AtariGoDbContext / repositorios cuando haga falta | Entidades EF Core internas: nunca exponerse como contratos externos. |

## 4. Matriz maestra: servicios, interfaces, clases y operaciones

Las cinco columnas siguientes corresponden exactamente a la matriz solicitada. Una fila por operación evita confundir entradas de métodos distintos. Cada grupo contiene un único servicio con responsabilidad única y muestra su estado de implementación.

### 4.1  Authentication

Responsabilidad: Identidad y acceso: iniciar, renovar y validar el acceso, incluido acceso invitado sin cuenta.

Cobertura: CU-03; acceso invitado como flujo, no CU independiente

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| authentication.proto | IAuthenticationService | AuthenticationGrpcService | Login | Identifier, Password |
|  |  |  | StartGuestSession | GuestName, RequestedScope |
|  |  |  | RefreshSession | RefreshToken |
|  |  |  | ValidateSession | SessionToken en metadata |

### 4.2  Registration

Responsabilidad: Alta de nueva cuenta.

Cobertura: CU-01

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| registration.proto | IRegistrationService | RegistrationGrpcService | RegisterAccount | RegisterAccountRequest request<br>Contiene: UserName, Email,<br>Password, PasswordConfirmation,<br>ConfirmationText |

### 4.3  PasswordRecovery

Responsabilidad: Recuperación de acceso por código de un solo uso, diferenciada del cambio autenticado.

Cobertura: CU-04

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| password_recovery.proto | IPasswordRecoveryService | PasswordRecoveryGrpcService | RequestPasswordRecovery | Identifier |
|  |  |  | VerifyRecoveryCode | RecoveryId, VerificationCode |
|  |  |  | ResetPassword | RecoveryProof, NewPassword, Confirmation |

### 4.4  Credentials

Responsabilidad: Cambio autenticado de contraseña y seguridad de credenciales.

Cobertura: CU-05

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| credentials.proto | ICredentialsService | CredentialsGrpcService | ChangePassword | CurrentPassword, NewPassword,<br>Confirmation |

### 4.5  Profile

Responsabilidad: Consultar y modificar el perfil del propietario; la identidad de cuenta no se altera sin validación.

Cobertura: CU-06

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| profile.proto | IProfileService | ProfileGrpcService | GetAuthenticatedUserProfile | Sin parámetros de negocio. |
|  |  |  | UpdateAuthenticatedUserProfile | ProfileChanges { AvatarId, Description}, ExpectedVersion |
|  |  |  | ChangeAuthenticatedUserName | NewUserName, ExpectedVersion |
|  |  |  | ChangeAuthenticatedUserEmail | NewEmail, ConfirmationProof |

### 4.6  Preferences

Responsabilidad: Preferencias persistentes por usuario; la localización WPF sigue usando archivos RESX.

Cobertura: CU-29 (persistencia opcional)

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| preferences.proto | IPreferencesService | PreferencesGrpcService | GetAuthenticatedUserPreferences | usuario en metadatos |
|  |  |  | UpdateAuthenticatedUserPreferences | LanguageCode, NotificationEnabled |

### 4.7  FriendDirectory

Responsabilidad: Buscar jugadores, consultar perfiles públicos y gestionar solicitudes de amistad.

Cobertura: CU-07, CU-13

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| friends.proto | IFriendService | FriendDirectoryGrpcService | SearchUsers | Query |
|  |  |  | SendFriendRequest | RecipientUserId |
|  |  |  | GetIncomingFriendRequests | Solicitud vacía |
|  |  |  | RespondToFriendRequest | RequestId, Accept |
|  |  |  | GetPublicProfileByUserId (propuesta) | UserId |
|  |  |  | ListFriendRequestsBySender (propuesta) | Filter, PageRequest { PageSize, PageToken } |
|  |  |  | CancelFriendRequest (propuesta) | RequestId |

### 4.8  Presence

Responsabilidad: Mostrar disponibilidad de amigos y jugadores, sin gestionar solicitudes de amistad.

Cobertura: CU-14; apoyo a CU-12

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| presence.proto | IPresenceService | PresenceGrpcService | GetPresenceByUserIds | UserIds[] |
|  |  |  | WatchPresenceByUserIds | UserIds[] |

### 4.9  Friendships

Responsabilidad: Administración de relaciones de amistad ya aceptadas.

Cobertura: CU-14, CU-15

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| friendships.proto | IFriendshipsService | FriendshipsGrpcService | ListFriendshipsByUser | PageRequest { PageSize, PageToken} |
|  |  |  | RemoveFriendship | FriendUserId |

### 4.10  Rooms

Responsabilidad: Existencia, visibilidad, configuración y membresía de salas antes de jugar.

Cobertura: CU-08, CU-09, CU-10, CU-11; abandonar sala

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| rooms.proto | IRoomsService | RoomsGrpcService | CreatePrivateRoom | GameConfiguration { CaptureGoal,<br>GameTimeSeconds,<br>TurnTimeSeconds } |
|  |  |  | GetRoomByCode | InvitationCode |
|  |  |  | ListRoomsByVisibility | ListRoomsByVisibilityQuery query<br>Contiene: Visibility, Availability,<br>PageRequest { PageSize, PageToken} |
|  |  |  | UpdateRoomConfiguration | UpdateRoomConfigurationCommand command<br>Contiene: RoomId, GameConfiguration<br>{ CaptureGoal, GameTimeSeconds,<br>TurnTimeSeconds }, ExpectedVersion |
|  |  |  | JoinRoom | RoomId, InvitationCode |
|  |  |  | LeaveRoom | RoomId |
|  |  |  | CloseRoom | RoomId, ExpectedVersion |
|  |  |  | WatchRoom | RoomId, FromVersion |

La primera entrega implementa `CreatePrivateRoom`, `JoinRoom`, `LeaveRoom` y
`WatchRoom` para jugadores registrados. Una cuenta puede alojar una sala privada
a la vez. Repetir la creación con la misma configuración devuelve la misma sala.
Para incorporar a un participante nuevo se requiere el código. El identificador
por sí solo permite recuperar la sala a un miembro existente.

`WatchRoom` entrega snapshots completos y versionados a los miembros. Si la versión
indicada coincide con la actual, espera un cambio. Si difiere o se omite, entrega
el estado actual. Los rechazos se comunican mediante códigos de estado gRPC.
En esta entrega la sala en espera reside en memoria y vence una hora después de
crearse. El inicio persistente de partida se incorpora con `GameLifecycle`.

### 4.11  RoomInvitations

Responsabilidad: Invitaciones a salas (amistad, enlace o correo) y respuesta del invitado.

Cobertura: CU-12

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| room_invitations.proto | IRoomInvitationsService | RoomInvitationsGrpcService | SendRoomInvitation | RoomId, Recipient, InvitationType |
|  |  |  | ListRoomInvitationsByRecipient | Filter, PageRequest { PageSize, PageToken } |
|  |  |  | RespondRoomInvitation | InvitationId, Decision |
|  |  |  | CancelRoomInvitation | InvitationId |

### 4.12  Matchmaking

Responsabilidad: Emparejamiento público, sin crear reglas de tablero ni gestionar salas privadas.

Cobertura: CU-16

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| matchmaking.proto | IMatchmakingService | MatchmakingGrpcService | JoinMatchmakingQueue | MatchmakingCriteria { CaptureGoal,<br>PreferredConfiguration |
|  |  |  | CancelMatchmaking | TicketId |
|  |  |  | WatchMatchmaking | TicketId |

### 4.13  GameLifecycle

Responsabilidad: Inicio, abandono, rendición y cierre administrativo/automático de una partida.

Cobertura: CU-16, CU-18, CU-19; iniciar desde sala

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| game_lifecycle.proto | IGameLifecycleService | GameLifecycleGrpcService | StartGame | RoomId, ExpectedRoomVersion |
|  |  |  | GetGameSummaryByGameId | GameId |
|  |  |  | LeaveGame | GameId, Reason |
|  |  |  | SurrenderGame | GameId, ExpectedStateVersion |
|  |  |  | WatchGameLifecycle | GameId, FromVersion |

### 4.14  GameMoves

Responsabilidad: Aceptación o rechazo autoritativo de jugadas según las reglas de Atari Go.

Cobertura: Jugada como acción dentro de CU-17 / participación en partida

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| game_moves.proto | IGameMovesService | GameMovesGrpcService | SubmitMove | SubmitMoveRequest request<br>Contiene: GameId, Row, Column,<br>ExpectedVersion, ActionId |

### 4.15  GameState

Responsabilidad: Lectura y difusión del tablero confirmado, turnos, grupos capturados y resultado.

Cobertura: CU-17, CU-19

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| game_state.proto | IGameStateService | GameStateGrpcService | GetGameStateByGameId | GameId |
|  |  |  | WatchGameStateByGameId | GameId, FromVersion |
|  |  |  | GetGameEventsByGameId | GameId, FromVersion |

### 4.16  GameHistory

Responsabilidad: Consulta de partidas terminadas y reproducción de movimientos registrados.

Cobertura: CU-20

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| game_history.proto | IGameHistoryService | GameHistoryGrpcService | ListGamesByPlayer | StateFilter, PageRequest { PageSize, PageToken } |
|  |  |  | GetGameHistoryByGameId | GameId |
|  |  |  | GetGameReplayByGameId | GameId |

### 4.17  PlayerStatistics

Responsabilidad: Agregación de resultados por jugador para estadísticas y perfil.

Cobertura: CU-21

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| player_statistics.proto | IPlayerStatisticsService | PlayerStatisticsGrpcService | GetPlayerStatistics | TimeRange (opcional) |
|  |  |  | GetPublicStatisticsByUserId | UserId, TimeRange (opcional) |

### 4.18  Leaderboard

Responsabilidad: Clasificación ordenada, separada de estadísticas individuales.

Cobertura: CU-22

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| leaderboard.proto | ILeaderboardService | LeaderboardGrpcService | GetTopPlayers | Limit |

### 4.19  GameChat

Responsabilidad: Mensajes de chat asociados a una partida, sin alterar turnos ni movimientos.

Cobertura: Chat incluido en tablero (no CU independiente)

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| game_chat.proto | IGameChatService | GameChatGrpcService | SendGameMessage | GameMessage {<br>GameId, Text, ActionId} |
|  |  |  | WatchGameMessages | GameId, AfterMessageId |

### 4.20  Reports

Responsabilidad: Presentación y consulta del estado de reportes por jugadores.

Cobertura: CU-23

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| reports.proto | IReportsService | ReportsGrpcService | SubmitPlayerReport | SubmitPlayerReportRequest request<br>Contiene: ReportedUserId, GameId<br>(opcional), Reason, Description |
|  |  |  | ListReportsByReporter | PageRequest { PageSize, PageToken} |
|  |  |  | GetReportStatusById | ReportId |

### 4.21  ReportReview

Responsabilidad: Revisión administrativa de reportes sin aplicar directamente sanciones.

Cobertura: CU-24

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| report_review.proto | IReportReviewService | ReportReviewGrpcService | ListReportsByReviewStatus | StateFilter, PageRequest { PageSize, PageToken } |
|  |  |  | GetReportById | ReportId |
|  |  |  | ReviewReport | ReviewReportRequest request<br>Contiene: ReportId, Resolution,<br>ReviewerNotes, ExpectedVersion |
|  |  |  | CloseReport | ReportId, ClosureReason |

### 4.22  Sanctions

Responsabilidad: Registro y levantamiento de sanciones justificadas, vinculables a reportes.

Cobertura: CU-25, CU-27

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| sanctions.proto | ISanctionsService | SanctionsGrpcService | ApplySanction | ApplySanctionRequest request<br>Contiene: UserId, ReportId (opcional),<br>Type, Reason, EndsAt (opcional) |
|  |  |  | ListSanctionsByUser | ListSanctionsByUserQuery query<br>Contiene: UserId (opcional),<br>StateFilter, PageRequest { PageSize,<br>PageToken |
|  |  |  | GetSanctionById | SanctionId |
|  |  |  | RevokeSanction | SanctionId, Justification |

### 4.23  UserAdministration

Responsabilidad: Consulta y modificación de estado administrativo de cuentas, separado del historial de sanciones.

Cobertura: CU-26, CU-28

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| user_administration.proto | IUserAdministrationService | UserAdministrationGrpcService | ListUsersByRestrictionStatus | RestrictionFilter, PageRequest {<br>PageSize, PageToken } |
|  |  |  | GetUserAdministrationByUserId | UserId |
|  |  |  | SuspendUser | UserId, Reason, Until (opcional) |
|  |  |  | RestoreUser | UserId, Justification |

### 4.24  Notifications

Responsabilidad: Avisos en tiempo real para invitaciones, solicitudes, emparejamiento y sanciones.

Cobertura: Apoya CU-12, CU-13, CU-16, CU-25

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| notifications.proto | INotificationsService | NotificationsGrpcService | ListNotificationsByRecipien | UnreadOnly, PageRequest { PageSize,<br>PageToken } |
|  |  |  | WatchNotificationsByRecipient | AfterNotificationId |
|  |  |  | MarkNotificationAsRead | NotificationId |

### 4.25 Session

Responsabilidad: Mantener la conexión autenticada del jugador y comunicar cambios de presencia en tiempo real.

Cobertura: Sesión del jugador; presencia de amigos en el lobby; cierre de sesión.

| Servicio .proto | Interfaz de aplicación | Clase gRPC | Operaciones | Parámetros definidos |
| --- | --- | --- | --- | --- |
| session.proto | ISessionService | SessionGrpcService | Connect<br>Logout | Connect: SessionClientEvent<br>(stream; ClientConnected);<br>SessionServerEvent (stream;<br>FriendPresenceSnapshot y<br>FriendPresenceChanged); token<br>de sesión en metadata.<br>Logout: LogoutRequest vacío;<br>token en metadata. |

## 5. Matriz de funcionalidades y contratos

Se identifican las funcionalidades, sus servicios principales y los servicios de apoyo, sin fusionar responsabilidades distintas.

| Funcionalidad | Servicios responsables |
| --- | --- |
| Registrar cuenta | registration.proto |
| Iniciar sesión | authentication.proto |
| Recuperar contraseña | password_recovery.proto |
| Cambiar contraseña | credentials.proto |
| Personalizar perfil | profile.proto; preferences.proto |
| Buscar jugadores | friends.proto |
| Consultar salas | rooms.proto |
| Crear sala privada | rooms.proto |
| Configurar partida privada | rooms.proto |
| Unirse a sala privada | rooms.proto |
| Invitar amigo a partida privada | room_invitations.proto; friendships.proto; notifications.proto |
| Gestionar solicitudes de amistad | friends.proto; notifications.proto |
| Consultar lista de amigos | friendships.proto; presence.proto |
| Eliminar amigo | friendships.proto |
| Participar en partida pública | matchmaking.proto; game_lifecycle.proto |
| Consultar estado de partida | game_state.proto; game_moves.proto |
| Abandonar partida | game_lifecycle.proto |
| Consultar resultado de partida | game_lifecycle.proto; game_state.proto |
| Consultar historial de partidas | game_history.proto |
| Consultar estadísticas | player_statistics.proto |
| Consultar marcadores | leaderboard.proto |
| Reportar jugador | reports.proto |
| Consultar reportes | report_review.proto |
| Aplicar sanción | sanctions.proto |
| Suspender jugador | user_administration.proto; sanctions.proto |
| Consultar jugadores sancionados | sanctions.proto |
| Gestionar jugadores suspendidos o bloqueados | user_administration.proto |
| Gestionar idioma y opciones de juego | preferences.proto |
| Mantener sesión y presencia de amigos | session.proto |

## 6. Contratos internos que NO requieren archivo .proto

Los siguientes límites son importantes para una arquitectura completa, pero no deben convertirse en RPC por defecto. Exponerlos en la red aumentaría acoplamiento y superficie de ataque.

| Interfaz interna propuesta o existente | Responsabilidad | Límite / razón para NO tener .proto |
| --- | --- | --- |
| IGameRulesEngine (propuesta) | Legalidad del turno, grupos, libertades, suicidio según reglas, capturas, victoria y configuración. | Lógica pura de dominio; usada por GameMoves y GameLifecycle. |
| IAuthenticationService (existe) | Inicio de sesión y validación de credenciales en Application. | Se publica únicamente mediante el adaptador AuthenticationGrpcService. |
| IRegistrationValidator (existe) | Validar información de una solicitud de alta. | Validación local del caso de uso, no operación de red. |
| ISessionTokenProvider (existe) | Emitir token aleatorio y su hash para almacenamiento. | Servicio de seguridad dentro del servidor. |
| IEmailDeliveryService (propuesta) | Enviar confirmaciones, recuperación e invitaciones por correo. | Adaptador a proveedor externo; nunca se invoca directamente desde WPF. |
| INotificationPublisher (propuesta) | Publicar eventos de dominio a Notifications. | Infraestructura interna; la lectura de avisos sí se expone por gRPC. |
| AtariGoDbContext (existe) | Persistencia EF Core de entidades relacionales. | Dependencia interna del servidor; sin generación de contratos de BD. |
| INavigationService (existe) | Cambiar CurrentViewModel, abrir diálogos y ventanas. | Navegación local de escritorio; no usa la red. |
| ICommand, RelayCommand, AsyncRelayCommand (existen) | Acciones enlazadas a controles WPF. | Parte de MVVM; no forman parte de un .proto. |
