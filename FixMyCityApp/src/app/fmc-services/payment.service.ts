// src/app/fmc-services/payment.service.ts
//
// Phase 5 (2026-05-19) — proper end-to-end Razorpay integration.
//
// Flow (server-orchestrated, per Razorpay's recommended pattern):
//
//   1.  contributeViaRazorpay(complaintId, citizenUserId, amount, prefill)
//   2.  POST /api/Payment/CreateRazorpayOrder
//          → { orderId, amountPaise, keyId, currency, companyName, demoMode }
//   3.  open Razorpay checkout with that order_id + keyId
//   4.  on modal success → SDK callback delivers
//          { razorpay_order_id, razorpay_payment_id, razorpay_signature }
//   5.  POST /api/Payment/VerifyRazorpayPayment with those three values
//          → server recomputes HMAC-SHA256(order|payment, key_secret),
//            persists the contribution row, flips PaymentStatus to Success.
//   6.  Promise resolves with the new contributionId.
//
// Demo mode (when the server reports `demoMode: true`):
//   - The server returns a synthetic DEMO_… orderId.
//   - The Razorpay SDK is NOT opened — we skip straight to step 5 with the
//     synthetic order_id. The server detects DEMO_… and records the
//     contribution without HMAC verification (still gated by JWT auth).
//   - Exactly mirrors the old demo bypass so dev environments work without
//     provisioning real keys.

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { environment } from '../../environments/environment';
import {
  IContribution,
  IFundingTotalResponse,
  ICreateContributionRequest,
  IUpdatePaymentStatusRequest,
  IRazorpayConfigResponse,
  IRazorpayCreateOrderRequest,
  IRazorpayCreateOrderResponse,
  IRazorpayVerifyRequest,
  IRazorpayVerifyResponse,
} from '../fmc-interfaces/payment.interface';
import { IApiResponse } from '../fmc-interfaces/api-response.interface';

declare const Razorpay: any;

/**
 * Subset of Razorpay's checkout options we set ourselves. Anything not listed
 * here is left to the caller / SDK defaults. See
 * https://razorpay.com/docs/payments/payment-gateway/web-integration/standard/checkout-options/
 */
export interface IRazorpayCheckoutPrefill {
  name?:     string;
  email?:    string;
  contact?:  string;   // phone — used for OTP, helpful on test keys
}

export interface IContributeResult {
  success:        boolean;
  message:        string;
  contributionId: number;
  transactionRef: string;
  demoMode:       boolean;
}

@Injectable({ providedIn: 'root' })
export class PaymentService {

  private baseUrl = `${environment.apiBaseUrl}/api/Payment`;

  // Cached config from /api/Payment/GetRazorpayConfig — populated on first call.
  private configCache: IRazorpayConfigResponse | null = null;

  // Phase 7 (2026-05-20) — single-flight promise that resolves once the
  // Razorpay checkout.js SDK has loaded. We inject the <script> on demand
  // the first time someone calls contributeViaRazorpay() so the
  //   "Unrecognized feature: 'otp-credentials'" warning and the
  //   "POST .../lumberjack.../logz ERR_BLOCKED_BY_CLIENT" noise
  // are confined to the contribution flow instead of firing on every
  // route, including pages that have nothing to do with payments.
  private static readonly RAZORPAY_SDK_URL = 'https://checkout.razorpay.com/v1/checkout.js';
  private razorpaySdkPromise: Promise<void> | null = null;

  constructor(private http: HttpClient) {}

