# Manual de usuario

BarDock es una barra superior minimalista para Windows 11. Sustituye la barra de tareas y reúne
tus aplicaciones, la bandeja del sistema, relojes y el estado del equipo en una sola franja
discreta.

> **Your desktop, your way.**

## La barra de un vistazo

```
[Inicio] [Vista de tareas]   …iconos de apps…      [chevron] [campana] [batería] [red] [volumen] | [ES 12:00] [CL 08:00] … | [WED 11/11/26]
```

- **Izquierda**: botón de Inicio y, opcionalmente, vista de tareas.
- **Centro**: aplicaciones ancladas y las que tienes abiertas.
- **Derecha**: bandeja nativa (chevron), notificaciones (campana), estado del sistema, relojes y
  fecha.

## Iconos de aplicaciones

- **Pasar el ratón**: los iconos se amplían en onda. El icono bajo el cursor **no se mueve** y sus
  vecinos se abren a ambos lados; la línea verde crece con él.
- **Clic izquierdo**: abre la app o trae al frente su ventana (con un pequeño rebote).
- **Clic derecho**: Abrir, Mostrar en el Explorador y Quitar de la barra.
- **Pasar el ratón por una app abierta**: aparece la lista de **sus ventanas** con el título de
  cada una. Al hacer clic en una entrada se trae **solo esa ventana** al frente.
- **Arrastrar un `.exe`, `.lnk` o `.url`** sobre la barra: se añade.
- **Anclar**: las apps que quieras fijas se añaden con **clic derecho → Añadir aplicación…**.
  Las demás aparecen y desaparecen solas cuando se abren y se cierran.

## Botones del extremo izquierdo

- **Logo de Windows**: abre el **menú Inicio** real. Con **clic derecho** ofrece *Todas las
  aplicaciones* y *Configuración*.
- **Vista de tareas** (opcional): abre la vista de tareas de Windows (`Win + Tab`) para cambiar de
  escritorio virtual. Se muestra u oculta en **clic derecho → Botón de vista de tareas**.

## Bandeja y notificaciones

- **Chevron**: abre el **panel nativo de iconos ocultos de Windows**, con los iconos reales y sus
  menús contextuales originales (OneDrive, WhatsApp, Teams, Bluetooth…). Es la bandeja de Windows,
  no una copia.
- **Campana**: abre el **centro de notificaciones** de Windows.
- **Clic derecho en la bandeja**: centro de notificaciones, ajustes de notificaciones y de la barra
  de tareas.

## Estado del sistema

- **Batería**: porcentaje; se pone roja con carga baja.
- **Red**: Wi-Fi o cable; el tooltip muestra la red.
- **Volumen**: clic para silenciar/activar, rueda del ratón para subir o bajar.
- **Clic derecho** sobre los indicadores: Red e Internet, Sonido, Energía y batería.

## Relojes y fecha

Cada reloj muestra un **alias vertical** de 2-3 letras y la hora:

```
E
S  12:47
```

La fecha sigue el mismo patrón con el **día de la semana** en el idioma de la barra:

```
W
E  11/11/26
D
```

### Personalizar

**Clic derecho en los relojes → Relojes y fecha…**

- **Estilo general** (común a relojes y fecha): fuente, negrita, tamaño e interlineado.
- **Por reloj**: zona horaria, alias y color (con selector de color).
- **Fecha**: alias (vacío = día de la semana) y color.
- **Añadir / Quitar** relojes.
- La vista previa se actualiza mientras cambias los valores.

## Menú de la barra

**Clic derecho en una zona vacía de la barra**:

