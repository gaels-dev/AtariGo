# Estándar de codificación y desarrollo de Atari Go

**Versión 1.5 **

- **Fecha:** 18 de noviembre de 2026.
- **Autores:** Morales Baizabal Emiliano y Amores Rivas Gael Sameí.
- **Contexto académico:** Tecnologías para la Construcción de Software, Ingeniería de Software, equipo 2.
- **Tecnologías:** C#, .NET 10, WPF, gRPC, Protobuf, EF Core y SQL Server.
- **Estado:** cerrado

Este documento reúne las convenciones de Atari Go y explica su aplicación a cliente,
servidor, lógica del juego y pruebas. Las reglas específicas prevalecen dentro de su alcance;
las propuestas pendientes se gestionan según 11.4.

## Índice

- [1 Introducción](#seccion-1)
  - [1.1 Propósito del Documento](#seccion-1-1)
  - [1.2 Alcance e interpretación de las reglas](#seccion-1-2)
  - [1.3 Entorno de Desarrollo](#seccion-1-3)
  - [1.4 Idioma y Localización](#seccion-1-4)
- [2 Convenciones de Nomenclatura](#seccion-2)
  - [2.1 Clases](#seccion-2-1)
  - [2.2 Estructuras](#seccion-2-2)
  - [2.3 Interfaces](#seccion-2-3)
  - [2.4 Métodos](#seccion-2-4)
  - [2.5 Variables locales](#seccion-2-5)
  - [2.6 Parámetros](#seccion-2-6)
  - [2.7 Campos Privados](#seccion-2-7)
  - [2.8 Campos Públicos](#seccion-2-8)
  - [2.9 Campos Protegidos](#seccion-2-9)
  - [2.10 Constantes](#seccion-2-10)
  - [2.11 Campos estáticos](#seccion-2-11)
  - [2.12 Enumeraciones](#seccion-2-12)
  - [2.13 Namespaces](#seccion-2-13)
  - [2.14 Organización de imports y miembros](#seccion-2-14)
  - [2.15 Eventos](#seccion-2-15)
  - [2.16 Propiedades](#seccion-2-16)
  - [2.17 Prefijos y Sufijos](#seccion-2-17)
  - [2.18 Clases de Prueba](#seccion-2-18)
  - [2.19 Métodos de Prueba](#seccion-2-19)
  - [2.20 Records, estructuras y tuplas](#seccion-2-20)
- [3 Estilo de Código y Formato](#seccion-3)
  - [3.1 Formato General](#seccion-3-1)
  - [3.2 Indentación](#seccion-3-2)
  - [3.3 Longitud de Línea](#seccion-3-3)
  - [3.4 Uso de Llaves](#seccion-3-4)
  - [3.5 Líneas en Blanco](#seccion-3-5)
  - [3.6 Espacios en Blanco](#seccion-3-6)
  - [3.7 Operadores](#seccion-3-7)
  - [3.8 Paréntesis](#seccion-3-8)
  - [3.9 Sentencias](#seccion-3-9)
  - [3.10 Uso de Var](#seccion-3-10)
  - [3.11 Números Mágicos](#seccion-3-11)
  - [3.12 Límite de Parámetros por Método](#seccion-3-12)
  - [3.13 Cláusulas de guarda y salidas anticipadas](#seccion-3-13)
  - [3.14 Lambdas y miembros con cuerpo de expresión](#seccion-3-14)
  - [3.15 Asincronía cancelación y plazos](#seccion-3-15)
  - [3.16 Condicionales ternarios](#seccion-3-16)
  - [3.17 Accesibilidad y puntos de extensión](#seccion-3-17)
  - [3.18 Propiedades, inicialización e inmutabilidad](#seccion-3-18)
  - [3.19 Estructuras de control y patrones](#seccion-3-19)
  - [3.20 LINQ y materialización de consultas](#seccion-3-20)
  - [3.21 Cadenas, cultura y unidades](#seccion-3-21)
- [4 Organización del Proyecto](#seccion-4)
  - [4.1 Una clase por archivo](#seccion-4-1)
  - [4.2 Responsabilidad única](#seccion-4-2)
  - [4.3 Complejidad Ciclomática y Tamaño de Métodos](#seccion-4-3)
  - [4.4 Responsabilidades del cliente WPF](#seccion-4-4)
  - [4.5 Configuración de persistencia](#seccion-4-5)
  - [4.6 Autoridad del servidor y concurrencia](#seccion-4-6)
  - [4.7 Semáforos asíncronos y escritura de streams](#seccion-4-7)
  - [4.8 DRY: una implementación por regla](#seccion-4-8)
  - [4.9 Simplicidad y funcionalidad necesaria: KISS y YAGNI](#seccion-4-9)
  - [4.10 Inyección de dependencias y ciclos de vida](#seccion-4-10)
  - [4.11 Bloqueos síncronos](#seccion-4-11)
  - [4.12 Operaciones atómicas](#seccion-4-12)
  - [4.13 Colecciones concurrentes](#seccion-4-13)
  - [4.14 Registros y colas en memoria](#seccion-4-14)
- [5 Contratos y comunicación gRPC](#seccion-5)
  - [5.1 Diseño y nombres de contratos](#seccion-5-1)
  - [5.2 Presencia y valores predeterminados](#seccion-5-2)
  - [5.3 Compatibilidad y evolución](#seccion-5-3)
  - [5.4 Resultados de negocio y errores](#seccion-5-4)
  - [5.5 Llamadas unarias, plazos y cancelación](#seccion-5-5)
  - [5.6 Streams, dúplex y presencia](#seccion-5-6)
  - [5.7 Verificación de cambios de contrato](#seccion-5-7)
- [6 Manejo de Errores y Excepciones](#seccion-6)
  - [6.1 Manejo General de Excepciones](#seccion-6-1)
  - [6.2 Tipos de Excepciones](#seccion-6-2)
  - [6.3 Propagación de Excepciones](#seccion-6-3)
  - [6.4 Límites de Manejo de Excepciones](#seccion-6-4)
  - [6.5 Excepciones Personalizadas](#seccion-6-5)
  - [6.6 Manejo de Estados No Válidos](#seccion-6-6)
  - [6.7 Liberación de Recursos](#seccion-6-7)
  - [6.8 Prácticas Prohibidas de Manejo de Excepciones](#seccion-6-8)
  - [6.9 Nulos, ausencia y resultados explícitos](#seccion-6-9)
  - [6.10 Uso de Bitácoras y Logs](#seccion-6-10)
  - [6.11 Cláusulas de guarda y validación de precondiciones](#seccion-6-11)
  - [6.12 Fallos no controlados y tareas de fondo](#seccion-6-12)
- [7 Comentarios y documentación](#seccion-7)
  - [7.1 Política de comentarios](#seccion-7-1)
  - [7.2 Comentarios de una línea](#seccion-7-2)
  - [7.3 Comentarios de bloque y código deshabilitado](#seccion-7-3)
  - [7.4 Documentación de decisiones y contratos](#seccion-7-4)
  - [7.5 Pendientes TODO](#seccion-7-5)
  - [7.6 Defectos FIXME](#seccion-7-6)
  - [7.7 Restricciones y mantenimiento de documentación](#seccion-7-7)
- [8 Control de Versiones Git](#seccion-8)
  - [8.1 Mensajes de Commit](#seccion-8-1)
  - [8.2 Archivo Gitignore](#seccion-8-2)
- [9 Pruebas y Aseguramiento de Calidad](#seccion-9)
  - [9.1 Estrategia de Pruebas](#seccion-9-1)
  - [9.2 Pruebas Unitarias](#seccion-9-2)
  - [9.3 Pruebas de Integración](#seccion-9-3)
  - [9.4 Estructura Arrange-Act-Assert](#seccion-9-4)
  - [9.5 Categorías de pruebas](#seccion-9-5)
  - [9.6 Criterios y casos de prueba](#seccion-9-6)
  - [9.7 Determinismo y dobles de prueba](#seccion-9-7)
  - [9.8 Verificación del estándar](#seccion-9-8)
- [10 Prefijos Permitidos y Abreviaciones](#seccion-10)
  - [10.1 Prefijos y sufijos de C#](#seccion-10-1)
  - [10.2 Vistas ViewModels y comandos](#seccion-10-2)
  - [10.3 Servicios datos y contratos](#seccion-10-3)
  - [10.4 Recursos abreviaciones y otros archivos](#seccion-10-4)
  - [10.5 Assets y catálogos de recursos](#seccion-10-5)
- [11 Mantenimiento y aplicación del estándar](#seccion-11)
  - [11.1 Lectura y aplicación](#seccion-11-1)
  - [11.2 Explicación y evidencia de cambios](#seccion-11-2)
  - [11.3 Propuestas y documentación compartida](#seccion-11-3)
  - [11.4 Adopción de reglas y propuestas pendientes](#seccion-11-4)
- [12 Seguridad y protección de información](#seccion-12)
  - [12.1 Configuración y secretos](#seccion-12-1)
  - [12.2 Contraseñas y tokens](#seccion-12-2)
  - [12.3 Transporte y certificados](#seccion-12-3)
  - [12.4 Validación y mensajes al usuario](#seccion-12-4)
  - [12.5 Verificación por correo, recuperación y límites de envío](#seccion-12-5)
- [13 Declaración de Uso de I.A](#seccion-13)
- [14 Referencias](#seccion-14)

---

<a id="seccion-1"></a>

## 1 Introducción



<a id="seccion-1-1"></a>

### 1.1 Propósito del Documento

El presente estándar tiene como propósito establecer las convenciones y prácticas que deberán seguirse durante el desarrollo del videojuego basado en el juego de mesa “Atari Go”, con el fin de mantener un código fuente consistente, legible, mantenible y fácil de revisar.

El documento define criterios relacionados con la escritura, organización, documentación, manejo de errores y desarrollo del código fuente. Su aplicación busca reducir inconsistencias entre los integrantes del equipo y facilitar la comprensión, modificación y mantenimiento del software durante las diferentes etapas del proyecto.

El estándar servirá como referencia común para las actividades de desarrollo y como criterio para revisar que el código producido cumpla con las convenciones establecidas para el proyecto.

<a id="seccion-1-2"></a>

### 1.2 Alcance e interpretación de las reglas

El estándar aplica al código C# escrito a mano del cliente WPF, servidor, lógica del juego
y proyectos de pruebas. Las reglas específicas de otros archivos están en 10.3 a 10.5.
El cliente utiliza WPF, el transporte utiliza gRPC y Protobuf, y el servidor utiliza EF Core
con SQL Server. No se incorporan convenciones de WCF o Unity como requisitos de estas capas.

- **Debe / deberá:** requisito obligatorio dentro de su alcance.
- **No debe / está prohibido:** práctica excluida.
- **Se permite / puede:** alternativa válida bajo las condiciones indicadas.

Las firmas impuestas por interfaces externas, overrides y herramientas conservan su contrato.
La excepción afecta a esa firma, no a toda la clase. Las fuentes generadas se modifican desde
su origen y no se reformatean manualmente. Una entidad de EF no se considera generada sin
una fuente y un procedimiento reproducible que lo demuestren.

Las reglas particulares, como la declaración de semáforos, precisan las generales. La
precedencia de nombres de campos está en 10.1.Las instrucciones expresas del responsable prevalecen 
y sus excepciones deben quedar claras.

Los ejemplos son fragmentos didácticos: pueden omitir imports, tipos colaboradores y miembros
no relacionados. Un ejemplo **correcto** debe respetar las reglas relevantes para lo que
demuestra; no implica que todo el fragmento sea una unidad compilable independiente.
Los ejemplos **incorrectos** muestran deliberadamente una práctica prohibida.

<a id="seccion-1-3"></a>

### 1.3 Entorno de Desarrollo

El proyecto utiliza C# sobre .NET 10; el cliente WPF y sus pruebas requieren Windows.
La versión del lenguaje y la versión de .NET son conceptos distintos. La versión del
lenguaje la determina el SDK y la configuración del proyecto; no debe inferirse a partir
del número del framework. Se utilizará una versión estable del SDK compatible con los
proyectos. No se introducirán características preliminares sin una decisión explícita.

Se puede trabajar con Visual Studio o VS Code. La persistencia utiliza SQL Server.
Las versiones concretas de dependencias se consultan en los archivos de proyecto;
no se duplican como listas manuales dentro del estándar.

<a id="seccion-1-4"></a>

### 1.4 Idioma y Localización

El código fuente, los identificadores y los comentarios estarán escritos en inglés.
La documentación del proyecto podrá escribirse en español.

El sistema permitirá que el usuario pueda elegir el idioma de los mensajes al jugador y la interfaz de usuario entre las siguientes opciones:

- Inglés

- Español

- Alemán

El sistema solo requerirá del huso horario para generar logs y reportes de chat, los cuales tendrán el huso horario UTC-6 (Ciudad de México).

Los textos de interfaz pertenecen a recursos localizados. No se traducen nombres de tipos,
propiedades o claves de recursos para cambiar el idioma mostrado al jugador.

Los textos visibles para el jugador deberán almacenarse en los recursos localizados del
cliente. No se deberán escribir directamente en las vistas, los ViewModels ni los servicios
los mensajes que deban presentarse en varios idiomas.

La cultura inicial de la interfaz se determinará a partir de la cultura configurada en el
sistema operativo. El cliente utilizará los recursos disponibles para esa cultura. Si no
existe una traducción específica, se utilizarán los recursos neutrales `Resources.resx`.

Cuando se cambie el idioma durante la ejecución, ese cambio se aplicará a la sesión actual
y no se guardará en la base de datos mientras el proyecto no defina expresamente una
preferencia persistente.

Los nombres de operaciones tampoco se traducirán. Las claves de recursos deberán
conservarse entre las culturas soportadas; solo se traducirán sus valores.

**Correcto**

```csharp
string message = Resources.Login_Err_IncorrectPassword;
```

**Incorrecto**

```csharp
string message = "El usuario o la contraseña son incorrectos.";
```

El desplazamiento UTC-6 anterior se aplica a la presentación de logs y reportes de chat.
Los instantes de sesiones, expiraciones y persistencia se calculan en UTC. No se compara
una hora local con una fecha UTC ni se resta un desplazamiento para calcular un plazo.
“UTC-6” expresa aquí un desplazamiento fijo; cambiarlo por una zona horaria identificada
requiere documentar esa decisión.

Las cadenas con formato deberán conservar los marcadores requeridos por sus llamadas entre
culturas. Por ejemplo, si se utilizan `{0}` y `{1}`, una traducción no debe introducir `{2}`
sin el argumento correspondiente. El texto de los correos también se obtiene de recursos
localizados. No se exige renombrar las claves existentes para añadir un sufijo nuevo.

Usar la cultura de presentación para fechas y números visibles; los formatos técnicos y
valores intercambiados por protocolo siguen 3.21 y 5. Los assets con texto traducible
requieren una representación localizada; los assets sin texto no se duplican por idioma.

<a id="seccion-2"></a>

## 2 Convenciones de Nomenclatura



<a id="seccion-2-1"></a>

### 2.1 Clases

Usar sustantivos descriptivos en PascalCase para nombrar clases. No utilizar prefijos como “c_” para indicar que es una clase.

**Correcto**

```csharp
public class PlayerCharacter
{
}
```

**Incorrecto**

```csharp
public class c_PlayerCharacter
{
}
```

<a id="seccion-2-2"></a>

### 2.2 Estructuras

Usar sustantivos descriptivos en PascalCase para nombrar estructuras.

**Correcto**

```csharp
public struct Vector3Int
{
}
```

**Incorrecto**

```csharp
public struct vector_3d
{
}
```

<a id="seccion-2-3"></a>

### 2.3 Interfaces

Usar PascalCase comenzando obligatoriamente con la letra 'I'. Deben usarse frases adjetivas o sustantivos.

**Correcto**

```csharp
public interface IDamageable
{
}
```

**Incorrecto**

```csharp
public interface Damageable
{
}
```

<a id="seccion-2-4"></a>

### 2.4 Métodos

Usar PascalCase combinando verbos o frases verbales de acción claras con el sustantivo que representa aquello sobre lo que se realiza la acción.

**Correcto**

```csharp
public void CalculateScore()
{
}
```

**Incorrecto**

```csharp
public void calculateScore()
{
}
```

<a id="seccion-2-5"></a>

### 2.5 Variables locales

Usar camelCase para nombrar variables locales dentro de los métodos.

**Correcto**

```csharp
int maxItems = 10;
```

**Incorrecto**

```csharp
int MaxItems = 10;
```

<a id="seccion-2-6"></a>

### 2.6 Parámetros

Usar camelCase para nombrar los parámetros recibidos en métodos o constructores.

**Correcto**

```csharp
public void SetScore(int newScore)
{
}
```

**Incorrecto**

```csharp
public void SetScore(int NewScore)
{
}
```

Los parámetros posicionales de un `record` que generan propiedades públicas usan PascalCase,
como `SessionIdentity(int UserId, string UserName)`. Esta es una excepción expresa porque
su identificador también nombra la propiedad sintetizada. Los parámetros ordinarios de
métodos y constructores, incluidos constructores que no generan esas propiedades, conservan
camelCase. Las firmas externas mantienen los nombres exigidos por su contrato.

<a id="seccion-2-7"></a>

### 2.7 Campos Privados

Usar camelCase prefijado con un guión bajo (_) para todos los campos privados de una clase.

**Correcto**

```csharp
private string _playerUsername;
```

**Incorrecto**

```csharp
private string playerUsername;
```

Esta regla se aplica a campos de instancia privados e internos que no son constantes.
Los campos estáticos y los campos `[ThreadStatic]` siguen las reglas específicas de 2.11
y 11, respectivamente. `readonly` no convierte un campo en una constante.

<a id="seccion-2-8"></a>

### 2.8 Campos Públicos

Usar PascalCase para campos públicos.

**Correcto**

```csharp
public float MoveSpeed;
```

**Incorrecto**

```csharp
public float moveSpeed;
```

Para campos públicos constantes o estáticos se aplica primero la categoría específica
establecida en 2.10, 2.11 y 11. Exponer un campo mutable requiere justificar por qué una
propiedad no representa adecuadamente la operación o el dato.

<a id="seccion-2-9"></a>

### 2.9 Campos Protegidos

Usar camelCase para los campos protegidos (protected).

**Correcto**

```csharp
protected bool isAlerted;
protected float attackPower;
```

**Incorrecto**

```csharp
protected bool IsAlerted;

protected float _attackPower;
```

<a id="seccion-2-10"></a>

### 2.10 Constantes

Usar PascalCase para nombrar constantes en lugar de mayúsculas sostenidas.

**Correcto**

```csharp
public const int MaxInventorySlots = 20;
```

**Incorrecto**

```csharp
public const int MAX_INVENTORY_SLOTS = 20;
```

<a id="seccion-2-11"></a>

### 2.11 Campos estáticos

Los campos estáticos deben nombrarse utilizando camelCase sin prefijo. Está prohibido utilizar el prefijo `s_` en el código del proyecto.

**Correcto**

```csharp
public class PlayerManager
{
    private static int activePlayersCount;
}
```

**Incorrecto**

```csharp
public class PlayerManager
{
    private static int s_activePlayersCount;
}
```

Esta regla incluye campos `static readonly` y prevalece sobre la regla de visibilidad.
Las constantes usan `PascalCase` aunque sean implícitamente estáticas. Los campos con
`[ThreadStatic]` usan `t_camelCase`. Las propiedades estáticas conservan `PascalCase`;
no son campos.

<a id="seccion-2-12"></a>

### 2.12 Enumeraciones

Usar PascalCase tanto para el nombre de la enumeración como para sus valores internos.

**Correcto**

```csharp
enum GameState
{
    MainMenu,
    Playing,
    Paused
}
```

**Incorrecto**

```csharp
enum game_state
{
    MainMenu,
    Playing,
    Paused
}
```

Preferir un nombre singular cuando el enum representa una selección. Si se define realmente
una combinación de bits, documentar ese significado y utilizar `[Flags]` con valores adecuados.
No convertir enums existentes en banderas ni renombrarlos por una preferencia editorial.

El valor cero debe tener un significado definido por su contrato.
Los enums Protobuf siguen su convención de valor inicial no especificado.
No renumerar valores publicados o persistidos. Una conversión debe tratar de forma controlada
los valores desconocidos, sin aceptarlos silenciosamente como un estado exitoso.

<a id="seccion-2-13"></a>

### 2.13 Namespaces

Los espacios de nombres deben utilizar PascalCase y reflejar la estructura lógica del proyecto. El nombre del namespace debe coincidir exactamente con la ruta de carpetas y el contexto del archivo que lo contiene

**Correcto**

```csharp
namespace Game.Core.Player
{
    public class PlayerController
    {
    }
}
```

**Incorrecto**

```csharp
namespace game_core_player
{
    public class PlayerController
    {
    }
}
```

La raíz es el nombre del proyecto, seguida de las carpetas relativas al proyecto.
Por ejemplo, `AtariGo.Client/Views/Dialogs/OptionsDialogView.xaml.cs` pertenece a
`AtariGo.Client.Views.Dialogs`. Se permiten namespaces con bloque o de ámbito de archivo;
los bloques que existan usan llaves en línea nueva. No se cambia el estilo de todo un
archivo ajeno a la tarea solo por preferencia. El `x:Class` de WPF debe coincidir con el
tipo parcial del code-behind.

<a id="seccion-2-14"></a>

### 2.14 Organización de imports y miembros

El orden de un tipo escrito a mano es el siguiente:

1. Imports de `System`, APIs `Microsoft`, terceros y proyectos `AtariGo`, con una línea
   en blanco entre grupos presentes.
2. Namespace y declaración del tipo.
3. Constantes y campos estáticos.
4. Campos de instancia.
5. Constructores: estático, sin parámetros y parametrizados.
6. Eventos.
7. Propiedades e indexadores.
8. Métodos normales.
9. Métodos `override`, al final, independientemente de su visibilidad.

Dentro de cada categoría con visibilidad se utiliza público, protegido, interno y privado.
Mantener las declaraciones relacionadas legibles; no se exige un orden alfabético adicional.
Los miembros parciales se organizan dentro de su archivo de responsabilidad según 4.1.

Los imports sin uso se eliminan. Se permiten aliases descriptivos para resolver colisiones
entre tipos de dominio, Protobuf o librerías, por ejemplo `ProtoSnapshot` y `DomainSnapshot`.
No utilizar aliases para abreviar arbitrariamente un tipo que ya sea claro. Los imports
ubicados dentro de un namespace conservan los grupos anteriores.

Los imports implícitos o generados conservan su origen. Las instrucciones de nivel superior
de `Program.cs` y las directivas de archivos de ensamblado no se tratan como una clase.

**Correcto**

```csharp
using System.Globalization;

using Grpc.Core;

using AtariGo.Contracts;
```

Los tres grupos presentes están separados. La presencia de un import no autoriza conservarlo
si el archivo no lo necesita.

**Incorrecto**

```csharp
using AtariGo.Contracts;
using System.Globalization;
using Grpc.Core;
```

El ejemplo mezcla las categorías sin la separación elegida para el proyecto.

<a id="seccion-2-15"></a>

### 2.15 Eventos

Los eventos deberán utilizar PascalCase y deberán utilizar nombres que describan la acción o situación representada.

Cuando sea necesario diferenciar estados anteriores y posteriores a una acción, deberá utilizarse el tiempo verbal correspondiente en lugar de prefijos como Before o After.

**Correcto**

```csharp
public event Action PlayerDied;
```

**Incorrecto**

```csharp
public event Action AfterPlayerDeath;
```

<a id="seccion-2-16"></a>

### 2.16 Propiedades

Las propiedades deberán utilizar PascalCase.

Las propiedades deberán utilizar sustantivos, frases nominales o adjetivos que representen claramente el dato o característica expuesta.

Las propiedades booleanas deberán utilizar nombres afirmativos y podrán utilizar prefijos como Is, Has o Can cuando mejoren la claridad.

**Correcto**

```csharp
public int CurrentHealth { get; private set; }
```

**Incorrecto**

```csharp
public int current_health { get; private set; }
```

Las propiedades públicas sintetizadas por records siguen la misma convención. La forma del
accesor y su mutabilidad se eligen según 3.18; el nombre PascalCase no obliga a exponer un setter.

<a id="seccion-2-17"></a>

### 2.17 Prefijos y Sufijos

Los prefijos y sufijos solo deberán utilizarse cuando representen un significado establecido por C#, .NET o por este estándar.

No deberá codificarse información redundante sobre el tipo de un elemento mediante prefijos o sufijos arbitrarios.

Se establecen las siguientes convenciones:

- Interfaces: prefijo I.

- Parámetros genéricos descriptivos: prefijo T.

- Campos privados o internos de instancia: prefijo _.

- Campos thread-static: prefijo t_.

- Métodos asíncronos: sufijo Async.

- Excepciones personalizadas: sufijo Exception.

- Tipos de datos derivados de EventArgs: sufijo EventArgs.

- Delegados de manejo de eventos: sufijo EventHandler.

- Otros delegados de callback: sufijo Callback.

No deberán utilizarse prefijos como C para las clases.

No deberán utilizarse los sufijos Enum, Delegate, Impl, Ex o New únicamente para indicar el tipo o implementación de un elemento.

**Correcto**

```csharp
public interface IDamageable
{
    void TakeDamage(int damage);
}
public class EnemyManager
{
    private int _remainingEnemies;

    public Task LoadEnemiesAsync()
    {
        return Task.CompletedTask;
    }
}
```

**Incorrecto**

```csharp
public interface DamageableInterface
{
    void TakeDamage(int damage);
}
public class CEnemyManagerImpl
{
    public Task LoadEnemies()
    {
        return Task.CompletedTask;
    }
}
```

La tabla de la sección 10 resuelve la prioridad entre prefijos y las convenciones de
vistas, controles, servicios, mensajes y recursos. Un prefijo de tipo como `Txt` o `chk`
no se adopta solo porque ya aparezca en un archivo. Los nombres impuestos por un framework
no se renombran para adaptarlos a una preferencia local.

<a id="seccion-2-18"></a>

### 2.18 Clases de Prueba

Las clases de prueba deberán seguir el patrón:

`<TypeUnderTest>Tests`

Este patrón permitirá identificar inmediatamente qué clase o componente de producción está siendo probado.

**Correcto**

```csharp
public class DamageCalculatorTests
{
}
```

**Incorrecto**

```csharp
public class TestPlayerStuff
{
}
```

<a id="seccion-2-19"></a>

### 2.19 Métodos de Prueba

Los nombres de los métodos de prueba deberán identificar claramente:

- El método o comportamiento probado.

- El escenario evaluado.

- El resultado esperado.

- El flujo específico, cuando sea necesario.

Estos elementos deberán separarse mediante guiones bajos.

**Correcto**

```csharp
public void TakeDamage_HealthAboveZero_DecreasesHealth()
{
}
public void TakeDamage_HealthAboveZero_DecreasesHealth_WhenArmorIsAbsent()
{
}
```

**Incorrecto**

```csharp
public void TestDamage()
{
}
```

Los segmentos se escriben en `PascalCase`. El resultado esperado describe una expectativa,
no si la última ejecución pasó o falló. No se añade `_Failed` para marcar una prueba rota.
Puede usarse un segmento adicional si describe un flujo o condición real del escenario.

<a id="seccion-2-20"></a>

### 2.20 Records, estructuras y tuplas

Elegir el tipo según la identidad, igualdad y propiedad de sus datos. Una clase puede
representar estado o comportamiento con identidad; un record es apropiado para datos cuyo
contrato aprovecha igualdad por valor y actualizaciones con `with`. Un `readonly record
struct` es una alternativa para un valor pequeño coherente, como una posición de tablero.
No se obliga a convertir modelos existentes a una única forma.

Los parámetros posicionales que generan propiedades públicas siguen 2.6. Las tuplas pueden
representar agrupaciones locales o claves coherentes; sus elementos nombrados usan PascalCase
y la variable usa camelCase. Preferir un tipo propio si la agrupación adquiere validaciones,
identidad o un contrato difícil de comprender. No crear un tipo solo por reemplazar dos datos.

**Correcto**

```csharp
public readonly record struct BoardPosition(int Row, int Column);
(int Sender, int Room, int Recipient) key = (senderId, roomId, recipientId);
```

Cada forma expresa una agrupación con significado. Una tupla se entiende por sus nombres,
no por recordar el orden de `Item1`, `Item2` y `Item3`.

**Incorrecto**

```csharp
public readonly record struct BoardPosition(int row, int column);
```

Los nombres generarían propiedades públicas que contradicen 2.16.
Un record con listas o arreglos no garantiza inmutabilidad profunda; su propiedad está en 3.18.

<a id="seccion-3"></a>

## 3 Estilo de Código y Formato



<a id="seccion-3-1"></a>

### 3.1 Formato General

Todo el código fuente escrito en C# deberá seguir un estilo de formato consistente en todo el proyecto. El formato deberá priorizar la legibilidad, la consistencia estructural y la facilidad de mantenimiento, aplicándose de manera uniforme por todos los integrantes del equipo.

<a id="seccion-3-2"></a>

### 3.2 Indentación

Cada nivel de anidamiento del código debe utilizar exactamente 4 espacios de indentación.

**Correcto**

```csharp
if (playerCount < maxPlayers)
{
    playerCount++;
}
```

**Incorrecto**

```csharp
if (playerCount < maxPlayers)
{
playerCount++;
}
```

<a id="seccion-3-3"></a>

### 3.3 Longitud de Línea

El límite máximo permitido es de 100 caracteres por línea. Cuando una sentencia supere esta longitud, deberá dividirse en varias líneas en puntos sintácticos lógicos, conservando la estructura lógica y manteniendo una indentación consistente.

**Correcto**

```csharp
PlayerScore playerScore = CalculatePlayerScore(
    player,
    completedObjectives,
    scoringSettings);
```

**Incorrecto**

```csharp
var playerScore = CalculatePlayerScore(player, completedObjectives, elapsedTime, currentLevel, difficultyMultiplier, bonusMultiplier, elapsedObjectives, playerExperience, currentGameMode, scoreMultiplier);
```

El límite incluye indentación y comentarios del C# escrito a mano. No se rompe una cadena
cambiando su valor ni una firma externa por cumplir el ancho. Los archivos generados se
tratan según 4.1. XAML, Protobuf, documentación y scripts tienen reglas específicas en 11;
este límite C# no se extiende implícitamente a esos lenguajes.

Para firmas extensas se permite dividir los parámetros en líneas de continuación. Si el
identificador de un método de prueba ya requiere una línea completa, se permite separar el
tipo de retorno del identificador. No abreviar el escenario hasta perder su significado.

<a id="seccion-3-4"></a>

### 3.4 Uso de Llaves

Las llaves de apertura deben colocarse en una nueva línea, debajo de la declaración correspondiente. Se permite colocarlas en la misma línea cuando sean parte de la sintaxis propia de una expresión o inicialización y no delimitan un bloque de código.

**Correcto**

```csharp
public void CalculateScore()
{
}

var scores = new List<int> { 1, 2, 3 };
```

**Incorrecto**

```csharp
public void CalculateScore() {
}
```

Un miembro con cuerpo de expresión, por ejemplo `IsOnline => _isOnline`, no abre un bloque
y no necesita llaves. Su uso se rige por 3.14. Una lambda con bloque sí coloca la llave de
apertura en una línea nueva.

Los cuerpos de `if`, `else`, `for`, `foreach`, `while`, `do`, `lock` y `using` con bloque
conservan las llaves aunque contengan una sola instrucción. Las declaraciones `using`
sin bloque y los miembros con cuerpo de expresión mantienen su sintaxis propia.

<a id="seccion-3-5"></a>

### 3.5 Líneas en Blanco

Se deberán utilizar líneas en blanco para separar secciones de código lógicamente independientes y mejorar la legibilidad. Deberá existir al menos una línea en blanco entre métodos y propiedades consecutivas. Las líneas en blanco no deberán utilizarse excesivamente ni como sustituto de una correcta indentación u organización del código.

**Correcto**

```csharp
public class PlayerSession
{
    private string _playerId;
    private int _ping;

    public void Connect()
    {
    }

    public void Disconnect()
    {
    }
}
```

**Incorrecto**

```csharp
public class PlayerSession
{
    private string _playerId;
    private int _ping;
    public void Connect()
    {
    }
    public void Disconnect()
    {
    }
}
```

Utilizar normalmente una línea vacía para separar fases o miembros. No añadir líneas vacías
para alinear visualmente archivos ni reformatear contenido ajeno a una tarea por preferencias
de espaciado que no cambien su legibilidad o incumplan una regla explícita.

<a id="seccion-3-6"></a>

### 3.6 Espacios en Blanco

No deberán utilizarse espacios innecesarios para alinear artificialmente declaraciones o instrucciones que no estén relacionadas. Los espacios alrededor de paréntesis, corchetes, operadores y demás elementos sintácticos de C# deberán seguir las reglas de formato configuradas para el proyecto. No deberán conservarse espacios en blanco al final de las líneas del código fuente.

**Correcto**

```csharp
public void CalculateTotals()
{
    int baseScore = 10;
    int multiplier = 2;
    int finalScore = baseScore * multiplier;
}
```

**Incorrecto**

```csharp
public void CalculateTotals()
{
    int    baseScore  = 10;
    int    multiplier = 2;
    int finalScore = ( baseScore * multiplier );
}
```

Las llamadas no llevan un espacio entre el identificador y `(`. Se utiliza un espacio después
de comas y se conservan juntos el acceso a miembros y sus puntos. Los atributos se colocan
en la declaración a la que pertenecen; los atributos de parámetros, como `[CallerMemberName]`,
permanecen junto al parámetro. Las inicializaciones y accesores breves siguen las excepciones
expresas de 3.4 y 3.14, sin compactar varias acciones independientes.

<a id="seccion-3-7"></a>

### 3.7 Operadores

Los operadores binarios deberán tener espacios consistentes alrededor de sus operandos. Cuando una expresión deba dividirse en varias líneas, el salto de línea deberá colocarse antes del operador binario en un punto lógico, manteniendo una indentación consistente y un formato uniforme en todo el proyecto.

**Correcto**

```csharp
public void CalculateFinalScore()
{
    int baseScore = 100;
    int bonusScore = 50;
    int penalty = 10;
    int totalScore = baseScore
        + bonusScore
        - penalty;
}
```

**Incorrecto**

```csharp
public void CalculateFinalScore()
{
    int baseScore=100;
    int bonusScore=50;
    int penalty= 10;
    int totalScore = baseScore +
        bonusScore -
        penalty;
}
```

<a id="seccion-3-8"></a>

### 3.8 Paréntesis

Deberá existir un espacio entre las palabras clave de control de flujo (if, while, for, switch) y el paréntesis de apertura. No deberán colocarse espacios inmediatamente después de un paréntesis de apertura ni inmediatamente antes de un paréntesis de cierre.

**Correcto**

```csharp
public void EvaluateCondition(int health, int ammo)
{
    if ((health > 0) && (ammo > 0))
    {
        Fight();
    }
}
```

**Incorrecto**

```csharp
public void EvaluateCondition(int health, int ammo)
{
    if ( ( health > 0 ) && ( ammo > 0 ) )
    {
        Fight();
    }
}
```

<a id="seccion-3-9"></a>

### 3.9 Sentencias

Cada sentencia deberá escribirse en una línea independiente. No deberán colocarse varias sentencias independientes en una misma línea, a excepción de estructuras estándar propias de C# que así lo requieran (como la inicialización, condición y avance dentro de un ciclo for). No deberá utilizarse un formato compacto cuando esto reduzca la legibilidad o dificulte identificar el flujo de ejecución.

**Correcto**

```csharp
public void UpdatePlayerState()
{
    int health = 100;
    bool isAlive = true;

    for (int i = 0; i < 10; i++)
    {
        ProcessItem(i);
    }
}
```

**Incorrecto**

```csharp
public void UpdatePlayerState()
{
    int health = 100; bool isAlive = true; ProcessStatus(health, isAlive);
}
```

Declarar una variable por declaración cuando representan valores independientes. Las partes
de un `for` y la deconstrucción de una tupla coherente conservan su sintaxis propia. No dividir
esas estructuras únicamente para aumentar el número de líneas.

**Correcto**

```csharp
int row = position.Row;
int column = position.Column;
```

**Incorrecto**

```csharp
int row = position.Row, column = position.Column;
```

<a id="seccion-3-10"></a>

### 3.10 Uso de Var

La palabra clave var deberá utilizarse únicamente cuando el tipo de la variable sea evidente de forma explícita en el lado derecho de la asignación (como al instanciar un objeto con new). Deberá evitarse su uso cuando la inferencia oscurezca el tipo de dato y dificulte la lectura del código.

**Correcto**

```csharp
public void InitializePlayer()
{
    var player = new PlayerController();
}
```

**Incorrecto**

```csharp
public void InitializePlayer()
{
    var result = CalculateSomethingComplex();
}
```

`new()` sin tipo escrito a la derecha no hace evidente el tipo para esta regla.
Las llamadas, `await`, consultas LINQ y expresiones condicionales se declaran con tipo
explícito aunque el compilador pueda inferirlo. Los tipos anónimos son una excepción
necesaria porque no tienen nombre utilizable; conservar `var` en ese caso.

Cuando el tipo está escrito explícitamente a la izquierda, se permite construcción dirigida
por el contexto, por ejemplo `List<int> scores = new();`. Las convenciones particulares como
`new SemaphoreSlim(1, 1)` en 4.7 prevalecen. No se prohíben todas las inferencias del lenguaje:
se exige que el tipo y la intención puedan entenderse en la declaración.

<a id="seccion-3-11"></a>

### 3.11 Números Mágicos

Queda estrictamente prohibido el uso de "números mágicos" (valores numéricos literales hardcodeados en la lógica). Todo valor numérico con un significado específico deberá ser extraído a una constante o variable con un nombre descriptivo que explique qué representa dicho número.

**Correcto**

```csharp
public const int MaxLobbyPlayers = 2;
public bool CanJoinLobby(int currentPlayerCount)
{
    return currentPlayerCount < MaxLobbyPlayers;
}
```

**Incorrecto**

```csharp
public bool CanJoinLobby(int currentPlayerCount)
{
    return currentPlayerCount < 2;
}
```

Un literal representa un número mágico cuando codifica una regla, límite, plazo o política
sin darle un nombre. Se permiten `0` y `1` en operaciones estructurales evidentes como
inicializar contadores, avanzar un índice o comprobar una colección vacía. Si esos mismos
valores significan un estado de base de datos, un límite de jugadores o una regla del juego,
deben tener nombre. Los datos que constituyen el escenario de una prueba pueden escribirse
como literales; no se extraen constantes sin aportar significado. Los números de campos
Protobuf y los tamaños de presentación XAML no se evalúan como constantes de negocio C#.

El mismo criterio aplica a cadenas que codifican estados o políticas compartidas. Reutilizar
el catálogo correspondiente, como `DatabaseValues`, cuando representa el mismo concepto.
Un mensaje de excepción, nombre de recurso o literal único no exige una constante adicional
solo por ser una cadena. No crear un catálogo genérico que mezcle dominios independientes.

Usar `const` para valores de compilación estables, `static readonly` para valores calculados
una vez y configuración para parámetros que el diseño permite variar por entorno. Esta
distinción no obliga a convertir todas las constantes existentes en opciones configurables.

<a id="seccion-3-12"></a>

### 3.12 Límite de Parámetros por Método

Todo método, constructor o función local escrito para el proyecto recibe como máximo
3 parámetros declarados, incluidos los opcionales y `CancellationToken`, salvo la excepción
de inyección de dependencias descrita abajo. Una interfaz propia respeta el mismo límite
que su implementación.

Si se necesitan más datos de un mismo caso de uso, encapsularlos en un comando, DTO o
objeto de contexto coherente. No introducir un contenedor genérico de servicios para
ocultar dependencias ni agrupar datos sin relación únicamente para reducir el conteo.

Las firmas impuestas por un `override`, una interfaz externa o código generado se conservan.
La excepción se limita a esa firma; su lógica auxiliar propia mantiene el límite.
Se permiten hasta 5 parámetros en constructores dedicados exclusivamente a recibir
servicios resueltos por el contenedor de inyección de dependencias. Solo validan y asignan
esas dependencias; no contienen reglas de negocio ni mezclan datos del caso de uso.
Esta excepción hace visibles las dependencias de autenticación y registro sin crear un
contenedor genérico de servicios. Más de 5 exige revisar responsabilidades. Los constructores
de ViewModels que reciben datos de presentación siguen sujetos al máximo de 3.

**Correcto**

```csharp
public Task RespondToRequestAsync(
    FriendRequestDecision decision,
    CancellationToken cancellationToken)
{
    return _friendService.RespondAsync(decision, cancellationToken);
}
```

**Incorrecto**

```csharp
public Task RespondToRequestAsync(
    string token,
    long requestId,
    bool accept,
    CancellationToken cancellationToken)
{
    return _friendService.RespondAsync(token, requestId, accept, cancellationToken);
}
```

En firmas propias que reciben cancelación, colocar normalmente `CancellationToken` al final.
Los modificadores `ref`, `out`, `in` y `params` no eliminan el parámetro del conteo. Un
parámetro `params` cuenta como uno declarado, pero no debe ocultar datos sin relación.

Se permite `ref` cuando la actualización por referencia es parte del contrato, como
`SetProperty(ref field, value)` o `Interlocked.Exchange`. Se permiten los patrones `Try…`
con `out` cuando expresan claramente una comprobación; evitar varios parámetros de salida
para devolver simultáneamente estado, datos y mensajes que pertenecen a un resultado coherente.
No introducir `in` ni copias alternativas sin una necesidad de contrato o rendimiento medida.

Los argumentos con nombre se utilizan cuando aclaran indicadores booleanos o valores del
mismo tipo. Los opcionales deben tener un significado estable; no acumular banderas para
representar operaciones distintas. Los mensajes gRPC se definen en `.proto`; no trasladar
modificadores de parámetros C# a una supuesta firma de red.

<a id="seccion-3-13"></a>

### 3.13 Cláusulas de guarda y salidas anticipadas

Comprobar las precondiciones disponibles antes de ejecutar el trabajo dependiente. Se permiten
retornos tempranos para entradas inválidas, estados que no requieren trabajo y resultados
que terminan la operación. Una condición puede descubrirse después de una consulta o cálculo;
su rechazo no tiene que trasladarse artificialmente al principio del método.

Las salidas deben mantener claras las obligaciones del flujo. No omitir confirmaciones,
transiciones necesarias o liberación de recursos. Utilizar `using` y `finally` cuando
corresponda. No se exige una variable mutable ni anidamiento adicional para obtener un único
punto de salida. Los retornos de lambdas y funciones locales pertenecen a su propia función.

**Correcto**

```csharp
public RoomOperationResult GetRoom(int roomId)
{
    PrivateRoom? room = FindRoom(roomId);
    if (room is null)
    {
        return new RoomOperationResult(RoomOperationStatus.NotFound);
    }
    return new RoomOperationResult(RoomOperationStatus.Success, room);
}
```

La guarda utiliza el resultado de una búsqueda sin mantener una variable de resultado mutable.

**Incorrecto**

```csharp
public void SaveProgress(bool canSave)
{
    _writeGate.Wait();
    if (!canSave)
    {
        return;
    }
    PersistProgress();
    _writeGate.Release();
}
```

La salida omite la liberación; además bloquea una espera que debe seguir el patrón de 4.7.

<a id="seccion-3-14"></a>

### 3.14 Lambdas y miembros con cuerpo de expresión

La presencia de `=>` no basta para identificar una lambda. `friend => friend.IsOnline`
es una función anónima; `public bool IsOnline => _isOnline;` es una propiedad con cuerpo
de expresión. No se deben contar ni restringir como si fueran lo mismo.

Usar lambdas cuando una API recibe un delegado o un árbol de expresión, por ejemplo un
predicado o proyección LINQ, un callback de Dispatcher, una fábrica o un comando. Que una
lambda compile no significa que sea la representación más legible.

Para callbacks ejecutables propios, se permite una expresión sencilla o un bloque con hasta
3 sentencias ejecutables y 5 líneas de código según 4.3, siempre que realice una sola acción.
Si contiene bucles, `switch`, `try/catch`, una regla de negocio o más de una responsabilidad,
extraer un método con nombre. La complejidad no se reduce ocultando lógica en una expresión.
Preferir un grupo de métodos cuando coincide la firma; conservar una lambda si adapta
argumentos, captura contexto necesario o aporta un predicado o proyección.

En expresiones `IQueryable`, conservar árboles traducibles por el proveedor. Extraer una
expresión a un método C# ordinario puede impedir su traducción a SQL: no aplicar esa
refactorización mecánicamente. Las configuraciones declarativas de EF Core son una excepción
al límite de 3 sentencias y 5 líneas por callback; deben organizarse por entidad, respetar
el tamaño del método contenedor y no incluir lógica de negocio. Usar clases de configuración
por entidad cuando el mapeo crezca.

Los parámetros tienen nombres camelCase descriptivos (`friend`, `request`, `entity`), no
letras sueltas como `e`, `d` o `p` en código propio. Usar `_` para parámetros no utilizados;
los descartes múltiples `(_, _)` son válidos. Las fuentes generadas conservan sus nombres.
Si es necesario desuscribir un evento, conservar el delegado o usar un método con nombre.
Revisar las variables capturadas y su duración, especialmente en bucles y pantallas.

Una lambda asíncrona debe devolver `Task` o `Task<T>` a un receptor que espere esa tarea.
No pasar una lambda `async` a `RelayCommand` o a un `Action` síncrono. Los manejadores de
eventos impuestos por el framework son una excepción de firma y delegan el trabajo asíncrono
a métodos que devuelven tareas, con manejo de errores en el límite.

Los miembros con cuerpo de expresión se permiten para una expresión sencilla, sin varios
pasos ni ternarios anidados. Si requieren varias decisiones o efectos secundarios difíciles
de seguir, usar un cuerpo con llaves y métodos auxiliares. Los brazos de `switch` con `=>`
son sintaxis de selección, no lambdas.

**Correcto**

```csharp
bool hasOnlineFriends = friends.Any(friend => friend.IsOnline);
RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), HandleFailure);
CloseCommand = new RelayCommand(CloseDialog);
```

**Incorrecto**

```csharp
RefreshCommand = new RelayCommand(async () => await RefreshAsync());
```

El último ejemplo convierte el trabajo asíncrono en un callback `void` que el comando
síncrono no puede esperar ni supervisar.

<a id="seccion-3-15"></a>

### 3.15 Asincronía cancelación y plazos

Los métodos propios que devuelven una tarea usan sufijo `Async`, aunque deleguen la tarea
sin escribir `async`. Las firmas externas conservan sus nombres. `async void` queda limitado
a eventos y límites `void` impuestos por interfaces como `ICommand.Execute`; su implementación
debe esperar la tarea y manejar las excepciones en ese límite, como `AsyncRelayCommand`.

No usar `.Wait()`, `.Result` ni `.GetAwaiter().GetResult()` para esperar entrada y salida en
el flujo de la interfaz. Propagar `CancellationToken` cuando la operación permita cancelación.
Una tarea descartada con `_ =` necesita un propietario, manejo de errores y un final de vida
explicables; no es una forma de ignorar fallos. Evitar actualizaciones de una pantalla después
de que su propietario la haya cerrado.

Las llamadas unarias remotas usan un plazo finito definido por política compartida.
Los streams mantienen propietario, cancelación y cierre según 5.6; no heredan
automáticamente el plazo breve de una llamada unaria. Reutilizar el
canal de comunicación por proceso cliente; la autenticación se mantiene por sesión y por
llamada. No repetir automáticamente operaciones que cambian datos sin garantizar idempotencia.
Una cancelación local o un plazo agotado no demuestran que el servidor haya deshecho el cambio.

La convención `Async` también se aplica a métodos propios que realizan trabajo asíncrono
mediante `ValueTask` o enumeración asíncrona. Una API externa como `MoveNext` conserva su
nombre. No aplicar `ConfigureAwait(false)` indiscriminadamente a código que necesita volver
al hilo WPF; la elección depende del contexto de ejecución y de quién actualiza la interfaz.

<a id="seccion-3-16"></a>

### 3.16 Condicionales ternarios

Se permite `condition ? valueIfTrue : valueIfFalse` para elegir entre dos valores claros,
sin efectos secundarios. No anidar ternarios. Cuando hay varias condiciones o acciones,
usar `if`, `switch` o un método con nombre. `??`, `?.` y `?:` son operadores distintos;
no sustituir uno por otro sin conservar el significado de `null` y de la condición.

<a id="seccion-3-17"></a>

### 3.17 Accesibilidad y puntos de extensión

Utilizar la visibilidad necesaria para la colaboración real y declararla explícitamente
cuando el lenguaje y la firma lo permitan. Los detalles de implementación no se exponen
solo para facilitar una prueba. Un miembro público entre ensamblados puede ser parte de
una colaboración legítima aunque no sea un endpoint de red.

`protected`, `virtual` y `abstract` expresan puntos de extensión reales. No se exige marcar
todas las clases como `sealed` ni convertir todas las públicas en internas. Antes de
restringir acceso se revisan consumidores, bindings, DI, EF y herramientas. Se conservan
las firmas externas y los miembros parciales que requiere el framework.

**Correcto**

```csharp
private readonly IFriendClient _friendClient;
public ICommand SearchCommand { get; }
```

El servicio pertenece al ViewModel y el comando debe estar disponible para el binding.

**Incorrecto**

```csharp
public IFriendClient FriendClient;
```

Exponer una dependencia mutable sin necesidad permite reemplazar un detalle interno.

<a id="seccion-3-18"></a>

### 3.18 Propiedades, inicialización e inmutabilidad

Utilizar propiedades automáticas si no se necesita lógica de acceso. Seleccionar `get`,
`init`, `private set` o `set` según quién debe modificar el dato. Un campo de respaldo tiene
sentido para notificación WPF, validación o coordinación; no se añade para repetir un acceso.

Preferir `readonly` en dependencias y referencias que permanecen estables. Se permite
mutabilidad cuando corresponde al estado del juego, una propiedad enlazada, configuración,
materialización de EF o retirada coordinada de una referencia. No se exige añadir `required`
o `init` a todos los modelos. El contrato de inicialización debe evitar estados requeridos
ausentes; `required` no sustituye validar una entrada externa en ejecución.

Un campo `readonly` impide reasignar la referencia; no vuelve inmutable su objeto. `with`
copia las referencias de un record, no clona automáticamente sus listas o arreglos. Cuando
una operación produce una nueva instantánea y necesita modificar una colección compartida,
debe respetar su propiedad y copiar el contenido que no pueda alterar en el estado anterior.
No reemplazar todas las colecciones por tipos inmutables si el diseño ya controla su acceso.

**Correcto**

```csharp
StoneColor[] board = (StoneColor[])state.Board.Clone();
board[index] = player;
GameState next = state with { Board = board };
```

La jugada modifica un arreglo propio de la nueva instantánea.

**Incorrecto**

```csharp
GameState next = state with { };
next.Board[index] = player;
```

La copia conserva el arreglo y modifica también la instantánea anterior.

<a id="seccion-3-19"></a>

### 3.19 Estructuras de control y patrones

Usar `foreach` al recorrer elementos cuando no se necesita un índice; `for` cuando la posición
o avance forman parte del algoritmo; `while` cuando la continuidad depende del estado.
Los bucles de operaciones cancelables respetan el token de su propietario. `break` y
`continue` se permiten si aclaran la terminación o descarte de un elemento sin omitir trabajo
obligatorio. Los cuerpos de control siguen 3.4.

Se permiten patrones de declaración y guardas negativas para comprobar un tipo sin repetir
casts. `is null` e `is not null` expresan ausencia; no reemplazar mecánicamente una igualdad
sobrecargada cuando su significado sea parte del contrato. Se permiten patrones relacionales
y lógicos y expresiones `switch` legibles. No convertir todos los `if` a patrones.

Un `switch` debe definir cómo trata los valores posibles y desconocidos según su contrato.
Evitar decisiones anidadas difíciles de seguir; extraer lógica solo cuando aporte un nombre
y una responsabilidad claros, respetando 3.14 y 4.3.

**Correcto**

```csharp
if (value is not int avatarId)
{
    return DependencyProperty.UnsetValue;
}
```

La guarda comprueba y obtiene el valor en un solo paso.

**Incorrecto**

```csharp
if (value is int)
{
    int avatarId = (int)value;
    ApplyAvatar(avatarId);
}
```

La comprobación y el cast repiten una operación que puede expresar el patrón de declaración.

<a id="seccion-3-20"></a>

### 3.20 LINQ y materialización de consultas

Utilizar LINQ para consultas legibles sin efectos secundarios; no modificar el dominio desde
predicados o proyecciones usados como consultas. La sintaxis encadenada existente es válida;
un bucle también lo es si expresa mejor el trabajo. No imponer una longitud de cadena arbitraria.

`IEnumerable<T>` puede diferir la ejecución; `IQueryable<T>` conserva una expresión que el
proveedor puede traducir. Materializar al necesitar una instantánea estable o antes de que
termine el contexto o protección requeridos para enumerar. No añadir `ToList()` a toda consulta.

Sobre una colección ordinaria compartida y mutable, la selección de una instantánea se
realiza bajo la sincronización correspondiente. No devolver una consulta diferida que
enumerará esa colección fuera de su protección. Las colecciones concurrentes siguen 4.13.
En EF, filtrar y proyectar antes de materializar; no sustituir una expresión traducible por
un método C# que el proveedor no pueda ejecutar. Las consultas solo de lectura evalúan
conscientemente el seguimiento según 4.5.

**Correcto**

```csharp
lock (_sync)
{
    snapshot = _entries.Values.Where(entry => entry.IsActive).ToArray();
}
```

El resultado ya está materializado antes de abandonar la protección de la colección.

**Incorrecto**

```csharp
lock (_sync)
{
    return _entries.Values.Where(entry => entry.IsActive);
}
```

La enumeración se aplaza hasta que el consumidor ya está fuera del bloqueo.

<a id="seccion-3-21"></a>

### 3.21 Cadenas, cultura y unidades

Elegir comparación según el significado del dato. Los identificadores técnicos, claves y
tokens se comparan con reglas estables; no dependen de la cultura de interfaz. Una búsqueda
o presentación de lenguaje natural puede necesitar comparación cultural. Seleccionar
`Ordinal` u `OrdinalIgnoreCase` cuando corresponda y conservar la normalización y collation
que exige la identidad persistida. No cambiar globalmente la semántica de nombres o correos.

La normalización es específica de cada campo. Un nombre puede admitir `Trim`; una contraseña
no se recorta ni cambia de mayúsculas para facilitar su validación. Los códigos que conservan
ceros iniciales se tratan como cadenas según su contrato.

Las fechas y números visibles usan la cultura de presentación. Los formatos técnicos deben
ser estables y no depender accidentalmente de esa cultura. Los nombres deben dejar clara
la unidad cuando pueda confundirse, como `RetryAfterSeconds` o `TimeoutMilliseconds`.
Un tipo como `TimeSpan` o el contexto explícito también puede aportar esa unidad; no se
renombran todos los identificadores existentes por redundancia.

**Correcto**

```csharp
bool isSameKey = string.Equals(firstKey, secondKey, StringComparison.Ordinal);
TimeSpan timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);
```

**Incorrecto**

```csharp
string password = request.Password.Trim().ToLower();
```

La transformación altera la credencial recibida en lugar de comprobarla conforme a su contrato.

<a id="seccion-4"></a>

## 4 Organización del Proyecto



<a id="seccion-4-1"></a>

### 4.1 Una clase por archivo

Cada archivo `.cs` contiene un único tipo de nivel superior escrito a mano y su nombre
coincide con el tipo. Esto incluye interfaces, estructuras, records, delegados y enumeraciones.
No añadir tipos auxiliares de nivel superior al final de un ViewModel o una interfaz.

Los archivos parciales de WPF (`View.xaml` y `View.xaml.cs`) y las partes generadas son una
excepción de estructura, no varios tipos propios dentro del mismo archivo. `App.xaml.cs`,
`AssemblyInfo.cs` y `Program.cs` conservan la organización requerida por su función.
Los tipos anidados se reservan a detalles privados inseparables del tipo contenedor; no
se anidan para evitar crear un archivo.

No editar manualmente `.Designer.cs`, `*.g.cs` ni clases generadas por Protobuf. Su fuente
son recursos, XAML o `.proto`. Los archivos de entidades o de EF Core no quedan exentos
por parecer generados: debe existir una fuente y un proceso reproducible de generación
para tratarlos así.

Se permiten partes escritas a mano de un mismo tipo cuando separan una responsabilidad
identificable y estrechamente relacionada. El archivo principal conserva `Tipo.cs` y las
partes usan `Tipo.Responsabilidad.cs`, como `GameBoardViewModel.Gameplay.cs` y
`GamePlayer.GameSession.cs`. Comparten namespace e identidad; cada archivo contiene ese
único tipo parcial. La partición no debe ocultar dependencias, duplicar miembros ni sustituir
una separación de responsabilidades que realmente requiera tipos distintos.

<a id="seccion-4-2"></a>

### 4.2 Responsabilidad única

Todas las clases deben adherirse estrictamente al Principio de Responsabilidad Única (SRP). Cada clase debe tener un único propósito bien definido dentro del sistema y, por lo tanto, "una única razón para cambiar". No se deberán mezclar diferentes dominios o capas lógicas en un mismo componente.

Evaluar el propósito, las dependencias y los motivos de cambio. Coordinar varios pasos de
un mismo caso de uso no exige una clase por paso. El tamaño es una señal para revisar, no
una prueba suficiente de incumplimiento. Una extracción debe tener un nombre y una función
que ayuden a seguir el flujo, además de respetar los límites de 4.3.

<a id="seccion-4-3"></a>

### 4.3 Complejidad Ciclomática y Tamaño de Métodos

Para garantizar la legibilidad y mantenibilidad, la complejidad ciclomática de cualquier método no deberá superar un valor de 10. 

La complejidad se evalúa por función, incluidas lambdas y funciones locales, sin sumar sus
ramificaciones a la función contenedora. Se parte de 1 y se añade una unidad por `if`, bucle,
`catch`, alternativa no predeterminada de `switch`, ternario y operador `&&`, `||` o `??`.
Cada alternativa de una expresión `switch` salvo el descarte predeterminado cuenta una vez.
Si una herramienta usa otra definición, se debe indicar la diferencia; no presentar una
estimación por búsqueda de texto como un resultado exacto del estándar.

<a id="seccion-4-4"></a>

### 4.4 Responsabilidades del cliente WPF

Las vistas presentan datos mediante bindings y comandos. El code-behind se limita a tareas
propias de WPF, como trasladar un PasswordBox al comando de su ViewModel. Los ViewModels
coordinan estado de presentación y servicios; no consultan SQL ni crean canales por pantalla.
Los servicios de transporte usan el canal compartido del proceso y mantienen el token de
cada sesión en los metadatos de sus llamadas.

Cada pantalla que inicia operaciones remotas define su propietario, cancelación al salir
y descarte de respuestas tardías. La navegación libera ViewModels que implementan
`IDisposable`. Los callbacks extensos se convierten en métodos con nombre. Los datos
necesarios para una operación se agrupan por significado, no como una bolsa de servicios.

Cada vista deberá tener un `DataContext` definido de forma clara. La vista presentará los
datos mediante bindings y comandos; el ViewModel no buscará controles visuales ni modificará
directamente sus propiedades.

Las propiedades del ViewModel que cambien después de mostrarse la vista deberán notificar
esos cambios mediante `INotifyPropertyChanged` o una clase base que implemente ese
mecanismo. Las colecciones que cambien durante la ejecución deberán utilizar una colección
observable cuando la vista necesite reflejar los cambios en sus elementos.

Las acciones iniciadas por el usuario deberán enlazarse preferentemente a comandos. Los
comandos síncronos se utilizarán para acciones breves que no realicen operaciones de entrada
y salida. Para operaciones asíncronas se utilizarán comandos que permitan esperar la tarea,
evitar ejecuciones simultáneas cuando corresponda y exponer su estado de ejecución.

El code-behind se reservará para operaciones que dependan de características específicas
de WPF y que no puedan expresarse razonablemente mediante bindings o comandos. La lógica de
negocio, el acceso a datos y las llamadas de red no deberán implementarse en el code-behind.

Las actualizaciones de propiedades enlazadas a la interfaz deberán realizarse en el hilo
de interfaz de usuario. Cuando una operación asíncrona termine después de que la vista
propietaria haya sido cerrada o reemplazada, el resultado no deberá actualizar esa vista.

**Correcto**

```csharp
public ICommand SearchCommand { get; }

public string SearchText
{
    get
    {
        return _searchText;
    }
    set
    {
        if (_searchText != value)
        {
            _searchText = value;
            OnPropertyChanged();
        }
    }
}
```

**Incorrecto**

```csharp
private void SearchButton_Click(object sender, RoutedEventArgs e)
{
    ResultsList.ItemsSource = Database.Search(SearchTextBox.Text);
}
```

<a id="seccion-4-5"></a>

### 4.5 Configuración de persistencia

La configuración de EF Core se mantiene en clases `IEntityTypeConfiguration<T>` por entidad.
Mover un mapeo exige comprobar que el modelo relacional conserve tablas, columnas, índices
y relaciones. La organización de estos archivos no implica ejecutar migraciones.

El acceso a SQL Server y a `DbContext` corresponderá al servidor. El cliente WPF no deberá
incluir cadenas de conexión, credenciales ni acceso directo a la base de datos.

Las consultas deberán seleccionar los datos que requiere la operación y evitar cargar
entidades o relaciones que no sean necesarias. Se deberá elegir conscientemente si una
consulta modifica entidades rastreadas o solo lee datos. Las operaciones de escritura
deberán guardar los cambios en el límite correspondiente al caso de uso.

Cuando una operación incluya varios cambios que deban considerarse una sola unidad, se
deberá evaluar si requiere una transacción. Las transacciones no se añadirán por rutina a
consultas independientes.

La configuración del modelo deberá mantener las restricciones definidas para la base de
datos, como claves, índices, relaciones, tamaños y restricciones de unicidad. Los cambios
de esquema deberán acompañarse de un procedimiento reproducible de actualización. No se
asumirá que modificar una entidad o su configuración actualiza automáticamente una base de
datos existente.

**Correcto**

```csharp
User? user = await dbContext.Users
    .AsNoTracking()
    .FirstOrDefaultAsync(
        currentUser => currentUser.UserId == userId,
        cancellationToken);
```

**Incorrecto**

```csharp
List<User> users = await dbContext.Users
    .ToListAsync(cancellationToken);
User? user = users.FirstOrDefault(
    currentUser => currentUser.UserId == userId);
```

Los datos externos incluidos en SQL deben utilizar parámetros. Las APIs de EF Core que
parametrizan valores interpolados, como `FromSqlInterpolated`, no equivalen a concatenar
valores externos dentro del texto de una consulta. No interpolar identificadores SQL sin
una selección controlada. El cliente no recibe entidades rastreadas ni detalles de consultas.

Los controles de concurrencia y unicidad de SQL mantienen su función aunque exista un
semáforo en el proceso. Confirmar la transacción antes de publicar un estado persistido.
No compartir un contexto entre tareas paralelas ni mantener una transacción durante el
envío de un correo o una interacción de interfaz.

Las entidades y mapeos existentes se mantienen desde su fuente real. No regenerarlos ni
ejecutar migraciones por rutina. La restricción vigente del proyecto prohíbe crear tablas
nuevas; una reorganización del código no autoriza cambios al esquema. Cualquier cambio
de columna indispensable requiere una decisión explícita y un procedimiento verificable.

<a id="seccion-4-6"></a>

### 4.6 Autoridad del servidor y concurrencia

Los adaptadores gRPC autentican y traducen solicitudes. Los servicios de aplicación
validan permisos y reglas de negocio, y la capa de datos persiste la información.
El servidor determina la membresía, configuración y estado de cada sala y partida.
Cada sala y cada partida se identifica por separado. No se usa una variable estática
compartida para representar el jugador, la sala o la partida actual.

El estado mutable compartido debe tener un dueño, un mecanismo explícito de
concurrencia y una política de liberación. Las comprobaciones y las escrituras que
mantienen una misma regla de negocio deben ejecutarse como una sola operación lógica.
Por ejemplo, verificar el cupo y asignar el último lugar son indivisibles. Si dos
solicitudes compiten por ese lugar, solo una puede obtenerlo. El servidor publica
el nuevo estado después de confirmar la transición. Si la transición requiere
persistencia, se publica después de confirmarla en la base de datos.

No se comparte una instancia de `DbContext` entre operaciones concurrentes. Las
esperas de red se realizan fuera de la sección crítica que protege el estado en memoria.

<a id="seccion-4-7"></a>

### 4.7 Semáforos asíncronos y escritura de streams

Cuando varias operaciones asíncronas puedan acceder al mismo recurso que exige exclusión
mutua, se utilizará `SemaphoreSlim` o una única tarea propietaria que serialice ese acceso.
El mecanismo elegido deberá identificar qué recurso protege y qué operaciones lo comparten.

**Declaración del semáforo**

Para un semáforo de exclusión mutua propiedad de una instancia, utilizar un campo privado
de solo lectura (`private readonly`), con nombre `_camelCase` que describa su función. En
este patrón se escribirá el tipo explícito tanto en la declaración como en el constructor:

```csharp
private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
```

`private` limita el acceso al propietario; `readonly` impide sustituir la referencia después
de inicializarla, pero no vuelve inmutable el semáforo. `_writeGate` describe la operación
protegida y cumple 2.7. La escritura `new SemaphoreSlim(1, 1)` hace explícitos el mecanismo y
su capacidad; usarla aquí es una convención de legibilidad del proyecto. C# también permite
`new(1, 1)` cuando el tipo se conoce por el contexto, pero no es la forma elegida para este
patrón.

El primer `1` es la cantidad inicial de permisos y el segundo es la cantidad máxima. El par
`(1, 1)` expresa exclusión mutua y capacidad disponible al iniciar; no representa un plazo ni
una regla del juego. No omitir el máximo ni aumentar la capacidad cuando el recurso solo
admite una operación a la vez.

Todas las operaciones que accedan al recurso protegido deberán utilizar la misma instancia
del semáforo. No crear un semáforo nuevo por llamada ni un semáforo estático global para
recursos independientes. Si el semáforo pertenece a un servicio compartido, mantener la
asignación estable al recurso que protege.

**Adquisición y liberación**

En código asíncrono, adquirir el permiso con `await WaitAsync(cancellationToken)`. El `try`
deberá comenzar inmediatamente después de adquirirlo y su `finally` deberá ejecutar
`Release()` exactamente una vez por adquisición exitosa. Las llaves y la indentación
siguen 3.2 y 3.4.

**Correcto**

```csharp
public async Task SendEventAsync(GameSessionClientEvent clientEvent,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(clientEvent);
    await _writeGate.WaitAsync(cancellationToken);
    try
    {
        await _call.RequestStream.WriteAsync(clientEvent, cancellationToken);
    }
    finally
    {
        _writeGate.Release();
    }
}
```

Las precondiciones se validan antes de adquirir el permiso. `WaitAsync` queda fuera del
`try`: si la espera se cancela antes de obtenerlo, no se entra al bloque y no se libera un
permiso ajeno. Si la operación protegida falla, se cancela o retorna, el `finally` devuelve
el permiso. Si se utiliza una sobrecarga de espera con tiempo límite que devuelve `bool`,
solo entrar a la sección protegida y ejecutar `Release()` cuando devuelva `true`.

**Incorrecto**

```csharp
await _writeGate.WaitAsync(cancellationToken);
await _call.RequestStream.WriteAsync(clientEvent, cancellationToken);
_writeGate.Release();
```

En el ejemplo incorrecto, una excepción durante el envío impide devolver el permiso y puede
dejar esperando a los demás envíos. Tampoco colocar `WaitAsync` dentro de un `try` cuyo
`finally` libere incondicionalmente: una cancelación durante la adquisición ejecutaría una
liberación sin permiso adquirido.

Se permite encapsular el permiso en un objeto `IDisposable`, como `AsyncOperationLease`,
consumido mediante `using`: el compilador garantiza el `finally` de liberación. El objeto
devuelve el permiso una sola vez, incluso si se llama a `Dispose()` repetidamente; no es
propietario del semáforo ni lo destruye. Su referencia al propietario puede ser mutable para
transferirla atómicamente al liberar el permiso. La cancelación previa a la adquisición no
crea un permiso que deba devolverse.

No usar `.Wait()` ni bloquear una tarea para adquirir el permiso en un flujo asíncrono. No
usar `await` dentro de `lock`; `lock` se reserva a secciones síncronas breves. No volver a
adquirir el mismo semáforo de capacidad uno desde una operación que ya conserva su permiso.

**Aplicación en la jugabilidad**

En `GrpcGameSessionStream`, el semáforo pertenece a la conexión y abarca toda la espera de
`WriteAsync`. Jugadas, rendición y chat utilizan el mismo camino de envío. Solo puede haber
una escritura pendiente por stream gRPC; liberar el permiso antes de esperar la escritura
permitiría solapar envíos. No depender de un orden FIFO del semáforo para validar turnos.

En `GameCoordinator`, las operaciones sobre una misma partida seleccionan el mismo semáforo
antes de cargar, validar y persistir su transición. El conjunto fijo de semáforos y su
asignación por `GameId` limitan la cantidad de objetos de sincronización; dos partidas pueden
compartir un semáforo y esperar entre sí. Esta protección se limita al proceso del servidor
y no sustituye los controles de concurrencia y unicidad de la base de datos.

La serialización de una transición puede abarcar operaciones asíncronas de persistencia
necesarias para mantenerla consistente. Los `lock` breves que protegen estructuras en memoria
no incluyen escritura de red. En el envío del servidor, `GameSessionGrpcService` consume la
cola de eventos desde una única tarea y espera cada escritura antes de iniciar la siguiente;
ese stream no necesita otro semáforo mientras conserve un único escritor.

`Release()` devuelve un permiso; `Dispose()` termina la vida del semáforo. El propietario
deberá coordinar su cierre según 6.7 y ejecutar `Dispose()` cuando las operaciones que lo
utilizan hayan terminado, sin competir con esperas, escrituras o liberaciones pendientes.

El cierre rechaza nuevas entradas y registra tanto las operaciones activas como las que
ya esperan un permiso. Una espera cancelada deja de contar sin ejecutar `Release()`. Cada
operación admitida deja de contar después de devolver su permiso. Si el cierre síncrono
comienza con operaciones pendientes, difiere la destrucción hasta que termine la última;
el cierre mediante `IAsyncDisposable` espera esa finalización sin bloquear el hilo. El cierre
repetido no vuelve a destruir los recursos. En conexiones gRPC se cierra la llamada para
interrumpir el transporte antes de esperar que terminen sus envíos.

<a id="seccion-4-8"></a>

### 4.8 DRY: una implementación por regla

Mantener una fuente autoritativa para cada regla o cálculo realmente compartido. Extraer
implementación común cuando significado, comportamiento y motivos de cambio coincidan.
La semejanza textual por sí sola no obliga a unificar conceptos distintos.

Se permiten validaciones preventivas de interfaz y autoritativas de servidor; no eliminar
la segunda para evitar repetición. También son válidos mapeos entre mensajes y dominio que
mantienen independencia de capas. Preferir reutilizar un componente existente antes de
crear un helper genérico que agrupe operaciones no relacionadas.

**Correcto:** compartir construcción de cabeceras en la política de transporte y conservar
la autorización en cada operación del servidor.

**Incorrecto:** copiar la misma política de plazo en cada adaptador o retirar la validación
del servidor porque el cliente ya comprobó el dato.

<a id="seccion-4-9"></a>

### 4.9 Simplicidad y funcionalidad necesaria: KISS y YAGNI

Elegir la solución más simple que cumpla los requisitos actuales con responsabilidades
claras. Una nueva clase, interfaz, capa, opción o punto de extensión necesita una función
presente y explicable. No añadirlos solo para un uso hipotético o una futura implementación
que aún no existe. Una abstracción puede justificarse por aislamiento y pruebas, sin exigir
dos implementaciones de producción.

Conservar abstracciones útiles que separan red, persistencia y presentación. Revisar
consumidores de C#, XAML, DI y herramientas antes de eliminar un tipo o miembro aparentemente
sin referencias. Las capas de paso requieren un propósito; no se eliminan automáticamente
si forman una frontera de aplicación legítima.

**Correcto:** reutilizar el emisor de correo para recuperar contraseña y enviar invitaciones,
manteniendo el contenido y los permisos propios de cada caso de uso.

**Incorrecto:** crear proveedores y opciones para canales de notificación no solicitados.

<a id="seccion-4-10"></a>

### 4.10 Inyección de dependencias y ciclos de vida

Registrar los servicios del servidor en su composición de arranque o registro cohesivo.
Elegir ciclo de vida según estado y dependencias: un singleton puede poseer un registro
compartido sincronizado; un servicio scoped puede utilizar un contexto de la operación;
un transient se utiliza cuando se necesita una instancia independiente de corta duración.

No retener desde un singleton una instancia scoped de `DbContext` ni compartirla entre
operaciones concurrentes. Los servicios de fondo crean y liberan scopes para su trabajo
cuando necesitan dependencias scoped. Una llamada de streaming puede durar más que una
operación unaria; no asumir que esa duración vuelve seguro paralelizar el uso de su contexto.

El contenedor libera los recursos que posee. El código libera los scopes y objetos que crea
y posee; no destruye una dependencia compartida recibida sin ese contrato. Se permite
construcción explícita de objetos de corta duración y componentes del cliente cuando su
propiedad está clara. No imponer otro contenedor ni localizar servicios desde cualquier clase.

**Correcto:** un servicio de reloj recibe `IServiceScopeFactory`, obtiene un contexto dentro
de un scope de trabajo y termina ese scope al completar la operación.

**Incorrecto:** guardar un contexto scoped en un registro singleton para reutilizarlo en
todas las solicitudes.

<a id="seccion-4-11"></a>

### 4.11 Bloqueos síncronos

Para proteger estado síncrono compartido, conservar una referencia privada, estable y
dedicada como identidad del bloqueo. El patrón del proyecto utiliza `private readonly
object` y un nombre que, junto al contexto, permita reconocer el estado protegido.

```csharp
private readonly object _stateLock = new object();
```

La misma instancia debe coordinar las operaciones que preservan una invariante. No bloquear
sobre `this`, cadenas o tipos accesibles a otros componentes. Mantener breve el bloque y
evitar I/O de red dentro de él. No usar `await` ni suspender una enumeración con `yield`
mientras se conserva ese bloqueo; para exclusión asíncrona utilizar 4.7.

Los bloqueos anidados requieren un orden consistente y una razón clara. No añadir un
segundo bloqueo sin comprobar la coordinación existente. No se exige migrar los objetos de
sincronización a `System.Threading.Lock`; esa alternativa está disponible para el entorno,
pero su adopción requiere una decisión y revisión propias.

**Correcto:** modificar la colección y obtener su instantánea bajo `_stateLock`, liberarlo
y después realizar la llamada de red.

**Incorrecto:** usar `lock (this)` o crear `new object()` como bloqueo dentro de cada llamada,
porque las operaciones no comparten una protección privada y estable.

<a id="seccion-4-12"></a>

### 4.12 Operaciones atómicas

Utilizar `Interlocked` cuando la operación necesita actualizar y observar un campo de forma
atómica. Una secuencia de actualizaciones que mantiene una invariante entre varios datos
requiere la coordinación correspondiente; un contador atómico no protege todo el objeto.

La retirada atómica de una referencia permite que un permiso se devuelva una sola vez.
La referencia del propietario puede ser mutable por esa razón; no sustituirla por `readonly`
si con ello se pierde la garantía de cierre idempotente.

**Correcto**

```csharp
Interlocked.Exchange(ref _owner, null)?.Exit(_gate);
```

Solo quien obtiene el propietario anterior puede devolver ese permiso.

**Incorrecto**

```csharp
if (_owner is not null)
{
    _owner.Exit(_gate);
    _owner = null;
}
```

Dos llamadas concurrentes pueden observar la misma referencia y liberar dos veces.

<a id="seccion-4-13"></a>

### 4.13 Colecciones concurrentes

Se permiten colecciones concurrentes para operaciones independientes realizadas por varios
hilos y colecciones ordinarias protegidas por un bloqueo para transiciones compuestas.
La elección depende de qué regla debe mantenerse; no se reemplazan todos los diccionarios
por `ConcurrentDictionary`.

Una operación segura del diccionario no vuelve seguros sus valores mutables ni hace atómica
la secuencia consultar, validar y actualizar. Coordinar esa secuencia cuando preserve una
invariante. La enumeración concurrente puede ser válida sin representar una instantánea
transaccional; no prometer una foto global coherente si el mecanismo no la proporciona.

**Correcto:** `TryAdd` para un alta independiente, o un bloqueo común para reemplazar una
conexión y actualizar el estado relacionado como una transición.

**Incorrecto:** comprobar `ContainsKey` y después escribir suponiendo que nadie puede
intervenir entre ambas operaciones porque el diccionario es concurrente.

<a id="seccion-4-14"></a>

### 4.14 Registros y colas en memoria

Cada registro o cola compartida debe tener propietario y una política comprensible de
vida y limpieza. Distinguir estado temporal, persistencia y lo que se pierde al reiniciar.
Conservar los límites y condiciones de saturación definidos por cada componente; no usar
una cifra global para todas las estructuras.

Cuando una cola está acotada, documentar si espera, rechaza o termina la conexión al llenarse.
Un registro con vencimiento debe explicar cuándo elimina entradas. Un vencimiento limita
duración y no demuestra por sí mismo una cantidad máxima de altas durante ese intervalo.
La descripción no atribuirá capacidad limitada a una estructura que no la implementa.

Las mejoras de capacidad adicionales se evalúan y verifican en su componente antes de
afirmar su garantía. Esta regla no introduce un máximo universal nuevo sobre registros
existentes ni autoriza borrar entradas vigentes para hacer espacio sin un contrato definido.

**Correcto:** una cola de eventos mantiene su capacidad explícita y cierra de forma controlada
un consumidor que no admite más eventos conforme a su política.

**Incorrecto:** presentar un registro con TTL como si ese TTL garantizara un máximo de memoria,
o descartar silenciosamente acciones aceptadas para reducir su tamaño.

<a id="seccion-5"></a>

## 5 Contratos y comunicación gRPC

La frontera entre cliente y servidor se define mediante `.proto`. Los servicios y mensajes
se generan con las herramientas del proyecto; no se escriben manualmente sustitutos de esas
clases generadas. Las reglas específicas de nombres están en 10.3.

Los adaptadores gRPC autentican, traducen y delegan; las decisiones de dominio y persistencia
conservan sus responsabilidades. Las subsecciones siguientes precisan la frontera sin
imponer una arquitectura nueva de repositorios, envolturas o interceptores.

<a id="seccion-5-1"></a>

### 5.1 Diseño y nombres de contratos

Cada solicitud, respuesta o evento representa una consulta, acción o notificación clara.
Los mensajes no exponen entidades EF, objetos rastreados ni ViewModels. Se permiten comandos
y resultados internos que separen aplicación y transporte aunque algunos campos coincidan.
Evitar copias intermedias que no aporten validación, independencia o una responsabilidad propia.

**Correcto:** convertir una solicitud Protobuf a un comando del caso de uso antes de invocar
la aplicación y traducir su resultado a la respuesta.

**Incorrecto:** devolver una entidad EF o hacer que un servicio de dominio consulte controles
de WPF para resolver una operación.

<a id="seccion-5-2"></a>

### 5.2 Presencia y valores predeterminados

Definir el significado de cero, cadena vacía, ausencia y variante de `oneof` en cada contrato.
En escalares proto3 con presencia implícita, el valor predeterminado no permite distinguir
por sí solo entre un campo omitido y ese mismo valor enviado explícitamente. Usar presencia
explícita o `oneof` cuando esa distinción sea necesaria para una operación nueva o modificada.
No añadir `optional` a todos los campos existentes sin una necesidad y revisión de compatibilidad.

Los enums Protobuf conservan su valor inicial no especificado. Los adaptadores tratan valores
desconocidos de forma controlada según la operación. Una variante desconocida no debe
interpretarse automáticamente como una autorización o una acción exitosa.

**Correcto:** comprobar `EventCase` para identificar una jugada, rendición o mensaje de chat.

**Incorrecto:** suponer que un mensaje sin variante seleccionada representa una jugada válida
en la posición predeterminada del tablero.

<a id="seccion-5-3"></a>

### 5.3 Compatibilidad y evolución

Conservar números y significado de campos publicados. Al eliminar campos, reservar sus
números y nombres para impedir reutilización accidental. Revisar tipos, valores de enums,
paquetes y nombres de servicios u operaciones antes de modificar un contrato publicado.

Agregar un campo no elimina la necesidad de definir qué hace un cliente que no lo recibe.
Un cambio debe considerar tanto compatibilidad binaria como comportamiento. No renumerar
contratos ni crear una versión nueva completa por una preferencia de nombres.

**Correcto**

```proto
message ExampleRequest {
  reserved 2;
  reserved "legacy_name";
  string identifier = 1;
  string culture_name = 3;
}
```

**Incorrecto:** asignar el número `2` eliminado a una nueva propiedad con otro significado.

<a id="seccion-5-4"></a>

### 5.4 Resultados de negocio y errores

Los rechazos previstos se representan con el resultado o estado definido por la operación.
Se permiten resultados de negocio dentro de una respuesta y estados gRPC controlados en el
límite de transporte. No se obliga a sustituir todas las respuestas por una envoltura única.

Distinguir resultado de negocio, cancelación, vencimiento y fallo técnico. No presentar un
fallo de SQL como una validación inválida ni mostrar el detalle de una excepción remota al
jugador. Los ViewModels reciben resultados o motivos de aplicación, sin depender directamente
de `RpcException`. El adaptador cliente realiza esa traducción.

Las excepciones inesperadas se registran con contexto seguro en el límite responsable y
se convierten en un fallo controlado. No enviar consultas SQL, rutas, credenciales o pilas
de llamadas para conservar la causa. No se exige un interceptor nuevo si el adaptador ya
delimita adecuadamente la operación.

**Correcto:** una verificación devuelve `InvalidCode`; un fallo técnico informa un resultado
no confirmado o un estado gRPC controlado, conforme al contrato.

**Incorrecto:** capturar cualquier excepción y devolver éxito, lista vacía o un supuesto
rechazo de negocio para esconder el problema.

<a id="seccion-5-5"></a>

### 5.5 Llamadas unarias, plazos y cancelación

Las llamadas unarias utilizan la política compartida de plazo finito. El adaptador propaga
la cancelación del propietario; el servidor utiliza la cancelación del contexto en las
operaciones que la admitan. La autenticación pertenece a cada sesión y llamada; el canal
se reutiliza durante la vida del proceso cliente.

Una cancelación local o un plazo vencido no prueban que el servidor haya revertido un
cambio ya iniciado. Los reintentos de escrituras necesitan una garantía de idempotencia.
Las consultas solo se reintentan según una política explícita; evitar bucles inmediatos
sin cancelación ni control de frecuencia. No cambiar todos los plazos para uniformar
operaciones que tienen necesidades distintas.

**Correcto:** configurar opciones mediante `GrpcCallPolicy` y distinguir el resultado no
confirmado de un rechazo confirmado.

**Incorrecto:** repetir un registro o una jugada automáticamente al vencer el plazo sin
comprobar si la primera solicitud produjo el cambio.

<a id="seccion-5-6"></a>

### 5.6 Streams, dúplex y presencia

La presencia utiliza comunicación dúplex; no se implementa heartbeat de aplicación. El
cliente abre la conexión con el evento inicial previsto por el contrato y recibe eventos
del servidor. Una caída de red se reconoce cuando el transporte detecta la terminación;
no se promete detección instantánea.

El stream tiene un propietario y se cancela o cierra cuando termina su uso. No aplicar a toda
su vida el plazo breve de una llamada unaria. Conservar una única escritura pendiente por
stream según 4.7. Las colas de salida y su saturación siguen su política de entrega; un
cliente lento no debe provocar escrituras concurrentes sobre el mismo stream.

Coordinar las tareas de lectura y escritura: observar sus fallos, cancelar la compañera al
terminar y liberar los recursos sin dejar tareas sin propietario. La reconexión recupera
el estado vigente y descarta respuestas de conexiones sustituidas. Los temporizadores de
partida y limpieza no se consideran heartbeat porque no son sondeos periódicos de presencia.

**Correcto:** un stream de presencia permanece abierto hasta cancelación, cierre o fallo y
se libera al terminar su propietario.

**Incorrecto:** mantener un stream de una pantalla cerrada o ejecutar varias escrituras
simultáneas sobre la misma conexión.

<a id="seccion-5-7"></a>

### 5.7 Verificación de cambios de contrato

Los cambios de contrato deben verificar los casos relevantes de campos omitidos, valores
desconocidos y datos incompatibles, además del flujo válido. Añadir pruebas focalizadas en
el comportamiento del adaptador y sus consumidores cuando esa cobertura falte.

No se exige una prueba mecánica de serialización por cada mensaje si solo repite el trabajo
de Protobuf. Una prueba de compatibilidad debe comprobar una consecuencia observable, como
rechazar un evento sin acción reconocida o no interpretar un estado desconocido como éxito.

**Correcto:** procesar un valor de resultado desconocido y verificar que el cliente no inicia
una sesión ni conserva un token como si se hubiera autenticado.

**Incorrecto:** cambiar números de campos y dar por compatible el contrato porque ambos
proyectos compilan utilizando simultáneamente el archivo nuevo.

<a id="seccion-6"></a>

## 6 Manejo de Errores y Excepciones



<a id="seccion-6-1"></a>

### 6.1 Manejo General de Excepciones

Las excepciones deberán utilizarse para manejar condiciones inesperadas que impidan que un método u operación complete la funcionalidad para la que fue diseñada.

Un bloque try deberá contener únicamente el código que pueda generar razonablemente una excepción y para el cual exista una estrategia específica de recuperación o manejo.

Las excepciones sólo deberán capturarse cuando la aplicación pueda manejar la condición y dejar el programa en un estado conocido y válido. Si el método actual no puede manejar la excepción, deberá permitir que esta se propague hacia un nivel superior que pueda manejarla adecuadamente.

El manejo de excepciones no deberá utilizarse como sustituto del flujo normal de control del programa cuando una condición pueda comprobarse razonablemente antes de realizar una operación.

**Correcto**

```csharp
try
{
    LoadGameData();
}
catch (FileNotFoundException)
{
    CreateNewGameData();
}
```

**Incorrecto**

```csharp
try
{
    if (player.Health <= 0)
    {
        throw new Exception();
    }
}
catch (Exception)
{
    player.Health = 100;
}
```

<a id="seccion-6-2"></a>

### 6.2 Tipos de Excepciones

El proyecto deberá utilizar los tipos de excepciones predefinidos de .NET siempre que exista un tipo apropiado para representar la condición.

El tipo de excepción deberá representar la condición que provocó el fallo. Entre los tipos de excepciones más comunes se encuentran:

- ArgumentException para argumentos no válidos.

- ArgumentNullException para argumentos que inesperadamente contienen un valor nulo.

- ArgumentOutOfRangeException para argumentos que se encuentran fuera del rango permitido.

- InvalidOperationException cuando una operación no es válida debido al estado actual de un objeto.

- NotSupportedException cuando una operación no es compatible o no está soportada.

- IOException para errores relacionados con operaciones de entrada y salida.

Los tipos de excepciones personalizados solo deberán crearse cuando un tipo de excepción existente de .NET no represente adecuadamente la condición.

**Correcto**

```csharp
if (damage < 0)
{
    throw new ArgumentOutOfRangeException(nameof(damage));
}
```

**Incorrecto**

```csharp
if (damage < 0)
{
    throw new Exception("Damage cannot be negative.");
}
```

<a id="seccion-6-3"></a>

### 6.3 Propagación de Excepciones

Las excepciones deberán permitirse que se propaguen cuando el método actual no pueda manejarlas adecuadamente.

Cuando una excepción vuelva a lanzarse desde su bloque catch, deberá utilizarse la instrucción throw sin un objeto de excepción para conservar la pila de llamadas original.

Una excepción no deberá capturarse y descartarse silenciosamente únicamente para evitar que la aplicación muestre un error.

Cuando sea necesario agregar información adicional al error, podrá lanzarse una nueva excepción proporcionando la excepción original como excepción interna.

**Correcto**

```csharp
try
{
    SaveGame();
}
catch (IOException)
{
    LogSaveError();
    throw;
}
```

**Incorrecto**

```csharp
try
{
    SaveGame();
}
catch (IOException ex)
{
    LogSaveError();
    throw ex;
}
```

Conservar la excepción interna se aplica a excepciones .NET que envuelven otra excepción
dentro del proceso. Traducir un error conocido a un estado gRPC no obliga a serializar la
excepción original ni a enviar su mensaje interno al cliente. Registrar contexto seguro en
el límite apropiado y enviar un estado y mensaje controlados. No exponer consultas SQL,
cadenas de conexión ni detalles privados para preservar la causa.

<a id="seccion-6-4"></a>

### 6.4 Límites de Manejo de Excepciones

El manejo de excepciones deberá realizarse en el nivel en el que la aplicación disponga de suficiente información para recuperarse del error, comunicar un resultado apropiado o finalizar de forma segura la operación afectada.

Los métodos de niveles inferiores no deberán capturar excepciones únicamente para evitar que se propaguen hacia componentes de niveles superiores.

Los límites de manejo de excepciones deberán establecerse alrededor de operaciones que representen unidades significativas del comportamiento de la aplicación, como la carga de recursos del videojuego, el guardado de datos de partida, el procesamiento de archivos externos u otras operaciones que puedan fallar de manera independiente.

Un método que no pueda recuperarse de una excepción deberá permitir normalmente que esta se propague hacia un nivel superior.

**Correcto**

```csharp
public void LoadGame()
{
    try
    {
        gameDataService.Load();
    }
    catch (FileNotFoundException)
    {
        CreateNewGame();
    }
}
```

**Incorrecto**

```csharp
public void LoadGameData()
{
    try
    {
        ReadSaveFile();
    }
    catch (Exception)
    {
    }
}
```

<a id="seccion-6-5"></a>

### 6.5 Excepciones Personalizadas

Los tipos de excepciones personalizados solo deberán crearse cuando un tipo de excepción existente de .NET no represente adecuadamente un error específico de la aplicación.

Las excepciones personalizadas deberán derivar de System.Exception o de un tipo de excepción derivado apropiado.

Las clases de excepciones personalizadas deberán utilizar un nombre significativo que termine en Exception.

Cuando sea necesaria una excepción personalizada, esta deberá proporcionar constructores apropiados para crear la excepción con un mensaje y, cuando corresponda, con una excepción interna.

**Correcto**

```csharp
public class SaveGameException : Exception
{
    public SaveGameException()
    {
    }

    public SaveGameException(string message)
        : base(message)
    {
    }
    public SaveGameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

**Incorrecto**

```csharp
public class SaveError : Exception
{
}
```

<a id="seccion-6-6"></a>

### 6.6 Manejo de Estados No Válidos

Las operaciones deberán validar el estado de los objetos cuando una operación no pueda realizarse correctamente debido a su estado actual.

Deberá utilizarse InvalidOperationException cuando un método u operación no sea válido debido al estado actual del objeto.

Las condiciones que formen parte del comportamiento normal de la aplicación deberán comprobarse preferentemente de forma explícita, en lugar de implementarse mediante excepciones.

**Correcto**

```csharp
if (!game.IsPaused)
{
    throw new InvalidOperationException(
        "The game must be paused before saving.");
}
```

**Incorrecto**

```csharp
try
{
    game.Save();
}
catch (Exception)
{
    // The game was not paused.
}
```

<a id="seccion-6-7"></a>

### 6.7 Liberación de Recursos

Los recursos que requieran una liberación explícita deberán liberarse de manera confiable cuando finalice una operación, independientemente de que esta se complete correctamente o se produzca una excepción.

La instrucción o declaración using deberá utilizarse preferentemente para los objetos que implementen IDisposable.

Deberá utilizarse un bloque finally cuando la liberación del recurso no pueda gestionarse adecuadamente mediante using.

La liberación de recursos no deberá depender de que el código que adquirió el recurso se complete correctamente.

**Correcto**

```csharp
using (FileStream file = File.OpenRead(savePath))
{
    LoadGameData(file);
}
```

**Incorrecto**

```csharp
var file = File.OpenRead(savePath);
try
{
    LoadGameData(file);
}
catch (IOException)
{
    LogError();
}
```

Usar `await using` cuando el recurso requiere liberación asíncrona. Cada canal, suscripción,
`CancellationTokenSource` y monitor debe tener un propietario y un final de vida definidos.
Una suscripción anónima no se puede deshacer creando otra lambda que se vea igual.

<a id="seccion-6-8"></a>

### 6.8 Prácticas Prohibidas de Manejo de Excepciones

Las siguientes prácticas no deberán utilizarse en el proyecto:

- Capturar System.Exception cuando la excepción no pueda manejarse de manera significativa.

- Utilizar bloques catch vacíos.

- Descartar excepciones silenciosamente.

- Utilizar excepciones como flujo de control normal para condiciones esperadas.

- Volver a lanzar una excepción utilizando throw ex; cuando throw; sea suficiente.

- Lanzar una excepción genérica Exception cuando exista un tipo de excepción predefinido más apropiado.

- Lanzar excepciones desde bloques finally.

- Utilizar excepciones para compensar una validación o gestión de estados inadecuada.

- Capturar una excepción y reemplazarla por otra menos informativa sin conservar la excepción original como excepción interna.

- Capturar una excepción únicamente para registrarla y continuar la ejecución cuando la aplicación no pueda continuar de manera segura.

**Correcto**

```csharp
try
{
    LoadGameData();
}
catch (IOException ex)
{
    LogError(ex);
    throw;
}
```

**Incorrecto**

```csharp
try
{
    LoadGameData();
}
catch (Exception)
{
}
```

<a id="seccion-6-9"></a>

### 6.9 Nulos, ausencia y resultados explícitos

Una ausencia válida en una búsqueda singular puede expresarse con `T?`. Una consulta válida
sin elementos devuelve una colección vacía. Una dependencia obligatoria ausente se rechaza
según 6.11. Un fallo de red o una sesión inválida no deben confundirse con una búsqueda vacía.

Declarar la nulabilidad conforme al contrato y mantener el análisis nullable habilitado.
`?.` es válido para referencias realmente opcionales cuando omitir la operación es seguro.
`??` puede aportar un valor alternativo válido; no debe ocultar un estado requerido ausente.
`!` necesita una invariante conocida, como la materialización controlada de una navegación
de EF; no reemplaza validar un dato externo.

No se exige crear Null Objects para evitar todos los `null`. Utilizar un resultado explícito
si hay estados distintos que no pueden distinguirse mediante el contrato existente. Revisar
los llamadores antes de cambiar el significado de ausencia o el tipo de retorno.

**Correcto**

```csharp
public Player? FindPlayer(string userName)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(userName);
    return _players.FirstOrDefault(player => player.UserName == userName);
}
```

El tipo hace explícita la ausencia legítima de un jugador.

**Incorrecto**

```csharp
catch (RpcException)
{
    return Array.Empty<FriendPresenceEntry>();
}
```

El código presenta un fallo de transporte como una consulta exitosa sin amigos.

<a id="seccion-6-10"></a>

### 6.10 Uso de Bitácoras y Logs

Registrar los eventos significativos y fallos técnicos en el límite que conoce su contexto.
Una captura que convierte un rechazo previsto no obliga a registrar cada capa ni cada
validación rutinaria. No utilizar `Console.WriteLine` como diagnóstico de clases de
producción. Mantener los mecanismos y niveles descritos a continuación.

**Correcto**

```csharp
try
{
    AuthenticateUser(credentials);
    _logger.LogInformation("Player authentication completed successfully.");
}
catch (AuthenticationException exception)
{
    _logger.LogWarning("Authentication failed with {ErrorType}.",
        exception.GetType().Name);
    ShowAuthenticationFailure();
}
```

**Incorrecto**

```csharp
try
{
    AuthenticateUser(credentials);
    Console.WriteLine("User logged in");
}
catch (AuthenticationException ex)
{
    Console.WriteLine(ex.Message);
}
```

El servidor utiliza `ILogger<T>` y mensajes con plantillas de parámetros. Los diagnósticos
del cliente utilizan `Trace` con un listener de archivo iniciado por `App`. Se crea un archivo
por ejecución en `%LOCALAPPDATA%/AtariGo/logs`, con fecha y desplazamiento UTC-6 en cada evento.
Si no hay permiso o falla el archivo, el diagnóstico cae a `Debug` sin impedir el arranque.
El mecanismo actual no elimina automáticamente los archivos de ejecuciones anteriores.
`Trace` sin un listener configurado no debe presentarse como una bitácora persistida.

Registrar información suficiente para distinguir operación, estado y causa sin secretos.
No registrar contraseñas, tokens, claves privadas ni cadenas de conexión. Evitar registrar
la misma excepción en cada capa. La cancelación solicitada es un resultado esperado: no se
registra como error. Si se registra una excepción completa, revisar que no exponga datos
sensibles; se puede registrar tipo, código y contexto saneado en su lugar.

| Nivel | Criterio de uso |
| --- | --- |
| Trace / Debug | Detalle de desarrollo o diagnóstico fino; aplicar filtros según el entorno. |
| Information | Inicio, término o transición significativa de una operación o componente. |
| Warning | Condición anormal de la que la operación puede recuperarse de forma controlada. |
| Error | Fallo inesperado de una operación; el proceso puede seguir atendiendo otras. |
| Critical | Fallo que compromete el proceso o su capacidad de continuar de forma segura. |

Las plantillas técnicas se escriben en inglés. En `ILogger`, los marcadores nombrados
describen datos seguros, como `{GameId}` y `{Status}`; en `Trace` se conserva el formato
compatible con el listener. No se impone una migración del cliente a otra API de logging.
Los identificadores de correlación se aprovechan cuando están disponibles; no se exige
crear un protocolo nuevo de correlación para todas las operaciones.

No registrar solicitudes completas de autenticación ni el `ToString()` de un record que
pueda incluir contraseñas o códigos. La persistencia y retención de diagnósticos debe
describirse conforme a lo implementado, sin afirmar que la salida de consola se archiva.

<a id="seccion-6-11"></a>

### 6.11 Cláusulas de guarda y validación de precondiciones

Los constructores y métodos públicos deberán validar sus precondiciones antes de utilizar
los parámetros, modificar el estado del objeto o iniciar operaciones externas. Si una entrada
no cumple el contrato, la ejecución deberá detenerse mediante una excepción específica.

- Utilizar el tipo de excepción adecuado, como `ArgumentNullException`, `ArgumentException`
  o `ArgumentOutOfRangeException`.
- Validar únicamente las condiciones definidas por el contrato. No rechazar un parámetro
  nullable u opcional cuando la operación permita que no tenga valor.
- En constructores que deleguen en otro mediante `: this(...)`, centralizar las validaciones
  en el constructor que realiza la inicialización. El constructor delegador puede limitarse
  a reenviar los parámetros.
- No repetir validaciones en métodos privados cuando el método público que los invoca ya
  garantice esas condiciones, salvo que el método privado tenga un contrato independiente.
- En operaciones de dominio y servicios gRPC, representar los errores según el contrato de
  la operación. No propagar directamente excepciones de argumentos de .NET al cliente si
  corresponde devolver un resultado o un estado gRPC, como `InvalidArgument`.

**Correcto**

```csharp
public GrpcMatchmakingClient(
    string? sessionToken,
    string? guestCredential,
    CallInvoker callInvoker)
{
    ArgumentNullException.ThrowIfNull(callInvoker);
    if (!string.IsNullOrWhiteSpace(sessionToken)
        && !string.IsNullOrWhiteSpace(guestCredential))
    {
        throw new ArgumentException(
            "Only one matchmaking credential can be configured.");
    }
    _sessionToken = sessionToken;
    _guestCredential = guestCredential;
    _client = new Matchmaking.MatchmakingClient(callInvoker);
}
```

**Incorrecto**

```csharp
public GrpcMatchmakingClient(
    string? sessionToken,
    string? guestCredential,
    CallInvoker callInvoker)
{
    _client = new Matchmaking.MatchmakingClient(callInvoker);
    _sessionToken = sessionToken;
    _guestCredential = guestCredential;
    if (!string.IsNullOrWhiteSpace(sessionToken)
        && !string.IsNullOrWhiteSpace(guestCredential))
    {
        throw new ArgumentException(
            "Only one matchmaking credential can be configured.");
    }
}
```

Las invariantes internas y los datos externos son condiciones distintas. Si se utiliza
`Debug.Assert`, su función es comprobar una suposición de desarrollo; no sustituye validar
permisos, códigos, rangos o datos recibidos por red. No se exige incorporar aserciones a
todos los métodos. Los datos pasivos pueden validar sus reglas en la frontera responsable
sin duplicar una misma comprobación en cada miembro que solo los representa.

<a id="seccion-6-12"></a>

### 6.12 Fallos no controlados y tareas de fondo

Cada operación remota, comando asíncrono y tarea de fondo debe tener un límite responsable
de observar sus fallos y determinar si termina, reintenta o deja de prestar la función.
Los servicios de reloj y limpieza no están protegidos por el manejo de errores de una
llamada gRPC independiente; mantienen su propio límite.

Una cancelación solicitada por el propietario termina de forma esperada. Un fallo técnico
se informa sin exponer información privada y sin continuar sobre un estado inválido.
No capturar globalmente todos los errores para permitir que la interfaz siga ejecutándose
como si nada hubiera ocurrido. Los manejadores de último recurso no sustituyen el tratamiento
de una operación conocida ni garantizan recuperación.

No se exige una clase nueva de interceptor ni instalar todos los hooks de excepciones del
runtime. Se añaden mecanismos cuando resuelven un caso demostrado y su alcance está definido.

**Correcto:** el servicio de reloj registra una operación fallida y conserva una política
explícita para el siguiente ciclo; la cancelación del servidor termina el bucle.

**Incorrecto:** iniciar una tarea de fondo sin propietario y descartar su excepción para
mantener artificialmente el proceso en ejecución.

<a id="seccion-7"></a>

## 7 Comentarios y documentación



<a id="seccion-7-1"></a>

### 7.1 Política de comentarios

El código debe expresar su intención mediante nombres y responsabilidades claros. Está
prohibido añadir comentarios al código de producción salvo autorización explícita para
un comentario específico. 

Los comentarios de pruebas están permitidos y siguen 10.4. Se conserva la excepción sin
comentarios del módulo MFA y sus pruebas relacionadas de autenticación, credenciales,
correo de autenticación, recuperación y protección de códigos. La excepción no se extiende
a cualquier otra prueba por utilizar un servicio que también intervenga en MFA.

Los ejemplos de comentarios de este capítulo describen formato para contextos autorizados
o de pruebas. No autorizan incorporarlos automáticamente en producción.

<a id="seccion-7-2"></a>

### 7.2 Comentarios de una línea

Cuando estén permitidos, usar `//` en una línea independiente, redactar en inglés y explicar
una intención no evidente. Una oración comienza con mayúscula y termina con punto. Los
marcadores estructurales `// Arrange`, `// Act` y `// Assert` no requieren punto.

**Correcto en una prueba fuera de la excepción de 9.4**

```csharp
// Arrange
var position = new BoardPosition(0, 0);
```

**Incorrecto**

```csharp
int count = 0; // set count to zero
```

El comentario está al final y repite una instrucción evidente.

<a id="seccion-7-3"></a>

### 7.3 Comentarios de bloque y código deshabilitado

Los comentarios `/* ... */` solo se utilizan en contextos autorizados cuando una explicación
extensa los justifica. No son el formato habitual ni un modo de conservar código muerto.
El código obsoleto se elimina; su historial permanece en Git.

**Correcto:** retirar una implementación sustituida después de comprobar sus consumidores.

**Incorrecto:** conservar métodos completos deshabilitados dentro de un comentario de bloque
con la intención de que otro integrante descubra cuál implementación debe utilizar.

<a id="seccion-7-4"></a>

### 7.4 Documentación de decisiones y contratos

Las decisiones relevantes de concurrencia, seguridad, compatibilidad y propiedad de recursos
se documentan cuando su motivo no es evidente. Utilizar documentación del proyecto para
explicar restricciones y contratos sin añadir comentarios de producción no autorizados.
Un identificador público no necesita XML que solo repita su nombre.

Si se autoriza documentación XML para una frontera concreta, las etiquetas deben coincidir
con su firma y describir entradas, resultados, efectos y fallos pertinentes. No se exige
XML en todos los miembros públicos, pruebas o implementaciones internas.

**Correcto:** documentar por qué un semáforo se conserva durante la transición persistente
y qué operaciones utilizan el mismo permiso.

**Incorrecto:** describir únicamente que `GetRoom` obtiene una sala, sin aportar contrato
ni una restricción que no pueda conocerse por su declaración.

<a id="seccion-7-5"></a>

### 7.5 Pendientes TODO

Cuando un contexto admita comentarios, un TODO utiliza `// TODO:` y una acción concreta
en inglés. Los trabajos de producción pendientes se registran en el seguimiento del proyecto
o documentación autorizada; un TODO no es una excepción a la prohibición de 7.1.

**Correcto en un contexto autorizado:** `// TODO: Add the expired-code boundary case.`

**Incorrecto:** `// TODO: Finish later.`

La nota debe permitir identificar el trabajo, y no sustituye asignación o planificación.

<a id="seccion-7-6"></a>

### 7.6 Defectos FIXME

Cuando esté permitido, utilizar `// FIXME:` para un defecto conocido con su condición de
activación. Registrar su seguimiento cuando necesite planificación. Una nota no autoriza
mantener un fallo indefinidamente ni sustituye corregirlo en el alcance de una tarea.

**Correcto en un contexto autorizado:** describir que un resultado tardío actualiza una
pantalla cerrada e identificar la condición que lo reproduce.

**Incorrecto:** `// FIXME: Broken.`

No añadir estos comentarios al código de producción sin la autorización prevista en 7.1.

<a id="seccion-7-7"></a>

### 7.7 Restricciones y mantenimiento de documentación

Los comentarios y documentos no deben contener credenciales ni datos privados, contradecir
la implementación, justificar código innecesario o narrar instrucciones evidentes.
Actualizar la explicación cuando cambien las condiciones que documenta. Una regla pendiente
se mantiene fuera de las obligaciones vigentes conforme a 11.4.

**Correcto:** una explicación del contrato indica que cancelar una llamada no prueba que
la escritura remota haya sido revertida.

**Incorrecto:** una explicación garantiza reversión automática o detección instantánea de
una desconexión sin que el sistema implemente esas garantías.

<a id="seccion-8"></a>

## 8 Control de Versiones Git



<a id="seccion-8-1"></a>

### 8.1 Mensajes de Commit

Los mensajes de commit se escriben en inglés, comienzan con un verbo en imperativo con
inicial mayúscula y describen el cambio concreto realizado. Usar frases directas como
`Implement`, `Add`, `Fix`, `Update`, `Remove` o `Refactor` seguidas del objeto del cambio.

No utilizar prefijos de Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`,
`chore:`, `test:`, ni variantes con alcance como `feat(network):`. No confundir el verbo
`Refactor` dentro de una frase con el prefijo `refactor:`.

**Ejemplos correctos**

```text
Setup EF Core structure and add authentication support
Implement user registration
Implement top wins section
Update coding standard and document repository audit
Refactor navigation logic into INavigationService
```

**Ejemplos incorrectos**

```text
feat(network): configure shared HTTPS gRPC transport
refactor: extract navigation logic
fix: resolve connection failure
docs: update coding standard
```

La instrucción explícita del responsable prevalece sobre capturas o commits históricos que
usen otro formato. El ejemplo de la captura que comienza con `refactor:` no autoriza ese
prefijo. No reescribir el historial publicado para corregir nombres sin una solicitud
específica. No hacer commit ni push sin que el usuario lo solicite para la tarea vigente.

<a id="seccion-8-2"></a>

### 8.2 Archivo Gitignore

Utilizar un archivo `.gitignore` adecuado para evitar subir carpetas temporales o de compilación.

<a id="seccion-9"></a>

## 9 Pruebas y Aseguramiento de Calidad



<a id="seccion-9-1"></a>

### 9.1 Estrategia de Pruebas

La estrategia de pruebas del proyecto se centrará en garantizar la estabilidad de la lógica central y la comunicación del sistema. Se priorizará la automatización de pruebas sobre los componentes críticos, separando claramente la evaluación de la lógica aislada y la interacción entre múltiples módulos.

<a id="seccion-9-2"></a>

### 9.2 Pruebas Unitarias

Las pruebas unitarias deberán enfocarse en validar el comportamiento de un único componente, método o clase de forma aislada, sin depender de bases de datos, redes o sistemas de archivos. Toda la lógica de reglas (por ejemplo, validaciones de turnos o condiciones de victoria) debe estar cubierta por pruebas unitarias.

Las pruebas utilizan MSTest y las dependencias declaradas en sus proyectos. Utilizar
`[TestClass]` y `[TestMethod]` para su descubrimiento; los datos parametrizados representan
escenarios explícitos. Las pruebas asíncronas devuelven tareas, se esperan y conservan el
sufijo `Async`; no se implementan como `async void` ni como verificadores manuales ocultos
al ejecutor de pruebas.

<a id="seccion-9-3"></a>

### 9.3 Pruebas de Integración

Las pruebas de integración validarán que dos o más módulos interactúen correctamente entre sí. Se utilizarán para asegurar que la comunicación entre la lógica del juego y los servicios externos (como la base de datos o el sistema de emparejamiento) fluya sin pérdida de datos ni estados inválidos.

La frontera que la prueba afirma verificar debe ser real: una prueba de persistencia no
reemplaza esa persistencia por un stub. Se permiten dobles de dependencias externas ajenas
a la frontera evaluada, como un emisor de correo controlado. Identificar si se verificó
SQLite, SQL Server, transporte local o una conexión real entre computadoras.

<a id="seccion-9-4"></a>

### 9.4 Estructura Arrange-Act-Assert

Las pruebas se organizan en preparar (Arrange), ejecutar (Act) y validar (Assert).
Fuera de la excepción sin comentarios definida en 7.1, esas fases se identifican con
`// Arrange`, `// Act` y `// Assert`. Las pruebas del módulo exceptuado conservan las
fases reconocibles mediante estructura y espacios, sin añadir esos marcadores.

- Arrange: Configura el estado inicial, inicializa variables y prepara los objetos necesarios.

- Act: Ejecuta la acción o el método específico que se va a probar.

- Assert: Verifica que el resultado obtenido sea el esperado.

**Correcto**

```csharp
public void PlaceStone_ValidMove_UpdatesBoardState()
{
    // Arrange
    var board = new GameBoard();
    var player = new Player(StoneColor.Black);
    var position = new Vector2Int(4, 4);
    // Act
    bool result = board.PlaceStone(player, position);
    // Assert
    Assert.IsTrue(result);
    Assert.AreEqual(StoneColor.Black, board.GetStoneAt(position));
}
```

**Incorrecto**

```csharp
public void TestPlaceStone()
{
    var board = new GameBoard();
    var player = new Player(StoneColor.Black);
    bool result = board.PlaceStone(player, new Vector2Int(4, 4));
    Assert.IsTrue(result);
    Assert.AreEqual(StoneColor.Black, board.GetStoneAt(new Vector2Int(4, 4)));
}
```

Cada prueba verifica un comportamiento identificable; puede incluir varias aserciones
sobre ese resultado. Una integración puede necesitar una secuencia como guardar y volver
a consultar. No imponer una única llamada literal ni extraer helpers artificiales para
ocultar pasos necesarios. Separar escenarios sin relación que dificulten localizar un fallo.

<a id="seccion-9-5"></a>

### 9.5 Categorías de pruebas

Las clases de pruebas nuevas o revisadas se marcan con `[TestCategory("Unit")]` cuando
no necesitan servicios externos y con `[TestCategory("Integration")]` cuando comprueban
transporte u otros límites reales. La ubicación de carpeta por sí sola no filtra la ejecución.
Las pruebas de presencia verifican dúplex y su ciclo de vida según 5.6.
La ejecución completa de la solución debe mantenerse además de cualquier filtro por categoría.

<a id="seccion-9-6"></a>

### 9.6 Criterios y casos de prueba

Las pruebas deberán derivarse de requisitos, reglas de negocio y riesgos identificados. Para
cada operación crítica se deberán considerar el caso válido, los datos inválidos y las
condiciones límite que puedan cambiar el resultado.

Las pruebas unitarias deberán aislar la lógica que se evalúa. Las pruebas de integración
deberán identificar expresamente los límites reales que verifican, como EF Core, SQL Server
o el transporte gRPC.

Las pruebas que utilicen una base de datos deberán definir cómo crean, aíslan y limpian sus
datos. No deberán depender accidentalmente de registros compartidos o del orden de ejecución.

Las pruebas no deberán afirmar que la comunicación entre computadoras funciona si solo se
verificó una conexión local. El informe de verificación deberá identificar el entorno y los
límites de la comprobación realizada.

Los nombres de pruebas seguirán la convención de la sección 2.19. Las pruebas podrán utilizar
datos literales representativos del escenario sin extraerlos a constantes, siempre que no
sean políticas compartidas del sistema.

**Correcto**

```csharp
public void ValidateMove_OccupiedPosition_RejectsMove()
{
    // Arrange
    GameState gameState = CreateGameWithOccupiedPosition();
    // Act
    MoveValidationResult result = _gameRules.ValidateMove(
        gameState,
        CreateMoveForOccupiedPosition());
    // Assert
    Assert.AreEqual(MoveValidationResult.Rejected, result);
}
```

**Incorrecto**

```csharp
public void TestMove()
{
    Assert.IsTrue(_gameRules.ValidateMove(_gameState, _move));
}
```

<a id="seccion-9-7"></a>

### 9.7 Determinismo y dobles de prueba

Controlar las dependencias que puedan cambiar el resultado de forma ajena al escenario.
Utilizar `TimeProvider` cuando el vencimiento o avance del tiempo sea lo que se prueba;
coordinar concurrencia mediante señales o transportes controlados cuando corresponda.
No depender de una pausa arbitraria para asegurar que otra tarea terminó.

Se permiten identificadores aleatorios cuando solo aportan unicidad y no afectan el
resultado esperado. Los datos literales del escenario no requieren constantes por rutina.
No enviar correo real ni utilizar credenciales reales como requisito de una prueba unitaria.

Los stubs devuelven datos predeterminados; los grabadores conservan interacciones observables;
los transportes controlados permiten ordenar eventos. Sus nombres deben indicar esa función
según 10.1. Mantener limpieza determinista de scopes, archivos y bases temporales.

**Correcto:** avanzar el reloj controlado hasta el vencimiento y comprobar el estado final.

**Incorrecto:** dormir varios segundos y asumir que el código venció o que el envío terminó
por el mero transcurso de tiempo de pared.

<a id="seccion-9-8"></a>

### 9.8 Verificación del estándar

Combinar compilación, formato, pruebas y revisión del comportamiento. `.editorconfig`
configura convenciones y sugerencias; no demuestra responsabilidad única ni verifica por
sí solo todas las reglas de este documento. Los analizadores adicionales se adoptan
explícitamente y sus métricas deben compararse con 4.3 antes de tratarlas como obligatorias.

Para cambios de código, utilizar desde la raíz de la solución:

```powershell
dotnet build .\AtariGo.slnx
dotnet test .\AtariGo.slnx --no-build
```

El cliente y sus pruebas necesitan Windows por WPF. Para cambios solo documentales,
verificar contenido, estructura, enlaces y diferencias; no se necesita ejecutar el juego.
Las herramientas y los informes utilizados deben identificar alcance, versión y limitaciones.
Los resultados locales no se presentan como una verificación entre computadoras.

Las fuentes generadas se excluyen de refactors manuales. Una herramienta no puede renombrar
contratos, eliminar bindings o ampliar dependencias solo para silenciar un aviso. Una
comprobación local opcional ignorada por Git no se convierte en dependencia obligatoria
para quien clone el repositorio.

<a id="seccion-10"></a>

## 10 Prefijos Permitidos y Abreviaciones

Esta sección define las convenciones específicas del repositorio. Las reglas del framework
se conservan cuando el nombre forma parte de un contrato externo.

<a id="seccion-10-1"></a>

### 10.1 Prefijos y sufijos de C#

| Elemento | Convención | Ejemplo |
| --- | --- | --- |
| Interfaz | `I` más nombre en PascalCase | `IFriendService` |
| Parámetro genérico descriptivo | `T` más nombre | `TResponse` |
| Constante de cualquier visibilidad | PascalCase sin prefijo | `MaximumSearchLength` |
| Campo con `[ThreadStatic]` | `t_camelCase` | `t_currentContext` |
| Campo estático no constante, incluido readonly | camelCase sin prefijo | `sessionLifetime` |
| Campo privado o interno de instancia | `_camelCase` | `_friendClient` |
| Campo protegido de instancia | camelCase sin prefijo | `isInitialized` |
| Campo público de instancia | PascalCase | `MoveSpeed` |
| Propiedad, incluida estática | PascalCase | `ServerAddress` |
| Método asíncrono propio | PascalCase con `Async` | `SearchUsersAsync` |
| Excepción personalizada | Sufijo `Exception` | `SaveGameException` |
| Argumentos y delegado de evento | `EventArgs` y `EventHandler` | `SessionExpiredEventArgs` |
| Callback delegado propio | Sufijo `Callback` cuando sea un tipo declarado | `MoveCompletedCallback` |
| Clase de pruebas | Tipo probado más `Tests` | `GrpcCallPolicyTests` |

Prioridad para campos: `const`, después `[ThreadStatic]`, después `static` y después
visibilidad de instancia. El prefijo `s_` está prohibido. Una variable local
que recibe un delegado no obtiene automáticamente el sufijo `Callback`.

Los tipos auxiliares de pruebas utilizan nombres que describen su función. Se conservan
familias coherentes como `StubFriendClient`, `RecordingInterceptor`, transportes `Controlled…`
y `Failing…`, y sufijos `TestHost`, `TestDatabase` y `TestContext`. No todos estos tipos
son clases de pruebas y no necesitan terminar en `Tests`. Las verificaciones de una función
completa pueden nombrarse por esa función; su categoría identifica el alcance de integración.

<a id="seccion-10-2"></a>

### 10.2 Vistas ViewModels y comandos

| Elemento | Convención | Ejemplo |
| --- | --- | --- |
| Pantalla o UserControl | Nombre de función más `View` | `FriendSearchView` |
| Ventana principal | Nombre de función más `Window` | `MainWindow` |
| Diálogo | Nombre de función más `DialogView` | `RegisterDialogView` |
| ViewModel | Nombre correspondiente más `ViewModel` | `FriendSearchViewModel` |
| ViewModel base | Nombre que indica su rol de base | `ViewModelBase` |
| Propiedad de comando | Acción más `Command` | `SearchCommand` |
| Evento manejado en code-behind | `On` más acción y evento en PascalCase | `OnSubmitButtonClick` |

`App` conserva el nombre de la aplicación WPF. Las vistas basadas en `Window`, como
`LoginView`, pueden conservar el sufijo `View` si representan una pantalla y corresponden
a su ViewModel; no es necesario renombrarlas solo por su clase base. Las clases del servidor
no utilizan los sufijos `View` o `ViewModel`.

No usar prefijos húngaros `Txt`, `Btn`, `Lbl`, `chk` o `Reg` para controles. Cuando un control
necesite `x:Name`, utilizar PascalCase descriptivo y su rol o tipo como sufijo si aporta
claridad: `PasswordBox`, `NewPasswordBox`, `ConfirmationCodeBox`, `CheckMark`.
No crear nombres para todos los controles si no se referencian.

Los nombres de partes de plantilla que pertenecen a WPF, como `PART_ContentHost`, se
conservan exactamente. Una parte visual propia puede llamarse `CheckBorder`; renombrarla
requiere actualizar también `TargetName`, `ElementName`, bindings y code-behind.

<a id="seccion-10-3"></a>

### 10.3 Servicios datos y contratos

| Elemento | Convención | Ejemplo |
| --- | --- | --- |
| Adaptador gRPC del cliente | `Grpc` más función más `Client` | `GrpcFriendClient` |
| Servicio de transporte del servidor | Función más `GrpcService` | `FriendDirectoryGrpcService` |
| Servicio de aplicación | Función más `Service` | `FriendService` |
| Mapeo EF | Entidad más `EntityConfiguration` | `UserEntityConfiguration` |
| Comando de caso de uso | Función más `Command` | `RegisterAccountCommand` |
| Datos de navegación | Función más `NavigationContext` | `PrivateRoomNavigationContext` |
| Solicitud y respuesta de transporte | Operación más `Request` o `Response` | `SearchUsersResponse` |
| Resultado de negocio | Nombre que indica resultado o estado | `FriendRequestResult` |

`SendFriendRequestRequest` repite `Request` porque la operación es enviar una solicitud
social y el mensaje es una solicitud de transporte. Es válido mientras no exista un nombre
más claro acordado; renombrarlo requiere revisar su contrato, no un reemplazo masivo.
No agregar `DTO`, `Impl`, `Enum`, `Ex` o `New` sin significado adicional.

En `.proto`, mantener nombres de mensajes y operaciones en PascalCase, campos en snake_case
y valores de enum en mayúsculas con guiones bajos y prefijo del enum. Esa convención es
específica de Protobuf y no contradice la de C#. No reutilizar números de campos eliminados;
reservarlos y revisar compatibilidad antes de cambiar contratos.

<a id="seccion-10-4"></a>

### 10.4 Recursos abreviaciones y otros archivos

Las claves de recursos siguen `<Feature>_<Category>_<Meaning>` o la familia semántica ya
establecida en la pantalla. Las abreviaciones actuales permitidas en claves son `Btn`
(botón), `Lbl` (etiqueta), `Msg` (mensaje), `Err` (error), `Txt` (texto), `Lnk` (enlace),
`Col` (columna), `Tip` (ayuda) y `Ph` (placeholder). También se
permiten segmentos completos como `Connection`, `Status`, `Chat`, `Title` y `Button` si
expresan el significado de la clave. No son prefijos de controles ni de campos C#.
Mantener la misma clave en los tres `.resx`; traducir solo el valor. No renombrar claves
existentes para normalizar abreviaciones sin revisar todas sus referencias y el generador.

Se permiten términos técnicos conocidos: `Id`, `Db`, `DbContext`, `DTO` en texto,
`Grpc` en identificadores, `gRPC` en prosa, `HTTP/2`, `TLS`, `SQL`, `UTC`, `UI` y `MVVM`.
No inventar abreviaciones para palabras de dominio cuando el nombre completo sea claro.
Los parámetros de lambdas siguen 3.14.

Los scripts PowerShell conservan nombres descriptivos `Verbo-Sustantivo.ps1`, parámetros
PascalCase y variables camelCase; las variables impuestas por PowerShell no se renombran.
XAML se indenta con espacios, conservando una estructura legible y sin tabulaciones en
código nuevo; no se extraen todos los tamaños de presentación como constantes C#.
JSON y Protobuf siguen la estructura y formato coherentes de su archivo. La documentación
puede escribirse en español. Las fuentes generadas conservan las convenciones del generador.

En contenido nuevo, JSON y Protobuf usan dos espacios por nivel y XAML usa cuatro, conforme
a `.editorconfig`. PowerShell mantiene variables camelCase, parámetros PascalCase y nombres
descriptivos `Verbo-Sustantivo.ps1`; no se imponen las métricas de métodos C# a sus funciones.
Los scripts SQL mantienen identificadores del esquema y una presentación consistente de
cláusulas. No renombrar tablas o columnas para uniformar estilo ni ejecutar scripts como
parte de una simple revisión documental.

<a id="seccion-10-5"></a>

### 10.5 Assets y catálogos de recursos

Los assets deben tener nombres y ubicación que permitan identificar su función y sus
referencias. Mantener catálogos estables basados en identificadores cuando forman parte
del diseño, como `Assets/Avatars/avatar_1.png` a `avatar_20.png`. Esos números representan
opciones del catálogo, no archivos genéricos sin significado.

Los recursos nuevos siguen la familia semántica de su carpeta. No crear copias por cultura de imágenes sin texto
traducible ni trasladar una ruta física del servidor a un contrato de cliente.

**Correcto:** construir la referencia al avatar desde un identificador validado del catálogo.

**Incorrecto:** renombrar los archivos del catálogo por estética sin actualizar el convertidor
y sus referencias, o aceptar una ruta arbitraria enviada por el cliente como avatar.

<a id="seccion-11"></a>

## 11 Mantenimiento y aplicación del estándar



<a id="seccion-11-1"></a>

### 11.1 Lectura y aplicación

Los integrantes y asistentes consultan el estándar y las instrucciones aplicables antes
de modificar código. Las convenciones se aplican dentro de su alcance; una firma externa
conserva su contrato y su lógica auxiliar mantiene las reglas propias.

Una herramienta o preferencia personal no sustituye una decisión documentada. Si existe
una contradicción, identificar la regla, el efecto y la decisión del responsable antes de
realizar una transformación que altere contratos o comportamiento. El código previo no
autoriza repetir un incumplimiento confirmado; una propuesta no adoptada tampoco crea
automáticamente una nueva obligación sobre ese código.

<a id="seccion-11-2"></a>

### 11.2 Explicación y evidencia de cambios

Cada entrega explica en español qué cambió, por qué, cómo se verificó y qué limitaciones
permanecen. El código y los textos técnicos siguen 1.4. Las comprobaciones identifican el
entorno y su alcance; una compilación no acredita una partida entre computadoras.

Distinguir cambios de formato, correcciones de comportamiento y decisiones de diseño.
Si se informa un conteo de líneas o archivos, indicar la base de comparación y tratar
renombres, archivos nuevos y fuentes generadas de forma consistente. No presentar una
estimación sintáctica como una medición semántica exacta.

<a id="seccion-12"></a>

## 12 Seguridad y protección de información



<a id="seccion-12-1"></a>

### 12.1 Configuración y secretos

Las contraseñas, tokens, claves privadas y cadenas de conexión con credenciales no deberán
guardarse en el repositorio ni escribirse directamente en el código fuente. Se obtendrán de
mecanismos de configuración adecuados para el entorno, como variables de entorno o
almacenamiento local de secretos.

Los archivos de configuración de ejemplo podrán incluir nombres de claves y valores
ficticios, pero no credenciales válidas. Los secretos de desarrollo y los de despliegue
deberán mantenerse separados.

**Correcto**

```csharp
string? connectionString = configuration.GetConnectionString(
    "AtariGoConnection");
```

**Incorrecto**

```csharp
string connectionString =
    "Server=server;Database=AtariGo;User Id=admin;Password=RealPassword;";
```

Los secretos de desarrollo del servidor pueden obtenerse de User Secrets. Ese mecanismo
los mantiene fuera del árbol del proyecto, pero los guarda sin cifrado en un archivo del
perfil de usuario; no es una garantía de cifrado ni un almacén de producción. Mantener
los permisos locales y evitar imprimir valores al configurar o diagnosticar.

Un `UserSecretsId` identifica configuración, no es una credencial. Compartir el repositorio
no configura automáticamente los secretos de otra computadora. Utilizar los scripts de
configuración y un canal autorizado para entregar lo que cada entorno necesite, sin copiar
los valores al código, argumentos visibles o mensajes de diagnóstico.

Una credencial SMTP debe ser recuperable por el servidor para autenticarse ante el proveedor.
Su protección pertenece al almacenamiento de secretos. Guardar un hash de esa credencial
no permite utilizarla en SMTP. Las contraseñas de jugadores siguen un mecanismo distinto
definido en 12.2. No se afirma que la configuración actual use cifrado adicional.

<a id="seccion-12-2"></a>

### 12.2 Contraseñas y tokens

Las contraseñas de los jugadores no se almacenarán en texto plano ni se cifrarán
para poder recuperarlas.
Se almacenarán utilizando un algoritmo de hashing de contraseñas aprobado por el proyecto
y sus parámetros vigentes.

Los tokens de sesión no se registrarán en logs ni se mostrarán en mensajes de error. Si el
servidor necesita conservar información de un token para validarlo o revocarlo, se almacenará
el valor protegido definido por el diseño de autenticación, no el token reutilizable del
cliente.

La validación de credenciales y de permisos deberá realizarse en el servidor. Ocultar un
botón o una opción de interfaz no constituye autorización.

Utilizar el mecanismo de hashing de contraseñas del servidor, basado en `PasswordHasher<User>`,
con su política y configuración aprobadas; no sustituirlo por un hash rápido casero.
Los códigos de verificación siguen 12.5 y no se tratan como contraseñas persistentes.

No registrar objetos completos que puedan incluir tokens, contraseñas o códigos. Retirar
referencias temporales cuando el flujo ya no las necesita no equivale a garantizar borrado
de todas las copias de una cadena administrada en memoria. No prometer esa garantía.

<a id="seccion-12-3"></a>

### 12.3 Transporte y certificados

La comunicación gRPC entre cliente y servidor deberá utilizar TLS. El cliente deberá
validar la identidad del servidor mediante la cadena de confianza y el nombre o dirección
contemplados por el certificado. El certificado de desarrollo local deberá ser confiable
para el equipo que ejecuta el cliente y no deberá desactivar la validación TLS.

No se deberá desactivar la validación de certificados para solucionar errores de conexión.
Los certificados privados y sus claves privadas no deberán incluirse en el repositorio ni
distribuirse junto con el cliente.

<a id="seccion-12-4"></a>

### 12.4 Validación y mensajes al usuario

Los datos recibidos del cliente deberán validarse en el servidor antes de realizar cambios.
La validación de interfaz mejora la experiencia, pero no reemplaza la validación del servidor.

Los mensajes al usuario deberán explicar el resultado sin revelar detalles internos que
faciliten el acceso a información sensible o la explotación del sistema. El detalle técnico
deberá registrarse únicamente en logs protegidos y saneados.

**Correcto**

```csharp
if (request.UserName.Length > UserNameRules.MaximumLength)
{
    return RegisterResult.InvalidUserName;
}
```

**Incorrecto**

```csharp
if (request.UserName.Length > 15)
{
    throw new Exception(
        $"Invalid username in database query {sqlCommandText}");
}
```

<a id="seccion-12-5"></a>

### 12.5 Verificación por correo, recuperación y límites de envío

La verificación de acceso y la recuperación de contraseña utilizan la infraestructura
compartida de correo, con contenido y autorización propios de cada caso de uso. Las
invitaciones reutilizan el transporte SMTP sin adquirir permisos de autenticación.
No duplicar emisores ni incorporar otro proveedor por una necesidad hipotética.

Los códigos se generan con aleatoriedad criptográfica y se conservan protegidos conforme
al mecanismo de autenticación. La protección vincula usuario, propósito y contexto cuando
la operación lo requiere; un código de recuperación no se acepta para verificar un acceso.
La verificación conserva la comparación protegida del componente responsable.

El servidor controla vencimiento, intentos, reenvíos y consumo del código. La UI puede
mostrar el tiempo restante, pero no autoriza una operación por sí sola. Las transiciones
concurrentes que verifican y consumen un código deben mantener la consistencia prevista
por la transacción y el coordinador del caso de uso.

Mantener los plazos y límites en sus opciones o componentes responsables, como
`LoginVerificationOptions`, `PasswordRecoveryOptions` y `RoomInvitationEmailLimiter`.
Las cifras se consultan en esas fuentes; no se reparten literales equivalentes por las
pantallas. Las reservas de envío y su finalización respetan la sincronización existente.

Un envío SMTP fallido o una cancelación no prueban que el destinatario no recibió el correo.
No registrar códigos ni cuerpos completos, no convertir el fallo en éxito y no reenviar
indefinidamente. Mantener resultados controlados y la posibilidad de reintento conforme
a la política del caso de uso. La comunicación SMTP conserva TLS y sus validaciones.

La descripción del mecanismo no autoriza modificar SQL. Se conserva la estructura existente
y las restricciones de 4.5. Los estados temporales en memoria no se presentan como si
persistieran tras reiniciar el servidor.

**Correcto:** aplicar la política de reenvío en el servidor y comunicar un instante o tiempo
de reintento sin revelar el código ni la contraseña SMTP.

**Incorrecto:** permitir el envío porque el botón ya se habilitó, reutilizar un código entre
propósitos o registrar el cuerpo del mensaje para diagnosticar su entrega.
<a id="seccion-13"></a>

## 13 Declaración de Uso de I.A

Se utilizaron herramientas de Inteligencia Artificial (IA) como recurso de apoyo durante la creación de este estándar de desarrollo.

La IA se utilizó para ayudar con:

- La organización, propuesta de secciones y estructuración general del documento.

- La mejora de la redacción y semántica de algunas reglas.

- La generación de código de ejemplo (correcto e incorrecto) para su revisión.

- La comparación de diferentes recomendaciones de codificación.

- La identificación de posibles inconsistencias entre las distintas secciones.

- La asistencia en la búsqueda y organización de las referencias.

<a id="seccion-14"></a>

## 14 Referencias

Estas fuentes respaldan conceptos de lenguaje, framework y seguridad. Las decisiones
específicas de Atari Go, como prefijos, límites y orden de miembros, son convenciones del
equipo; una alternativa válida de otra fuente no las modifica automáticamente. Fecha de
consulta de las fuentes revisadas para esta propuesta: 18 de noviembre de 2026.

- Microsoft. (s. f.). [Convenciones de C#](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- Microsoft. (s. f.). [Convenciones de nombres](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/general-naming-conventions).
- Microsoft. (s. f.). [Nombres de miembros](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-type-members).
- Microsoft. (s. f.). [Records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record).
- Microsoft. (s. f.). [Tipos de referencia anulables](https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references).
- Microsoft. (s. f.). [Buenas prácticas para excepciones](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions).
- Microsoft. (s. f.). [La instrucción lock](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock).
- Microsoft. (s. f.). [SemaphoreSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim).
- Microsoft. (s. f.). [Interlocked](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked).
- Microsoft. (s. f.). [Colecciones concurrentes](https://learn.microsoft.com/en-us/dotnet/standard/collections/thread-safe/).
- Microsoft. (s. f.). [Ciclo de vida de DbContext](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/).
- Microsoft. (s. f.). [Consultas eficientes en EF Core](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
- Microsoft. (s. f.). [Servicios gRPC](https://learn.microsoft.com/en-us/aspnet/core/grpc/services?view=aspnetcore-10.0).
- Microsoft. (s. f.). [Plazos y cancelación gRPC](https://learn.microsoft.com/en-us/aspnet/core/grpc/deadlines-cancellation?view=aspnetcore-10.0).
- Protocol Buffers. (s. f.). [Guía de proto3](https://protobuf.dev/programming-guides/proto3/).
- Protocol Buffers. (s. f.). [Guía de estilo](https://protobuf.dev/programming-guides/style/).
- Microsoft. (s. f.). [Logging en C# y .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/overview).
- Microsoft. (s. f.). [MSTest](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests).
- Microsoft. (s. f.). [Pruebas unitarias](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices).
- Microsoft. (s. f.). [Pruebas de integración](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).
- Microsoft. (s. f.). [Secretos de desarrollo](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).
- Material académico proporcionado para comparación: *C# Video Game Development Standard*. Se consultó su organización y precisión; sus convenciones de WCF no son contratos de Atari Go.

