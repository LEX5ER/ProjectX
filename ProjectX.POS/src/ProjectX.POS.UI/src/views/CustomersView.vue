<script setup lang="ts">
import { computed, reactive, ref, watch } from "vue";
import { auth } from "../auth/session";
import { canManageAcrossProjects, canManageProjectData, canReadProjectData } from "../auth/access";
import {
  createCustomer,
  deleteCustomer,
  listCustomersPage,
  type CustomerRecord,
  updateCustomer
} from "../customers/client";

const customers = ref<CustomerRecord[]>([]);
const currentPage = ref(1);
const pageSize = 10;
const totalCount = ref(0);
const totalPages = ref(0);
const searchDraft = ref("");
const appliedSearch = ref("");
const loading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");
const successMessage = ref("");

const customerForm = reactive({
  id: "",
  firstName: "",
  lastName: "",
  email: "",
  phone: "",
  notes: "",
  marketingOptIn: false,
  taxExempt: false
});

const editingCustomer = computed(() => Boolean(customerForm.id));
const currentUser = computed(() => auth.state.user);
const activeProjectId = computed(() => currentUser.value?.activeProjectId ?? "");
const activeProjectName = computed(() => auth.getActiveProject()?.name ?? "the active IAM project");
const canReadCustomers = computed(() => canReadProjectData(currentUser.value));
const canManageCustomers = computed(() => canManageProjectData(currentUser.value));
const canManageAnyProject = computed(() => canManageAcrossProjects(currentUser.value));
const selectedCustomer = computed(() => customers.value.find(customer => customer.id === customerForm.id) ?? null);
const canSubmitCustomer = computed(() => {
  if (!canManageCustomers.value) {
    return false;
  }

  if (editingCustomer.value) {
    return Boolean(selectedCustomer.value && canEditCustomer(selectedCustomer.value));
  }

  return Boolean(activeProjectId.value);
});

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function clearForm(): void {
  customerForm.id = "";
  customerForm.firstName = "";
  customerForm.lastName = "";
  customerForm.email = "";
  customerForm.phone = "";
  customerForm.notes = "";
  customerForm.marketingOptIn = false;
  customerForm.taxExempt = false;
}

function canEditCustomer(customer: CustomerRecord): boolean {
  return canManageCustomers.value
    && (canManageAnyProject.value || customer.projectId === activeProjectId.value);
}

function editCustomer(customer: CustomerRecord): void {
  clearMessages();

  if (!canEditCustomer(customer)) {
    errorMessage.value = `This session can only manage customers linked to ${activeProjectName.value}.`;
    return;
  }

  customerForm.id = customer.id;
  customerForm.firstName = customer.firstName;
  customerForm.lastName = customer.lastName;
  customerForm.email = customer.email;
  customerForm.phone = customer.phone;
  customerForm.notes = customer.notes;
  customerForm.marketingOptIn = customer.marketingOptIn;
  customerForm.taxExempt = customer.taxExempt;
}

async function loadData(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";

  try {
    if (!canReadCustomers.value) {
      customers.value = [];
      totalCount.value = 0;
      totalPages.value = 0;
      clearForm();
      return;
    }

    const result = await listCustomersPage(appliedSearch.value, currentPage.value, pageSize);
    customers.value = result.items;
    totalCount.value = result.totalCount;
    totalPages.value = result.totalPages;
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load customers.";
  } finally {
    loading.value = false;
  }
}

async function applySearch(): Promise<void> {
  currentPage.value = 1;
  appliedSearch.value = searchDraft.value.trim();
  await loadData();
}

async function resetSearch(): Promise<void> {
  if (!searchDraft.value && !appliedSearch.value) {
    return;
  }

  searchDraft.value = "";
  appliedSearch.value = "";
  currentPage.value = 1;
  await loadData();
}

async function goToPage(page: number): Promise<void> {
  if (page < 1 || (totalPages.value > 0 && page > totalPages.value) || page === currentPage.value) {
    return;
  }

  currentPage.value = page;
  await loadData();
}

async function submitCustomer(): Promise<void> {
  clearMessages();

  if (!activeProjectId.value && !editingCustomer.value) {
    errorMessage.value = "Select the POS IAM project before creating customers.";
    return;
  }

  if (!canSubmitCustomer.value) {
    errorMessage.value = editingCustomer.value
      ? `This session can only manage customers linked to ${activeProjectName.value}.`
      : `This session does not have permission to create customers for ${activeProjectName.value}.`;
    return;
  }

  busyAction.value = "customer";

  try {
    const payload = {
      firstName: customerForm.firstName,
      lastName: customerForm.lastName,
      email: customerForm.email,
      phone: customerForm.phone,
      notes: customerForm.notes,
      marketingOptIn: customerForm.marketingOptIn,
      taxExempt: customerForm.taxExempt
    };

    if (editingCustomer.value) {
      await updateCustomer(customerForm.id, payload);
      successMessage.value = "Customer updated.";
    } else {
      await createCustomer(payload);
      successMessage.value = "Customer created.";
    }

    clearForm();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to save customer.";
  } finally {
    busyAction.value = "";
  }
}

async function removeCustomer(customer: CustomerRecord): Promise<void> {
  clearMessages();

  if (!canEditCustomer(customer)) {
    errorMessage.value = `This session can only manage customers linked to ${activeProjectName.value}.`;
    return;
  }

  if (!window.confirm(`Delete customer "${customer.fullName}"? Historical sales will keep the receipt snapshot.`)) {
    return;
  }

  busyAction.value = `delete-${customer.id}`;

  try {
    await deleteCustomer(customer.id);
    successMessage.value = "Customer deleted.";

    if (customerForm.id === customer.id) {
      clearForm();
    }

    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to delete customer.";
  } finally {
    busyAction.value = "";
  }
}