| Opción | Para qué |
| --- | --- |
| Añadir aplicación… | Ancla un `.exe`, `.lnk` o `.url` |
| Relojes y fecha… | Personaliza relojes, fecha y estilos |
| Centro de notificaciones | Abre el panel de notificaciones |
| Posición de la barra | Arriba o abajo (la ampliación cambia de sentido) |
| Alineación de iconos | Centrados o a la izquierda, junto a Inicio |
| Altura de la barra | 36 a 72 px |
| Tamaño del icono | 18 a 36 px |
| Ampliación | Escala máxima al pasar el ratón |
| Separación | Espacio entre iconos |
| Transparencia | 100 % (opaca) a 50 % |
| Idioma | Español o English (menús, configuración y día de la fecha) |
| Botón de vista de tareas | Mostrar u ocultar |
| Sustituir la barra de Windows | Oculta la barra de Windows mientras BarDock está abierto |
| Iniciar con Windows | Arranque automático al iniciar sesión |
| Recargar | Relee `settings.json` |
| Editar settings.json | Abre el archivo de ajustes |
| Salir | Cierra BarDock y restaura la barra de Windows |

## Ajustes (`settings.json`)

Se guardan en `%APPDATA%\BarDock\settings.json` y puedes editarlos a mano y **Recargar**.

```jsonc
{
  "version": 5,
  "barHeight": 36,
  "iconSize": 26,
  "maxScale": 2.2,
  "spacing": 8,
  "topMargin": 0,
  "replaceTaskbar": true,
  "barPosition": "top",        // "top" o "bottom"
  "barOpacity": 0.9,           // 0.3 - 1.0
  "iconAlignment": "center",   // "center" o "left"
  "showTaskView": true,
  "renderMode": "hardware",     // "hardware" o "software"
  "language": "es",            // "es" o "en"

  "clockFontFamily": null,
  "clockFontSize": 14,
  "clockBold": false,
  "clockLetterSpacing": 0.95,

  "dateAlias": "",             // vacío = día de la semana
  "dateColor": null,

  "items": [
    { "path": "shell:AppsFolder\\MSTeams_8wekyb3d8bbwe!MSTeams", "name": "Microsoft Teams" },
    { "path": "C:\\Ruta\\App.exe", "name": "Nombre opcional" }
  ],

  "clocks": [
    { "label": "Madrid",   "zone": "Romance Standard Time", "alias": "ES", "color": null },
    { "label": "Santiago", "zone": "Pacific SA Standard Time", "alias": "CL" },
    { "label": "Lisboa",   "zone": "GMT Standard Time", "alias": "PT" },
    { "label": "Tel Aviv", "zone": "Israel Standard Time", "alias": "IL" }
  ]
}
```

- **`color`** acepta `#RRGGBB` o un nombre (`Gold`, `Tomato`…). Vacío = color del tema.
- **`alias`** es el texto vertical; si falta se deduce de la zona (`Romance Standard Time` → `ES`).
- **`barOpacity`**: opacidad del fondo de la barra.
- **`renderMode`**: `hardware` (por defecto) usa la GPU; `software` dibuja con la CPU y usa **unos
  90 MB menos** de memoria, a cambio de algo más de CPU al repintar y animaciones menos suaves.
- La barra de Windows se oculta con una **región vacía** para no romper la bandeja nativa.

## Consejos

- Si quieres la barra abajo, cambia **Posición** y el área de trabajo se reserva en el borde
  inferior; las ventanas maximizadas se recolocan solas.
- Con **iconos a la izquierda**, el dock empieza junto al botón de Inicio.
- La transparencia al 50 % deja ver el escritorio; los iconos y textos siguen opacos.
- Puedes tener BarDock sin sustituir la barra de Windows: se coloca justo al lado y sigues usando
  la bandeja original.

## Diagnóstico

```bat
BarDock.exe --dump-icons
BarDock.exe --dump-windows
```

- `--dump-icons`: guarda en `%TEMP%\barDock-icons` los iconos tal y como los extrae el shell.
- `--dump-windows`: guarda en `%TEMP%\barDock-windows.txt` las ventanas agrupadas por app.

## Preguntas frecuentes

**¿Puedo tener dos monitores?**
BarDock vive en el monitor principal.

**¿Necesito .NET?**
No con el instalador: es autocontenido.

**¿Se puede usar sin ocultar la barra de Windows?**
Sí: desactiva *Sustituir la barra de Windows*.

**¿Qué pasa si cierro BarDock?**
La barra de Windows y el área de trabajo se restauran automáticamente.

**¿Se pierden los ajustes al actualizar?**
No; se conservan en `%APPDATA%\BarDock`.
