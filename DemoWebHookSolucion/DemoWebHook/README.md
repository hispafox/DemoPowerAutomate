# FileSystemWatcher ? Power Automate Webhook

## Descripción

Aplicación de consola .NET 8 que monitorea cambios en un directorio (local o de red) y envía notificaciones en tiempo real a un webhook de Power Automate.

## Características

- ? Detección de eventos: Created, Changed, Deleted, Renamed
- ? Soporte para rutas locales y UNC (red)
- ? Configuración externa sin recompilar
- ? Reintentos automáticos con backoff exponencial
- ? Logging a consola y archivo
- ? Procesamiento asíncrono con cola interna
- ? Enmascaramiento de URLs sensibles en logs
- ? Manejo robusto de errores

## Requisitos

- .NET 8.0 Runtime
- Windows Server o Windows 10/11
- Permisos de lectura en la carpeta a monitorear
- URL de webhook de Power Automate

## Configuración

### appsettings.json

```json
{
  "Watcher": {
    "Path": "C:\\temp\\watch",                    // Ruta a vigilar
    "IncludeSubdirectories": true,                // Incluir subdirectorios
    "Filter": "*.*",                              // Filtro de archivos
    "NotifyOnCreated": true,                      // Notificar creaciones
    "NotifyOnChanged": true,                      // Notificar modificaciones
    "NotifyOnDeleted": true,                      // Notificar eliminaciones
    "NotifyOnRenamed": true                       // Notificar renombrados
  },
  "Webhook": {
    "Url": "https://prod-00.westeurope.logic.azure.com:443/...",
    "Method": "POST",
    "AdditionalHeaders": {
      "x-api-key": "tu-clave-opcional"
    },
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "RetryBaseDelaySeconds": 5
  },
  "Logging": {
    "Level": "Information",                       // Trace, Debug, Information, Warning, Error, Critical
    "LogToFile": true,
    "LogFilePath": "logs\\watcher.log"
  }
}
```

### Variables de Entorno

Prefijo: `FILEWATCHER_`

Ejemplos:
```bash
FILEWATCHER_Watcher__Path=C:\MiCarpeta
FILEWATCHER_Webhook__Url=https://...
```

### Parámetros de Línea de Comandos

```bash
DemoWebHook.exe --path "C:\temp\watch" --filter "*.pdf" --webhookUrl "https://..."
```

Parámetros disponibles:
- `--path`: Ruta a vigilar
- `--filter`: Filtro de archivos
- `--includeSubdirectories`: true/false
- `--webhookUrl`: URL del webhook

## Uso

### Ejecución Manual

```bash
cd DemoWebHook
dotnet run
```

O ejecutar el binario compilado:
```bash
DemoWebHook.exe
```

### Detener la Aplicación

Presionar `Ctrl+C` para detener gracefully.

## Power Automate - Configuración del Flujo

### 1. Crear Flujo Instantáneo

1. Ir a Power Automate (https://make.powerautomate.com)
2. Crear ? Flujo de nube automatizado
3. Elegir trigger: **"When a HTTP request is received"**

### 2. Esquema JSON del Trigger

```json
{
    "type": "object",
    "properties": {
        "eventType": {
            "type": "string"
        },
        "fullPath": {
            "type": "string"
        },
        "name": {
            "type": "string"
        },
        "oldFullPath": {
            "type": ["string", "null"]
        },
        "oldName": {
            "type": ["string", "null"]
        },
        "changeTimeUtc": {
            "type": "string"
        },
        "machineName": {
            "type": "string"
        },
        "watcherConfig": {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string"
                },
                "includeSubdirectories": {
                    "type": "boolean"
                },
                "filter": {
                    "type": "string"
                }
            }
        }
    }
}
```

### 3. Copiar URL del Webhook

Una vez guardado el flujo, copiar la **HTTP POST URL** y configurarla en `appsettings.json`.

### 4. Ejemplo de Flujo

```
Trigger: When a HTTP request is received
  ?
Condition: eventType equals 'Created'
  ? [Yes]
  Send email notification
  Register in SharePoint/Excel
  Move file to another location
  etc.
```

## Formato del Mensaje Enviado

```json
{
  "eventType": "Created",
  "fullPath": "C:\\temp\\watch\\documento.pdf",
  "name": "documento.pdf",
  "oldFullPath": null,
  "oldName": null,
  "changeTimeUtc": "2025-01-13T10:30:45Z",
  "machineName": "SERVIDOR-01",
  "watcherConfig": {
    "path": "C:\\temp\\watch",
    "includeSubdirectories": true,
    "filter": "*.*"
  }
}
```

## Despliegue en Producción

### Como Aplicación de Consola

1. Publicar:
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

2. Copiar archivos a servidor (ej: `C:\Apps\FileWatcher`)

3. Configurar `appsettings.json`

4. Crear tarea programada de Windows:
   - Trigger: Al iniciar el sistema
   - Acción: Iniciar programa `C:\Apps\FileWatcher\DemoWebHook.exe`
   - Configurar para ejecutar siempre

### Como Servicio Windows (Mejora Futura)

Se puede convertir fácilmente a Worker Service para ejecutar como servicio de Windows.

## Logs

### Consola
Muestra eventos en tiempo real con emojis:
```
? Evento enviado exitosamente: Created - documento.pdf (200)
? Error al enviar evento...
```

### Archivo
Ubicación: `logs\watcher.log` (configurable)

Incluye:
- Timestamp
- Nivel de log
- Categoría
- Mensaje detallado

## Solución de Problemas

### Error: "La ruta especificada no existe"
- Verificar que `Watcher.Path` existe y es accesible
- Para rutas UNC, verificar permisos de red

### Error: "Timeout al enviar evento"
- Verificar conectividad a Internet
- Aumentar `Webhook.TimeoutSeconds`
- Verificar que la URL del webhook es correcta

### No se detectan eventos
- Verificar que `Watcher.EnableRaisingEvents` está activo (revisar logs)
- Comprobar filtro de archivos
- Verificar permisos de lectura en la carpeta

### Demasiados eventos de "Changed"
- FileSystemWatcher puede generar múltiples eventos Changed para un mismo archivo
- Considerar implementar debouncing en versión futura

## Arquitectura

```
???????????????????
?   Program.cs    ?  Configuración, DI, Logging
???????????????????
         ?
         ???? WatcherService      (FileSystemWatcher)
         ?         ?
         ?         ???? NotificationService (Channel/Queue)
         ?                   ?
         ?                   ???? WebhookClient (HttpClient + Retry)
         ?                             ?
         ?                             ???? Power Automate
         ?
         ???? Logging (Console + File)
```

## Próximas Mejoras

- [ ] Soporte para múltiples carpetas
- [ ] Persistencia de eventos en caso de falla prolongada
- [ ] Debouncing para eventos Changed repetidos
- [ ] Panel web de monitoreo
- [ ] Modo batch (agrupar eventos)
- [ ] Conversión a Worker Service

## Licencia

Uso interno / educativo

## Soporte

Para issues o consultas, contactar al equipo de desarrollo.
