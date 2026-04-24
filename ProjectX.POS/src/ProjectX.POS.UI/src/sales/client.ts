import { auth } from "../auth/session";
import type { PagedResult, ProductStatus } from "../products/client";

export type PaymentMethod = "Cash" | "Card" | "EWallet" | "BankTransfer" | "Other";
export type SaleStatus = "Completed" | "Refunded";

export interface SaleSummaryRecord {
  id: string;
  projectId: string;
  receiptNumber: string;
  customerId: string | null;
  customerName: string;
  cashierUserName: string;
  status: SaleStatus;
  paymentMethod: PaymentMethod;
  totalItems: number;
  totalAmount: number;
  createdAtUtc: string;
  refundedAtUtc: string | null;
  restockedOnRefund: boolean;
}

export interface SaleLineRecord {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  category: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  lineSubtotalAmount: number;
  lineTotalAmount: number;
}

export interface SaleDetailRecord {
  id: string;
  projectId: string;
  receiptNumber: string;
  customerId: string | null;
  customerName: string;
  customerEmail: string;
  cashierUserName: string;
  status: SaleStatus;
  paymentMethod: PaymentMethod;
  subtotalAmount: number;
  lineDiscountAmount: number;
  cartDiscountAmount: number;
  taxRatePercentage: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  changeAmount: number;
  note: string;
  receiptEmail: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  refundedAtUtc: string | null;
  refundReason: string;
  refundedByUserName: string;
  restockedOnRefund: boolean;
  lineItems: SaleLineRecord[];
}

export interface DashboardLowStockRecord {
  id: string;
  code: string;
  name: string;
  stockQuantity: number;
  status: ProductStatus;
}

export interface DashboardSummaryRecord {
  totalProducts: number;
  totalCustomers: number;
  lowStockProducts: number;
  todaySalesCount: number;
  todaySalesAmount: number;
  todayRefundedAmount: number;
  todayNetSales: number;
  inventoryValue: number;
  lowStockItems: DashboardLowStockRecord[];
  recentSales: SaleSummaryRecord[];
}

const defaultApiBaseUrl = "";

function resolveApiUrl(path: string): string {
  if (/^https?:\/\//.test(path)) {
    return path;
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL?.trim() || defaultApiBaseUrl;
  return `${baseUrl.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  return auth.authorizedJson<T>(resolveApiUrl(path), init);
}

function jsonRequest(method: string, payload?: object): RequestInit {
  const headers = new Headers();

  if (payload) {
    headers.set("Content-Type", "application/json");
  }

  return {
    method,
    headers,
    body: payload ? JSON.stringify(payload) : undefined
  };
}

function buildPagedQuery(search: string, page: number, pageSize: number): string {
  const query = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString()
  });

  if (search.trim()) {
    query.set("search", search.trim());
  }

  return `?${query.toString()}`;
}

export async function listSalesPage(search = "", page = 1, pageSize = 10): Promise<PagedResult<SaleSummaryRecord>> {
  return request<PagedResult<SaleSummaryRecord>>(`/api/sales${buildPagedQuery(search, page, pageSize)}`);
}

export async function getSale(id: string): Promise<SaleDetailRecord> {
  return request<SaleDetailRecord>(`/api/sales/${id}`);
}

export async function checkoutSale(payload: {
  customerId: string | null;
  cartDiscountAmount: number;
  taxRatePercentage: number;
  paymentMethod: PaymentMethod;
  amountReceived: number;
  note: string;
  receiptEmail: string;
  lines: Array<{
    productId: string;
    quantity: number;
    discountAmount: number;
  }>;
}): Promise<SaleDetailRecord> {
  return request<SaleDetailRecord>("/api/sales/checkout", jsonRequest("POST", payload));
}

export async function refundSale(
  id: string,
  payload: {
    reason: string;
    restock: boolean;
  }
): Promise<SaleDetailRecord> {
  return request<SaleDetailRecord>(`/api/sales/${id}/refund`, jsonRequest("POST", payload));
}

export async function getDashboardSummary(): Promise<DashboardSummaryRecord> {
  return request<DashboardSummaryRecord>("/api/dashboard/summary");
}
