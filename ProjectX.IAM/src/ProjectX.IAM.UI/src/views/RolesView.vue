<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import {
  createPermission,
  createRole,
  deletePermission,
  deleteRole,
  listPermissions,
  listRoles,
  type PermissionRecord,
  type RoleRecord,
  type RoleScope,
  updatePermission,
  updateRole,
  updateRolePermissions
} from "../admin/client";
import { auth } from "../auth/session";

const roles = ref<RoleRecord[]>([]);
const permissions = ref<PermissionRecord[]>([]);
const selectedRoleId = ref("");
const assignedPermissionIds = ref<string[]>([]);

const loading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");
const successMessage = ref("");

const currentUser = computed(() => auth.state.user);
const hasProjectContext = computed(() => Boolean(currentUser.value?.activeProjectId));
const grantedPermissions = computed(() => new Set(currentUser.value?.permissions ?? []));
const globalPermissions = computed(() => new Set(currentUser.value?.globalPermissions ?? []));
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));

const roleForm = reactive({
  id: "",
  name: "",
  description: "",
  scope: "Project" as RoleScope
});

const permissionForm = reactive({
  id: "",
  name: "",
  description: "",
  scope: "Project" as RoleScope
});

const canReadRoles = computed(() => Boolean(currentUser.value?.hasAllPermissions || grantedPermissions.value.has("roles.read")));
const canReadPermissions = computed(() => Boolean(currentUser.value?.hasAllPermissions || grantedPermissions.value.has("permissions.read")));
const canManageGlobalRoles = computed(() => Boolean(currentUser.value?.hasGlobalFullAccess || globalPermissions.value.has("roles.write")));
const canManageProjectRoles = computed(() => Boolean(
  hasProjectContext.value && (currentUser.value?.hasAllPermissions || activeProjectPermissions.value.has("roles.write"))
));
const canManageGlobalPermissions = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess || globalPermissions.value.has("permissions.write")
));
const canManageProjectPermissions = computed(() => Boolean(
  hasProjectContext.value && (currentUser.value?.hasAllPermissions || activeProjectPermissions.value.has("permissions.write"))
));
const editingRole = computed(() => Boolean(roleForm.id));
const editingPermission = computed(() => Boolean(permissionForm.id));
const selectedRole = computed(() => roles.value.find(role => role.id === selectedRoleId.value) ?? null);
const availablePermissionsForSelectedRole = computed(() =>
  permissions.value.filter(permission =>
    permission.scope === selectedRole.value?.scope
    && permission.projectId === selectedRole.value?.projectId)
);
const globalRoles = computed(() => roles.value.filter(role => role.scope === "Global"));
const projectRoles = computed(() => roles.value.filter(role => role.scope === "Project"));
const globalPermissionsList = computed(() => permissions.value.filter(permission => permission.scope === "Global"));
const projectPermissionsList = computed(() => permissions.value.filter(permission => permission.scope === "Project"));

function defaultScope(): RoleScope {
  return hasProjectContext.value ? "Project" : "Global";
}

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function clearRoleForm(): void {
  roleForm.id = "";
  roleForm.name = "";
  roleForm.description = "";
  roleForm.scope = defaultScope();
}

function clearPermissionForm(): void {
  permissionForm.id = "";
  permissionForm.name = "";
  permissionForm.description = "";
  permissionForm.scope = defaultScope();
}

function syncSelectedRoleSelection(): void {
  const roleExists = roles.value.some(role => role.id === selectedRoleId.value);

  if (!roleExists) {
    selectedRoleId.value = roles.value[0]?.id ?? "";
  }

  assignedPermissionIds.value = selectedRole.value?.permissions.map(permission => permission.id) ?? [];
}

function selectRole(role: RoleRecord): void {
  selectedRoleId.value = role.id;
  assignedPermissionIds.value = role.permissions.map(permission => permission.id);
}

function editRole(role: RoleRecord): void {
  clearMessages();
  selectRole(role);
  roleForm.id = role.id;
  roleForm.name = role.name;
  roleForm.description = role.description;
  roleForm.scope = role.scope;
}

function editPermission(permission: PermissionRecord): void {
  clearMessages();
  permissionForm.id = permission.id;
  permissionForm.name = permission.name;
  permissionForm.description = permission.description;
  permissionForm.scope = permission.scope;
}

