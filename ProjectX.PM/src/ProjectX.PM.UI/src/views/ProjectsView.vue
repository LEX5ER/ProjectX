<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { auth } from "../auth/session";
import {
  createProject,
  deleteProject,
  listProjectsPage,
  type ProjectRecord,
  type ProjectStatus,
  updateProject
} from "../projects/client";

const projects = ref<ProjectRecord[]>([]);
const currentPage = ref(1);
const pageSize = 10;
const totalCount = ref(0);
const totalPages = ref(0);
const loading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");
const successMessage = ref("");

const projectForm = reactive({
  id: "",
  code: "",
  name: "",
  description: "",
  ownerName: "",
  status: "Draft" as ProjectStatus,
  startDate: "",
  targetDate: ""
});

const editingProject = computed(() => Boolean(projectForm.id));
const currentUser = computed(() => auth.state.user);
const activeProject = computed(() => auth.getActiveProject());
const activeProjectSummary = computed(() => activeProject.value?.name ?? "the active project");
const activePmProjectId = computed(() => activeProject.value?.id ?? "");
const globalPermissions = computed(() => new Set(currentUser.value?.globalPermissions ?? []));
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));
const canCreateProjects = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess || globalPermissions.value.has("projects.write")
));
const canDeleteProjects = computed(() => canCreateProjects.value);
const canReadProjects = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess
  || globalPermissions.value.has("projects.read")
  || globalPermissions.value.has("projects.write")
  || currentUser.value?.hasAllPermissions
  || activeProjectPermissions.value.has("projects.read")
  || activeProjectPermissions.value.has("projects.write")
));
const statusOptions: ProjectStatus[] = ["Draft", "Active", "OnHold", "Completed", "Archived"];

const selectedProject = computed(() => projects.value.find(project => project.id === projectForm.id) ?? null);
const canSubmitProject = computed(() => editingProject.value
  ? Boolean(selectedProject.value && canEditProject(selectedProject.value))
  : canCreateProjects.value);
const canEditFormFields = computed(() => editingProject.value ? canSubmitProject.value : canCreateProjects.value);

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function clearForm(): void {
  projectForm.id = "";
  projectForm.code = "";
  projectForm.name = "";
  projectForm.description = "";
  projectForm.ownerName = "";
  projectForm.status = "Draft";
  projectForm.startDate = "";
  projectForm.targetDate = "";
}

function canEditProject(project: ProjectRecord): boolean {
  return canCreateProjects.value
    || (Boolean(currentUser.value?.hasAllPermissions || activeProjectPermissions.value.has("projects.write"))
      && activePmProjectId.value === project.id);
}

