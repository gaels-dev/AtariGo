# Estándar de codificación y desarrollo de Atari Go

Este documento es la referencia vigente para escribir y revisar el código del proyecto.
Aplica al trabajo de los integrantes del equipo y de los asistentes de programación.
Su objetivo es mantener código consistente, comprensible y fácil de mantener.

- Versión: 1.2 en Markdown.
- Última revisión: 2026-11-02.
- Documento de origen: `SOFTWARE CODING AND DEVELOPMENT STANDARD.docx`.
- Autores del documento de origen: Morales Baizabal Emiliano y Amores Rivas Gael Sameí.
- Contexto académico: Tecnologías para la Construcción de Software, Ingeniería de Software, equipo 2.

## Índice

- [1 Introducción](#seccion-1)
  - [1.1 Propósito del Documento](#seccion-1-1)
  - [1.2 Alcance](#seccion-1-2)
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
  - [2.14 Estructura Jerárquica dentro de la Clase](#seccion-2-14)
  - [2.15 Eventos](#seccion-2-15)
  - [2.16 Propiedades](#seccion-2-16)
  - [2.17 Prefijos y Sufijos](#seccion-2-17)
  - [2.18 Clases de Prueba](#seccion-2-18)
  - [2.19 Métodos de Prueba](#seccion-2-19)
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
  - [3.13 Retornos Únicos](#seccion-3-13)
  - [3.14 Lambdas y miembros con cuerpo de expresión](#seccion-3-14)
  - [3.15 Asincronía cancelación y plazos](#seccion-3-15)
  - [3.16 Condicionales ternarios](#seccion-3-16)
- [4 Organización del Proyecto](#seccion-4)
  - [4.1 Una clase por archivo](#seccion-4-1)
  - [4.2 Responsabilidad única](#seccion-4-2)
  - [4.3 Complejidad Ciclomática y Tamaño de Métodos](#seccion-4-3)
  - [4.4 Responsabilidades del cliente WPF](#seccion-4-4)
  - [4.5 Configuración de persistencia](#seccion-4-5)
  - [4.6 Autoridad del servidor y concurrencia](#seccion-4-6)
- [5 Prácticas Específicas de Videojuegos](#seccion-5)
- [6 Manejo de Errores y Excepciones](#seccion-6)
  - [6.1 Manejo General de Excepciones](#seccion-6-1)
  - [6.2 Tipos de Excepciones](#seccion-6-2)
  - [6.3 Propagación de Excepciones](#seccion-6-3)
  - [6.4 Límites de Manejo de Excepciones](#seccion-6-4)
  - [6.5 Excepciones Personalizadas](#seccion-6-5)
  - [6.6 Manejo de Estados No Válidos](#seccion-6-6)
  - [6.7 Liberación de Recursos](#seccion-6-7)
  - [6.8 Prácticas Prohibidas de Manejo de Excepciones](#seccion-6-8)
  - [6.9 Manejo y Retorno de Valores Nulos](#seccion-6-9)
  - [6.10 Uso de Bitácoras y Logs](#seccion-6-10)
- [7 Comentarios](#seccion-7)
  - [7.1 Principios para la Documentación del Código](#seccion-7-1)
  - [7.2 Comentarios de Una Línea](#seccion-7-2)
  - [7.3 Comentarios de Bloque](#seccion-7-3)
  - [7.4 Justificación de Decisiones de Diseño](#seccion-7-4)
  - [7.5 Comentarios TODO](#seccion-7-5)
  - [7.6 Comentarios FIXME](#seccion-7-6)
  - [7.7 Restricciones para los Comentarios](#seccion-7-7)
- [8 Control de Versiones Git](#seccion-8)
  - [8.1 Mensajes de Commit](#seccion-8-1)
  - [8.2 Estrategia de Ramificación](#seccion-8-2)
  - [8.3 Archivo Gitignore](#seccion-8-3)
- [9 Pruebas y Aseguramiento de Calidad](#seccion-9)
  - [9.1 Estrategia de Pruebas](#seccion-9-1)
  - [9.2 Pruebas Unitarias](#seccion-9-2)
  - [9.3 Pruebas de Integración](#seccion-9-3)
  - [9.4 Estructura Arrange-Act-Assert](#seccion-9-4)
  - [9.5 Categorías de pruebas](#seccion-9-5)
- [10 Prefijos Permitidos y Abreviaciones](#seccion-10)
- [11 Declaración de Uso de I.A](#seccion-11)
- [12 Referencias](#seccion-12)
- [13 Mantenimiento del estándar y trabajo con Codex](#seccion-13)

<a id="seccion-1"></a>

## 1 Introducción

<a id="seccion-1-1"></a>

### 1.1 Propósito del Documento

El presente estándar tiene como propósito establecer las convenciones y prácticas que deberán seguirse durante el desarrollo del videojuego basado en el juego de mesa “Atari Go”, con el fin de mantener un código fuente consistente, legible, mantenible y fácil de revisar.

El documento define criterios relacionados con la escritura, organización, documentación, manejo de errores y desarrollo del código fuente. Su aplicación busca reducir inconsistencias entre los integrantes del equipo y facilitar la comprensión, modificación y mantenimiento del software durante las diferentes etapas del proyecto.

El estándar servirá como referencia común para las actividades de desarrollo y como criterio para revisar que el código producido cumpla con las convenciones establecidas para el proyecto.

<a id="seccion-1-2"></a>

### 1.2 Alcance

Este estándar será aplicable al desarrollo y mantenimiento del código fuente del videojuego.

Las disposiciones establecidas deberán ser utilizadas por todos los integrantes que participen en la implementación, modificación, revisión o mantenimiento del código del proyecto.

El estándar comprende las convenciones relacionadas con el lenguaje C#, incluyendo aspectos de estilo y formato del código, organización de archivos y namespaces, manejo de errores y excepciones, así como otras prácticas de desarrollo definidas en las secciones correspondientes de este documento.

Las reglas establecidas serán aplicables al código desarrollado específicamente para el proyecto y deberán mantenerse durante las diferentes etapas de desarrollo. Las ampliaciones de esta revisión y su alcance están identificados en el registro de cambios.

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

El código y los comentarios estarán escritos completamente en inglés.

El sistema permitirá que el usuario pueda elegir el idioma de los mensajes al jugador y la interfaz de usuario entre las siguientes opciones:

- Inglés

- Español

- Alemán

El sistema solo requerirá del huso horario para generar logs y reportes de chat, los cuales tendrán el huso horario UTC-6 (Ciudad de México).

Los textos de interfaz pertenecen a recursos localizados. No se traducen nombres de tipos,
propiedades o claves de recursos para cambiar el idioma mostrado al jugador.

El desplazamiento UTC-6 anterior se aplica a la presentación de logs y reportes de chat.
Los instantes de sesiones, expiraciones y persistencia se calculan en UTC. No se compara
una hora local con una fecha UTC ni se resta un desplazamiento para calcular un plazo.
“UTC-6” expresa aquí un desplazamiento fijo; cambiarlo por una zona horaria identificada
requiere documentar esa decisión.

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
y 10, respectivamente. `readonly` no convierte un campo en una constante.

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
establecida en 2.10, 2.11 y 10. Exponer un campo mutable requiere justificar por qué una
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

### 2.14 Estructura Jerárquica dentro de la Clase

Los miembros de una clase deben organizarse en un orden lógico y estricto para facilitar la lectura. El orden obligatorio es:

- Directivas using: Sistema (.NET) primero, luego terceros/librerías, y finalmente propios del proyecto (cada grupo separado por un salto de línea).

- Declaración de namespace y class.

- Constantes y Campos Estáticos: Públicos seguidos de privados.

- Campos de Instancia (Atributos): Públicos, protegidos y privados.

- Constructores: Estático, por defecto y parametrizados.

- Propiedades: Getters y setters.

- Métodos Normales: Públicos, protegidos y privados.

- Métodos Sobrescritos (override): Al final de la clase (ej. ToString(), Equals()).

**Correcto**

```csharp
using System;

using Newtonsoft.Json;

using AtariGo.Core.Utils;

namespace AtariGo.Core.AI
{
    public class EnemyAI
    {
        public const int MaxHealth = 100;
        private static int enemyCount;
        public bool IsActive;
        protected bool isAlerted;
        private float _speed;

        static EnemyAI()
        {
            enemyCount = 0;
        }

        public EnemyAI()
        {
            _speed = 1.0f;
        }

        public EnemyAI(float speed)
        {
            _speed = speed;
        }

        public float Speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = value;
            }
        }

        public void Patrol()
        {
        }

        protected void Move()
        {
        }

        private void Scan()
        {
        }

        public override string ToString()
        {
            return "Enemy";
        }
    }
}
```

**Incorrecto**

```csharp
using AtariGo.Core.Utils;
using System;
using Newtonsoft.Json;
namespace AtariGo.Core.AI
{
    public class EnemyAI
    {
        public override string ToString()
        {
            return "Enemy";
        }
        private void Scan()
        {
        }
        protected void Move()
        {
        }
        public bool IsActive;
        private float _speed;
        public float Speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = value;
            }
        }
        public EnemyAI()
        {
            _speed = 1.0f;
        }
        public void Patrol()
        {
        }
        public EnemyAI(float speed)
        {
            _speed = speed;
        }
        static EnemyAI()
        {
            enemyCount = 0;
        }
        protected bool isAlerted;
        public const int MaxHealth = 100;
        private static int enemyCount;
    }
}
```

Para categorías antes no indicadas, aplicar este orden completo:

1. `using` de `System`, después APIs de plataforma `Microsoft`, después terceros y después
   proyectos `AtariGo`, con una línea en blanco entre grupos presentes.
2. Namespace y declaración del tipo.
3. Constantes y campos estáticos.
4. Campos de instancia.
5. Constructores: estático, sin parámetros y parametrizados.
6. Eventos.
7. Propiedades e indexadores.
8. Métodos normales.
9. Métodos `override`, al final, aunque su visibilidad los situaría antes.

Dentro de una categoría con visibilidad, usar público, protegido, interno y privado.
Los eventos que implementan un contrato de .NET conservan el nombre de ese contrato.
Los `using` necesarios solo dentro de un namespace siguen los mismos grupos.
La estructura de `Program.cs` con instrucciones de nivel superior no se trata como una
clase escrita a mano.

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
tratan según 4.1. XAML, Protobuf, documentación y scripts tienen reglas específicas en 10;
este límite C# no se extiende implícitamente a esos lenguajes.

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

<a id="seccion-3-13"></a>

### 3.13 Retornos Únicos

Debe evitarse el uso de múltiples declaraciones return dentro de un método a menos que sea estrictamente necesario para cláusulas de guarda (guard clauses) al inicio del método. Se debe priorizar un único punto de salida para mantener claro el flujo de ejecución.

**Correcto**

```csharp
public bool ValidateConnection(string token)
{
    bool isValid = false;
    if (!string.IsNullOrEmpty(token))
    {
        isValid = TokenService.Verify(token);
    }
    return isValid;
}
```

**Incorrecto**

```csharp
public bool ValidateConnection(string token)
{
    if (string.IsNullOrEmpty(token))
    {
        return false;
    }
    else
    {
        if (TokenService.Verify(token))
        {
            return true;
        }
        return false;
    }
}
```

Una cláusula de guarda comprueba una precondición y termina antes del flujo principal;
pueden existir varias guardas consecutivas. No se fuerza una variable mutable ni anidamiento
adicional solo para evitar esas salidas. Los retornos de una lambda o función local se
evalúan dentro de esa función y no como salidas del método que la contiene.

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

Las llamadas remotas usan un plazo finito definido por política compartida. Reutilizar el
canal de comunicación por proceso cliente; la autenticación se mantiene por sesión y por
llamada. No repetir automáticamente operaciones que cambian datos sin garantizar idempotencia.
Una cancelación local o un plazo agotado no demuestran que el servidor haya deshecho el cambio.

<a id="seccion-3-16"></a>

### 3.16 Condicionales ternarios

Se permite `condition ? valueIfTrue : valueIfFalse` para elegir entre dos valores claros,
sin efectos secundarios. No anidar ternarios. Cuando hay varias condiciones o acciones,
usar `if`, `switch` o un método con nombre. `??`, `?.` y `?:` son operadores distintos;
no sustituir uno por otro sin conservar el significado de `null` y de la condición.

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

<a id="seccion-4-2"></a>

### 4.2 Responsabilidad única

Todas las clases deben adherirse estrictamente al Principio de Responsabilidad Única (SRP). Cada clase debe tener un único propósito bien definido dentro del sistema y, por lo tanto, "una única razón para cambiar". No se deberán mezclar diferentes dominios o capas lógicas en un mismo componente.

<a id="seccion-4-3"></a>

### 4.3 Complejidad Ciclomática y Tamaño de Métodos

Para garantizar la legibilidad y mantenibilidad, la complejidad ciclomática de cualquier método no deberá superar un valor de 10. Asimismo, los métodos deberán ser pequeños y concisos, no excediendo las 50 líneas de código. Si un método supera estas métricas, deberá refactorizarse dividiendo su lógica en métodos de apoyo más pequeños.

Para las 50 líneas se cuenta el cuerpo de cada método, constructor, función local o
accesor: líneas físicas que contienen código, sin firma, líneas vacías, comentarios solos
ni líneas que contienen únicamente llaves. Una línea con código y comentario sí cuenta.
Se incluyen las líneas de lambdas escritas dentro del cuerpo; mover código a una lambda
no sirve para ocultar el tamaño del método. Una cadena multilínea cuenta sus líneas de
contenido. La regla de parámetros es independiente de esta medición.

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

<a id="seccion-4-5"></a>

### 4.5 Configuración de persistencia

La configuración de EF Core se mantiene en clases `IEntityTypeConfiguration<T>` por entidad.
Mover un mapeo exige comprobar que el modelo relacional conserve tablas, columnas, índices
y relaciones. La organización de estos archivos no implica ejecutar migraciones.

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

<a id="seccion-5"></a>

## 5 Prácticas Específicas de Videojuegos

———— Esta sección se verá más adelante ————

Apartado reservado para futuras reglas del motor para el juego y el desarrollo de gameplay

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

### 6.9 Manejo y Retorno de Valores Nulos

Se debe evitar en lo posible el retorno de valores nulos. En su lugar, se priorizará devolver colecciones vacías (como Enumerable.Empty<T>() o listas vacías) o utilizar el patrón Null Object.

Cuando sea estrictamente necesario indicar que una variable o tipo de retorno puede ser nulo, se deberá utilizar el operador ? propio de C# para tipos de referencia anulables (Nullable reference types).

**Correcto**

```csharp
public Player? FindPlayerByEmail(string email)
{
    if (string.IsNullOrEmpty(email))
    {
        return null;
    }
    return _players.FirstOrDefault(player => player.Email == email);
}

public List<Player> GetActivePlayers()
{
    return new List<Player>();
}
```

**Incorrecto**

```csharp
public Player FindPlayerByEmail(string email)
{
    return null;
}

public List<Player> GetActivePlayers()
{
    return null;
}
```

Una colección vacía significa que la consulta válida no encontró elementos; no significa
sesión inválida ni fallo de red. Un resultado de búsqueda singular puede devolver `T?` si
la ausencia es esperada y el contrato lo documenta. Los estados de negocio diferentes deben
representarse explícitamente; no reemplazar todo `null` por un objeto ficticio ni usar `!`
para silenciar una validación ausente. No cambiar una API existente sin revisar sus llamadores.

<a id="seccion-6-10"></a>

### 6.10 Uso de Bitácoras y Logs

Cualquier evento significativo, cambio de estado crítico o captura de excepción deberá registrarse utilizando un sistema formal de bitácoras (Logging). No se deberá utilizar la consola estándar (Console.WriteLine) para la salida de mensajes de producción. Los logs deberán incluir contexto suficiente y utilizar niveles apropiados (Info, Warning, Error).

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
La retención automática sigue pendiente; no se eliminan logs existentes en esta entrega.
`Trace` sin un listener configurado no debe presentarse como una bitácora persistida.

Registrar información suficiente para distinguir operación, estado y causa sin secretos.
No registrar contraseñas, tokens, claves privadas ni cadenas de conexión. Evitar registrar
la misma excepción en cada capa. La cancelación solicitada es un resultado esperado: no se
registra como error. Si se registra una excepción completa, revisar que no exponga datos
sensibles; se puede registrar tipo, código y contexto saneado en su lugar.

<a id="seccion-7"></a>

## 7 Comentarios

<a id="seccion-7-1"></a>

### 7.1 Principios para la Documentación del Código

Los comentarios y la documentación del código deberán utilizarse para explicar información que no sea evidente a partir del propio código.

Los comentarios deberán aportar información útil para comprender la intención, el comportamiento, las restricciones o las decisiones relevantes del código. No deberán utilizarse para describir de manera redundante lo que el código ya expresa claramente.

Los comentarios deberán mantenerse actualizados cuando se modifique el código al que hacen referencia. Un comentario que contradiga el comportamiento actual del código deberá corregirse o eliminarse.

Los comentarios deberán redactarse de forma clara, concisa y consistente. Cuando un comentario describa una decisión o comportamiento importante, deberá explicar principalmente el motivo o la intención, en lugar de repetir cómo funciona el código.

**Correcto**

```csharp
// Prevents the player from moving while the game is paused.
if (game.IsPaused)
{
    return;
}
```

**Incorrecto**

```csharp
// Check if game is paused.
if (game.IsPaused)
{
    return;
}
```

<a id="seccion-7-2"></a>

### 7.2 Comentarios de Una Línea

Los comentarios de una sola línea deberán utilizarse para explicaciones breves relacionadas con una instrucción, sección o decisión específica del código.

Deberán utilizarse mediante el delimitador //.

El comentario deberá colocarse preferentemente en una línea independiente de la instrucción que documenta. Deberá comenzar con una letra mayúscula y terminar con un punto cuando constituya una oración completa.

**Correcto**

```csharp
// Keep the previous position to restore it if movement is blocked.
Vector3 previousPosition = transform.position;
```

**Incorrecto**

```csharp
Vector3 previousPosition = transform.position; // previous position
```

<a id="seccion-7-3"></a>

### 7.3 Comentarios de Bloque

Los comentarios de bloque deberán utilizarse únicamente cuando sea necesario documentar una sección que requiera una explicación más extensa y que no pueda expresarse adecuadamente mediante comentarios de una sola línea.

Los comentarios de bloque deberán utilizar el formato /* */.

No deberán utilizarse comentarios de bloque como sustituto habitual de los comentarios de una sola línea.

Los comentarios de bloque no deberán utilizarse para ocultar código obsoleto o deshabilitado. El código que ya no sea necesario deberá eliminarse mediante el sistema de control de versiones.

**Correcto**

```csharp
/*
 * The initial position is stored before starting the
 * movement sequence so it can be restored if needed.
 */
Vector3 initialPosition = transform.position;
```

**Incorrecto**

```csharp
/*
 * This is the player.
 * This gets the position.
 * The position is stored.
 * Then the position can be used.
 */
Vector3 initialPosition = transform.position;
```

<a id="seccion-7-4"></a>

### 7.4 Justificación de Decisiones de Diseño

Los comentarios deberán documentar las decisiones de diseño cuando la razón de una implementación no sea evidente a partir del código.

La documentación de una decisión de diseño deberá explicar principalmente el motivo de la decisión, la restricción que la originó o el problema que busca evitar.

No deberá utilizarse un comentario para justificar una implementación arbitraria cuando una solución más clara o simple pueda eliminar la necesidad de dicha explicación.

Las decisiones importantes que afecten la arquitectura, rendimiento, compatibilidad, seguridad, comportamiento del videojuego o mantenimiento del código deberán documentarse cuando su propósito no sea evidente.

**Correcto**

```csharp
// Cache the calculated path to avoid recalculating it every frame.
GamePath path = pathfinder.CalculatePath(target);
```

**Incorrecto**

```csharp
// Calculate the path.
GamePath path = pathfinder.CalculatePath(target);
```

<a id="seccion-7-5"></a>

### 7.5 Comentarios TODO

Los comentarios TODO deberán utilizarse únicamente para identificar trabajo pendiente que sea relevante para el desarrollo del proyecto.

Cada comentario TODO deberá describir claramente qué debe realizarse y, cuando sea necesario, proporcionar suficiente contexto para identificar la tarea.

Los comentarios TODO no deberán utilizarse para almacenar tareas permanentes ni para sustituir el sistema de seguimiento de tareas del proyecto cuando exista uno disponible.

Las tareas importantes identificadas mediante TODO deberán trasladarse al sistema de seguimiento correspondiente cuando representen trabajo que deba planificarse o asignarse.

**Correcto**

```csharp
// TODO: Replace the temporary player spawn position with the level spawn point.
Vector3 spawnPosition = Vector3.zero;
```

**Incorrecto**

```csharp
// TODO: Fix this later.
Vector3 spawnPosition = Vector3.zero;
```

<a id="seccion-7-6"></a>

### 7.6 Comentarios FIXME

Los comentarios FIXME deberán utilizarse para identificar código que presenta un problema conocido y requiere una corrección.

Cada comentario FIXME deberá describir el problema de manera específica y deberá indicar, cuando sea necesario, la condición que debe corregirse.

Los comentarios FIXME no deberán utilizarse para ocultar errores conocidos de manera indefinida. Los problemas identificados deberán registrarse en el sistema de seguimiento del proyecto cuando requieran seguimiento, asignación o planificación.

**Correcto**

```csharp
// FIXME: Prevent duplicate damage when this method is called twice during the same frame.
ApplyDamage(damage);
```

**Incorrecto**

```csharp
// FIXME: This does not work correctly.
ApplyDamage(damage);
```

<a id="seccion-7-7"></a>

### 7.7 Restricciones para los Comentarios

Los comentarios deberán cumplir las siguientes restricciones:

- No deberán describir información que sea evidente a partir del código.

- No deberán contener información incorrecta, desactualizada o contradictoria con la implementación.

- No deberán utilizarse para justificar código que debería refactorizar.

- No deberán utilizarse para ocultar código obsoleto o deshabilitado.

- No deberán contener información confidencial, credenciales, contraseñas, claves privadas o información sensible.

- Los comentarios TODO y FIXME deberán ser específicos y deberán mantenerse bajo control durante el desarrollo.

- Los comentarios deberán colocarse en una línea independiente cuando describan una sección o instrucción completa.

- Los comentarios deberán actualizarse o eliminarse cuando la implementación a la que hacen referencia cambie.

- Los comentarios no deberán utilizarse para justificar prácticas que contradigan las reglas establecidas en este estándar.

**Correcto**

```csharp
public int CalculateScore(int baseScore, int bonus)
{
    return baseScore + bonus;
}
```

**Incorrecto**

```csharp
// This method calculates the player's movement speed.
// It gets the base speed.
// Then it multiplies the base speed.
// The result is returned.
// Do not change this code because it works.
public float CalculateMovementSpeed()
{
    return baseSpeed * speedMultiplier;
}
```

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

### 8.2 Estrategia de Ramificación

Pendiente de definición. No se establece todavía una convención obligatoria.

<a id="seccion-8-3"></a>

### 8.3 Archivo Gitignore

Utilizar un archivo `.gitignore` adecuado para evitar subir carpetas temporales o de compilación.

<a id="seccion-9"></a>

## 9 Pruebas y Aseguramiento de Calidad

<a id="seccion-9-1"></a>

### 9.1 Estrategia de Pruebas

La estrategia de pruebas del proyecto se centrará en garantizar la estabilidad de la lógica central y la comunicación del sistema. Se priorizará la automatización de pruebas sobre los componentes críticos, separando claramente la evaluación de la lógica aislada y la interacción entre múltiples módulos.

<a id="seccion-9-2"></a>

### 9.2 Pruebas Unitarias

Las pruebas unitarias deberán enfocarse en validar el comportamiento de un único componente, método o clase de forma aislada, sin depender de bases de datos, redes o sistemas de archivos. Toda la lógica de reglas (por ejemplo, validaciones de turnos o condiciones de victoria) debe estar cubierta por pruebas unitarias.

<a id="seccion-9-3"></a>

### 9.3 Pruebas de Integración

Las pruebas de integración validarán que dos o más módulos interactúen correctamente entre sí. Se utilizarán para asegurar que la comunicación entre la lógica del juego y los servicios externos (como la base de datos o el sistema de emparejamiento) fluya sin pérdida de datos ni estados inválidos.

<a id="seccion-9-4"></a>

### 9.4 Estructura Arrange-Act-Assert

Todas las pruebas automatizadas deberán dividirse claramente en tres bloques utilizando comentarios: preparar (Arrange), ejecutar (Act) y validar (Assert).

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

<a id="seccion-9-5"></a>

### 9.5 Categorías de pruebas

Las clases de pruebas nuevas o revisadas se marcan con `[TestCategory("Unit")]` cuando
no necesitan servicios externos y con `[TestCategory("Integration")]` cuando comprueban
transporte u otros límites reales. La ubicación de carpeta por sí sola no filtra la ejecución.
Las pruebas de heartbeat/presencia heredadas quedan sin cambios hasta su sustitución.
La ejecución completa de la solución debe mantenerse además de cualquier filtro por categoría.

<a id="seccion-10"></a>

## 10 Prefijos Permitidos y Abreviaciones

Esta sección define las convenciones específicas del repositorio. Las reglas del framework
se conservan cuando el nombre forma parte de un contrato externo.

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

<a id="seccion-11"></a>

## 11 Declaración de Uso de I.A

Se utilizaron herramientas de Inteligencia Artificial (IA) como recurso de apoyo durante la creación de este estándar de desarrollo.

La IA se utilizó para ayudar con:

- La organización, propuesta de secciones y estructuración general del documento.

- La mejora de la redacción y semántica de algunas reglas.

- La generación de código de ejemplo (correcto e incorrecto) para su revisión.

- La comparación de diferentes recomendaciones de codificación.

- La identificación de posibles inconsistencias entre las distintas secciones.

- La asistencia en la búsqueda y organización de las referencias.

<a id="seccion-12"></a>

## 12 Referencias

- Microsoft. (2023, 3 de octubre). Capitalization conventions. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions

- Microsoft. (2023, 3 de octubre). Design guidelines for exceptions. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions

- Microsoft. (2023, 3 de octubre). General naming conventions. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/general-naming-conventions

- Microsoft. (2023, 14 de diciembre). Best practices for writing unit tests. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices

- Microsoft. (2024, 18 de enero). C# coding conventions. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions

- Microsoft. (2024, 25 de febrero). Exception handling (C# programming guide). Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/exceptions/exception-handling

- Microsoft. (2024, 12 de marzo). Nullable reference types. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references

- van der Til, J., Costello, M., & Calvarro Nelson, J. (2026, March 10). Integration tests in ASP.NET Core. Microsoft Learn. https://learn.microsoft.com/enus/aspnet/core/test/integration-tests?view=aspnetcore-10.0

- Unity Technologies. (n.d.-b). Naming and code style tips for C# scripting in Unity. Unity. Retrieved August 29, 2026, from https://unity.com/how-to/naming-andcode-style-tips-c-scripting-unity

<a id="seccion-13"></a>

## 13 Mantenimiento del estándar y trabajo con Codex

### 13.1 Lectura y aplicación

Antes de modificar código, Codex debe leer `AGENTS.md` y este estándar, además de las
instrucciones específicas que existan en la carpeta afectada. Debe aplicar las reglas
vigentes a cada cambio que realice. La presencia de código antiguo que incumpla una regla
no es autorización para repetir el incumplimiento.

Los archivos generados deben modificarse mediante su fuente o herramienta generadora;
no deben reformatearse manualmente para satisfacer ejemplos del estándar. Si una firma
impuesta por un framework o por código generado entra en conflicto con una regla,
Codex debe explicar la restricción concreta y proponer cómo documentarla. No debe cambiar
contratos externos para cumplir una métrica de estilo.

### 13.2 Explicación de los cambios

Cada entrega debe explicar en español qué cambió, por qué y cómo funciona, con enlaces
a los archivos relevantes. Las explicaciones deben permitir al responsable del proyecto
comprender y mantener el código. Se deben informar las comprobaciones realizadas y sus
límites; no se debe presentar una prueba local como evidencia de una prueba entre máquinas.
El código y los comentarios continúan en inglés según la sección 1.4.

### 13.3 Propuestas de mejora

Cuando aparezca una necesidad no cubierta, una contradicción o una regla difícil de aplicar,
Codex debe proponer una mejora concreta. Cada propuesta debe indicar el problema, la regla
sugerida, un ejemplo cuando ayude y el impacto sobre el código existente.

Cuando se necesite un registro escrito, las propuestas se guardan localmente en
`docs/local/PROPUESTAS_ESTANDAR.md` con estado pendiente. El registro anterior
`PROPUESTAS_ESTANDAR.md` en la raíz puede consultarse si existe. Ambos son opcionales y no
se publican por defecto; su ausencia al clonar no impide trabajar ni obliga a reconstruirlos.

Las propuestas no pasan a ser obligatorias hasta que el responsable las adopte explícitamente.
Las reglas adoptadas se incorporan completas a este estándar compartido. Si el usuario ya
solicita una modificación concreta del estándar, esa solicitud autoriza realizarla directamente.
Este mecanismo no exige detener el trabajo para consultar decisiones rutinarias ya autorizadas.

Los informes de auditoría, notas del chat y registros de cambios de trabajo se guardan en
`docs/local/`, excluida de Git. Los informes anteriores `AUDITORIA_CODIGO.md` y
`CAMBIOS_ESTANDAR.md` de la raíz también se conservan como documentos locales opcionales.
`CODING_STANDARD.md` y `AGENTS.md` permanecen como documentación compartida; sus instrucciones
no deben depender de enlaces a informes ignorados. No es necesario generar un Markdown
adicional cuando la explicación en el chat sea suficiente, salvo solicitud del usuario.


### Observaciones conservadas para revisión

- Falta Servidor
- Falta Patrón MVVM
- Checar en el estándar el nombrado de los elementos de las vistas
- Checar uso de valores anulables y condicionales ternarios.
- Checar nombrado de propiedades de las clases.
- Uso de bindings
- Estructura de protos, contratos y clases que los implementan (también nombrado de clases prefijo y sufijo)
- Configuración de servidor usando HTTP2 y TLC
- Llamadas entre cliente y servidor
- Número máximo de parámetros por métodos
- Manejo de excepciones
- Métodos síncronos y asíncronos
