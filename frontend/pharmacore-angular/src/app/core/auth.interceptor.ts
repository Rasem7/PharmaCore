import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const isApi = req.url.startsWith(API_BASE_URL);
  const token = auth.token();
  const authed = isApi && token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(authed).pipe(catchError((e: HttpErrorResponse) => {
    if (e.status === 401 && isApi && !req.url.endsWith('/api/auth/login')) auth.logout();
    return throwError(() => e);
  }));
};
