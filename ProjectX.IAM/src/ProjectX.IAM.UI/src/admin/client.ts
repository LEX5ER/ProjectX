import { auth } from "../auth/session";

export type RoleScope = "Global" | "Project";

export interface ProjectRecord {
  id: string;
  name: string;
  description: string;
  memberCount: number;
}

export interface PermissionRecord {
  id: string;
  name: string;
  description: string;
  scope: RoleScope;
  projectId: string | null;
  projectName: string | null;
  roleCount: number;
}

export interface RoleRecord {
  id: string;
  name: string;
  description: string;
  scope: RoleScope;
  projectId: string | null;
  projectName: string | null;
  isProtected: boolean;
  hasAllPermissions: boolean;
  assignmentCount: number;
  permissions: PermissionRecord[];
}

export interface UserAssignmentRecord {
  id: string;
  roleId: string;
  roleName: string;
  scope: RoleScope;
  projectId: string | null;
  projectName: string | null;
  isProtected: boolean;
  hasAllPermissions: boolean;
  permissions: string[];
}

export interface UserRecord {
  id: string;
  isProtected: boolean;
  userName: string;
  email: string;
  assignments: UserAssignmentRecord[];
  permissions: string[];
}

export type AuthenticationAuditAction = "Login" | "Logout";

export type AuthenticationAuditOutcome = "Succeeded" | "Failed";

export interface AuthenticationAuditRecord {
  id: string;
  occurredAtUtc: string;
  action: AuthenticationAuditAction;
  outcome: AuthenticationAuditOutcome;
  userId: string | null;
  userNameOrEmail: string | null;
  projectId: string | null;
  projectName: string | null;
  failureReason: string | null;
  clientApplication: string | null;
  ipAddress: string | null;
  userAgent: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

function jsonRequestInit(method: string, payload?: object): RequestInit {
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

async function fetchAllPages<T>(
  loadPage: (page: number, pageSize: number) => Promise<PagedResult<T>>,
  pageSize = 100
): Promise<T[]> {
  const items: T[] = [];
  let page = 1;

  while (true) {
    const result = await loadPage(page, pageSize);
    items.push(...result.items);

    if (result.totalPages <= 0 || page >= result.totalPages) {
      break;
    }

    page += 1;
  }

  return items;
}

export async function listProjectsPage(page = 1, pageSize = 10): Promise<PagedResult<ProjectRecord>> {
  return auth.authorizedJson<PagedResult<ProjectRecord>>(`/api/projects${buildPagedQuery(page, pageSize)}`);
}

export async function listProjects(): Promise<ProjectRecord[]> {
  return fetchAllPages((page, pageSize) => listProjectsPage(page, pageSize));
}

export async function listRolesPage(page = 1, pageSize = 20): Promise<PagedResult<RoleRecord>> {
  return auth.authorizedJson<PagedResult<RoleRecord>>(`/api/roles${buildPagedQuery(page, pageSize)}`);
}

export async function listRoles(): Promise<RoleRecord[]> {
  return fetchAllPages((page, pageSize) => listRolesPage(page, pageSize));
}

export async function createRole(payload: {
  name: string;
  description: string;
  scope: RoleScope;
}): Promise<RoleRecord> {
  return auth.authorizedJson<RoleRecord>("/api/roles", jsonRequestInit("POST", payload));
}

export async function updateRole(
  id: string,
  payload: { name: string; description: string; scope: RoleScope }
): Promise<RoleRecord> {
  return auth.authorizedJson<RoleRecord>(`/api/roles/${id}`, jsonRequestInit("PUT", payload));
}

export async function updateRolePermissions(id: string, payload: { permissionIds: string[] }): Promise<RoleRecord> {
  return auth.authorizedJson<RoleRecord>(`/api/roles/${id}/permissions`, jsonRequestInit("PUT", payload));
}

export async function deleteRole(id: string): Promise<void> {
  await auth.authorizedJson<null>(`/api/roles/${id}`, jsonRequestInit("DELETE"));
}

export async function listPermissionsPage(page = 1, pageSize = 20): Promise<PagedResult<PermissionRecord>> {
  return auth.authorizedJson<PagedResult<PermissionRecord>>(`/api/permissions${buildPagedQuery(page, pageSize)}`);
}

export async function listPermissions(): Promise<PermissionRecord[]> {
  return fetchAllPages((page, pageSize) => listPermissionsPage(page, pageSize));
}

export async function createPermission(payload: {
  name: string;
  description: string;
  scope: RoleScope;
}): Promise<PermissionRecord> {
  return auth.authorizedJson<PermissionRecord>("/api/permissions", jsonRequestInit("POST", payload));
}

export async function updatePermission(
  id: string,
  payload: { name: string; description: string; scope: RoleScope }
): Promise<PermissionRecord> {
  return auth.authorizedJson<PermissionRecord>(`/api/permissions/${id}`, jsonRequestInit("PUT", payload));
}

export async function deletePermission(id: string): Promise<void> {
  await auth.authorizedJson<null>(`/api/permissions/${id}`, jsonRequestInit("DELETE"));
}

export async function listUsersPage(page = 1, pageSize = 15): Promise<PagedResult<UserRecord>> {
  return auth.authorizedJson<PagedResult<UserRecord>>(`/api/users${buildPagedQuery(page, pageSize)}`);
}

export async function listAuthenticationAuditsPage(page = 1, pageSize = 20): Promise<PagedResult<AuthenticationAuditRecord>> {
  return auth.authorizedJson<PagedResult<AuthenticationAuditRecord>>(`/api/authentication-audits${buildPagedQuery(page, pageSize)}`);
}

export async function createUser(payload: {
  userName: string;
  email: string;
  password: string;
  globalRoleIds: string[];
  projectRoleIds: string[];
}): Promise<UserRecord> {
  return auth.authorizedJson<UserRecord>("/api/users", jsonRequestInit("POST", payload));
}

export async function updateUser(
  id: string,
  payload: { userName: string; email: string; password?: string; globalRoleIds: string[]; projectRoleIds: string[] }
): Promise<UserRecord> {
  return auth.authorizedJson<UserRecord>(`/api/users/${id}`, jsonRequestInit("PUT", payload));
}

export async function deleteUser(id: string): Promise<void> {
  await auth.authorizedJson<null>(`/api/users/${id}`, jsonRequestInit("DELETE"));
}
