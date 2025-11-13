# Preguntas Frecuentes (FAQ)

## General

### ¿Qué hace exactamente esta aplicación?
Monitorea una carpeta en tiempo real y envía una notificación HTTP a Power Automate cada vez que se crea, modifica, elimina o renombra un archivo.

### ¿Necesito conocimientos de programación para usarla?
No. Solo necesitas editar el archivo `appsettings.json` con la ruta a vigilar y la URL del webhook de Power Automate.

### ¿Funciona en Linux o Mac?
La aplicación está diseñada para .NET 8, que es multiplataforma. Sin embargo, el PRD especifica Windows Server. Para Linux/Mac habría que hacer ajustes menores (rutas, logs, etc.).

---

## Configuración

### ¿Cómo obtengo la URL del webhook?
1. Crear un flujo en Power Automate
2. Usar el trigger "When a HTTP request is received"
3. Guardar el flujo
4. Copiar la URL que aparece en el trigger

### ¿Puedo vigilar múltiples carpetas?
En la versión actual, solo una carpeta por instancia. Para múltiples carpetas, ejecutar múltiples instancias de la aplicación (cada una con su propia configuración).

### ¿Puedo vigilar rutas de red (UNC)?
Sí, completamente. Usa rutas como `\\\\Servidor\\Carpeta` en el campo `Path`. Asegúrate de que la cuenta que ejecuta la aplicación tiene permisos.

### ¿Cómo filtro solo ciertos tipos de archivos?
Usa el campo `Filter` en la configuración:
- `*.pdf` - Solo PDFs
- `*.txt` - Solo archivos de texto
- `*.*` - Todos los archivos
- `documento*.pdf` - PDFs que empiezan con "documento"

---

## Eventos

### ¿Por qué recibo múltiples eventos "Changed" para un mismo archivo?
Es normal. Cuando una aplicación guarda un archivo, puede modificarlo varias veces (metadatos, contenido, etc.). FileSystemWatcher reporta cada cambio.

**Soluciones:**
- Desactivar `NotifyOnChanged` si no lo necesitas
- Implementar debouncing en el flujo de Power Automate (esperar X segundos antes de procesar)

### ¿Qué eventos puedo desactivar?
Puedes desactivar cualquier combinación en `appsettings.json`:
```json
"NotifyOnCreated": true,   // Nuevos archivos
"NotifyOnChanged": false,  // Modificaciones (puede ser ruidoso)
"NotifyOnDeleted": true,   // Archivos eliminados
"NotifyOnRenamed": true    // Archivos renombrados
```

### ¿Detecta cambios en subdirectorios?
Sí, si `IncludeSubdirectories` está en `true`.

---

## Rendimiento

### ¿Cuántos eventos por segundo puede manejar?
La aplicación usa una cola interna (Channel) con capacidad de 1000 eventos. En condiciones normales puede manejar varios cientos de eventos por minuto. Para volúmenes muy altos, considerar usar Azure Functions o Service Bus.

### ¿Consume muchos recursos?
No. El consumo es mínimo:
- CPU: < 1% en reposo, picos breves al procesar eventos
- RAM: ~ 50-100 MB
- Red: Solo al enviar eventos al webhook

### ¿Afecta al rendimiento del sistema de archivos?
No. FileSystemWatcher usa notificaciones del sistema operativo, no hace polling.

---

## Errores y Troubleshooting

### Error: "La ruta especificada no existe"
**Causa:** La carpeta en `Watcher.Path` no existe o no es accesible.

**Solución:**
- Verificar que la ruta existe
- Para rutas UNC, verificar conectividad de red
- Verificar permisos de la cuenta que ejecuta la app

### Error: "Timeout al enviar evento"
**Causa:** El webhook de Power Automate no responde a tiempo.

**Solución:**
- Aumentar `Webhook.TimeoutSeconds` en configuración
- Verificar conectividad a Internet
- Simplificar el flujo de Power Automate
- Verificar que la URL del webhook es correcta

### Error: "401 Unauthorized" o "403 Forbidden"
**Causa:** El webhook requiere autenticación adicional.

**Solución:**
- Verificar que la URL del webhook incluye el token/signature
- Agregar headers de autenticación en `Webhook.AdditionalHeaders`

### No se detectan eventos
**Causas posibles:**
1. FileSystemWatcher no está activo ? Revisar logs
2. Filtro muy restrictivo ? Verificar `Filter`
3. Permisos insuficientes ? Ejecutar con permisos adecuados
4. Volumen de red desconectado ? Verificar conectividad

### ¿Cómo veo los logs?
- **Consola:** Salida en tiempo real
- **Archivo:** Ubicación definida en `Logging.LogFilePath` (por defecto `logs\watcher.log`)

