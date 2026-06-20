// src/app/core/interceptors/http-error.interceptor.ts

import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor,
  HttpErrorResponse
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { ToastService } from '../../fmc-services/toast.service';

@Injectable()
export class HttpErrorInterceptor implements HttpInterceptor {

  // Track in-flight requests for global loading indicator
  private activeRequests = 0;

  constructor(private toast: ToastService) {}

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    this.activeRequests++;

    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        let message = 'An unexpected error occurred.';

        if (error.status === 0) {
          message = 'Cannot reach the server. Please check your connection.';
        } else if (error.status === 400) {
          // ASP.NET's ApiBehaviorOptions.InvalidModelStateResponseFactory returns
          // { success: false, errors: ["Title must be …", "Description must be …"] }
          // on model validation failure. Surface those so the user knows what to fix
          // instead of a generic "check your input". Fall back to message / generic.
          const errs = error.error?.errors;
          if (Array.isArray(errs) && errs.length) {
            message = errs.join(' ');
          } else if (error.error?.message) {
            message = error.error.message;
          } else {
            message = 'Invalid request. Please check your input.';
          }
        } else if (error.status === 401) {
          message = 'Session expired. Please log in again.';
        } else if (error.status === 403) {
          message = 'You do not have permission to perform this action.';
        } else if (error.status === 404) {
          message = 'The requested resource was not found.';
        } else if (error.status >= 500) {
          message = 'Server error. Please try again later.';
        }

        // Only show toast for non-silent errors (avoid double-toasting)
        // Components that handle their own errors should pass X-Silent: true
        const silent = request.headers.has('X-Silent');
        if (!silent) {
          this.toast.error(message);
        }

        console.error(`[HTTP ${error.status}] ${request.method} ${request.url}`, error);
        return throwError(() => error);
      }),
      finalize(() => {
        this.activeRequests--;
      })
    );
  }

  get isLoading(): boolean {
    return this.activeRequests > 0;
  }
}
