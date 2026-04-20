<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import { auth } from "../auth/session";
import {
  createUser,
  deleteUser,
  listRoles,
  listUsersPage,
  type RoleRecord,
  type UserAssignmentRecord,
  type UserRecord,
  updateUser
} from "../admin/client";

const roles = ref<RoleRecord[]>([]);
const users = ref<UserRecord[]>([]);
const currentPage = ref(1);
const pageSize = 12;
const totalCount = ref(0);
const totalPages = ref(0);

const loading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");
const successMessage = ref("");

const userForm = reactive({
  id: "",
  userName: "",
  email: "",
  password: "",
  globalRoleIds: [] as string[],
  projectRoleIds: [] as string[]
});

const editingUser = computed(() => Boolean(userForm.id));
const currentUser = computed(() => auth.state.user);
const currentUserId = computed(() => currentUser.value?.id ?? "");
const activeProjectId = computed(() => currentUser.value?.activeProjectId ?? "");
const hasProjectContext = computed(() => Boolean(activeProjectId.value));
const grantedPermissions = computed(() => new Set(currentUser.value?.permissions ?? []));
const globalPermissions = computed(() => new Set(currentUser.value?.globalPermissions ?? []));
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));

const hasGlobalUsersRead = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess || globalPermissions.value.has("users.read")
));
const hasGlobalUsersWrite = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess || globalPermissions.value.has("users.write")
));
const hasProjectUsersWrite = computed(() => Boolean(
  hasProjectContext.value && (currentUser.value?.hasAllPermissions || activeProjectPermissions.value.has("users.write"))
));
const canWriteUsers = computed(() => hasGlobalUsersWrite.value || hasProjectUsersWrite.value);
const canReadRoles = computed(() => Boolean(currentUser.value?.hasAllPermissions || grantedPermissions.value.has("roles.read")));
const canManageGlobalAssignments = computed(() => hasGlobalUsersWrite.value);
const canManageProjectAssignments = computed(() => hasProjectUsersWrite.value);
const globalRoles = computed(() => roles.value.filter(role => role.scope === "Global"));
const projectRoles = computed(() => roles.value.filter(role => role.scope === "Project"));
const activeProjectName = computed(() => currentUser.value?.activeProjectName?.trim() ?? "");
const activeProjectSummary = computed(() => summarizeLabel(activeProjectName.value, 40));

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function summarizeLabel(value: string, maxLength: number): string {
  if (!value) {
    return "selected project";
  }

  return value.length > maxLength
    ? `${value.slice(0, maxLength - 3)}...`
    : value;
}

function clearUserForm(): void {
  userForm.id = "";
  userForm.userName = "";
  userForm.email = "";
  userForm.password = "";
  userForm.globalRoleIds = [];
  userForm.projectRoleIds = [];
}

function canEditUserRecord(user: UserRecord): boolean {
  if (!canWriteUsers.value) {
    return false;
  }

  if (user.isProtected) {
    return false;
  }

  if (!hasProjectContext.value && !hasGlobalUsersWrite.value) {
    return false;
  }

  return true;
}

function isCurrentUserRecord(user: UserRecord): boolean {
  return Boolean(currentUserId.value) && user.id === currentUserId.value;
}

function canDeleteUserRecord(user: UserRecord): boolean {
  if (!canWriteUsers.value) {
    return false;
  }

  if (user.isProtected) {
    return false;
  }

  if (isCurrentUserRecord(user)) {
    return false;
  }

  if (!hasProjectContext.value && !hasGlobalUsersWrite.value) {
    return false;
  }

  return true;
}

function assignmentLabel(assignment: UserAssignmentRecord): string {
  return assignment.projectName ? `${assignment.roleName} @ ${assignment.projectName}` : assignment.roleName;
}