function canManageRole(role: RoleRecord): boolean {
  return role.scope === "Global" ? canManageGlobalRoles.value : canManageProjectRoles.value;
}

function canManagePermission(permission: PermissionRecord): boolean {
  return permission.scope === "Global" ? canManageGlobalPermissions.value : canManageProjectPermissions.value;
}

function canSubmitRole(scope: RoleScope): boolean {
  return scope === "Global" ? canManageGlobalRoles.value : canManageProjectRoles.value;
}

function canSubmitPermission(scope: RoleScope): boolean {
  return scope === "Global" ? canManageGlobalPermissions.value : canManageProjectPermissions.value;
}

async function loadData(): Promise<void> {
  loading.value = true;
  clearMessages();

  try {
    roles.value = canReadRoles.value ? await listRoles() : [];
    permissions.value = canReadPermissions.value ? await listPermissions() : [];
    syncSelectedRoleSelection();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load roles and permissions.";
  } finally {
    loading.value = false;
  }
}

async function submitRole(): Promise<void> {
  clearMessages();

  if (roleForm.scope === "Project" && !hasProjectContext.value) {
    errorMessage.value = "Select a project before managing project roles.";
    return;
  }

  if (!canSubmitRole(roleForm.scope)) {
    errorMessage.value = roleForm.scope === "Global"
      ? "Global roles.write is required to manage IAM roles."
      : "Project roles.write is required to manage project roles.";
    return;
  }

  busyAction.value = "role";

  try {
    if (editingRole.value) {
      await updateRole(roleForm.id, {
        name: roleForm.name,
        description: roleForm.description,
        scope: roleForm.scope
      });

      successMessage.value = "Role updated.";
    } else {
      const createdRole = await createRole({
        name: roleForm.name,
        description: roleForm.description,
        scope: roleForm.scope
      });

      selectedRoleId.value = createdRole.id;
      successMessage.value = "Role created.";
    }

    clearRoleForm();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to save role.";
  } finally {
    busyAction.value = "";
  }
}

async function submitPermission(): Promise<void> {
  clearMessages();

  if (permissionForm.scope === "Project" && !hasProjectContext.value) {
    errorMessage.value = "Select a project before managing project permissions.";
    return;
  }

  if (!canSubmitPermission(permissionForm.scope)) {
    errorMessage.value = permissionForm.scope === "Global"
      ? "Global permissions.write is required to manage IAM permissions."
      : "Project permissions.write is required to manage project permissions.";
    return;
  }

  busyAction.value = "permission";

  try {
    if (editingPermission.value) {
      await updatePermission(permissionForm.id, {
        name: permissionForm.name,
        description: permissionForm.description,
        scope: permissionForm.scope
      });

      successMessage.value = "Permission updated.";
    } else {
      await createPermission({
        name: permissionForm.name,
        description: permissionForm.description,
        scope: permissionForm.scope
      });

      successMessage.value = "Permission created.";
    }

    clearPermissionForm();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to save permission.";
  } finally {
    busyAction.value = "";
  }
}

function isPermissionAssigned(permissionId: string): boolean {
  return assignedPermissionIds.value.includes(permissionId);
}

function togglePermission(permissionId: string, checked: boolean): void {
  if (checked) {
    if (!assignedPermissionIds.value.includes(permissionId)) {
      assignedPermissionIds.value = [...assignedPermissionIds.value, permissionId];
    }

    return;
  }

  assignedPermissionIds.value = assignedPermissionIds.value.filter(currentPermissionId => currentPermissionId !== permissionId);
}

async function submitRolePermissions(): Promise<void> {
  clearMessages();

  if (!selectedRole.value) {
    errorMessage.value = "Select a role before managing permissions.";
    return;
  }

  if (!canManageRole(selectedRole.value)) {
    errorMessage.value = selectedRole.value.scope === "Global"
      ? "Global roles.write is required to manage IAM role permissions."
      : "Project roles.write is required to manage project role permissions.";
    return;
  }

  if (selectedRole.value.isProtected) {
    errorMessage.value = "Protected roles cannot be modified.";
    return;
  }

  if (selectedRole.value.hasAllPermissions) {
    errorMessage.value = "This role already grants full access automatically.";
    return;
  }

  busyAction.value = "role-permissions";

  try {
    await updateRolePermissions(selectedRole.value.id, {
      permissionIds: assignedPermissionIds.value
    });

    successMessage.value = "Role permissions updated.";
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to update role permissions.";
  } finally {
    busyAction.value = "";
  }
}

