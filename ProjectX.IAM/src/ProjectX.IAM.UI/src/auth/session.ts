import { reactive } from "vue";

export interface AuthProject {
  id: string;
  name: string;
}

export interface AuthUser {
  id: string;
  userName: string;
  email: string;
  hasGlobalFullAccess: boolean;
  projects: AuthProject[];
  activeProjectId: string | null;
  activeProjectName: string | null;
  globalRoles: string[];
  globalPermissions: string[];
  activeProjectPermissions: string[];
  roles: string[];
  hasAllPermissions: boolean;
  permissions: string[];
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  identityToken: string;
  identityTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: AuthUser;
}

interface JwtPayload {
  sub?: string;
  unique_name?: string;
  email?: string;
  exp?: number;
}

interface ProjectCatalogResponse {
  id: string;
  name: string;
  description: string;
  memberCount: number;
}

interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

interface StoredSession {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  identityToken: string;
  identityTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: AuthUser;
}

const storageKey = "projectx-iam.session";
const projectHeaderName = "X-Project-Id";
const clientApplicationHeaderName = "X-Client-Application";
const clientApplicationName = "IAM";

const state = reactive({
  initialized: false,
  authenticating: false,
  accessToken: "",
  accessTokenExpiresAtUtc: "",
  identityToken: "",
  identityTokenExpiresAtUtc: "",
  refreshToken: "",
  refreshTokenExpiresAtUtc: "",
  user: null as AuthUser | null
});

let refreshPromise: Promise<boolean> | null = null;

