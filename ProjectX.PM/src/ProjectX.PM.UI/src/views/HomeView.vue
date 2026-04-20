<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { auth } from "../auth/session";
import { listProjects, type ProjectRecord } from "../projects/client";

const projects = ref<ProjectRecord[]>([]);
const loading = ref(false);
const errorMessage = ref("");

const currentUser = computed(() => auth.state.user);
const projectCount = computed(() => projects.value.length);
const activeCount = computed(() => projects.value.filter(project => project.status === "Active").length);
const deliveryCount = computed(() => projects.value.filter(project => project.status === "Completed").length);
const recentProjects = computed(() => projects.value.slice(0, 4));

async function loadData(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";

  try {
    projects.value = await listProjects();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load projects.";
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await loadData();
});
</script>

<template>
  <section class="dashboard-page">
    <div class="hero-panel">
      <p class="eyebrow">ProjectX.PM</p>
      <h1 class="hero-title">Own the project record here, not in IAM.</h1>
      <p class="hero-text">
        This service is the project-management source of truth. Use each project ID as the external reference when you
        register a matching project in IAM.
      </p>
      <p class="hero-text">
        Signed in as {{ currentUser?.userName }} through IAM.
      </p>
      <RouterLink class="primary-button hero-action-link" to="/projects">
        Open project workspace
      </RouterLink>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>

    <div class="dashboard-grid">
      <article class="surface-card metric-card">
        <p class="card-label">Portfolio</p>
        <strong>{{ loading ? "..." : projectCount }}</strong>
        <span>Total projects</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Execution</p>
        <strong>{{ loading ? "..." : activeCount }}</strong>
        <span>Active projects</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Delivered</p>
        <strong>{{ loading ? "..." : deliveryCount }}</strong>
        <span>Completed projects</span>
      </article>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Recent updates</p>
            <h2>Latest project records</h2>
          </div>
        </div>

        <div v-if="loading" class="empty-state">Loading project activity...</div>
        <div v-else-if="recentProjects.length === 0" class="empty-state">No projects exist yet.</div>
        <div v-else class="record-list">
          <article v-for="project in recentProjects" :key="project.id" class="record-card">
            <div class="record-body">
              <strong>{{ project.name }}</strong>
              <p>{{ project.description }}</p>
              <small>{{ project.code }} | {{ project.status }} | {{ project.ownerName }}</small>
              <code>{{ project.id }}</code>
            </div>
          </article>
        </div>
      </article>
    </div>
  </section>
</template>
