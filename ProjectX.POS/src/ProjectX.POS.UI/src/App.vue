<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { auth } from "./auth/session";

const router = useRouter();
const route = useRoute();
const currentUser = computed(() => auth.state.user);
const activeRouteName = computed(() => route.name);
const switchingProject = ref(false);
const projectError = ref("");
const projects = computed(() => auth.getProjects(currentUser.value));
const currentRoleSummary = computed(() => auth.state.user?.roles.join(", ") || "No IAM roles");
const activeProjectSummary = computed(() => auth.getActiveProject()?.name ?? "No POS IAM project selected");

async function changeProject(event: Event): Promise<void> {
  const selectedValue = (event.target as HTMLSelectElement).value || null;
  switchingProject.value = true;
  projectError.value = "";

  try {
    await auth.setActiveProject(selectedValue);
  } catch (error) {
    projectError.value = error instanceof Error ? error.message : "Unable to switch project.";
  } finally {
    switchingProject.value = false;
  }
}

async function logout(): Promise<void> {
  await auth.logout();
  await router.replace({ name: "login" });
}
</script>

<template>
  <div class="app-shell" :class="{ 'app-shell-auth': !currentUser }">
    <aside v-if="currentUser" class="side-nav">
      <div class="side-nav-brand">
        <p class="brand-kicker">ProjectX</p>
        <h1 class="brand-title">Point of Sale</h1>
        <p class="brand-copy">Catalog, pricing, and stock posture live here. Actions follow the active IAM project.</p>
      </div>

      <label v-if="projects.length > 0" class="field project-switcher">
        <span>Active IAM project</span>
        <select :disabled="switchingProject" :value="currentUser.activeProjectId ?? ''" @change="changeProject">
          <option disabled value="">Select project</option>
          <option v-for="project in projects" :key="project.id" :value="project.id">
            {{ project.name }}
          </option>
        </select>
      </label>

      <p v-if="projectError" class="form-error">{{ projectError }}</p>

      <nav class="side-nav-links" aria-label="Primary">
        <RouterLink class="side-nav-link" :class="{ 'side-nav-link-active': activeRouteName === 'home' }" to="/">
          Dashboard
        </RouterLink>
        <RouterLink class="side-nav-link" :class="{ 'side-nav-link-active': activeRouteName === 'products' }" to="/products">
          Products
        </RouterLink>
      </nav>

      <div class="side-nav-footer">
        <div class="identity-card">
          <span>{{ currentUser.userName }}</span>
          <small>{{ currentRoleSummary }}</small>
          <p>{{ activeProjectSummary }}</p>
          <p>{{ currentUser.email }}</p>
        </div>
        <button class="ghost-button" type="button" @click="logout">Logout</button>
      </div>
    </aside>

    <main class="page-frame">
      <RouterView />
    </main>
  </div>
</template>
