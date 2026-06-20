// src/app/fmc-interfaces/payment.interface.ts

// ── Contribution entity ────────────────────────────────────────────────────
export interface IContribution {
  contributionId: number;
  complaintId: number;
  citizenUserId: number;
  amount: number;
  transactionRef: string;
  paymentStatus: string;        // 'Pending' | 'Success' | 'Failed' | 'Refunded'
  contributedAt: string;
}

// ── Funding total (GET /api/Payment/GetFundingTotal) ─────────────────────
export interface IFundingTotalResponse {
  success: boolean;
  complaintId: number;
  fundingTotal: number;
}

// ── Request Payloads (verified against backend Models/) ──────────────────

// TransactionRef must be obtained from the payment gateway BEFORE calling this
export interface ICreateContributionRequest {
  complaintId: number;
  citizenUserId: number;
  amount: number;
  transactionRef: string;
}

export interface IUpdatePaymentStatusRequest {
  transactionRef: string;
  newStatus: string;            // 'Success' | 'Failed' | 'Refunded'
  failureReason?: string;
}

// ── Razorpay (Phase 5) ────────────────────────────────────────────────────

export interface IRazorpayConfigResponse {
  success:     boolean;
  configured:  boolean;
  keyId:       string | null;
  currency:    string;
  companyName: string;
}

export interface IRazorpayCreateOrderRequest {
  complaintId:   number;
  citizenUserId: number;
  amount:        number;        // rupees
}

export interface IRazorpayCreateOrderResponse {
  success:     boolean;
  message?:    string;
  demoMode:    boolean;
  keyId:       string;
  orderId:     string;          // "order_…" or "DEMO_…"
  amountPaise: number;
  currency:    string;
  receipt:     string;
  companyName: string;
}

export interface IRazorpayVerifyRequest {
  complaintId:        number;
  citizenUserId:      number;
  amount:             number;   // rupees
  razorpayOrderId:    string;
  razorpayPaymentId:  string;
  razorpaySignature:  string;
}

export interface IRazorpayVerifyResponse {
  success:        boolean;
  message?:       string;
  contributionId: number;
  transactionRef: string;
}