  /**
   * Returns a promise that resolves when `window.Razorpay` is available.
   * Idempotent — second call piggybacks on the first. Reuses a tag if one
   * is already in the DOM (e.g. left over from a previous build that still
   * had the static <script> in index.html).
   */
  private ensureRazorpaySdk(): Promise<void> {
    if (typeof (window as any).Razorpay === 'function') {
      return Promise.resolve();
    }
    if (this.razorpaySdkPromise) {
      return this.razorpaySdkPromise;
    }
    this.razorpaySdkPromise = new Promise<void>((resolve, reject) => {
      // Don't add the script twice if a previous build / hot reload put it there.
      let s: HTMLScriptElement | null = document.querySelector(
        `script[src="${PaymentService.RAZORPAY_SDK_URL}"]`
      );
      if (s && typeof (window as any).Razorpay === 'function') {
        return resolve();
      }
      if (!s) {
        s = document.createElement('script');
        s.src   = PaymentService.RAZORPAY_SDK_URL;
        s.async = true;
        // Eat the network error if ad blockers nuke the script entirely.
        s.onerror = () => reject(new Error(
          'Could not load the Razorpay checkout script. ' +
          'A browser extension may be blocking it.'
        ));
        document.head.appendChild(s);
      }
      s.addEventListener('load', () => {
        if (typeof (window as any).Razorpay === 'function') {
          resolve();
        } else {
          reject(new Error('Razorpay SDK loaded but window.Razorpay is missing.'));
        }
      }, { once: true });
    });
    // If loading fails, clear the cache so a retry can try again from scratch.
    this.razorpaySdkPromise.catch(() => { this.razorpaySdkPromise = null; });
    return this.razorpaySdkPromise;
  }

  // ── Backend bootstrap ─────────────────────────────────────────────────────

  /**
   * Fetches the public Razorpay configuration (keyId, currency, company,
   * demo flag). The KeySecret is NEVER returned. Cached after first hit.
   */
  getRazorpayConfig(force = false): Observable<IRazorpayConfigResponse> {
    if (this.configCache && !force) {
      return new Observable(sub => { sub.next(this.configCache!); sub.complete(); });
    }
    return this.http.get<IRazorpayConfigResponse>(`${this.baseUrl}/GetRazorpayConfig`)
      .pipe(
        catchError(this.handleError),
      );
  }

  // ── End-to-end contribution flow ─────────────────────────────────────────

