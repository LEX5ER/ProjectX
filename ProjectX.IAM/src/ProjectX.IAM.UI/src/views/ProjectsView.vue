<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { auth } from "../auth/session";
import {
  listProjectsPage,
  listProjects,
  type ProjectRecord
} from "../admin/client";

const projects = ref<ProjectRecord[]>([]);
const currentPage = ref(1);
const pageSize = 8;
const totalCount = ref(0);
const totalPages = ref(0);
const loading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");

const currentUser = computed(() => auth.state.user);
const grantedPermissions = computed(() => new Set(currentUser.value?.permissions ?? []));
const canReadProjects = computed(() => Boolean(
  currentUser.value?.hasAllPermissions || grantedPermissions.value.has("projects.read")
));
const activeProjectId = computed(() => currentUser.value?.activeProjectId ?? "");
const activeProjectName = computed(() => currentUser.value?.activeProjectName ?? "IAM context");
const activeProject = computed(() =>
  projects.value.find(project => project.id === activeProjectId.value) ?? null);

function clearMessages(): void {
  errorMessage.value = "";
}

async function syncSessionProjects(): Promise<void> {
  const allProjects = await listProjects();

  auth.replaceProjects(allProjects.map(project => ({
    id: project.id,
    name: project.name
  })));
}

async function loadData(): Promise<void> {
  loading.value = true;
  clearMessages();

  try {
    if (!canReadProjects.value) {
      projects.value = [];
      totalCount.value = 0;
      totalPages.value = 0;
      return;
    }

    const result = await listProjectsPage(currentPage.value, pageSize);
    projects.value = result.items;
    totalCount.value = result.totalCount;
    totalPages.value = result.totalPages;
    await syncSessionProjects();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load projects from PM.";
  } finally {
    loading.value = false;
  }
}

async function goToPage(page: number): Promise<void> {
  if (page < 1 || (totalPages.value > 0 && page > totalPages.value) || page === currentPage.value) {
    return;
  }

  currentPage.value = page;
  await loadData();
}

async function selectProject(project: ProjectRecord): Promise<void> {
  clearMessages();
  busyAction.value = `select-${project.id}`;

  try {
    await auth.setActiveProject(project.id);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to switch project.";
  } finally {
    busyAction.value = "";
  }
}

onMounted(async () => {
  await loadData();
});
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Projects</p>
      <h1 class="hero-title">Use the PM catalog as the IAM project source</h1>
      <p class="hero-text">
        IAM no longer stores a separate internal project catalog. This page reads projects from ProjectX.PM
        and uses them directly for project roles, permissions, and memberships.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>

    <div v-if="!canReadProjects" class="empty-state">
      This session cannot view PM-backed projects.
    </div>

    <div v-else class="management-split">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Context</p>
            <h2>Current IAM project</h2>
          </div>
          <button class="ghost-button" :disabled="loading" type="button" @click="loadData">
            {{ loading ? "Refreshing..." : "Refresh from PM" }}
          </button>
        </div>

        <div class="record-list">
          <article class="record-card record-card-active">
            <div class="record-body">
              <strong>{{ activeProjectName }}</strong>
              <p>
                {{
                  activeProject
                    ? activeProject.description
                    : "Select a PM project below to narrow IAM roles and permissions to that project."
                }}
              </p>
              <small v-if="activeProject">{{ activeProject.id }}</small>
            </div>
          </article>
        </div>

        <p class="empty-state slim-empty-state">
          Create, rename, or archive projects in ProjectX.PM. IAM only consumes that catalog and isolates access per
          project.
        </p>
      </article>

      <article class="surface-card users-card">
        <div class="management-header">
          <div>
            <p class="card-label">PM Catalog</p>
            <h2>Accessible projects</h2>
          </div>
          <span class="stat-chip">{{ totalCount }} total</span>
        </div>

        <div v-if="loading" class="empty-state">Loading projects from PM...</div>
        <div v-else-if="projects.length === 0" class="empty-state">
          No PM-backed projects are visible for this session yet.
        </div>
        <div v-else class="record-list">
          <article
            v-for="project in projects"
            :key="project.id"
            class="record-card"
            :class="{ 'record-card-active': activeProjectId === project.id }"
          >
            <div class="record-body">
              <strong>{{ project.name }}</strong>
              <p>{{ project.description }}</p>
              <div class="chip-list">
                <span class="stat-chip">{{ project.memberCount }} members</span>
              </div>
              <small>{{ project.id }}</small>
            </div>
            <div class="record-actions">
              <button
                class="secondary-button"
                :disabled="busyAction === `select-${project.id}` || activeProjectId === project.id"
                type="button"
                @click="selectProject(project)"
              >
                {{
                  activeProjectId === project.id
                    ? "Selected"
                    : busyAction === `select-${project.id}`
                      ? "Switching..."
                      : "Select"
                }}
              </button>
            </div>
          </article>
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
    </div>
  </section>
</template>
