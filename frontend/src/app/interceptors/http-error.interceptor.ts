import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../services/notification.service';

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const notify = inject(NotificationService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        const msg = resolveMessage(err);
        notify.error(msg);
      }
      return throwError(() => err);
    })
  );
};

function resolveMessage(err: HttpErrorResponse): string {
  if (err.status === 0) return 'Network error — check your connection.';
  if (err.status === 408 || err.status === 504) return 'Request timed out. Try again.';
  if (err.status >= 500) return `Server error ${err.status}. Try again later.`;
  if (err.status === 404) return 'Resource not found.';
  if (err.status === 400) {
    const detail = err.error?.message ?? err.error?.title;
    return detail ? `Bad request: ${detail}` : 'Invalid request.';
  }
  return `Request failed (${err.status}).`;
}
