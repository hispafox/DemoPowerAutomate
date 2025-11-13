# PRD Definicion de proyecto 

## 1. Resumen ejecutivo

Se requiere una aplicación de consola en .NET 8 que monitorice cambios en una carpeta (local o de red) mediante `FileSystemWatcher` y envíe notificaciones de cada cambio a un webhook HTTP consumido por un flujo de Power Automate.

La herramienta deberá ser configurable sin recompilar (carpeta a vigilar, tipos de eventos, filtros de archivos, URL del webhook, cabeceras, etc.) y deberá ser robusta frente a caídas temporales de red o del endpoint.

---

## 2. Objetivos

### 2.1. Objetivo principal

* Detectar **en tiempo casi real** los cambios en un directorio (creación, modificación, eliminación, renombrado de ficheros) y notificar dichos eventos a un flujo de Power Automate mediante llamadas HTTP a un webhook.

### 2.2. Objetivos secundarios

* Permitir modificar la configuración (carpeta, filtro, URL del webhook…) sin necesidad de recompilar.
* Minimizar el impacto en rendimiento y uso de recursos.
* Ofrecer trazabilidad mediante logs (consola y archivo).
* Facilitar su despliegue como proceso de fondo (por ejemplo, lanzado por tarea programada o como servicio).

---

## 3. Alcance

### 3.1. Alcance incluido (In Scope)

* Monitorización de una única carpeta por instancia del proceso (path local o UNC).
* Soporte para eventos:

  * Creación (`Created`)
  * Modificación (`Changed`)
  * Eliminación (`Deleted`)
  * Renombrado (`Renamed`)
* Envío de una petición HTTP por evento detectado al webhook de Power Automate.
* Configuración externa:

  * Carpeta a monitorizar.
  * Inclusión de subcarpetas.
  * Filtro de archivos (`*.txt`, `*.pdf`, `*.*`, etc.).
  * Tipos de eventos a notificar.
  * URL del webhook.
  * Método HTTP (por defecto `POST`).
  * Cabeceras adicionales (por ejemplo, `x-api-key`, `Authorization`, etc.).
* Logging básico:

  * Arranque de la app y parámetros efectivos.
  * Eventos detectados.
  * Llamadas al webhook (al menos éxito / error).
* Reintentos básicos ante fallo del webhook (patrón simple tipo retry con backoff).

### 3.2. Fuera de alcance (Out of Scope) para la primera versión

* Interfaz gráfica.
* Gestión de múltiples carpetas en un único proceso (se podría soportar en una versión futura).
* Agrupación/batch de eventos (se envía 1 petición por evento).
* Persistencia de cola de mensajes offline (solo reintentos en memoria, no colas tipo MSMQ/Rabbit, etc.).
* Autoconfiguración del flujo de Power Automate.

---

## 4. Stakeholders y usuarios

* **Administrador de sistemas / IT**: Configura la app en el servidor, define la carpeta y el webhook, supervisa logs.
* **Equipo de negocio / procesos**: Recibe los eventos en Power Automate para desencadenar flujos (registro, validación, notificación, etc.).
* **Desarrollador .NET**: Mantiene el código, implementa extensiones y soporte.

---

## 5. Requerimientos funcionales

### 5.1. Configuración

**RF-01 – Fichero de configuración**

* La aplicación leerá un fichero `appsettings.json` (o similar) con, al menos:

```json
{
  "Watcher": {
    "Path": "\\\\Servidor\\CarpetaCompartida",
    "IncludeSubdirectories": true,
    "Filter": "*.*",
    "NotifyOnCreated": true,
    "NotifyOnChanged": true,
    "NotifyOnDeleted": true,
    "NotifyOnRenamed": true
  },
  "Webhook": {
    "Url": "https://prod-00.westeurope.logic.azure.com:443/.../triggers/manual/paths/invoke?code=...",
    "Method": "POST",
    "AdditionalHeaders": {
      "x-api-key": "MI-CLAVE-OPCIONAL"
    },
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "RetryBaseDelaySeconds": 5
  },
  "Logging": {
    "Level": "Information",
    "LogToFile": true,
    "LogFilePath": "logs\\watcher.log"
  }
}
```