async function loadData(): Promise<void> {
  loading.value = true;
  clearMessages();

  try {
    roles.value = canReadRoles.value ? await listRoles() : [];

    if (hasProjectContext.value || hasGlobalUsersRead.value) {
      const result = await listUsersPage(currentPage.value, pageSize);
      users.value = result.items;
      totalCount.value = result.totalCount;
      totalPages.value = result.totalPages;
    } else {
      users.value = [];
      totalCount.value = 0;
      totalPages.value = 0;
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load users.";
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

function editUser(user: UserRecord): void {
  clearMessages();

  if (!canEditUserRecord(user)) {
    errorMessage.value = user.isProtected
      ? "Protected accounts cannot be modified."
      : "This session cannot modify the selected user.";
    return;
  }

  userForm.id = user.id;
  userForm.userName = user.userName;
  userForm.email = user.email;
  userForm.password = "";
  userForm.globalRoleIds = canManageGlobalAssignments.value
    ? user.assignments
        .filter(assignment => assignment.scope === "Global")
        .map(assignment => assignment.roleId)
    : [];
  userForm.projectRoleIds = user.assignments
    .filter(assignment => assignment.scope === "Project" && assignment.projectId === activeProjectId.value)
    .map(assignment => assignment.roleId);
}

function toggleRole(roleId: string, scope: "Global" | "Project", checked: boolean): void {
  const target = scope === "Global" ? userForm.globalRoleIds : userForm.projectRoleIds;

  if (checked) {
    if (!target.includes(roleId)) {
      if (scope === "Global") {
        userForm.globalRoleIds = [...target, roleId];
      } else {
        userForm.projectRoleIds = [...target, roleId];
      }
    }

    return;
  }

  if (scope === "Global") {
    userForm.globalRoleIds = target.filter(currentRoleId => currentRoleId !== roleId);
  } else {
    userForm.projectRoleIds = target.filter(currentRoleId => currentRoleId !== roleId);
  }
}

function isRoleSelected(roleId: string, scope: "Global" | "Project"): boolean {
  return scope === "Global"
    ? userForm.globalRoleIds.includes(roleId)
    : userForm.projectRoleIds.includes(roleId);
}

async function submitUser(): Promise<void> {
  clearMessages();

  if (!canWriteUsers.value) {
    errorMessage.value = "You do not have permission to modify users.";
    return;
  }

  if (!canReadRoles.value) {
    errorMessage.value = "Role visibility is required to assign user access.";
    return;
  }

  if (!hasProjectContext.value && !hasGlobalUsersWrite.value) {
    errorMessage.value = "Select a project before managing project memberships.";
    return;
  }

  if (!editingUser.value && userForm.globalRoleIds.length === 0 && userForm.projectRoleIds.length === 0) {
    errorMessage.value = "Assign at least one IAM or project role.";
    return;
  }

  if (!hasProjectContext.value && userForm.projectRoleIds.length > 0) {
    errorMessage.value = "Select a project before assigning project roles.";
    return;
  }

  if (!canManageGlobalAssignments.value && userForm.globalRoleIds.length > 0) {
    errorMessage.value = "Global users.write is required to assign IAM roles.";
    return;
  }

  busyAction.value = "user";

  try {
    if (editingUser.value) {
      const payload: {
        userName: string;
        email: string;
        password?: string;
        globalRoleIds: string[];
        projectRoleIds: string[];
      } = {
        userName: userForm.userName,
        email: userForm.email,
        globalRoleIds: userForm.globalRoleIds,
        projectRoleIds: userForm.projectRoleIds
      };

      if (userForm.password.trim()) {
        payload.password = userForm.password;
      }

      await updateUser(userForm.id, payload);
      successMessage.value = "User updated.";
    } else {
      await createUser({
        userName: userForm.userName,
        email: userForm.email,
        password: userForm.password,
        globalRoleIds: userForm.globalRoleIds,
        projectRoleIds: userForm.projectRoleIds
      });

      successMessage.value = "User created.";
    }

    clearUserForm();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to save user.";
  } finally {
    busyAction.value = "";
  }
}

async function removeUser(user: UserRecord): Promise<void> {
  clearMessages();

  if (user.isProtected) {
    errorMessage.value = "Protected accounts cannot be deleted.";
    return;
  }

  if (isCurrentUserRecord(user)) {
    errorMessage.value = "You cannot delete your own account.";
    return;
  }

  if (!window.confirm(hasProjectContext.value
    ? `Remove user "${user.userName}" from the active project?`
    : `Delete user "${user.userName}"?`)) {
    return;
  }

  busyAction.value = `delete-user-${user.id}`;

  try {
    await deleteUser(user.id);
    successMessage.value = hasProjectContext.value
      ? "User access removed from the active project."
      : "User deleted.";

    if (userForm.id === user.id) {
      clearUserForm();
    }

    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to delete user.";
  } finally {
    busyAction.value = "";
  }
}

onMounted(async () => {
  await loadData();
});

watch(
  () => `${currentUser.value?.activeProjectId ?? ""}|${currentUser.value?.permissions.join(",") ?? ""}|${currentUser.value?.globalPermissions.join(",") ?? ""}|${currentUser.value?.activeProjectPermissions.join(",") ?? ""}`,
  async () => {
    clearUserForm();
    currentPage.value = 1;
    await loadData();
  }
);
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Users</p>
      <h1 class="hero-title">Manage identities and project memberships</h1>
      <p class="hero-text">
        Users are global identities. IAM roles apply across the platform, while project roles apply only
        inside the selected project. Protected accounts remain visible but cannot be changed.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div v-if="!hasProjectContext && !hasGlobalUsersRead" class="empty-state">
      Select a project before managing project users, or use an IAM session with `users.read` to inspect
      global identities.
    </div>

    <div v-else class="management-split">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">User Form</p>
            <h2>{{ editingUser ? "Edit user" : "Create user" }}</h2>
          </div>
          <button class="ghost-button" :disabled="!canWriteUsers" type="button" @click="clearUserForm">Reset</button>
        </div>

        <form class="stack-form" @submit.prevent="submitUser">
          <label class="field">
            <span>Username</span>
            <input v-model="userForm.userName" :disabled="!canWriteUsers" required type="text" />
          </label>

          <label class="field">
            <span>Email</span>
            <input v-model="userForm.email" :disabled="!canWriteUsers" required type="email" />
          </label>

          <label class="field">
            <span>Password {{ editingUser ? "(leave blank to keep current)" : "" }}</span>
            <input v-model="userForm.password" :disabled="!canWriteUsers" :required="!editingUser" type="password" />
          </label>

          <div v-if="globalRoles.length > 0" class="permission-selector">
            <p class="card-label">IAM roles</p>
            <label
              v-for="role in globalRoles"
              :key="role.id"
              class="permission-option"
              :class="{ 'permission-option-active': isRoleSelected(role.id, 'Global') }"
            >
              <input
                :checked="isRoleSelected(role.id, 'Global')"
                :disabled="!canManageGlobalAssignments || role.isProtected"
                type="checkbox"
                @change="toggleRole(role.id, 'Global', ($event.target as HTMLInputElement).checked)"
              />
              <div class="permission-option-meta">
                <div class="permission-option-header">
                  <strong :title="role.name">{{ role.name }}</strong>
                  <span class="stat-chip">
                    {{ role.isProtected ? "Protected" : role.hasAllPermissions ? "Full access" : "IAM" }}
                  </span>
                </div>
                <p>{{ role.description }}</p>
              </div>
            </label>
          </div>

          <div v-if="hasProjectContext" class="permission-selector">
            <p class="card-label" :title="activeProjectName">Roles for {{ activeProjectSummary }}</p>
            <label
              v-for="role in projectRoles"
              :key="role.id"
              class="permission-option"
              :class="{ 'permission-option-active': isRoleSelected(role.id, 'Project') }"
            >
              <input
                :checked="isRoleSelected(role.id, 'Project')"
                :disabled="!canManageProjectAssignments"
                type="checkbox"
                @change="toggleRole(role.id, 'Project', ($event.target as HTMLInputElement).checked)"
              />
              <div class="permission-option-meta">
                <div class="permission-option-header">
                  <strong :title="role.name">{{ role.name }}</strong>
                  <span class="stat-chip">
                    {{ role.isProtected ? "Protected role" : role.hasAllPermissions ? "Full access" : `${role.permissions.length} perms` }}
                  </span>
                </div>
                <p>{{ role.description }}</p>
              </div>
            </label>
          </div>

          <button class="primary-button" :disabled="busyAction === 'user' || !canWriteUsers" type="submit">
            {{ busyAction === "user" ? "Saving..." : editingUser ? "Update user" : "Create user" }}
          </button>
        </form>

        <p v-if="!canWriteUsers" class="empty-state slim-empty-state">
          This session is read-only for users.
        </p>
        <p v-if="!canManageGlobalAssignments && globalRoles.length > 0" class="empty-state slim-empty-state">
          IAM role assignments require global `users.write`.
        </p>
        <p v-if="hasProjectContext && !canManageProjectAssignments" class="empty-state slim-empty-state">
          Project memberships in {{ activeProjectSummary }} require project `users.write`.
        </p>
        <p class="empty-state slim-empty-state">
          The protected `SuperAdmin` account is immutable and cannot be edited or deleted by any session.
        </p>
        <p class="empty-state slim-empty-state">
          The signed-in account cannot remove or delete itself from IAM.
        </p>
      </article>

      <article class="surface-card users-card">
        <div class="management-header">
          <div>
            <p class="card-label">Users</p>
            <h2 :title="hasProjectContext ? activeProjectName : undefined">
              {{ hasProjectContext ? `Visible in ${activeProjectSummary}` : "Visible IAM users" }}
            </h2>
          </div>
          <span class="stat-chip">{{ totalCount }} total</span>
        </div>

        <div v-if="loading" class="empty-state">Loading users...</div>
        <div v-else-if="users.length === 0" class="empty-state">No users are visible for this session yet.</div>
        <div v-else class="table-shell">
          <table class="management-table">
            <thead>
              <tr>
                <th>Username</th>
                <th>Email</th>
                <th>Assignments</th>
                <th>Explicit permissions</th>
                <th v-if="canWriteUsers">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in users" :key="user.id">
                <td>
                  <div class="record-body">
                    <span>{{ user.userName }}</span>
                    <small v-if="user.isProtected">Protected account</small>
                    <small v-else-if="isCurrentUserRecord(user)">Current session</small>
                  </div>
                </td>
                <td>{{ user.email }}</td>
                <td>
                  <div class="chip-list">
                    <span v-for="assignment in user.assignments" :key="assignment.id" class="permission-chip">
                      {{ assignmentLabel(assignment) }}
                    </span>
                  </div>
                </td>
                <td>
                  <div class="chip-list">
                    <span v-if="user.assignments.some(assignment => assignment.hasAllPermissions)" class="stat-chip">
                      Full access
                    </span>
                    <span v-for="permission in user.permissions" :key="permission" class="permission-chip">
                      {{ permission }}
                    </span>
                  </div>
                </td>
                <td v-if="canWriteUsers" class="table-actions">
                  <button class="secondary-button" :disabled="!canEditUserRecord(user)" type="button" @click="editUser(user)">
                    {{ user.isProtected ? "Protected" : "Edit" }}
                  </button>
                  <button class="ghost-button" :disabled="!canDeleteUserRecord(user)" type="button" @click="removeUser(user)">
                    {{ user.isProtected ? "Protected" : isCurrentUserRecord(user) ? "Current user" : hasProjectContext ? "Remove" : "Delete" }}
                  </button>
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
    </div>
  </section>
</template>