async function removeRole(role: RoleRecord): Promise<void> {
  clearMessages();

  if (role.isProtected) {
    errorMessage.value = "Protected roles cannot be deleted.";
    return;
  }

  if (!canManageRole(role)) {
    errorMessage.value = role.scope === "Global"
      ? "Global roles.write is required to delete IAM roles."
      : "Project roles.write is required to delete project roles.";
    return;
  }

  if (!window.confirm(`Delete role "${role.name}"?`)) {
    return;
  }

  busyAction.value = `delete-role-${role.id}`;

  try {
    await deleteRole(role.id);
    successMessage.value = "Role deleted.";

    if (roleForm.id === role.id) {
      clearRoleForm();
    }

    if (selectedRoleId.value === role.id) {
      selectedRoleId.value = "";
    }

    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to delete role.";
  } finally {
    busyAction.value = "";
  }
}

async function removePermission(permission: PermissionRecord): Promise<void> {
  clearMessages();

  if (!canManagePermission(permission)) {
    errorMessage.value = permission.scope === "Global"
      ? "Global permissions.write is required to delete IAM permissions."
      : "Project permissions.write is required to delete project permissions.";
    return;
  }

  if (!window.confirm(`Delete permission "${permission.name}"?`)) {
    return;
  }

  busyAction.value = `delete-permission-${permission.id}`;

  try {
    await deletePermission(permission.id);
    successMessage.value = "Permission deleted.";

    if (permissionForm.id === permission.id) {
      clearPermissionForm();
    }

    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to delete permission.";
  } finally {
    busyAction.value = "";
  }
}

onMounted(async () => {
  clearRoleForm();
  clearPermissionForm();
  await loadData();
});

