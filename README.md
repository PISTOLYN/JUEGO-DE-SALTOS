# JUEGO DE SALTOS - 2 JUGADORES

Videojuego arcade local para dos jugadores, pensado para Windows 10 y Windows 11.

## Controles iniciales
- **M**: abrir el menú de configuración.
- **A**: salto del equipo rojo (editable).
- **L**: salto del equipo azul (editable).
- **ENTER**: iniciar una nueva ronda después del resultado.

## Diseño
La pantalla sigue la maqueta: paneles de jugador arriba (retrato, nombre, saltos), marcador central con luces, escenario de circo (rojo) y castillo (azul), trampolines con estrella y barra inferior con las teclas activas. Todo se dibuja por código y se escala a cualquier tamaño de ventana. El personaje por defecto es el payaso de `Assets/` (parado, agachado, salto y brazos arriba), incrustado en el EXE; la animación del salto cambia entre esas cuatro poses. Si se elige una imagen propia en el menú M, se usa esa en lugar del payaso.

Las teclas se cambian en el menú M: haz clic en la casilla y pulsa la tecla deseada. Mantener la tecla apretada no suma saltos extra.

## Configuración
Desde el menú M se pueden editar:
- Duración de la partida.
- Nombres y teclas de cada jugador.
- Imagen PNG/JPG de cada payaso.
- Fondo independiente para cada escenario.
- Música MP3/WAV y efecto de salto MP3/WAV.
- Volumen de música y efectos.

Los ajustes se guardan en la carpeta de usuario de Windows:
`%AppData%\JuegoDeSaltos\config.json`

La aplicación genera automáticamente una música arcade original y un efecto de salto WAV si no se han elegido archivos propios.

## Compilación automática
GitHub Actions genera dos artefactos independientes:
- `JuegoDeSaltos-Windows-x86`: Windows de 32 bits.
- `JuegoDeSaltos-Windows-x64`: Windows de 64 bits.

En GitHub entra a **Actions**, abre **Build Windows EXE** y descarga los artefactos al terminar.

## Nota de desarrollo
La compilación de GitHub comprueba que el proyecto compile y genera los paquetes. Todavía se requiere probar el juego en equipos Windows reales, revisar controles/audio y ajustar las imágenes finales de los payasos.
