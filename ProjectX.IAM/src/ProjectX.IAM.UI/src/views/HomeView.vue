<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { auth, type AuthUser } from "../auth/session";

const serverUser = ref<AuthUser | null>(null);
const serverError = ref("");
const loadingProfile = ref(false);

const currentUser = computed(() => auth.state.user);
const activeProject = computed(() =>
  currentUser.value?.projects.find(project => project.id === currentUser.value?.activeProjectId) ?? null);
const projectCount = computed(() => currentUser.value?.projects.length ?? 0);
const roleCount = computed(() => currentUser.value?.roles.length ?? 0);
const permissionCount = computed(() => currentUser.value?.hasAllPermissions ? "All" : String(currentUser.value?.permissions.length ?? 0));
const recentProjects = computed(() => currentUser.value?.projects.slice(0, 4) ?? []);
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));
const canReadAudit = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess
  || currentUser.value?.hasAllPermissions
  || currentUser.value?.globalPermissions.includes("reports.read")
  || activeProjectPermissions.value.has("reports.read")
));
const contextCards = computed(() => [
  {
    id: "project",
    title: activeProject.value?.name ?? "IAM context",
    description: activeProject.value
      ? "Project admin work is currently narrowed to this PM project."
      : "Global IAM context is active. Select a PM project from the left navigation to isolate access.",
    meta: activeProject.value
      ? "Project context active"
      : "No PM project selected"
  },
  {
    id: "access",
    title: currentUser.value?.hasGlobalFullAccess
      ? "Protected global full access"
      : currentUser.value?.globalRoles.join(", ") || currentUser.value?.roles.join(", ") || "Scoped IAM access",
    description: currentUser.value?.hasAllPermissions
      ? "This session resolves to full access in the current context."
      : `${currentUser.value?.permissions.length ?? 0} effective permissions are active for this session.`,
    meta: `${currentUser.value?.globalPermissions.length ?? 0} global permissions | ${currentUser.value?.activeProjectPermissions.length ?? 0} project permissions`
  },
  {
    id: "session",
    title: serverUser.value ? "Server profile confirmed" : "Session lifecycle",
    description: serverUser.value
      ? `Live profile loaded for ${serverUser.value.userName}. Refresh token expires ${formatDate(auth.state.refreshTokenExpiresAtUtc)}.`
      : `Refresh token expires ${formatDate(auth.state.refreshTokenExpiresAtUtc)}. Use the actions above to rotate or re-verify the session.`,
    meta: loadingProfile.value
      ? "Checking /api/auth/me..."
      : serverUser.value
        ? `Active project: ${serverUser.value.activeProjectName ?? "IAM context"}`
        : "Profile not refreshed yet"
  }
]);

function formatDate(value: string): string {
  if (!value) {
    return "Not issued";
  }

  return new Date(value).toLocaleString();
}

async function loadProfile(): Promise<void> {
  loadingProfile.value = true;
  serverError.value = "";

  try {
    serverUser.value = await auth.authorizedJson<AuthUser>("/api/auth/me");
  } catch (error) {
    serverError.value = error instanceof Error ? error.message : "Unable to load the current profile.";
  } finally {
    loadingProfile.value = false;
  }
}

async function rotateSession(): Promise<void> {
  serverError.value = "";

  const refreshed = await auth.refresh();

  if (!refreshed) {
    serverError.value = "Your session could not be refreshed.";
    return;
  }

  await loadProfile();
}

onMounted(async () => {
  await loadProfile();
});
</script>

<template>
  <section class="dashboard-page">
    <div class="hero-panel">
      <p class="eyebrow">ProjectX.IAM</p>
      <h1 class="hero-title">Operate identity once, then narrow it per project.</h1>
      <p class="hero-text">
        Global roles and permissions live here. The active PM project controls which isolated roles,
        permissions, and memberships you are editing.
      </p>
      <p class="hero-text">
        Signed in as {{ currentUser?.userName }} through IAM.
      </p>

      <div class="hero-actions">
        <button class="secondary-button" :disabled="loadingProfile" @click="loadProfile">
          {{ loadingProfile ? "Loading..." : "Reload profile" }}
        </button>
        <button class="primary-button" @click="rotateSession">
          Rotate refresh token
        </button>
        <RouterLink class="ghost-button" to="/projects">
          Open projects
        </RouterLink>
        <RouterLink class="ghost-button" to="/users">
          Open users
        </RouterLink>
        <RouterLink class="ghost-button" to="/roles">
          Open roles
        </RouterLink>
        <RouterLink v-if="canReadAudit" class="ghost-button" to="/audits">
          Open audit
        </RouterLink>
      </div>
    </div>

    <p v-if="serverError" class="form-error management-banner">{{ serverError }}</p>

    <div class="dashboard-grid">
      <article class="surface-card metric-card">
        <p class="card-label">Project Catalog</p>
        <strong>{{ projectCount }}</strong>
        <span>Accessible PM projects</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Assignments</p>
        <strong>{{ roleCount }}</strong>
        <span>Effective roles in the current session</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Effective Access</p>
        <strong>{{ permissionCount }}</strong>
        <span>{{ currentUser?.hasAllPermissions ? "Full access in current context" : "Explicit permissions" }}</span>
      </article>

      <article class="surface-card dashboard-span">
        <div class="management-header">
          <div>
            <p class="card-label">Current Context</p>
            <h2>Live IAM session status</h2>
          </div>
        </div>

        <div class="record-list">
          <article
            v-for="card in contextCards"
            :key="card.id"
            class="record-card"
            :class="{ 'record-card-active': card.id === 'project' }"
          >
            <div class="record-body">
              <strong>{{ card.title }}</strong>
              <p>{{ card.description }}</p>
              <small>{{ card.meta }}</small>
            </div>
          </article>
        </div>
      </article>

      <article class="surface-card dashboard-span">
        <div class="management-header">
          <div>
            <p class="card-label">Accessible Projects</p>
            <h2>Latest PM-backed projects</h2>
          </div>
          <span class="stat-chip">{{ projectCount }} total</span>
        </div>

        <div v-if="recentProjects.length === 0" class="empty-state">
          No projects are assigned to this session yet.
        </div>
        <div v-else class="record-list">
          <article
            v-for="project in recentProjects"
            :key="project.id"
            class="record-card"
            :class="{ 'record-card-active': activeProject?.id === project.id }"
          >
            <div class="record-body">
              <strong>{{ project.name }}</strong>
              <p>
                {{
                  activeProject?.id === project.id
                    ? "This is the current project context for role, permission, and membership changes."
                    : "Select this project from the left navigation to narrow IAM management to that PM project."
                }}
              </p>
              <div class="chip-list">
                <span class="stat-chip">{{ project.id }}</span>
                <span v-if="activeProject?.id === project.id" class="stat-chip">Active</span>
              </div>
            </div>
          </article>
        </div>
      </article>
    </div>
  </section>
</template>