  /**
   * The single entry-point any UI component should call.
   * Handles the full order → checkout → verify → persist round-trip and
   * resolves with the new contributionId. Throws on signature failure /
   * verification rejection / user cancellation.
   */
  async contributeViaRazorpay(
    complaintId:   number,
    citizenUserId: number,
    amount:        number,
    prefill?:      IRazorpayCheckoutPrefill,
  ): Promise<IContributeResult> {

    if (!(amount > 0)) {
      throw new Error('Amount must be greater than zero.');
    }

    // 1. Create the Razorpay Order on the server (binds amount + receipt + notes
    //    to a gateway-issued order_id that the modal will reference).
    const orderPayload: IRazorpayCreateOrderRequest = { complaintId, citizenUserId, amount };
    const order = await firstValueFrom(
      this.http.post<IRazorpayCreateOrderResponse>(
        `${this.baseUrl}/CreateRazorpayOrder`, orderPayload,
      ).pipe(catchError(this.handleError)));

    if (!order?.success) {
      throw new Error(order?.message ?? 'Could not create the payment order.');
    }

    // 2a. Demo mode — skip the SDK and call verify directly.
    if (order.demoMode) {
      // Server already returned a DEMO_… orderId we can replay verbatim.
      const verifyPayload: IRazorpayVerifyRequest = {
        complaintId,
        citizenUserId,
        amount,
        razorpayOrderId:   order.orderId,
        razorpayPaymentId: '',
        razorpaySignature: '',
      };
      const verified = await firstValueFrom(
        this.http.post<IRazorpayVerifyResponse>(
          `${this.baseUrl}/VerifyRazorpayPayment`, verifyPayload,
        ).pipe(catchError(this.handleError)));

      if (!verified?.success) {
        throw new Error(verified?.message ?? 'Demo contribution could not be recorded.');
      }
      return {
        success:        true,
        message:        verified.message ?? 'Demo payment recorded.',
        contributionId: verified.contributionId,
        transactionRef: verified.transactionRef,
        demoMode:       true,
      };
    }

    // 2b. Real flow — make sure the Razorpay SDK is loaded, then open the
    // modal with the server-issued order_id.
    try {
      await this.ensureRazorpaySdk();
    } catch (err: any) {
      throw new Error(
        err?.message ??
        'Could not load the Razorpay payment SDK. Please disable any ad blocker for this page and try again.'
      );
    }
    if (typeof Razorpay === 'undefined') {
      throw new Error('Razorpay SDK not available. Refresh the page and try again.');
    }

    const sdkPayload = await new Promise<{
      razorpay_order_id:   string;
      razorpay_payment_id: string;
      razorpay_signature:  string;
    }>((resolve, reject) => {
      const opts: any = {
        key:         order.keyId,
        amount:      order.amountPaise,
        currency:    order.currency,
        name:        order.companyName || 'FixMyCity',
        description: `Contribution toward complaint #${complaintId}`,
        order_id:    order.orderId,
        prefill:     prefill ?? {},
        theme:       { color: '#2563eb' },
        handler: (resp: any) => resolve({
          razorpay_order_id:   resp.razorpay_order_id   || order.orderId,
          razorpay_payment_id: resp.razorpay_payment_id || '',
          razorpay_signature:  resp.razorpay_signature  || '',
        }),
        modal: {
          ondismiss: () => reject(new Error('Payment cancelled.')),
        },
      };
      try {
        const rzp = new Razorpay(opts);
        // Surface gateway-side payment failures (card declined, etc.)
        rzp.on?.('payment.failed', (e: any) => {
          reject(new Error(e?.error?.description ?? 'Payment failed on the gateway.'));
        });
        rzp.open();
      } catch (err) {
        reject(new Error('Could not open the Razorpay checkout.'));
      }
    });

    // 3. Verify the signature server-side and persist the contribution.
    const verifyPayload: IRazorpayVerifyRequest = {
      complaintId,
      citizenUserId,
      amount,
      razorpayOrderId:   sdkPayload.razorpay_order_id,
      razorpayPaymentId: sdkPayload.razorpay_payment_id,
      razorpaySignature: sdkPayload.razorpay_signature,
    };
    const verified = await firstValueFrom(
      this.http.post<IRazorpayVerifyResponse>(
        `${this.baseUrl}/VerifyRazorpayPayment`, verifyPayload,
      ).pipe(catchError(this.handleError)));

    if (!verified?.success) {
      throw new Error(verified?.message ?? 'Signature verification failed.');
    }
    return {
      success:        true,
      message:        verified.message ?? 'Payment verified.',
      contributionId: verified.contributionId,
      transactionRef: verified.transactionRef,
      demoMode:       false,
    };
  }

  // ── Backend API calls (legacy create + status) ───────────────────────────

  createContribution(payload: ICreateContributionRequest): Observable<IApiResponse & { contributionId: number }> {
    return this.http.post<IApiResponse & { contributionId: number }>(
      `${this.baseUrl}/CreateContribution`, payload
    ).pipe(catchError(this.handleError));
  }

  updatePaymentStatus(payload: IUpdatePaymentStatusRequest): Observable<IApiResponse> {
    return this.http.put<IApiResponse>(`${this.baseUrl}/UpdatePaymentStatus`, payload)
      .pipe(catchError(this.handleError));
  }

  getContributionsByComplaint(complaintId: number): Observable<IContribution[]> {
    return this.http.get<IContribution[]>(
      `${this.baseUrl}/GetContributionsByComplaint?complaintId=${complaintId}`
    ).pipe(catchError(this.handleError));
  }

  getContributionsByCitizen(citizenUserId: number): Observable<IContribution[]> {
    return this.http.get<IContribution[]>(
      `${this.baseUrl}/GetContributionsByCitizen?citizenUserId=${citizenUserId}`
    ).pipe(catchError(this.handleError));
  }

  getFundingTotal(complaintId: number): Observable<IFundingTotalResponse> {
    return this.http.get<IFundingTotalResponse>(
      `${this.baseUrl}/GetFundingTotal?complaintId=${complaintId}`
    ).pipe(catchError(this.handleError));
  }

  // ── Error handler ────────────────────────────────────────────────────────

  private handleError(error: HttpErrorResponse): Observable<never> {
    console.error('[PaymentService]', error);
    return throwError(() => error);
  }
}