watch(
  () => `${currentUser.value?.activeProjectId ?? ""}|${currentUser.value?.permissions.join(",") ?? ""}|${currentUser.value?.globalPermissions.join(",") ?? ""}|${currentUser.value?.activeProjectPermissions.join(",") ?? ""}`,
  async () => {
    clearRoleForm();
    clearPermissionForm();
    await loadData();
  }
);
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Roles & Permissions</p>
      <h1 class="hero-title">Manage IAM access and project access</h1>
      <p class="hero-text">
        IAM roles and permissions apply globally. Project roles and permissions belong only to the selected
        project. Protected roles such as `SuperAdmin` and `Project Admin` stay visible but cannot be edited.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div class="role-workspace">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Roles</p>
            <h2>{{ editingRole ? "Edit role" : "Create role" }}</h2>
          </div>
          <button class="ghost-button" type="button" @click="clearRoleForm">Reset</button>
        </div>

        <form class="stack-form" @submit.prevent="submitRole">
          <label class="field">
            <span>Name</span>
            <input v-model="roleForm.name" :disabled="!canSubmitRole(roleForm.scope)" required type="text" />
          </label>

          <label class="field">
            <span>Description</span>
            <input v-model="roleForm.description" :disabled="!canSubmitRole(roleForm.scope)" required type="text" />
          </label>

          <label class="field">
            <span>Scope</span>
            <select v-model="roleForm.scope" :disabled="editingRole">
              <option value="Global">IAM</option>
              <option value="Project">Project</option>
            </select>
          </label>

          <p class="empty-state slim-empty-state">
            Full access stays reserved for protected built-in roles only.
          </p>

          <button class="primary-button" :disabled="busyAction === 'role' || !canSubmitRole(roleForm.scope)" type="submit">
            {{ busyAction === "role" ? "Saving..." : editingRole ? "Update role" : "Create role" }}
          </button>
        </form>

        <div v-if="loading" class="empty-state">Loading roles...</div>
        <div v-else-if="roles.length === 0" class="empty-state">No roles are visible for this session.</div>
        <div v-else class="record-list">
          <article>
            <p class="card-label">IAM roles</p>
            <div v-if="globalRoles.length === 0" class="empty-state slim-empty-state">No IAM roles are visible.</div>
            <div v-else class="record-list nested-record-list">
              <article
                v-for="role in globalRoles"
                :key="role.id"
                class="record-card"
                :class="{ 'record-card-active': selectedRoleId === role.id }"
              >
                <div class="record-body">
                  <strong>{{ role.name }}</strong>
                  <p>{{ role.description }}</p>
                  <small>{{ role.assignmentCount }} assignments</small>
                  <div class="chip-list role-chip-list">
                    <span class="stat-chip">IAM</span>
                    <span v-if="role.isProtected" class="stat-chip">Protected</span>
                    <span v-if="role.hasAllPermissions" class="stat-chip">Full access</span>
                    <span v-for="permission in role.permissions" :key="permission.id" class="permission-chip">
                      {{ permission.name }}
                    </span>
                  </div>
                </div>
                <div class="record-actions">
                  <button class="secondary-button" type="button" @click="selectRole(role)">Select</button>
                  <button class="secondary-button" :disabled="!canManageRole(role) || role.isProtected" type="button" @click="editRole(role)">
                    {{ role.isProtected ? "Protected" : "Edit" }}
                  </button>
                  <button class="ghost-button" :disabled="!canManageRole(role) || role.isProtected" type="button" @click="removeRole(role)">
                    {{ role.isProtected ? "Protected" : "Delete" }}
                  </button>
                </div>
              </article>
            </div>
          </article>

          <article>
            <p class="card-label">Project roles</p>
            <div v-if="!hasProjectContext" class="empty-state slim-empty-state">
              Select a project to view and manage its isolated roles.
            </div>
            <div v-else-if="projectRoles.length === 0" class="empty-state slim-empty-state">
              No project roles are visible in {{ currentUser?.activeProjectName }}.
            </div>
            <div v-else class="record-list nested-record-list">
              <article
                v-for="role in projectRoles"
                :key="role.id"
                class="record-card"
                :class="{ 'record-card-active': selectedRoleId === role.id }"
              >
                <div class="record-body">
                  <strong>{{ role.name }}</strong>
                  <p>{{ role.description }}</p>
                  <small>{{ role.assignmentCount }} assignments</small>
                  <div class="chip-list role-chip-list">
                    <span class="stat-chip">{{ role.projectName ?? currentUser?.activeProjectName ?? "Project" }}</span>
                    <span v-if="role.isProtected" class="stat-chip">Protected</span>
                    <span v-if="role.hasAllPermissions" class="stat-chip">Full access</span>
                    <span v-for="permission in role.permissions" :key="permission.id" class="permission-chip">
                      {{ permission.name }}
                    </span>
                  </div>
                </div>
                <div class="record-actions">
                  <button class="secondary-button" type="button" @click="selectRole(role)">Select</button>
                  <button class="secondary-button" :disabled="!canManageRole(role) || role.isProtected" type="button" @click="editRole(role)">
                    {{ role.isProtected ? "Protected" : "Edit" }}
                  </button>
                  <button class="ghost-button" :disabled="!canManageRole(role) || role.isProtected" type="button" @click="removeRole(role)">
                    {{ role.isProtected ? "Protected" : "Delete" }}
                  </button>
                </div>
              </article>
            </div>
          </article>
        </div>
      </article>

      <div class="role-detail-column">
        <article class="surface-card">
          <div class="management-header">
            <div>
              <p class="card-label">Permission Assignment</p>
              <h2>{{ selectedRole ? selectedRole.name : "Select a role" }}</h2>
            </div>
            <span v-if="selectedRole" class="stat-chip">{{ selectedRole.assignmentCount }} assignments</span>
          </div>

          <p v-if="selectedRole" class="hero-text compact-text">
            {{ selectedRole.description }}
          </p>
          <div v-else class="empty-state">
            Choose a role from the left column to manage its explicit permissions.
          </div>

          <div v-if="selectedRole?.isProtected" class="empty-state slim-empty-state">
            Protected roles keep their definition locked.
          </div>

          <div v-else-if="selectedRole?.hasAllPermissions" class="empty-state slim-empty-state">
            This role grants full access automatically. Explicit permissions are not required.
          </div>

          <div v-else-if="selectedRole && availablePermissionsForSelectedRole.length === 0" class="empty-state slim-empty-state">
            No permissions exist in this scope yet.
          </div>

          <div v-else-if="selectedRole" class="permission-selector">
            <label
              v-for="permission in availablePermissionsForSelectedRole"
              :key="permission.id"
              class="permission-option"
              :class="{ 'permission-option-active': isPermissionAssigned(permission.id) }"
            >
              <input
                :checked="isPermissionAssigned(permission.id)"
                :disabled="!canManageRole(selectedRole) || selectedRole.hasAllPermissions || selectedRole.isProtected"
                type="checkbox"
                @change="togglePermission(permission.id, ($event.target as HTMLInputElement).checked)"
              />
              <div class="permission-option-meta">
                <div class="permission-option-header">
                  <strong :title="permission.name">{{ permission.name }}</strong>
                  <span class="stat-chip">{{ permission.roleCount }} roles</span>
                </div>
                <p>{{ permission.description }}</p>
              </div>
            </label>
          </div>

          <div v-if="selectedRole" class="hero-actions">
            <button
              class="primary-button"
              :disabled="busyAction === 'role-permissions' || !canManageRole(selectedRole) || selectedRole.hasAllPermissions || selectedRole.isProtected"
              type="button"
              @click="submitRolePermissions"
            >
              {{ busyAction === "role-permissions" ? "Saving..." : "Save permissions" }}
            </button>
          </div>
        </article>

        <article class="surface-card">
          <div class="management-header">
            <div>
              <p class="card-label">Permissions</p>
              <h2>{{ editingPermission ? "Edit permission" : "Create permission" }}</h2>
            </div>
            <button class="ghost-button" type="button" @click="clearPermissionForm">Reset</button>
          </div>

          <form class="stack-form" @submit.prevent="submitPermission">
            <label class="field">
              <span>Name</span>
              <input v-model="permissionForm.name" :disabled="!canSubmitPermission(permissionForm.scope)" required type="text" />
            </label>

            <label class="field">
              <span>Description</span>
              <input v-model="permissionForm.description" :disabled="!canSubmitPermission(permissionForm.scope)" required type="text" />
            </label>

            <label class="field">
              <span>Scope</span>
              <select v-model="permissionForm.scope" :disabled="editingPermission">
                <option value="Global">IAM</option>
                <option value="Project">Project</option>
              </select>
            </label>

            <button class="primary-button" :disabled="busyAction === 'permission' || !canSubmitPermission(permissionForm.scope)" type="submit">
              {{ busyAction === "permission" ? "Saving..." : editingPermission ? "Update permission" : "Create permission" }}
            </button>
          </form>

          <div class="record-list">
            <article>
              <p class="card-label">IAM permissions</p>
              <div v-if="globalPermissionsList.length === 0" class="empty-state slim-empty-state">No IAM permissions are visible.</div>
              <div v-else class="record-list nested-record-list">
                <article v-for="permission in globalPermissionsList" :key="permission.id" class="record-card">
                  <div class="record-body">
                    <strong>{{ permission.name }}</strong>
                    <p>{{ permission.description }}</p>
                    <small>{{ permission.roleCount }} roles assigned</small>
                  </div>
                  <div class="record-actions">
                    <button class="secondary-button" :disabled="!canManagePermission(permission)" type="button" @click="editPermission(permission)">Edit</button>
                    <button class="ghost-button" :disabled="!canManagePermission(permission) || permission.roleCount > 0" type="button" @click="removePermission(permission)">
                      {{ permission.roleCount > 0 ? "In use" : "Delete" }}
                    </button>
                  </div>
                </article>
              </div>
            </article>

            <article>
              <p class="card-label">Project permissions</p>
              <div v-if="!hasProjectContext" class="empty-state slim-empty-state">
                Select a project to manage its isolated permissions.
              </div>
              <div v-else-if="projectPermissionsList.length === 0" class="empty-state slim-empty-state">
                No project permissions are visible in {{ currentUser?.activeProjectName }}.
              </div>
              <div v-else class="record-list nested-record-list">
                <article v-for="permission in projectPermissionsList" :key="permission.id" class="record-card">
                  <div class="record-body">
                    <strong>{{ permission.name }}</strong>
                    <p>{{ permission.description }}</p>
                    <small>{{ permission.roleCount }} roles assigned</small>
                  </div>
                  <div class="record-actions">
                    <button class="secondary-button" :disabled="!canManagePermission(permission)" type="button" @click="editPermission(permission)">Edit</button>
                    <button class="ghost-button" :disabled="!canManagePermission(permission) || permission.roleCount > 0" type="button" @click="removePermission(permission)">
                      {{ permission.roleCount > 0 ? "In use" : "Delete" }}
                    </button>
                  </div>
                </article>
              </div>
            </article>
          </div>
        </article>
      </div>
    </div>
  </section>
</template>
