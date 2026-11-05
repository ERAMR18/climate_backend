# OAuth 2.0 / OIDC

Auth es el único cliente de proveedores. `src/Services/AuthService/src/oauth.ts` usa openid-client 6.8.8. Google: discovery OIDC, code flow, S256 PKCE, state, nonce y validación de ID token por la biblioteca. GitHub: OAuth code flow con PKCE y consultas autenticadas a `/user` y `/user/emails`; no se presenta como OIDC.

El navegador genera un verifier que conserva en sessionStorage y entrega únicamente su challenge al inicio. Auth guarda state hasheado, verifier del proveedor, nonce, enlace opcional y vínculo al navegador en PostgreSQL por cinco minutos. Una cookie HttpOnly/SameSite=Lax vincula callback con el navegador. El callback consume el estado y produce un código de uso único de 60 segundos, nunca un JWT en URL. Angular limpia el fragmento y canjea el código con su verifier. Se conservan interceptor, guards, logout y roles Administrator/Operator/Viewer (equivalentes a ADMIN/OPERATOR/VIEWER).

Variables privadas en `backend/.env.phase3.local`, inyectadas por el overlay Fase 4:
```text
OAUTH_FRONTEND_URL=http://localhost:4200
OAUTH_GOOGLE_CLIENT_ID=<client id registrado>
OAUTH_GOOGLE_CLIENT_SECRET=<secret real>
OAUTH_GOOGLE_CALLBACK_URL=http://localhost:8080/api/auth/oauth/google/callback
OAUTH_GITHUB_CLIENT_ID=<client id registrado>
OAUTH_GITHUB_CLIENT_SECRET=<secret real>
OAUTH_GITHUB_CALLBACK_URL=http://localhost:8080/api/auth/oauth/github/callback
```
No copiar estos secretos a Angular. Registrar exactamente los callbacks anteriores para desarrollo; las rutas de ejemplo del requisito oficial son ilustrativas. Para producción usar URLs HTTPS y retirar `OAUTH_ALLOW_LOCAL_HTTP`; la excepción local solo acepta loopback. Crear la aplicación Google con scopes openid/email/profile y una OAuth App GitHub con read:user/user:email. No se han creado credenciales externas.

Rutas públicas Gateway: `GET /api/auth/oauth/{google|github}/start?challenge=...`, callback y `POST /api/auth/oauth/exchange` con `{code,verifier}`. El endpoint `POST /api/auth/oauth/{provider}/link` exige JWT vigente mediante AuthGuard y devuelve `{url}` con cookie de vinculación; un cliente debe usar `credentials: include`. Permite vincular después de demostrar control de ambas cuentas. Nunca se vincula automáticamente por email; una colisión pide iniciar sesión en la cuenta existente. Los usuarios nuevos externos reciben Viewer y una contraseña local inutilizable. Las cuentas inactivas no reciben JWT.

Extensibilidad: registrar metadata revisada en `providers`, scopes y configuración; OIDC reutiliza discovery y validación. No aceptar issuers arbitrarios desde parámetros HTTP. Facebook u otros proveedores sin OIDC requieren adaptador específico de identidad verificada.

JWT conserva firma HS256, issuer, audience, expiración, roles y permisos; los servicios mantienen sus validadores. Auth conserva revocación/logout existentes. La revocación inmediata en todos los servicios no se agrega: servicios que validan JWT localmente respetan su expiración. Configurar duración según política del sistema.

Cambio de esquema idempotente en Auth: `oauth_flows`, `oauth_handoffs`; se reutilizan `users`, `external_identities`, `revoked_tokens` y audit_outbox. No almacenar tokens de Google/GitHub a largo plazo. Expirados se purgan al iniciar/canjear flujos. Limitar acceso a DB y respaldos porque verifier/nonce son sensibles durante su breve vigencia.

Pruebas automatizadas: `node --test tests/*.test.cjs` desde Auth después de compilar. Incluyen proveedor Google simulado con ID token, nonce inválido, callback sin cookie, replay, challenge, rol Viewer y JWT. No equivalen a login real Google/GitHub; ese chequeo queda pendiente del responsable con credenciales válidas.

Referencias: [openid-client](https://github.com/panva/openid-client), [GitHub OAuth](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps).

## Diagnóstico de callback 401

El issuer configurado de GitHub debe ser `https://github.com/login/oauth`. Verificar la imagen desplegada además del archivo fuente. Un callback fallido consume su estado; comenzar un intento nuevo desde /login, nunca recargar o compartir su URL con code/state.

OAuthController registra `OAuth callback rejected provider=... reason=... reference=...` con etiquetas fijas y añade X-OAuth-Error/X-OAuth-Reference a la respuesta. No registra excepciones crudas ni secretos. En el VPS:
```bash
docker logs --since 5m climate-phase3-vps-identity-service-1 2>&1 | grep 'OAuth callback rejected'
```
SESSION_MISSING: cookie/state ausentes; SESSION_EXPIRED_OR_REPLAYED: flujo expirado o consumido; ACCOUNT_LINK_REQUIRED: email ya pertenece a cuenta local y requiere vinculación explícita; VERIFIED_EMAIL_REQUIRED: proveedor sin email primario verificado; PROVIDER_INCORRECT_CLIENT_CREDENTIALS/PROVIDER_INVALID_CLIENT: revisar secret vigente sin imprimirlo; códigos TLS: corregir cadena de confianza sin desactivar TLS. Si no existe registro, comprobar que la imagen contiene estos cambios y si el 401 se origina antes de Auth.
