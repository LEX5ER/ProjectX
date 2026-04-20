<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { listAuthenticationAuditsPage, type AuthenticationAuditAction, type AuthenticationAuditOutcome, type AuthenticationAuditRecord } from "../admin/client";
import { auth } from "../auth/session";

const audits = ref<AuthenticationAuditRecord[]>([]);
const currentPage = ref(1);
const pageSize = 20;
const totalCount = ref(0);
const totalPages = ref(0);
const loading = ref(false);
const errorMessage = ref("");

const filters = reactive({
  search: "",
  action: "All" as AuthenticationAuditAction | "All",
  outcome: "All" as AuthenticationAuditOutcome | "All",
  project: "All" as string
});

const currentUser = computed(() => auth.state.user);
const globalPermissions = computed(() => new Set(currentUser.value?.globalPermissions ?? []));
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));
const canReadAudit = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess
  || currentUser.value?.hasAllPermissions
  || globalPermissions.value.has("reports.read")
  || activeProjectPermissions.value.has("reports.read")
));
const projectOptions = computed(() => {
  const options = audits.value
    .map(audit => ({
      value: audit.projectId ?? "__none__",
      label: formatProjectLabel(audit)
    }))
    .filter((option, index, items) => items.findIndex(current => current.value === option.value) === index)
    .sort((left, right) => left.label.localeCompare(right.label));

  return [
    {
      value: "All",
      label: "All projects"
    },
    {
      value: "__none__",
      label: "Global / no project context"
    },
    ...options.filter(option => option.value !== "__none__")
  ];
});

const filteredAudits = computed(() => {
  const normalizedSearch = filters.search.trim().toLowerCase();

  return audits.value.filter(audit => {
    if (filters.action !== "All" && audit.action !== filters.action) {
      return false;
    }

    if (filters.outcome !== "All" && audit.outcome !== filters.outcome) {
      return false;
    }

    if (filters.project !== "All") {
      const auditProjectValue = audit.projectId ?? "__none__";

      if (auditProjectValue !== filters.project) {
        return false;
      }
    }

    if (!normalizedSearch) {
      return true;
    }

    return [
      audit.userNameOrEmail,
      audit.clientApplication,
      audit.projectName,
      audit.projectId,
      audit.failureReason,
      audit.ipAddress,
      audit.userAgent
    ]
      .filter((value): value is string => Boolean(value))
      .some(value => value.toLowerCase().includes(normalizedSearch));
  });
});

const failedCount = computed(() => audits.value.filter(audit => audit.outcome === "Failed").length);
const loginCount = computed(() => audits.value.filter(audit => audit.action === "Login").length);
const logoutCount = computed(() => audits.value.filter(audit => audit.action === "Logout").length);

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

function formatProjectLabel(audit: AuthenticationAuditRecord): string {
  if (audit.projectName) {
    return audit.projectName;
  }

  if (audit.projectId) {
    return audit.projectId;
  }

  return "Global / no project context";
}

function normalizeClientApplication(value: string | null | undefined): string | null {
  const clientApplication = value?.trim();

  if (!clientApplication) {
    return null;
  }

  return clientApplication;
}

function formatApplicationLabel(audit: AuthenticationAuditRecord): string {
  const clientApplication = normalizeClientApplication(audit.clientApplication);

  if (clientApplication) {
    return clientApplication;
  }

  if (audit.projectName) {
    return audit.projectName;
  }

  return audit.projectId ? audit.projectId : "No client application";
}

async function loadAudits(): Promise<void> {
  if (!canReadAudit.value) {
    errorMessage.value = "Current-context reports.read or full access is required to view authentication audit activity.";
    audits.value = [];
    return;
  }

  loading.value = true;
  errorMessage.value = "";

  try {
    const result = await listAuthenticationAuditsPage(currentPage.value, pageSize);
    audits.value = result.items;
    totalCount.value = result.totalCount;
    totalPages.value = result.totalPages;
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load authentication audit activity.";
  } finally {
    loading.value = false;
  }
}

async function goToPage(page: number): Promise<void> {
  if (page < 1 || (totalPages.value > 0 && page > totalPages.value) || page === currentPage.value) {
    return;
  }

  currentPage.value = page;
  await loadAudits();
}

