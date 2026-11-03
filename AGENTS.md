# Instrucciones para trabajar en Atari Go

Estas instrucciones se aplican a todo este repositorio.

## Estándar obligatorio

Antes de modificar código, lee [CODING_STANDARD.md](CODING_STANDARD.md) completo y aplica
sus reglas vigentes. Consulta también cualquier `AGENTS.md` específico de la carpeta afectada.
El Markdown es la fuente vigente del estándar; no dependas del Word que pueda existir fuera
del repositorio. El código antiguo no autoriza repetir incumplimientos.

El estándar define los detalles de nomenclatura, formato, organización, límites de métodos,
excepciones, comentarios y pruebas. No los sustituyas por tus convenciones predeterminadas.
Las instrucciones explícitas del usuario tienen precedencia; explica las contradicciones
relevantes y no inventes restricciones o excepciones silenciosas.

## Explicaciones al responsable del proyecto

Explica en español qué cambiaste, por qué y cómo funciona. Usa lenguaje claro, enlaces a los
archivos relevantes y el detalle necesario para que el usuario pueda comprender y mantener
el código. El código y los comentarios se escriben en inglés según el estándar.

Informa cómo verificaste el cambio y qué no pudiste comprobar. No afirmes que la conexión
entre computadoras, la presencia o el lobby funcionan solo porque compila el proyecto.

## Evolución del estándar

Sugiere mejoras cuando encuentres vacíos, contradicciones o necesidades nuevas. Si existe,
consulta el registro local de propuestas para evitar duplicar pendientes. Puedes registrar
propuestas con problema, regla sugerida e impacto; son pendientes, no obligaciones.
Incorpora reglas nuevas al estándar cuando el usuario las adopte o solicite modificarlas.
No detengas trabajo ya autorizado por decisiones rutinarias ni conviertas cada sugerencia
en una solicitud de permiso.

## Auditorías y ambigüedades

Distingue una lambda de un miembro con cuerpo de expresión o un brazo de `switch`, aunque
usen `=>`. Aplica los criterios de 3.14 y las convenciones de nombres de la sección 10.
Consulta los informes locales de auditoría y cambios si están disponibles. No presentes
estimaciones sintácticas como métricas semánticas exactas ni una regla nueva como si ya
hubiera sido obligatoria antes de su adopción.

## Informes locales del chat

`CODING_STANDARD.md` y `AGENTS.md` son documentación compartida del repositorio. Guarda los
nuevos informes de trabajo del chat en `docs/local/`, carpeta excluida por `.gitignore`.
Créala cuando necesites escribir un informe. No generes un archivo Markdown adicional si
la explicación en el chat es suficiente, salvo que el usuario solicite el archivo.

Los registros locales pueden llamarse `docs/local/PROPUESTAS_ESTANDAR.md`,
`docs/local/AUDITORIA_CODIGO.md` y `docs/local/CAMBIOS_ESTANDAR.md`. Los informes anteriores
con esos nombres en la raíz también son opcionales y están excluidos de Git. Consúltalos
si existen; no los exijas ni reconstruyas automáticamente al clonar el repositorio.

Las reglas adoptadas siempre deben quedar completas en `CODING_STANDARD.md` para que
puedan aplicarse sin los informes locales. No añadas enlaces obligatorios desde archivos
compartidos a documentos ignorados, no uses `git add -f` para publicar estos informes y no
los incluyas en un commit salvo una instrucción explícita que cambie esta política.

## Presencia y dúplex

La decisión vigente del proyecto es sustituir heartbeat por comunicación dúplex. En la
aplicación de esta auditoría se excluyen las clases de heartbeat/presencia y sus pruebas,
`MainViewModel`, el contrato `session.proto` y el mapeo de sesión. No invertir trabajo en
refactorizarlos antes de la sustitución. Una solicitud posterior para implementar dúplex
puede modificar ese alcance. Lobby y partidas se dejaron expresamente para esa etapa.

## Cuidado del repositorio y verificación

Conserva los cambios locales del usuario y evita mezclar cambios ajenos a la tarea. Modifica
archivos generados mediante su fuente o generador. No incorpores secretos, claves privadas
ni configuración local. No hagas commit ni push salvo que el usuario lo solicite para la tarea vigente. Si lo pide,
los mensajes deben seguir la sección 8.1: frase en inglés con verbo inicial y sin prefijos
como `feat:`, `fix:`, `refactor:`, `docs:` ni variantes con alcance. Ejemplo:
`Update coding standard and document repository audit`. No reescribas commits publicados
para renombrarlos sin una solicitud específica.

Para cambios de código, ejecuta las comprobaciones pertinentes desde esta carpeta:

```powershell
dotnet build .\AtariGo.slnx
dotnet test .\AtariGo.slnx --no-build
```

El cliente y sus pruebas requieren Windows por WPF. Si no puedes ejecutar una comprobación,
explica la limitación. Para cambios solo documentales, revisa contenido, enlaces y diff;
no hace falta ejecutar la aplicación.
