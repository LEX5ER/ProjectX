import { auth } from "../auth/session";
import type { PagedResult } from "../products/client";

export interface CustomerRecord {
  id: string;
  projectId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phone: string;
  notes: string;
  marketingOptIn: boolean;
  taxExempt: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
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

export async function listCustomersPage(search = "", page = 1, pageSize = 10): Promise<PagedResult<CustomerRecord>> {
  return request<PagedResult<CustomerRecord>>(`/api/customers${buildPagedQuery(search, page, pageSize)}`);
}

export async function listCustomers(search = ""): Promise<CustomerRecord[]> {
  const items: CustomerRecord[] = [];
  let page = 1;

  while (true) {
    const result = await listCustomersPage(search, page, 100);
    items.push(...result.items);

    if (result.totalPages <= 0 || page >= result.totalPages) {
      break;
    }

    page += 1;
  }

  return items;
}

export async function createCustomer(payload: {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  notes: string;
  marketingOptIn: boolean;
  taxExempt: boolean;
}): Promise<CustomerRecord> {
  return request<CustomerRecord>("/api/customers", jsonRequest("POST", payload));
}

export async function updateCustomer(
  id: string,
  payload: {
    firstName: string;
    lastName: string;
    email: string;
    phone: string;
    notes: string;
    marketingOptIn: boolean;
    taxExempt: boolean;
  }
): Promise<CustomerRecord> {
  return request<CustomerRecord>(`/api/customers/${id}`, jsonRequest("PUT", payload));
}

export async function deleteCustomer(id: string): Promise<void> {
  await request<null>(`/api/customers/${id}`, jsonRequest("DELETE"));
}