---

## Power Automate

### ¿Cuánto cuesta Power Automate?
- Plan gratuito: 750 ejecuciones/mes
- Plan por usuario: $15/mes (40,000 ejecuciones/día)
- Plan por flujo: $100/mes (250,000 ejecuciones/día)

Revisa precios actuales en [Microsoft Power Automate Pricing](https://powerautomate.microsoft.com/pricing/)

### ¿Puedo usar Logic Apps en lugar de Power Automate?
Sí, Azure Logic Apps tiene el mismo trigger "HTTP Request". La configuración es muy similar.

### El flujo se ejecuta pero no hace nada
**Verificar:**
1. Que el flujo está activado (On)
2. Que las condiciones están correctas
3. Revisar el historial de ejecuciones en Power Automate
4. Verificar que el esquema JSON del trigger coincide

---

## Seguridad

### ¿Es seguro almacenar la URL del webhook en appsettings.json?
La URL incluye un token de seguridad. Recomendaciones:
- No commitear `appsettings.json` a repositorios públicos
- Usar variables de entorno en producción: `FILEWATCHER_Webhook__Url`
- Configurar permisos adecuados al archivo
- Considerar Azure Key Vault para entornos empresariales

### ¿Puedo agregar autenticación adicional?
Sí, usando `AdditionalHeaders`:
```json
"AdditionalHeaders": {
  "x-api-key": "mi-clave-secreta",
  "Authorization": "Bearer mi-token"
}
```

Luego validar estos headers en Power Automate.

### ¿Los datos viajan encriptados?
Sí, siempre que uses HTTPS (recomendado). Power Automate siempre usa HTTPS.

---

## Despliegue

### ¿Cómo ejecuto la aplicación al iniciar Windows?
Usa el script `install-scheduled-task.ps1` (ejecutar como Administrador) o crea manualmente una tarea programada.

### ¿Puedo ejecutarla como servicio de Windows?
La versión actual es aplicación de consola. Para servicio de Windows, sería necesario refactorizar a Worker Service (mejora futura en el roadmap).

### ¿Cómo actualizo la aplicación?
1. Detener la aplicación/tarea programada
2. Reemplazar binarios
3. Verificar que `appsettings.json` no se sobreescribe
4. Reiniciar

### ¿Puedo ejecutarla en Docker?
Sí, aunque requeriría:
- Montar el volumen a vigilar
- Configurar red para acceso al webhook
- No está incluido en esta versión (Dockerfile sería una mejora futura)

---

## Casos de Uso

### Caso 1: Backup automático a SharePoint
**Escenario:** Cuando se crea un PDF en una carpeta local, copiarlo automáticamente a SharePoint.

**Configuración:**
- `NotifyOnCreated: true`
- `Filter: *.pdf`
- Flujo de Power Automate que lee el archivo y lo sube a SharePoint

### Caso 2: Procesamiento de facturas
**Escenario:** Detectar facturas escaneadas, extraer datos con AI Builder y crear registro en Dynamics.

**Configuración:**
- `NotifyOnCreated: true`
- `Filter: FACTURA*.pdf`
- Flujo con AI Builder ? Process form ? Create record in Dynamics

### Caso 3: Auditoría de cambios
**Escenario:** Registrar todos los cambios en carpeta compartida para auditoría.

**Configuración:**
- Todos los eventos activados
- Flujo que registra en SharePoint List con timestamp y usuario

### Caso 4: Sincronización entre servidores
**Escenario:** Cuando se crea archivo en Servidor A, copiarlo a Servidor B.

**Configuración:**
- `NotifyOnCreated: true`
- Flujo que usa File System connector para copiar

---

## Limitaciones Conocidas

### v1.0
- ? Solo una carpeta por instancia
- ? No persiste eventos si el webhook está caído por mucho tiempo
- ? No agrupa/batch eventos
- ? No es servicio de Windows nativo (es consola)
- ? Sin UI gráfica

### Mitigaciones
La mayoría están en el roadmap para versiones futuras.

---

## Mejoras Futuras (Roadmap)

- [ ] Soporte para múltiples carpetas en una instancia
- [ ] Persistencia en SQLite/Azure Storage
- [ ] Debouncing configurable para eventos Changed
- [ ] Panel web de monitoreo
- [ ] Modo batch (agrupar eventos)
- [ ] Conversión a Worker Service
- [ ] Docker support
- [ ] Configuración via portal web
- [ ] Métricas y telemetría (Application Insights)

---

## Contacto y Soporte

Para reportar bugs o solicitar features, contactar al equipo de desarrollo.

---

## Recursos Adicionales

- [Documentación de FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
- [Power Automate Documentation](https://learn.microsoft.com/en-us/power-automate/)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
