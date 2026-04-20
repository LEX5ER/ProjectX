import { auth } from "../auth/session";

export type ProjectStatus = "Draft" | "Active" | "OnHold" | "Completed" | "Archived";

export interface ProjectRecord {
  id: string;
  code: string;
  name: string;
  description: string;
  ownerName: string;
  status: ProjectStatus;
  startDate: string | null;
  targetDate: string | null;
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

const defaultApiBaseUrl = "https://localhost:7013";

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

export async function listProjectsPage(page = 1, pageSize = 10): Promise<PagedResult<ProjectRecord>> {
  return request<PagedResult<ProjectRecord>>(`/api/projects${buildPagedQuery(page, pageSize)}`);
}

export async function listProjects(): Promise<ProjectRecord[]> {
  const items: ProjectRecord[] = [];
  let page = 1;

  while (true) {
    const result = await listProjectsPage(page, 100);
    items.push(...result.items);

    if (result.totalPages <= 0 || page >= result.totalPages) {
      break;
    }

    page += 1;
  }

  return items;
}

export async function createProject(payload: {
  code: string;
  name: string;
  description: string;
  ownerName: string;
  status: ProjectStatus;
  startDate: string | null;
  targetDate: string | null;
}): Promise<ProjectRecord> {
  return request<ProjectRecord>("/api/projects", jsonRequest("POST", payload));
}

export async function updateProject(
  id: string,
  payload: {
    code: string;
    name: string;
    description: string;
    ownerName: string;
    status: ProjectStatus;
    startDate: string | null;
    targetDate: string | null;
  }
): Promise<ProjectRecord> {
  return request<ProjectRecord>(`/api/projects/${id}`, jsonRequest("PUT", payload));
}

export async function deleteProject(id: string): Promise<void> {
  await request<null>(`/api/projects/${id}`, jsonRequest("DELETE"));
}
