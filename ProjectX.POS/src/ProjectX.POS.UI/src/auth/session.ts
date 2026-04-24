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

interface StoredSession {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  identityToken: string;
  identityTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: AuthUser;
}

const storageKey = "projectx-pos.session";
const projectHeaderName = "X-Project-Id";
const clientApplicationHeaderName = "X-Client-Application";
const clientApplicationName = "POS";
const defaultIamApiBaseUrl = "/iam-api";

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

function resolveIamApiUrl(path: string): string {
  if (/^https?:\/\//.test(path)) {
    return path;
  }

  const baseUrl = import.meta.env.VITE_IAM_API_BASE_URL?.trim() || defaultIamApiBaseUrl;
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

function clearPersistedSession(): void {
  localStorage.removeItem(storageKey);
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

function applySession(session: StoredSession): void {
  state.accessToken = session.accessToken;
  state.accessTokenExpiresAtUtc = session.accessTokenExpiresAtUtc;
  state.identityToken = session.identityToken;
  state.identityTokenExpiresAtUtc = session.identityTokenExpiresAtUtc;
  state.refreshToken = session.refreshToken;
  state.refreshTokenExpiresAtUtc = session.refreshTokenExpiresAtUtc;
  state.user = hydrateUser(session.identityToken, session.user);
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

function getProjects(user: AuthUser | null): AuthProject[] {
  return user?.projects ?? [];
}

function getActiveProject(user: AuthUser | null): AuthProject | null {
  if (!user?.activeProjectId) {
    return null;
  }

  return user.projects.find(project => project.id === user.activeProjectId) ?? null;
}

async function readResponsePayload(response: Response): Promise<{ json: unknown; text: string }> {
  if (response.status === 204) {
    return { json: null, text: "" };
  }

  const text = await response.text();

  if (!text) {
    return { json: null, text: "" };
  }

  try {
    return {
      json: JSON.parse(text),
      text
    };
  } catch {
    return {
      json: null,
      text
    };
  }
}

function describeFailure(payload: unknown, text: string, response: Response, fallbackMessage: string): string {
  if (typeof (payload as { detail?: unknown })?.detail === "string") {
    return (payload as { detail: string }).detail;
  }

  if (typeof (payload as { title?: unknown })?.title === "string") {
    return (payload as { title: string }).title;
  }

  const normalizedText = text.trim();

  if (normalizedText) {
    return normalizedText;
  }

  return `${fallbackMessage} (${response.status} ${response.statusText})`;
}

async function requestIamJson<T>(path: string, init: RequestInit): Promise<T> {
  const headers = new Headers(init.headers);
  withClientApplicationHeader(headers);

  let response: Response;

  try {
    response = await fetch(resolveIamApiUrl(path), {
      ...init,
      headers
    });
  } catch {
    throw new Error("Unable to reach IAM from POS. Make sure the IAM API and the POS UI dev server are running.");
  }

  const { json: payload, text } = await readResponsePayload(response);

  if (!response.ok) {
    throw new Error(describeFailure(payload, text, response, "IAM request failed"));
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

  const user = await requestIamJson<AuthUser>("/api/auth/me", {
    method: "GET",
    headers
  });

  state.user = user;
  persistCurrentState();
  return user;
}

async function switchProject(projectId: string | null): Promise<void> {
  if (!state.user) {
    throw new Error("You are not authenticated.");
  }

  if (projectId && !state.user.projects.some(project => project.id === projectId)) {
    throw new Error("The selected project is not available for this session.");
  }

  const user = await fetchProfile(projectId);

  if (projectId && user.activeProjectId !== projectId) {
    throw new Error("The selected project is not available for this session.");
  }
}

async function ensureProjectSelectionInternal(): Promise<void> {
  const user = state.user;

  if (!user || user.activeProjectId || user.projects.length === 0) {
    return;
  }

  await switchProject(user.projects[0].id);
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

    const session = await requestIamJson<TokenResponse>("/api/auth/refresh", {
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

export const auth = {
  state,

  get isAuthenticated(): boolean {
    return Boolean(state.user && state.accessToken && state.refreshToken);
  },

  getProjects(user: AuthUser | null = state.user): AuthProject[] {
    return getProjects(user);
  },

  getActiveProject(): AuthProject | null {
    return getActiveProject(state.user);
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

      await ensureProjectSelectionInternal();
    }

    state.initialized = true;
  },

  async login(userNameOrEmail: string, password: string): Promise<void> {
    state.authenticating = true;

    try {
      const session = await requestIamJson<TokenResponse>("/api/auth/login", {
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
      await ensureProjectSelectionInternal();
      state.initialized = true;
    } finally {
      state.authenticating = false;
    }
  },

  async refresh(): Promise<boolean> {
    return ensureFreshAccessToken();
  },

  async loadProfile(): Promise<AuthUser> {
    const user = await fetchProfile(state.user?.activeProjectId ?? null);
    await ensureProjectSelectionInternal();
    return user;
  },

  async setActiveProject(projectId: string | null): Promise<void> {
    await switchProject(projectId);
  },

  async ensureProjectSelection(): Promise<void> {
    await ensureProjectSelectionInternal();
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

    await fetch(resolveIamApiUrl("/api/auth/logout"), {
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

    let response: Response;

    try {
      response = await fetch(path, {
        ...init,
        headers
      });
    } catch {
      throw new Error("Unable to reach the POS API. Make sure the POS API and the POS UI dev server are running.");
    }

    if (response.status === 401 && await ensureFreshAccessToken() && state.accessToken) {
      headers.set("Authorization", `Bearer ${state.accessToken}`);
      withProjectHeader(headers, state.user?.activeProjectId ?? null);

      try {
        response = await fetch(path, {
          ...init,
          headers
        });
      } catch {
        throw new Error("Unable to reach the POS API. Make sure the POS API and the POS UI dev server are running.");
      }
    }

    const { json: payload, text } = await readResponsePayload(response);

    if (!response.ok) {
      throw new Error(describeFailure(payload, text, response, "POS request failed"));
    }

    return payload as T;
  }
};
