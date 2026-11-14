# Estándar de codificación y desarrollo de Atari Go

Este documento es la referencia vigente para escribir y revisar el código del proyecto.
Aplica al trabajo de los integrantes del equipo y de los asistentes de programación.
Su objetivo es mantener código consistente, comprensible y fácil de mantener.

- Versión: 1.4 consolidada en Markdown.
- Última revisión: 2026-11-14.
- Documento de origen: `SOFTWARE CODING AND DEVELOPMENT STANDARD.docx`.
- Consolidación: versión Markdown 1.2 del repositorio y PDF `EstandarV1.4.pdf`,
  revisión 2026-11-06, conservando las decisiones posteriores adoptadas en el proyecto.
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
  - [4.7 Contratos y comunicación gRPC](#seccion-4-7)
  - [4.8 Semáforos asíncronos y escritura de streams](#seccion-4-8)
- [5 Prácticas Específicas de Videojuegos](#seccion-5)
  - [5.1 Estado de la partida](#seccion-5-1)
  - [5.2 Consistencia entre jugadores](#seccion-5-2)
  - [5.3 Pruebas de reglas del juego](#seccion-5-3)
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
  - [6.11 Cláusulas de guarda y validación de precondiciones](#seccion-6-11)
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
  - [9.6 Criterios y casos de prueba](#seccion-9-6)
- [10 Prefijos Permitidos y Abreviaciones](#seccion-10)
- [11 Declaración de Uso de I.A](#seccion-11)
- [12 Referencias](#seccion-12)
- [13 Mantenimiento del estándar y trabajo con Codex](#seccion-13)
  - [13.4 Pendientes de evolución del estándar](#seccion-13-4)
- [14 Seguridad y protección de información](#seccion-14)
  - [14.1 Configuración y secretos](#seccion-14-1)
  - [14.2 Contraseñas y tokens](#seccion-14-2)
  - [14.3 Transporte y certificados](#seccion-14-3)
  - [14.4 Validación y mensajes al usuario](#seccion-14-4)

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
    if (string.IsNullOrEmpty(token))
    {
        return false;
    }
    return TokenService.Verify(token);
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
    if (TokenService.Verify(token))
    {
        return true;
    }
    return false;
}
```

Una cláusula de guarda comprueba una precondición y termina antes del flujo principal;
pueden existir varias guardas consecutivas. No se fuerza una variable mutable ni anidamiento
adicional solo para evitar esas salidas. Los retornos de una lambda o función local se
evalúan dentro de esa función y no como salidas del método que la contiene.

La cláusula de guarda debe comprobar una precondición al inicio del método y terminar el
flujo cuando no se cumple. No se añadirán retornos dentro de ramas anidadas si una expresión
de retorno sencilla mantiene claro el resultado.

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

### 4.7 Contratos y comunicación gRPC

Los archivos `.proto` definirán el contrato de comunicación entre cliente y servidor. Los
mensajes representarán los datos intercambiados y las operaciones representarán acciones o
consultas claramente identificadas.

Los contratos serán independientes de las clases internas de aplicación, del modelo de EF
Core y de los ViewModels. Los servicios gRPC actuarán como adaptadores de transporte:
recibirán el mensaje, invocarán la operación correspondiente y traducirán su respuesta al
contrato gRPC.

Los cambios a un contrato deberán considerar su compatibilidad con los clientes que todavía
puedan utilizar versiones anteriores. No se reutilizarán números de campos Protobuf
eliminados. Los campos que ya no se utilicen deberán reservarse cuando sea necesario
mantener la compatibilidad.

Los errores previstos deberán comunicarse mediante estados gRPC y mensajes controlados. No
se enviarán al cliente mensajes internos de excepciones, consultas SQL, rutas locales ni
detalles de configuración. Las excepciones inesperadas deberán registrarse en el servidor
con contexto seguro y transformarse en una respuesta adecuada en el límite de transporte.

En el cliente, el adaptador gRPC traducirá los estados conocidos a resultados de aplicación;
los ViewModels no deberán depender directamente de excepciones de transporte.

Las llamadas que puedan tardar deberán utilizar cancelación y plazos cuando la operación
lo permita. El cliente deberá distinguir entre una respuesta de negocio, una cancelación y
un fallo de transporte. Una cancelación local o un plazo agotado no demuestran que el
servidor haya revertido una operación que ya comenzó.

Los canales gRPC se reutilizarán durante la vida del proceso cliente. Los metadatos de
autenticación se añadirán desde el servicio responsable de la comunicación, no desde las
vistas.

Las operaciones que cambien datos no deberán repetirse automáticamente salvo que su diseño
garantice idempotencia. Las operaciones de consulta podrán reintentarse solo cuando la
política de comunicación lo permita.

**Correcto**

```csharp
try
{
    LoginResponse response = await _client.LoginAsync(
        request,
        cancellationToken: cancellationToken).ResponseAsync;
}
catch (RpcException exception)
    when (exception.StatusCode == StatusCode.Unauthenticated)
{
    return AuthenticationResult.InvalidCredentials;
}
```

**Incorrecto**

```csharp
try
{
    LoginResponse response = await _client.LoginAsync(request).ResponseAsync;
}
catch (Exception exception)
{
    ShowMessage(exception.ToString());
}
```

<a id="seccion-4-8"></a>

### 4.8 Semáforos asíncronos y escritura de streams

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

Se permite encapsular el permiso en un objeto `IDisposable`, como `FriendMutationLease`,
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

<a id="seccion-5"></a>

## 5 Prácticas Específicas de Videojuegos

<a id="seccion-5-1"></a>

### 5.1 Estado de la partida

El estado de una partida deberá tener una representación definida y validable. Las reglas
para colocar fichas, cambiar turnos, determinar capturas y reconocer el fin de la partida
deberán implementarse en componentes de dominio identificables y verificables.

La validación de una jugada no deberá depender exclusivamente de la interfaz. El servidor
será responsable de validar las acciones recibidas de un cliente antes de aceptar un cambio
de estado compartido.

Las vistas podrán representar el estado de la partida, pero no serán la fuente autoritativa
de las reglas ni del resultado.

**Correcto**

```csharp
MoveValidationResult result = gameRules.ValidateMove(
    currentGameState,
    requestedMove);
```

**Incorrecto**

```csharp
private void BoardCell_Click(object sender, RoutedEventArgs e)
{
    if (BoardGrid.SelectedItem == null)
    {
        return;
    }
    SaveMoveDirectlyToDatabase();
}
```

<a id="seccion-5-2"></a>

### 5.2 Consistencia entre jugadores

Cuando una partida se comunique por red, el servidor deberá conservar la autoridad sobre
el estado aceptado de la partida. El cliente enviará acciones y actualizará su presentación
a partir de respuestas o eventos recibidos del servidor.

Los mensajes de red deberán permitir identificar la partida y el estado al que pertenecen.
El servidor deberá rechazar acciones que no correspondan al jugador, turno o estado actual.

La reconexión de un jugador deberá recuperar el estado vigente desde la autoridad definida
para la partida. No se deberá asumir que el estado local del cliente sigue siendo válido
después de una desconexión.

Los tiempos de espera para una desconexión y sus consecuencias deberán ser reglas de
dominio configurables y documentadas, no valores literales distribuidos por el código.

**Correcto**

```csharp
if (move.GameId != gameState.GameId
    || move.PlayerId != gameState.CurrentPlayerId)
{
    return MoveValidationResult.Rejected;
}
```

**Incorrecto**

```csharp
if (playerName == "Player1")
{
    AcceptMove();
}
```

<a id="seccion-5-3"></a>

### 5.3 Pruebas de reglas del juego

Cada regla implementada deberá contar con pruebas que cubran los resultados válidos y los
casos límite relevantes. Las pruebas deberán incluir, cuando aplique, movimientos fuera
del tablero, posiciones ocupadas, cambio de turno, capturas y condiciones de finalización.

Las pruebas deberán validar el estado resultante y no depender de elementos visuales para
determinar si una regla del juego se cumplió.

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

La rama `main` representará las versiones estables del proyecto. La rama `develop` será
la rama de integración del trabajo en curso.

Los cambios se desarrollarán en ramas de trabajo creadas a partir de `develop`. Al terminar,
se integrarán en `develop` después de revisar los cambios y ejecutar las verificaciones
pertinentes. La publicación de una versión estable se realizará mediante la integración del
estado aprobado de `develop` en `main`.

No se deberán integrar directamente cambios incompletos en `main`. Los conflictos deberán
resolverse revisando el comportamiento afectado; no se deberán descartar cambios de otros
integrantes sin verificar su propósito.

Los nombres de ramas deberán describir brevemente el trabajo y utilizar un formato acordado
por el equipo. Los nombres históricos no obligan a cambiar ramas ya publicadas.

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

- Microsoft. (s. f.). SemaphoreSlim Class. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim?view=net-10.0

- Microsoft. (s. f.). The lock statement. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock

- gRPC Authors. (s. f.). Interface `IAsyncStreamWriter<T>`. gRPC C#. https://grpc.github.io/grpc/csharp/api/Grpc.Core.IAsyncStreamWriter-1.html

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


<a id="seccion-13-4"></a>

### 13.4 Pendientes de evolución del estándar

Las propuestas de reglas que todavía no hayan sido adoptadas deberán mantenerse fuera de
las obligaciones vigentes del estándar. Una propuesta pendiente deberá identificar el
problema que busca resolver y la regla sugerida.

Cuando el equipo adopte una propuesta, la regla completa deberá incorporarse en la sección
correspondiente del estándar. No se mantendrán listas de observaciones que contradigan o
dupliquen las reglas vigentes.

<a id="seccion-14"></a>

## 14 Seguridad y protección de información

<a id="seccion-14-1"></a>

### 14.1 Configuración y secretos

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

<a id="seccion-14-2"></a>

### 14.2 Contraseñas y tokens

Las contraseñas no se almacenarán en texto plano ni se cifrarán para poder recuperarlas.
Se almacenarán utilizando un algoritmo de hashing de contraseñas aprobado por el proyecto
y sus parámetros vigentes.

Los tokens de sesión no se registrarán en logs ni se mostrarán en mensajes de error. Si el
servidor necesita conservar información de un token para validarlo o revocarlo, se almacenará
el valor protegido definido por el diseño de autenticación, no el token reutilizable del
cliente.

La validación de credenciales y de permisos deberá realizarse en el servidor. Ocultar un
botón o una opción de interfaz no constituye autorización.

<a id="seccion-14-3"></a>

### 14.3 Transporte y certificados

La comunicación gRPC entre cliente y servidor deberá utilizar TLS. El cliente deberá
validar la identidad del servidor mediante la cadena de confianza y el nombre o dirección
contemplados por el certificado. El certificado de desarrollo local deberá ser confiable
para el equipo que ejecuta el cliente y no deberá desactivar la validación TLS.

No se deberá desactivar la validación de certificados para solucionar errores de conexión.
Los certificados privados y sus claves privadas no deberán incluirse en el repositorio ni
distribuirse junto con el cliente.

<a id="seccion-14-4"></a>

### 14.4 Validación y mensajes al usuario

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