onMounted(async () => {
  await loadAudits();
});
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Authentication Audit</p>
      <h1 class="hero-title">Inspect login and logout activity recorded by IAM.</h1>
      <p class="hero-text">
        This view is backed by the persisted authentication audit trail. It records successful and failed logins,
        logout attempts, request context, and any captured failure reason.
      </p>
      <p class="hero-text">
        Current-context <code>reports.read</code> or full access is required to review the audit.
      </p>

      <div class="hero-actions">
        <button class="secondary-button" :disabled="loading || !canReadAudit" @click="loadAudits">
          {{ loading ? "Loading..." : "Reload audit" }}
        </button>
      </div>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>

    <div v-if="!canReadAudit" class="empty-state">
      This session does not include the required audit visibility in the active context.
    </div>

    <template v-else>
      <div class="dashboard-grid">
        <article class="surface-card metric-card">
          <p class="card-label">Entries</p>
          <strong>{{ totalCount }}</strong>
          <span>Captured authentication events</span>
        </article>

        <article class="surface-card metric-card">
          <p class="card-label">Failed</p>
          <strong>{{ failedCount }}</strong>
          <span>Failed login or logout attempts</span>
        </article>

        <article class="surface-card metric-card">
          <p class="card-label">Mix</p>
          <strong>{{ loginCount }}/{{ logoutCount }}</strong>
          <span>Logins vs. logouts</span>
        </article>
      </div>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Filters</p>
            <h2>Refine the audit view</h2>
          </div>
        </div>

        <div class="field-grid">
          <label class="field">
            <span>Search</span>
            <input
              v-model="filters.search"
              placeholder="User, application, project, IP, user agent, or failure reason"
              type="text"
            />
          </label>

          <label class="field">
            <span>Action</span>
            <select v-model="filters.action">
              <option value="All">All actions</option>
              <option value="Login">Login</option>
              <option value="Logout">Logout</option>
            </select>
          </label>

          <label class="field">
            <span>Outcome</span>
            <select v-model="filters.outcome">
              <option value="All">All outcomes</option>
              <option value="Succeeded">Succeeded</option>
              <option value="Failed">Failed</option>
            </select>
          </label>

          <label class="field">
            <span>Project</span>
            <select v-model="filters.project">
              <option v-for="option in projectOptions" :key="option.value" :value="option.value">
                {{ option.label }}
              </option>
            </select>
          </label>
        </div>
      </article>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Activity</p>
            <h2>Authentication events</h2>
          </div>
          <span class="stat-chip">{{ filteredAudits.length }} shown on page · {{ totalCount }} total</span>
        </div>

        <div v-if="filteredAudits.length === 0" class="empty-state">
          No authentication audit entries match the current filters.
        </div>

        <div v-else class="table-shell">
          <table class="management-table">
            <thead>
              <tr>
                <th>Time</th>
                <th>Action</th>
                <th>User</th>
                <th>Application</th>
                <th>Detail</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="audit in filteredAudits" :key="audit.id">
                <td>
                  <div class="record-body">
                    <strong>{{ formatDate(audit.occurredAtUtc) }}</strong>
                    <small>{{ audit.id }}</small>
                  </div>
                </td>
                <td>
                  <div class="chip-list">
                    <span class="stat-chip">{{ audit.action }}</span>
                    <span class="permission-chip">{{ audit.outcome }}</span>
                  </div>
                </td>
                <td>
                  <div class="record-body">
                    <strong>{{ audit.userNameOrEmail ?? "Unknown identity" }}</strong>
                    <small>{{ audit.userId ?? "No linked user id" }}</small>
                  </div>
                </td>
                <td>
                  <div class="record-body">
                    <strong>{{ formatApplicationLabel(audit) }}</strong>
                    <small>{{ audit.ipAddress ?? "No IP captured" }}</small>
                  </div>
                </td>
                <td>
                  <div class="record-body">
                    <p>{{ audit.failureReason ?? "No failure reason captured." }}</p>
                    <small>{{ audit.userAgent ?? "No user agent captured." }}</small>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="totalPages > 1" class="hero-actions">
          <button class="ghost-button" :disabled="loading || currentPage <= 1" type="button" @click="goToPage(currentPage - 1)">
            Previous
          </button>
          <span class="stat-chip">Page {{ currentPage }} of {{ totalPages }}</span>
          <button class="ghost-button" :disabled="loading || currentPage >= totalPages" type="button" @click="goToPage(currentPage + 1)">
            Next
          </button>
        </div>
      </article>
    </template>
  </section>
</template>