function resolveApiUrl(path: string): string {
  if (/^https?:\/\//.test(path)) {
    return path;
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

  if (!baseUrl) {
    return path;
  }

  return `${baseUrl.replace(/\/$/, "")}/${path.replace(/^\//, "")}`;
}

function decodeJwtPayload(token: string): JwtPayload {
  const [, payload] = token.split(".");

  if (!payload) {
    return {};
  }

  const normalizedPayload = payload.replace(/-/g, "+").replace(/_/g, "/");
  const paddedPayload = normalizedPayload.padEnd(Math.ceil(normalizedPayload.length / 4) * 4, "=");
  const binary = atob(paddedPayload);
  const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
  const json = new TextDecoder().decode(bytes);

  return JSON.parse(json) as JwtPayload;
}

function isExpired(token: string, graceSeconds = 0): boolean {
  const payload = decodeJwtPayload(token);

  if (!payload.exp) {
    return true;
  }

  return payload.exp * 1000 <= Date.now() + graceSeconds * 1000;
}

function hydrateUser(identityToken: string, fallbackUser: AuthUser | null): AuthUser | null {
  if (!fallbackUser && !identityToken) {
    return null;
  }

  const payload = identityToken ? decodeJwtPayload(identityToken) : {};

  return {
    id: payload.sub ?? fallbackUser?.id ?? "",
    userName: payload.unique_name ?? fallbackUser?.userName ?? "",
    email: payload.email ?? fallbackUser?.email ?? "",
    hasGlobalFullAccess: fallbackUser?.hasGlobalFullAccess ?? false,
    projects: fallbackUser?.projects ?? [],
    activeProjectId: fallbackUser?.activeProjectId ?? null,
    activeProjectName: fallbackUser?.activeProjectName ?? null,
    globalRoles: fallbackUser?.globalRoles ?? [],
    globalPermissions: fallbackUser?.globalPermissions ?? [],
    activeProjectPermissions: fallbackUser?.activeProjectPermissions ?? [],
    roles: fallbackUser?.roles ?? [],
    hasAllPermissions: fallbackUser?.hasAllPermissions ?? false,
    permissions: fallbackUser?.permissions ?? []
  };
}

function persistSession(session: StoredSession): void {
  localStorage.setItem(storageKey, JSON.stringify(session));
}

function persistCurrentState(): void {
  if (!state.user || !state.accessToken || !state.refreshToken) {
    clearPersistedSession();
    return;
  }

  persistSession({
    accessToken: state.accessToken,
    accessTokenExpiresAtUtc: state.accessTokenExpiresAtUtc,
    identityToken: state.identityToken,
    identityTokenExpiresAtUtc: state.identityTokenExpiresAtUtc,
    refreshToken: state.refreshToken,
    refreshTokenExpiresAtUtc: state.refreshTokenExpiresAtUtc,
    user: state.user
  });
}

function clearPersistedSession(): void {
  localStorage.removeItem(storageKey);
}

function applySession(session: StoredSession): void {
  state.accessToken = session.accessToken;
  state.accessTokenExpiresAtUtc = session.accessTokenExpiresAtUtc;
  state.identityToken = session.identityToken;
  state.identityTokenExpiresAtUtc = session.identityTokenExpiresAtUtc;
  state.refreshToken = session.refreshToken;
  state.refreshTokenExpiresAtUtc = session.refreshTokenExpiresAtUtc;
  state.user = hydrateUser(session.identityToken, session.user);
  persistSession({
    ...session,
    user: state.user!
  });
}

function applyProjectCatalog(projects: AuthProject[]): void {
  if (!state.user) {
    return;
  }

  const nextProjects = [...projects].sort((left, right) => left.name.localeCompare(right.name));
  const activeProject = nextProjects.find(project => project.id === state.user?.activeProjectId);

  state.user = {
    ...state.user,
    projects: nextProjects,
    activeProjectName: activeProject?.name ?? state.user.activeProjectName
  };

  persistCurrentState();
}

function clearSession(): void {
  state.accessToken = "";
  state.accessTokenExpiresAtUtc = "";
  state.identityToken = "";
  state.identityTokenExpiresAtUtc = "";
  state.refreshToken = "";
  state.refreshTokenExpiresAtUtc = "";
  state.user = null;
  clearPersistedSession();
}

function getStoredSession(): StoredSession | null {
  const rawValue = localStorage.getItem(storageKey);

  if (!rawValue) {
    return null;
  }

  try {
    return JSON.parse(rawValue) as StoredSession;
  } catch {
    clearPersistedSession();
    return null;
  }
}

function withProjectHeader(headers: Headers, projectId: string | null | undefined): Headers {
  if (projectId) {
    headers.set(projectHeaderName, projectId);
  } else {
    headers.delete(projectHeaderName);
  }

  return headers;
}

function withClientApplicationHeader(headers: Headers): Headers {
  headers.set(clientApplicationHeaderName, clientApplicationName);
  return headers;
}

async function requestJson<T>(path: string, init: RequestInit): Promise<T> {
  const headers = new Headers(init.headers);
  withClientApplicationHeader(headers);

  const response = await fetch(resolveApiUrl(path), {
    ...init,
    headers
  });
  const payload = response.status === 204 ? null : await response.json().catch(() => null);

  if (!response.ok) {
    const detail = typeof payload?.detail === "string"
      ? payload.detail
      : typeof payload?.title === "string"
        ? payload.title
        : "The request failed.";

    throw new Error(detail);
  }

  return payload as T;
}

async function fetchProfile(projectId: string | null): Promise<AuthUser> {
  const hasSession = await ensureFreshAccessToken();

  if (!hasSession || !state.accessToken) {
    throw new Error("You are not authenticated.");
  }

  const headers = new Headers();
  headers.set("Authorization", `Bearer ${state.accessToken}`);
  withProjectHeader(headers, projectId);
  withClientApplicationHeader(headers);

  const user = await requestJson<AuthUser>("/api/auth/me", {
    method: "GET",
    headers
  });

  state.user = user;
  persistCurrentState();
  return user;
}

async function refreshInternal(): Promise<boolean> {
  if (!state.refreshToken) {
    return false;
  }

  try {
    const headers = new Headers({
      "Content-Type": "application/json"
    });
    withProjectHeader(headers, state.user?.activeProjectId ?? null);
    withClientApplicationHeader(headers);

    const session = await requestJson<TokenResponse>("/api/auth/refresh", {
      method: "POST",
      headers,
      body: JSON.stringify({
        refreshToken: state.refreshToken
      })
    });

    applySession(session);
    return true;
  } catch {
    clearSession();
    return false;
  }
}

async function ensureFreshAccessToken(): Promise<boolean> {
  if (!state.refreshToken) {
    return false;
  }

  if (state.accessToken && !isExpired(state.accessToken, 30)) {
    return true;
  }

  if (!refreshPromise) {
    refreshPromise = refreshInternal().finally(() => {
      refreshPromise = null;
    });
  }

  return refreshPromise;
}

async function syncProjectsFromServer(): Promise<void> {
  if (!state.user) {
    return;
  }

  const hasSession = await ensureFreshAccessToken();

  if (!hasSession || !state.accessToken) {
    return;
  }

  try {
    const projects = await fetchAllProjectsFromServer(state.user.activeProjectId);

    applyProjectCatalog(projects.map(project => ({
      id: project.id,
      name: project.name
    })));
  } catch {
    // Project catalog sync is best-effort so authentication remains usable when PM is unavailable.
  }
}

async function fetchProjectCatalogPage(projectId: string | null, page: number, pageSize: number): Promise<PagedResponse<ProjectCatalogResponse>> {
  if (!state.accessToken) {
    throw new Error("You are not authenticated.");
  }

  const headers = new Headers();
  headers.set("Authorization", `Bearer ${state.accessToken}`);
  withProjectHeader(headers, projectId);
  withClientApplicationHeader(headers);

  return requestJson<PagedResponse<ProjectCatalogResponse>>(`/api/projects?page=${page}&pageSize=${pageSize}`, {
    method: "GET",
    headers
  });
}

async function fetchAllProjectsFromServer(projectId: string | null): Promise<ProjectCatalogResponse[]> {
  const pageSize = 100;
  const projects: ProjectCatalogResponse[] = [];
  let page = 1;

  while (true) {
    const response = await fetchProjectCatalogPage(projectId, page, pageSize);
    projects.push(...response.items);

    if (response.totalPages <= 0 || page >= response.totalPages) {
      break;
    }

    page += 1;
  }

  return projects;
}

export const auth = {
  state,

  get isAuthenticated(): boolean {
    return Boolean(state.user && state.accessToken && state.refreshToken);
  },

  async initialize(): Promise<void> {
    if (state.initialized) {
      return;
    }

    const session = getStoredSession();

    if (session) {
      applySession(session);

      if (isExpired(session.accessToken, 30)) {
        await ensureFreshAccessToken();
      }

      void syncProjectsFromServer();
    }

    state.initialized = true;
  },

  async login(userNameOrEmail: string, password: string): Promise<void> {
    state.authenticating = true;

    try {
      const session = await requestJson<TokenResponse>("/api/auth/login", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          userNameOrEmail,
          password
        })
      });

      applySession(session);
      state.initialized = true;
      void syncProjectsFromServer();
    } finally {
      state.authenticating = false;
    }
  },

  async refresh(): Promise<boolean> {
    return ensureFreshAccessToken();
  },

  replaceProjects(projects: AuthProject[]): void {
    applyProjectCatalog(projects);
  },

  async setActiveProject(projectId: string | null): Promise<void> {
    if (!state.user) {
      throw new Error("You are not authenticated.");
    }

    if (projectId
      && !state.user.hasGlobalFullAccess
      && !state.user.projects.some(project => project.id === projectId)) {
      throw new Error("The selected project is not available for this session.");
    }

    const user = await fetchProfile(projectId);

    if (projectId && user.activeProjectId !== projectId) {
      throw new Error("The selected project is not available for this session.");
    }
  },

  async logout(): Promise<void> {
    const refreshToken = state.refreshToken;
    const activeProjectId = state.user?.activeProjectId ?? null;

    clearSession();

    if (!refreshToken) {
      return;
    }

    const headers = new Headers({
      "Content-Type": "application/json"
    });
    withProjectHeader(headers, activeProjectId);
    withClientApplicationHeader(headers);

    await fetch(resolveApiUrl("/api/auth/logout"), {
      method: "POST",
      headers,
      body: JSON.stringify({
        refreshToken
      })
    }).catch(() => undefined);
  },

  async authorizedJson<T>(path: string, init: RequestInit = {}): Promise<T> {
    const hasSession = await ensureFreshAccessToken();

    if (!hasSession || !state.accessToken) {
      throw new Error("You are not authenticated.");
    }

    const headers = new Headers(init.headers);
    headers.set("Authorization", `Bearer ${state.accessToken}`);
    withProjectHeader(headers, state.user?.activeProjectId ?? null);
    withClientApplicationHeader(headers);

    let response = await fetch(resolveApiUrl(path), {
      ...init,
      headers
    });

    if (response.status === 401 && await ensureFreshAccessToken() && state.accessToken) {
      headers.set("Authorization", `Bearer ${state.accessToken}`);
      withProjectHeader(headers, state.user?.activeProjectId ?? null);
      response = await fetch(resolveApiUrl(path), {
        ...init,
        headers
      });
    }

    const payload = response.status === 204 ? null : await response.json().catch(() => null);

    if (!response.ok) {
      const detail = typeof payload?.detail === "string"
        ? payload.detail
        : typeof payload?.title === "string"
          ? payload.title
          : "The request failed.";

      throw new Error(detail);
    }

    return payload as T;
  }
};