function formatTimestamp(value: string): string {
  return new Date(value).toLocaleString();
}

watch(activeProjectId, async () => {
  currentPage.value = 1;
  searchDraft.value = "";
  appliedSearch.value = "";
  clearMessages();
  clearForm();
  await loadData();
}, { immediate: true });
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Customers</p>
      <h1 class="hero-title">Keep customer profiles attached to the active IAM project.</h1>
      <p class="hero-text">
        Customer records drive receipt contact details, tax-exempt checkout handling, and repeat buyer history for
        {{ activeProjectName }}.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div v-if="!canReadCustomers" class="empty-state">
      Select the POS IAM project in IAM before loading customer records.
    </div>

    <div v-else class="management-split">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Customer Form</p>
            <h2>{{ editingCustomer ? "Edit customer" : "Create customer" }}</h2>
          </div>
          <button class="ghost-button" type="button" @click="clearForm">Reset</button>
        </div>

        <form class="stack-form" @submit.prevent="submitCustomer">
          <div class="field-grid">
            <label class="field">
              <span>First name</span>
              <input
                v-model="customerForm.firstName"
                :disabled="busyAction === 'customer'"
                maxlength="100"
                placeholder="Maria"
                type="text"
              />
            </label>

            <label class="field">
              <span>Last name</span>
              <input
                v-model="customerForm.lastName"
                :disabled="busyAction === 'customer'"
                maxlength="100"
                placeholder="Santos"
                type="text"
              />
            </label>
          </div>

          <div class="field-grid">
            <label class="field">
              <span>Email</span>
              <input
                v-model="customerForm.email"
                :disabled="busyAction === 'customer'"
                maxlength="200"
                placeholder="customer@example.com"
                type="email"
              />
            </label>

            <label class="field">
              <span>Phone</span>
              <input
                v-model="customerForm.phone"
                :disabled="busyAction === 'customer'"
                maxlength="50"
                placeholder="+63-917-555-0101"
                type="text"
              />
            </label>
          </div>

          <label class="field">
            <span>Notes</span>
            <textarea
              v-model="customerForm.notes"
              :disabled="busyAction === 'customer'"
              maxlength="500"
              placeholder="Receipt preference, loyalty notes, or account context."
              rows="4"
            />
          </label>

          <div class="toggle-grid">
            <label class="checkbox-field">
              <input v-model="customerForm.marketingOptIn" :disabled="busyAction === 'customer'" type="checkbox" />
              <span>Marketing opt-in</span>
            </label>

            <label class="checkbox-field">
              <input v-model="customerForm.taxExempt" :disabled="busyAction === 'customer'" type="checkbox" />
              <span>Tax exempt</span>
            </label>
          </div>

          <button class="primary-button" :disabled="busyAction === 'customer' || !canSubmitCustomer" type="submit">
            {{ busyAction === "customer" ? "Saving..." : editingCustomer ? "Update customer" : "Create customer" }}
          </button>
        </form>

        <p v-if="!editingCustomer && !activeProjectId" class="empty-state slim-empty-state">
          Select the POS IAM project before adding customer profiles so the record is scoped correctly.
        </p>
      </article>

      <article class="surface-card users-card">
        <div class="management-header">
          <div>
            <p class="card-label">Directory</p>
            <h2>Accessible customers</h2>
          </div>
          <span class="stat-chip">{{ totalCount }} total</span>
        </div>

        <div class="toolbar-row">
          <label class="field toolbar-field">
            <span>Search customers</span>
            <input
              v-model="searchDraft"
              maxlength="100"
              placeholder="Name, email, phone, or notes"
              type="text"
              @keyup.enter="applySearch"
            />
          </label>

          <div class="toolbar-actions">
            <button class="secondary-button" :disabled="loading" type="button" @click="applySearch">
              Search
            </button>
            <button class="ghost-button" :disabled="loading" type="button" @click="resetSearch">
              Clear
            </button>
          </div>
        </div>

        <div v-if="loading" class="empty-state">Loading customers...</div>
        <div v-else-if="customers.length === 0" class="empty-state">No customer records are visible for this scope yet.</div>
        <div v-else class="record-list">
          <article v-for="customer in customers" :key="customer.id" class="record-card">
            <div class="record-body">
              <strong>{{ customer.fullName || "Unnamed customer" }}</strong>
              <p>{{ customer.notes || "No notes saved for this customer yet." }}</p>
              <div class="chip-list">
                <span v-if="customer.marketingOptIn" class="stat-chip">Marketing opt-in</span>
                <span v-if="customer.taxExempt" class="stat-chip">Tax exempt</span>
                <span v-if="!customer.marketingOptIn && !customer.taxExempt" class="stat-chip">Standard profile</span>
              </div>
              <small>{{ customer.email || "No email saved" }}</small>
              <small>{{ customer.phone || "No phone saved" }}</small>
              <small>Updated: {{ formatTimestamp(customer.updatedAtUtc) }}</small>
              <code>{{ customer.id }}</code>
            </div>
            <div class="record-actions">
              <button class="secondary-button" :disabled="!canEditCustomer(customer)" type="button" @click="editCustomer(customer)">
                {{ canEditCustomer(customer) ? "Edit" : "Read only" }}
              </button>
              <button
                class="ghost-button"
                :disabled="busyAction === `delete-${customer.id}` || !canEditCustomer(customer)"
                type="button"
                @click="removeCustomer(customer)"
              >
                {{
                  busyAction === `delete-${customer.id}`
                    ? "Deleting..."
                    : canEditCustomer(customer)
                      ? "Delete"
                      : "Locked"
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