function editProject(project: ProjectRecord): void {
  clearMessages();

  if (!canEditProject(project)) {
    errorMessage.value = `This session can only edit the PM project linked to ${activeProjectSummary.value}.`;
    return;
  }

  projectForm.id = project.id;
  projectForm.code = project.code;
  projectForm.name = project.name;
  projectForm.description = project.description;
  projectForm.ownerName = project.ownerName;
  projectForm.status = project.status;
  projectForm.startDate = project.startDate ?? "";
  projectForm.targetDate = project.targetDate ?? "";
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
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load projects.";
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

async function submitProject(): Promise<void> {
  clearMessages();

  if (!canSubmitProject.value) {
    errorMessage.value = editingProject.value
      ? `This session can only edit the PM project linked to ${activeProjectSummary.value}.`
      : "Global ProjectX.PM admin access is required to create projects.";
    return;
  }

  busyAction.value = "project";

  try {
    const payload = {
      code: projectForm.code,
      name: projectForm.name,
      description: projectForm.description,
      ownerName: projectForm.ownerName,
      status: projectForm.status,
      startDate: projectForm.startDate || null,
      targetDate: projectForm.targetDate || null
    };

    if (editingProject.value) {
      await updateProject(projectForm.id, payload);
      successMessage.value = "Project updated.";
    } else {
      await createProject(payload);
      successMessage.value = "Project created.";
    }

    clearForm();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to save project.";
  } finally {
    busyAction.value = "";
  }
}

async function removeProject(project: ProjectRecord): Promise<void> {
  clearMessages();

  if (!canDeleteProjects.value) {
    errorMessage.value = "Global ProjectX.PM admin access is required to delete projects.";
    return;
  }

  if (!window.confirm(`Delete project "${project.name}"?`)) {
    return;
  }

  busyAction.value = `delete-${project.id}`;

  try {
    await deleteProject(project.id);
    successMessage.value = "Project deleted.";

    if (projectForm.id === project.id) {
      clearForm();
    }

    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to delete project.";
  } finally {
    busyAction.value = "";
  }
}

function formatDate(value: string | null): string {
  return value || "Not scheduled";
}

function formatTimestamp(value: string): string {
  return new Date(value).toLocaleString();
}

onMounted(async () => {
  await loadData();
});
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Projects</p>
      <h1 class="hero-title">Manage the project portfolio</h1>
      <p class="hero-text">
        Every project created here is a real PM record. The generated project ID is the clean external reference to use
        when IAM needs a matching project.
      </p>
      <p class="hero-text">
        Project admins can only edit the PM project linked to the active IAM project. Deletion remains
        global-only.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div v-if="!canReadProjects" class="empty-state">
      Select an IAM project with `projects.read` access before managing ProjectX.PM records.
    </div>

    <div v-else class="management-split">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Project Form</p>
            <h2>{{ editingProject ? "Edit project" : "Create project" }}</h2>
          </div>
          <button class="ghost-button" type="button" @click="clearForm">Reset</button>
        </div>

        <form class="stack-form" @submit.prevent="submitProject">
          <label class="field">
            <span>Code</span>
            <input
              v-model="projectForm.code"
              :disabled="!canEditFormFields"
              maxlength="50"
              placeholder="PM-ALPHA"
              required
              type="text"
            />
          </label>

          <label class="field">
            <span>Name</span>
            <input
              v-model="projectForm.name"
              :disabled="!canEditFormFields"
              maxlength="150"
              placeholder="Alpha Revamp"
              required
              type="text"
            />
          </label>

          <label class="field">
            <span>Description</span>
            <textarea
              v-model="projectForm.description"
              :disabled="!canEditFormFields"
              maxlength="500"
              placeholder="Modernize the operating platform."
              required
              rows="4"
            />
          </label>

          <label class="field">
            <span>Owner</span>
            <input
              v-model="projectForm.ownerName"
              :disabled="!canEditFormFields"
              maxlength="100"
              placeholder="Jane Doe"
              required
              type="text"
            />
          </label>

          <label class="field">
            <span>Status</span>
            <select v-model="projectForm.status" :disabled="!canEditFormFields">
              <option v-for="status in statusOptions" :key="status" :value="status">
                {{ status }}
              </option>
            </select>
          </label>

          <div class="field-grid">
            <label class="field">
              <span>Start date</span>
              <input v-model="projectForm.startDate" :disabled="!canEditFormFields" type="date" />
            </label>

            <label class="field">
              <span>Target date</span>
              <input v-model="projectForm.targetDate" :disabled="!canEditFormFields" type="date" />
            </label>
          </div>

          <button class="primary-button" :disabled="busyAction === 'project' || !canSubmitProject" type="submit">
            {{ busyAction === "project" ? "Saving..." : editingProject ? "Update project" : "Create project" }}
          </button>
        </form>

        <p v-if="!editingProject && !canCreateProjects" class="empty-state slim-empty-state">
          Global ProjectX.PM admin access is required to create new projects. Project admins can only edit the PM
          project linked to {{ activeProjectSummary }}. Delete remains global-only.
        </p>
      </article>

      <article class="surface-card users-card">
        <div class="management-header">
          <div>
            <p class="card-label">Portfolio</p>
            <h2>Accessible projects</h2>
          </div>
          <span class="stat-chip">{{ totalCount }} total</span>
        </div>

        <div v-if="loading" class="empty-state">Loading projects...</div>
        <div v-else-if="projects.length === 0" class="empty-state">No projects are visible for this session yet.</div>
        <div v-else class="record-list">
          <article v-for="project in projects" :key="project.id" class="record-card">
            <div class="record-body">
              <strong>{{ project.name }}</strong>
              <p>{{ project.description }}</p>
              <div class="chip-list">
                <span class="stat-chip">{{ project.code }}</span>
                <span class="stat-chip">{{ project.status }}</span>
                <span class="stat-chip">{{ project.ownerName }}</span>
              </div>
              <small>Schedule: {{ formatDate(project.startDate) }} to {{ formatDate(project.targetDate) }}</small>
              <small>Updated: {{ formatTimestamp(project.updatedAtUtc) }}</small>
              <code>{{ project.id }}</code>
            </div>
            <div class="record-actions">
              <button class="secondary-button" :disabled="!canEditProject(project)" type="button" @click="editProject(project)">
                {{ canEditProject(project) ? "Edit" : "Read only" }}
              </button>
              <button
                class="ghost-button"
                :disabled="busyAction === `delete-${project.id}` || !canDeleteProjects"
                type="button"
                @click="removeProject(project)"
              >
                {{
                  busyAction === `delete-${project.id}`
                    ? "Deleting..."
                    : canDeleteProjects
                      ? "Delete"
                      : "Global only"
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
