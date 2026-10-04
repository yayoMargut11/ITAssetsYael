# IT Assets: Frontend (Angular)

SPA en Angular 20 (componentes standalone, signals, formularios reactivos) que consume la API de IT Assets.

## Pantallas
- **Login** → JWT guardado en `sessionStorage` (se pierde al cerrar la pestaña); el interceptor lo envía en cada petición y cierra sesión ante un 401.
- **Listado de activos** → filtros (`search`, `status`, `category`) y paginación del lado del servidor.
- **Alta de activo** (solo Administrador) → validaciones en cliente que reflejan las del servidor; el proveedor es obligatorio si es *Arrendado*.
- **Detalle del activo** → asignar (si está *Disponible*), registrar devolución (si está *Asignado*) e **historial** paginado.

> Las validaciones del cliente son solo ayuda de UX: la API es quien valida y la fuente de verdad. Los mensajes de error del servidor (`detail`) se muestran tal cual.

## Ejecutar
```bash
cd itassets-web
npm install
# 1) Edita proxy.conf.json: "target" debe apuntar al puerto de tu API (el que imprime `dotnet run`)
npm start          # = ng serve → http://localhost:4200
```
El `proxy.conf.json` reenvía `/api/*` a la API, por lo que **no se necesita CORS en desarrollo**.

Inicia sesión con `admin` / `Admin#2026` (o `operador` / `Operador#2026`, que no ve «Nuevo activo»).

## Estructura
```
src/app/
  core/    models, auth.service, auth.interceptor, guards, api.service, error.util
  pages/   login, assets-list, asset-form, asset-detail
```

## Pendiente / mejoras
- Edición de activos y cambio de estado (la API ya lo soporta: `PUT /api/assets/{id}`).
- Pantallas de colaboradores y proveedores.
- Pruebas unitarias y e2e del frontend.
- Para producción: servir detrás del mismo dominio que la API, o habilitar CORS en la API para el origen del frontend.
