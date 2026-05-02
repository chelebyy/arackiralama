import { post } from './client';
import { API_ENDPOINTS } from './config';

export interface PaymentCardRequest {
  holderName: string;
  number: string;
  expiryMonth: string;
  expiryYear: string;
  cvv: string;
}

export interface CreatePaymentIntentRequest {
  reservationId: string;
  idempotencyKey: string;
  installmentCount?: number;
  card: PaymentCardRequest;
}

export interface PaymentIntentResponse {
  paymentIntentId: string;
  paymentKind: string;
  status: string;
  redirectUrl: string | null;
  amount: number;
  currency: string;
  expiresAt: string;
  transactionId: string | null;
  reservationStatus: string;
}

export interface ThreeDsReturnRequest {
  bankResponse: string;
}

export async function createPaymentIntent(
  data: CreatePaymentIntentRequest
): Promise<PaymentIntentResponse> {
  return post<PaymentIntentResponse>(API_ENDPOINTS.payments.createIntent, data);
}

export async function complete3dsReturn(
  intentId: string,
  data: ThreeDsReturnRequest
): Promise<PaymentIntentResponse> {
  return post<PaymentIntentResponse>(
    API_ENDPOINTS.payments.complete3ds(intentId),
    data
  );
}
