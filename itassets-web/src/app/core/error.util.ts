import { HttpErrorResponse } from '@angular/common/http';

/** Convierte un error HTTP del backend (ProblemDetails) en un mensaje legible para el usuario. */
export function errorMessage(err: unknown): string {
  const e = err as HttpErrorResponse;
  if (!e || e.status === 0) return 'No se pudo conectar con el servidor. Verifica que la API esté en ejecución.';

  const body = e.error;
  // Errores de validación de ASP.NET: { errors: { Campo: ['mensaje'] } }
  if (body?.errors && typeof body.errors === 'object') {
    const msgs = Object.values(body.errors as Record<string, string[]>).flat();
    if (msgs.length) return msgs.join(' · ');
  }
  if (typeof body?.detail === 'string') return body.detail;
  if (e.status === 429) return 'Demasiados intentos. Espera un momento.';
  if (e.status === 403) return 'No tienes permisos para esta operación.';
  return 'Ocurrió un error inesperado. Inténtalo de nuevo.';
}
