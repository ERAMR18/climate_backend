# Hostinger con Traefik

Dominio: `analisissistemas2026proyecto.xyz`.

## Preparación

1. Apunta el DNS A del dominio al VPS. Si existe AAAA, debe apuntar también al
   servidor o eliminarse si no usas IPv6.
2. Instala o reutiliza el proyecto Traefik del catálogo de Docker Manager:
   entrypoints `web` (80), `websecure` (443), resolver `letsencrypt`. Configura
   su correo y conserva su almacenamiento de certificados. Abre 80 y 443.
3. Comprueba su red: `docker network inspect traefik-proxy`. Si usa otro nombre,
   establece `TRAEFIK_NETWORK` con ese nombre en ambos proyectos.
4. Si el proyecto `agua` sigue usando este dominio, cambia su dominio o detén
   esa aplicación antes de activar Climate para evitar reglas competidoras.
5. Copia los dos repositorios al VPS, incluidos los nuevos archivos Hostinger.
   Utiliza Docker Compose 2.24.4 o posterior.

Se reutiliza el Traefik de Hostinger; Climate no inicia un segundo proxy.

## Variables

En cada repositorio copia `.env.example` a `.env` si todavía no existe.
En backend reemplaza todos los valores `CHANGE_ME`: contraseñas de SQL Server,
RabbitMQ, administrador y claves JWT e interna. No publiques `.env`.
Cambiar la contraseña de seed no modifica una cuenta existente. Conserva las
credenciales correspondientes al reutilizar volúmenes con datos.

Estas variables son opcionales, con los siguientes valores predeterminados.
Usa los mismos valores en ambos proyectos:

```dotenv
CLIMATE_DOMAIN=analisissistemas2026proyecto.xyz
TRAEFIK_NETWORK=traefik-proxy
```

Los complementos configuran CORS y `GATEWAY_PUBLIC_URL` con el dominio HTTPS.

## Despliegue desde la terminal del VPS

Primero, desde la carpeta del repositorio backend:

```bash
docker compose --env-file .env -f docker-compose.yml -f docker-compose.hostinger.yml config --quiet
docker compose --env-file .env -f docker-compose.yml -f docker-compose.hostinger.yml up -d --build --wait
```

Después, desde la carpeta del repositorio frontend, ejecuta esos mismos comandos.
En Docker Manager selecciona ambos archivos en ese orden si la interfaz lo
permite. Si solo admite uno, usa la terminal del VPS. El complemento Hostinger
no funciona solo; los contextos `build` también necesitan el código fuente.

Conserva ambos argumentos `-f` al actualizar o consultar `ps`, `logs` y `down`.
No uses `down -v` si quieres conservar los datos.

## Rutas

| Ruta HTTPS | Destino |
| --- | --- |
| `/` y rutas Angular | Frontend, prioridad 10 |
| `/api` y `/api/...` | Gateway, prioridad 100 |
| `/hubs` y `/hubs/...` | SignalR por Gateway, prioridad 100 |

Traefik conserva las rutas. HTTP redirige a HTTPS. Solo frontend y Gateway se
unen a la red del proxy, usando su puerto interno 8080. SQL Server, RabbitMQ
y las API permanecen en la red backend. Los complementos eliminan los puertos
publicados del host. Swagger y la salud del Gateway no se publican por Traefik.

## Verificación en el VPS

```bash
curl -I http://analisissistemas2026proyecto.xyz/
curl -I https://analisissistemas2026proyecto.xyz/
curl https://analisissistemas2026proyecto.xyz/runtime-config.json
docker compose --env-file .env -f docker-compose.yml -f docker-compose.hostinger.yml ps
docker compose --env-file .env -f docker-compose.yml -f docker-compose.hostinger.yml logs --tail=100
```

HTTP debe redirigir a HTTPS y HTTPS devolver 200. Runtime config debe indicar
el dominio HTTPS para API y SignalR. Comprueba login y monitoreo en el navegador,
incluida la negociación SignalR y WebSocket seguro cuando esté disponible.
`config --quiet` valida Compose; los certificados y la conexión pública requieren
verificación tras desplegar realmente en el VPS.

Referencia: [guía de Hostinger](https://www.hostinger.com/support/connecting-multiple-docker-compose-projects-using-traefik-in-hostinger-docker-manager/).
