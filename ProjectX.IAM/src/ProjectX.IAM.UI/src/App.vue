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
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));
const canReadAudit = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess
  || currentUser.value?.hasAllPermissions
  || currentUser.value?.globalPermissions.includes("reports.read")
  || activeProjectPermissions.value.has("reports.read")
));
const navigationItems = computed(() => [
  {
    name: "home",
    label: "Dashboard",
    to: "/"
  },
  {
    name: "projects",
    label: "Projects",
    to: "/projects"
  },
  {
    name: "users",
    label: "Users",
    to: "/users"
  },
  {
    name: "roles",
    label: "Roles",
    to: "/roles"
  },
  {
    name: "audits",
    label: "Audit",
    to: "/audits"
  }
].filter(item => item.name !== "audits" || canReadAudit.value));

const currentRoleSummary = computed(() => currentUser.value?.roles.join(", ") || "No IAM roles");
const activeProjectSummary = computed(() => currentUser.value?.activeProjectName ?? "No PM project selected");

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
        <h1 class="brand-title">Identity & Access Management</h1>
        <p class="brand-copy">Global identity and access controls live here. Admin actions follow the active PM project.</p>
      </div>

      <label v-if="currentUser.projects.length > 0" class="field project-switcher">
        <span>Active PM project</span>
        <select :disabled="switchingProject" :value="currentUser.activeProjectId ?? ''" @change="changeProject">
          <option disabled value="">Select project</option>
          <option v-for="project in currentUser.projects" :key="project.id" :value="project.id">
            {{ project.name }}
          </option>
        </select>
      </label>

      <p v-if="projectError" class="form-error">{{ projectError }}</p>

      <nav class="side-nav-links" aria-label="Primary">
        <RouterLink
          v-for="item in navigationItems"
          :key="item.name"
          class="side-nav-link"
          :class="{ 'side-nav-link-active': activeRouteName === item.name }"
          :to="item.to"
        >
          {{ item.label }}
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