**RF-02 – Parámetros por línea de comandos**

* Será posible **sobrescribir** algunos valores via CLI:

  * `--path`
  * `--webhookUrl`
  * `--filter`
  * `--includeSubdirectories`
* Si no se pasan, se usarán los del `appsettings.json`.

### 5.2. Monitorización de carpeta

**RF-03 – Inicialización de FileSystemWatcher**

* El sistema inicializará un `FileSystemWatcher` apuntando a `Watcher.Path`.
* `IncludeSubdirectories` será configurable.
* `Filter` se mapeará a `FileSystemWatcher.Filter`.

**RF-04 – Eventos soportados**

* Se suscribirá a:

  * `Created` (si `NotifyOnCreated` = true)
  * `Changed` (si `NotifyOnChanged` = true)
  * `Deleted` (si `NotifyOnDeleted` = true)
  * `Renamed` (si `NotifyOnRenamed` = true)

**RF-05 – Estabilidad**

* La app deberá manejar excepciones comunes de `FileSystemWatcher` (errores de IO, pérdida de acceso a la ruta, etc.) registrando el error y, si es posible, reintentando la inicialización tras un pequeño delay configurable (por ejemplo, 10–30 segundos).

### 5.3. Envío de notificaciones al webhook

**RF-06 – Formato del mensaje**

* Por cada evento se enviará un JSON al webhook con al menos estas propiedades:

```json
{
  "eventType": "Created | Changed | Deleted | Renamed",
  "fullPath": "C:\\Rutas\\Fichero.txt",
  "name": "Fichero.txt",
  "oldFullPath": "C:\\Rutas\\AntiguoNombre.txt",
  "oldName": "AntiguoNombre.txt",
  "changeTimeUtc": "2025-11-13T07:55:23Z",
  "machineName": "SERVIDOR-FS01",
  "watcherConfig": {
    "path": "\\\\Servidor\\CarpetaCompartida",
    "includeSubdirectories": true,
    "filter": "*.*"
  }
}
```

* Para eventos que no apliquen (por ejemplo, `oldFullPath` en `Created`), los campos podrán omitirse o enviarse como `null`.

**RF-07 – Invocación HTTP**

* Se utilizará `HttpClient` (idealmente compartido) con:

  * Método `Webhook.Method` (por defecto `POST`).
  * Cabeceras `Webhook.AdditionalHeaders` si existen.
  * Timeout configurable (`Webhook.TimeoutSeconds`).
* Cualquier código de estado no exitoso (no 2xx) se considerará error y disparará la lógica de reintento.

### 5.4. Gestión de errores y reintentos

**RF-08 – Reintentos**

* Si el webhook no responde o devuelve un código no 2xx:

  * La app reintentará la petición hasta `Webhook.MaxRetries` veces.
  * Entre reintentos, esperará un `RetryBaseDelaySeconds` aplicado con backoff (por ejemplo, 5s, 10s, 20s).

**RF-09 – Manejo tras agotar reintentos**

* Si tras los reintentos el envío sigue fallando:

  * Se registrará un error con nivel `Error`.
  * No es obligatorio persistir el evento para posterior reenvío en esta versión (queda como mejora futura).

### 5.5. Logging y observabilidad

**RF-10 – Logs de consola**

* La aplicación deberá escribir en consola información de alto nivel:

  * Inicio y fin del proceso.
  * Parámetros de configuración efectivos.
  * Número de eventos procesados.
  * Errores relevantes.

**RF-11 – Logs a archivo**

* Opcional y configurable:

  * En `Logging.LogToFile = true`, registrar en un fichero rotativo simple (por tamaño o por fecha).
  * Registrar:

    * Eventos detectados.
    * Llamadas al webhook (éxito / fallo).
    * Excepciones.

### 5.6. Modo de ejecución

**RF-12 – Ejecución como consola**

* La app arrancará `FileSystemWatcher` y permanecerá en ejecución hasta:

  * Que el usuario la detenga (`Ctrl+C` o cierre de ventana).
  * O se reciba una señal de cancelación (por ejemplo, `CancellationToken`).

**RF-13 – Integración como servicio (opcional futuro)**

