import { auth } from "../auth/session";

export type ProductStatus = "Draft" | "Active" | "LowStock" | "OutOfStock" | "Archived";

export interface ProductRecord {
  id: string;
  projectId: string;
  code: string;
  name: string;
  description: string;
  category: string;
  unitPrice: number;
  stockQuantity: number;
  status: ProductStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
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

function buildPagedQuery(page: number, pageSize: number): string {
  return `?page=${page}&pageSize=${pageSize}`;
}

export async function listProductsPage(page = 1, pageSize = 10): Promise<PagedResult<ProductRecord>> {
  return request<PagedResult<ProductRecord>>(`/api/products${buildPagedQuery(page, pageSize)}`);
}

export async function listCatalogPage(page = 1, pageSize = 10): Promise<PagedResult<ProductRecord>> {
  return request<PagedResult<ProductRecord>>(`/api/products/catalog${buildPagedQuery(page, pageSize)}`);
}

export async function listProducts(): Promise<ProductRecord[]> {
  const items: ProductRecord[] = [];
  let page = 1;

  while (true) {
    const result = await listProductsPage(page, 100);
    items.push(...result.items);

    if (result.totalPages <= 0 || page >= result.totalPages) {
      break;
    }

    page += 1;
  }

  return items;
}

export async function listCatalog(): Promise<ProductRecord[]> {
  const items: ProductRecord[] = [];
  let page = 1;

  while (true) {
    const result = await listCatalogPage(page, 100);
    items.push(...result.items);

    if (result.totalPages <= 0 || page >= result.totalPages) {
      break;
    }

    page += 1;
  }

  return items;
}

export async function createProduct(payload: {
  code: string;
  name: string;
  description: string;
  category: string;
  unitPrice: number;
  stockQuantity: number;
  status: ProductStatus;
}): Promise<ProductRecord> {
  return request<ProductRecord>("/api/products", jsonRequest("POST", payload));
}

export async function updateProduct(
  id: string,
  payload: {
    code: string;
    name: string;
    description: string;
    category: string;
    unitPrice: number;
    stockQuantity: number;
    status: ProductStatus;
  }
): Promise<ProductRecord> {
  return request<ProductRecord>(`/api/products/${id}`, jsonRequest("PUT", payload));
}

export async function deleteProduct(id: string): Promise<void> {
  await request<null>(`/api/products/${id}`, jsonRequest("DELETE"));
}
