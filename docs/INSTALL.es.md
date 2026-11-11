# Guía de instalación

**Español** · [English](INSTALL.md)

## Requisitos

- **Windows 11** de 64 bits (probado en 23H2/24H2/25H2).
- Unos **250 MB** libres.
- **No** hace falta instalar .NET ni tener permisos de administrador: el instalador es autocontenido
  y se instala por usuario.

## Instalar

1. Descarga **`BarDockSetup.exe`** desde la sección **Releases** del repositorio.
2. Haz doble clic en el archivo.
3. Si Windows muestra el aviso **«Windows protegió tu PC»**, pulsa **Más información →
   Ejecutar de todas formas**. Es normal: el instalador no está firmado con un certificado de
   código.
4. Elige las opciones:
   - **Crear acceso directo en el escritorio**.
   - **Iniciar con Windows**.
   - **Abrir BarDock al terminar**.
5. Pulsa **Instalar**. Al terminar, BarDock arranca y la barra de tareas de Windows se oculta.

### Instalación silenciosa

```bat
BarDockSetup.exe /S
```

Instala con las opciones por defecto (acceso directo en el escritorio, sin arranque automático y
sin abrir al terminar).

## Dónde se instala

| Elemento | Ruta |
| --- | --- |
| Programa | `%LOCALAPPDATA%\Programs\BarDock\BarDock.exe` |
| Ajustes | `%APPDATA%\BarDock\settings.json` |
| Acceso directo | Menú Inicio (`BarDock`) y, opcionalmente, escritorio |
| Desinstalación | Configuración → Aplicaciones → BarDock |

Si vienes de la versión anterior (**MacDock**), la primera vez se copian automáticamente los
ajustes desde `%APPDATA%\MacDock\settings.json`; no pierdes colores, tamaños ni relojes.

## Primer arranque

- La barra aparece en el borde superior (o inferior, si lo cambias) y la barra de Windows se oculta.
- Se crea `settings.json` y se rellena con las aplicaciones **ancladas** a la barra de tareas.
- Para volver a abrir BarDock cuando la cierres:
  - **Inicio → BarDock**, o el acceso directo del escritorio.
  - `Win + R` → `%LOCALAPPDATA%\Programs\BarDock\BarDock.exe`.
  - Si ya está abierta, abrirla de nuevo **la trae al frente** (no se duplica).

## Actualizar

Ejecuta el instalador nuevo: cierra BarDock de forma ordenada, sustituye los archivos y conserva
tus ajustes. No hace falta desinstalar antes.

## Desinstalar

- **Configuración → Aplicaciones → BarDock → Desinstalar**, o
- `"%LOCALAPPDATA%\Programs\BarDock\BarDock.exe" --uninstall`.

Se cierra la barra, se restauran la barra de Windows y el área de trabajo, y se borran accesos
directos, entradas del registro y la carpeta de instalación. Tus ajustes de `%APPDATA%\BarDock`
se conservan por si vuelves a instalarlo.

## Problemas frecuentes

**Windows avisa al ejecutar el instalador.**
Es un ejecutable propio sin firma de código. Pulsa *Más información → Ejecutar de todas formas*.

**He cerrado BarDock a la fuerza y la barra de Windows no vuelve.**
Abre BarDock y ciérralo con normalidad (**clic derecho → Salir**), o reinicia el Explorador desde
el Administrador de tareas.

**No aparece la barra.**
Comprueba que no haya otra instancia en el Administrador de tareas. Si el proceso está pero no se
ve, abre BarDock de nuevo: la instancia existente se trae al frente.

**Solo aparece en un monitor.**
BarDock vive en el **monitor principal** y ocupa todo su ancho. Es una limitación conocida.

**Quiero volver a la barra de Windows.**
Clic derecho en la barra → **Sustituir la barra de Windows** (se desactiva). BarDock se coloca
junto a la barra de Windows para que puedas seguir usando su menú.