* El diseño deberá facilitar su refactor a:

  * Worker Service `.NET 8` (para crear servicio Windows o Linux) sin grandes cambios de lógica.
* Aunque no se implementa en la primera versión, se tendrá en cuenta a nivel de diseño de clases.

---

## 6. Requerimientos no funcionales

**RNF-01 – Rendimiento**

* El sistema deberá poder procesar al menos varios cientos de eventos por minuto sin bloquear el hilo principal.
* Recomendable usar colas internas (por ejemplo, `Channel<T>`) para desacoplar recepción de eventos y envío HTTP.

**RNF-02 – Robustez**

* Recuperación automática tras:

  * Pérdida temporal de red.
  * Pérdida temporal de acceso a la carpeta (por ejemplo, recurso de red no disponible).

**RNF-03 – Seguridad**

* La URL del webhook podrá contener tokens sensibles; se recomienda:

  * No escribir la URL completa en logs (enmascarar querystring).
  * Permitir sobreescribir la URL mediante variable de entorno para evitar almacenarla en texto claro en disco (mejora deseable).

**RNF-04 – Portabilidad**

* La app deberá ejecutarse en Windows Server (on-premise), con framework .NET 8.
* En caso de path UNC, se asume que el proceso se ejecuta bajo una cuenta con permisos adecuados.

---

## 7. Arquitectura de alto nivel

Componentes principales:

1. **Program/Main**

   * Parseo de argumentos.
   * Carga de configuración (`appsettings.json` + variables de entorno + CLI).
   * Inicialización de DI (opcional pero recomendable).

2. **WatcherService**

   * Encapsula `FileSystemWatcher`.
   * Se encarga de suscribirse a eventos y publicarlos en una cola interna.

3. **NotificationService**

   * Consume eventos de la cola.
   * Construye el payload JSON.
   * Llama a `WebhookClient`.

4. **WebhookClient**

   * Encapsula `HttpClient`.
   * Implementa reintentos y manejo de errores.

5. **Logging**

   * Integración con `ILogger<T>` (por ejemplo, `Microsoft.Extensions.Logging.Console` y opcionalmente archivo).

---

## 8. Esquema del mensaje para Power Automate

El flujo de Power Automate tendrá un trigger **“When a HTTP request is received”** que recibirá un cuerpo JSON similar a:

```json
{
  "eventType": "Created",
  "fullPath": "\\\\Servidor\\Carpeta\\Documento.pdf",
  "name": "Documento.pdf",
  "oldFullPath": null,
  "oldName": null,
  "changeTimeUtc": "2025-11-13T07:55:23Z",
  "machineName": "SERVIDOR-FS01",
  "watcherConfig": {
    "path": "\\\\Servidor\\CarpetaCompartida",
    "includeSubdirectories": true,
    "filter": "*.*"
  }
}
```

Esto permitirá en Power Automate:

* Filtrar por `eventType`.
* Usar `fullPath` y `name` para acciones posteriores (mover, copiar, registrar, etc.).
* Guardar contexto de configuración si es necesario.

---

## 9. Configuración y despliegue

### 9.1. Pasos de despliegue típicos

1. Publicar la app:

   * `dotnet publish -c Release -r win-x64 --self-contained false` (o según necesidad).
2. Copiar el contenido a una carpeta en el servidor (por ejemplo, `C:\Apps\FileWatcher`).
3. Editar `appsettings.json` con:

   * Ruta a vigilar.
   * URL del webhook.
   * Parámetros de logging y reintentos.
4. Probar ejecución manual:

   * `FileWatcher.exe --path "\\Servidor\CarpetaCompartida"`.
5. (Opcional) Configurar una tarea programada o servicio para arrancar la app en el inicio.

---

## 10. Roadmap / mejoras futuras

* Soporte para **múltiples carpetas** en una única instancia.
* Persistencia de eventos en una cola duradera cuando el webhook no responda (por ejemplo, SQLite local o Azure Storage).
* Panel de control web ligero para ver estado y estadísticas.
* Modo batch (enviar cada X segundos un lote de eventos en lugar de uno por uno).
* Empaquetar directamente como Worker Service y generador de servicio Windows.

---

