<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { auth } from "../auth/session";
import { canManageAcrossProjects, canManageProjectData, canReadProjectData } from "../auth/access";
import {
  getSale,
  listSalesPage,
  refundSale,
  type SaleDetailRecord,
  type SaleSummaryRecord
} from "../sales/client";

const sales = ref<SaleSummaryRecord[]>([]);
const currentPage = ref(1);
const pageSize = 10;
const totalCount = ref(0);
const totalPages = ref(0);
const searchDraft = ref("");
const appliedSearch = ref("");
const selectedSaleId = ref("");
const selectedSale = ref<SaleDetailRecord | null>(null);
const refundReason = ref("");
const restockOnRefund = ref(true);
const loading = ref(false);
const detailLoading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");
const successMessage = ref("");

const currentUser = computed(() => auth.state.user);
const activeProjectId = computed(() => currentUser.value?.activeProjectId ?? "");
const activeProjectName = computed(() => auth.getActiveProject()?.name ?? "the active IAM project");
const canReadSales = computed(() => canReadProjectData(currentUser.value));
const canManageSales = computed(() => canManageProjectData(currentUser.value));
const canManageAnyProject = computed(() => canManageAcrossProjects(currentUser.value));
const canRefundSelectedSale = computed(() => Boolean(
  selectedSale.value
  && canManageSale(selectedSale.value)
  && selectedSale.value.status !== "Refunded"
  && refundReason.value.trim()
  && busyAction.value !== "refund"
));

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function canManageSale(sale: { projectId: string }): boolean {
  return canManageSales.value
    && (canManageAnyProject.value || sale.projectId === activeProjectId.value);
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-PH", {
    style: "currency",
    currency: "PHP"
  }).format(value);
}

function formatTimestamp(value: string | null): string {
  if (!value) {
    return "Not recorded";
  }

  return new Date(value).toLocaleString();
}

function formatPaymentMethod(value: string): string {
  return value.replace(/([A-Z])/g, " $1").trim();
}

async function loadSaleDetail(id: string, preserveMessages = false): Promise<void> {
  if (!id) {
    selectedSaleId.value = "";
    selectedSale.value = null;
    return;
  }

  if (!preserveMessages) {
    clearMessages();
  }

  detailLoading.value = true;
  selectedSaleId.value = id;

  try {
    selectedSale.value = await getSale(id);
  } catch (error) {
    selectedSale.value = null;
    errorMessage.value = error instanceof Error ? error.message : "Unable to load sale details.";
  } finally {
    detailLoading.value = false;
  }
}

async function loadData(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";

  try {
    if (!canReadSales.value) {
      sales.value = [];
      totalCount.value = 0;
      totalPages.value = 0;
      selectedSaleId.value = "";
      selectedSale.value = null;
      return;
    }

    const result = await listSalesPage(appliedSearch.value, currentPage.value, pageSize);
    sales.value = result.items;
    totalCount.value = result.totalCount;
    totalPages.value = result.totalPages;

    const nextSelectedSaleId = result.items.some(sale => sale.id === selectedSaleId.value)
      ? selectedSaleId.value
      : (result.items[0]?.id ?? "");

    if (nextSelectedSaleId) {
      await loadSaleDetail(nextSelectedSaleId, true);
    } else {
      selectedSaleId.value = "";
      selectedSale.value = null;
    }
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load sales.";
  } finally {
    loading.value = false;
  }
}

async function selectSale(id: string): Promise<void> {
  await loadSaleDetail(id);
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

async function submitRefund(): Promise<void> {
  if (!selectedSale.value) {
    return;
  }

  clearMessages();

  if (!canManageSale(selectedSale.value)) {
    errorMessage.value = `This session can only manage sales linked to ${activeProjectName.value}.`;
    return;
  }

  if (selectedSale.value.status === "Refunded") {
    errorMessage.value = "This receipt has already been refunded.";
    return;
  }

  if (!refundReason.value.trim()) {
    errorMessage.value = "Refund reason is required.";
    return;
  }

  if (!window.confirm(`Refund receipt ${selectedSale.value.receiptNumber}?`)) {
    return;
  }

  busyAction.value = "refund";

  try {
    const updatedSale = await refundSale(selectedSale.value.id, {
      reason: refundReason.value.trim(),
      restock: restockOnRefund.value
    });

    selectedSale.value = updatedSale;
    refundReason.value = "";
    successMessage.value = `Receipt ${updatedSale.receiptNumber} refunded successfully.`;
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to refund sale.";
  } finally {
    busyAction.value = "";
  }
}

watch(activeProjectId, async () => {
  currentPage.value = 1;
  searchDraft.value = "";
  appliedSearch.value = "";
  selectedSaleId.value = "";
  selectedSale.value = null;
  refundReason.value = "";
  restockOnRefund.value = true;
  clearMessages();
  await loadData();
}, { immediate: true });
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Sales</p>
      <h1 class="hero-title">Review receipts, payment history, and refunds from one queue.</h1>
      <p class="hero-text">
        Each receipt stays attached to the active IAM project and preserves customer, cashier, payment, and line-item
        snapshots even if catalog data changes later.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div v-if="!canReadSales" class="empty-state">
      Select the POS IAM project in IAM before loading sales and receipt history.
    </div>

    <div v-else class="sales-layout">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Receipt Queue</p>
            <h2>Accessible sales</h2>
          </div>
          <span class="stat-chip">{{ totalCount }} total</span>
        </div>

        <div class="toolbar-row">
          <label class="field toolbar-field">
            <span>Search receipts</span>
            <input
              v-model="searchDraft"
              maxlength="100"
              placeholder="Receipt, customer, cashier, or note"
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

        <div v-if="loading" class="empty-state">Loading sales...</div>
        <div v-else-if="sales.length === 0" class="empty-state">No sales are visible for this scope yet.</div>
        <div v-else class="record-list">
          <button
            v-for="sale in sales"
            :key="sale.id"
            class="record-card-button"
            type="button"
            @click="selectSale(sale.id)"
          >
            <article class="record-card" :class="{ 'record-card-active': selectedSaleId === sale.id }">
              <div class="record-body">
                <strong>{{ sale.receiptNumber }}</strong>
                <p>{{ sale.customerName || "Walk-in customer" }}</p>
                <div class="chip-list">
                  <span class="stat-chip">{{ sale.status }}</span>
                  <span class="stat-chip">{{ formatPaymentMethod(sale.paymentMethod) }}</span>
                  <span class="stat-chip">{{ sale.totalItems }} items</span>
                </div>
                <small>{{ formatCurrency(sale.totalAmount) }}</small>
                <small>{{ formatTimestamp(sale.createdAtUtc) }}</small>
              </div>
            </article>
          </button>
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

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Receipt Detail</p>
            <h2>{{ selectedSale?.receiptNumber || "Select a receipt" }}</h2>
          </div>
          <div v-if="selectedSale" class="chip-list">
            <span class="stat-chip">{{ selectedSale.status }}</span>
            <span class="stat-chip">{{ formatPaymentMethod(selectedSale.paymentMethod) }}</span>
          </div>
        </div>

        <div v-if="detailLoading" class="empty-state">Loading receipt detail...</div>
        <div v-else-if="!selectedSale" class="empty-state">Choose a receipt from the queue to inspect the full sale.</div>
        <div v-else class="stack-form">
          <div class="detail-grid">
            <section class="detail-panel">
              <p class="card-label">Customer</p>
              <strong>{{ selectedSale.customerName || "Walk-in customer" }}</strong>
              <p>{{ selectedSale.customerEmail || selectedSale.receiptEmail || "No customer contact saved." }}</p>
              <small>Cashier: {{ selectedSale.cashierUserName }}</small>
              <small>Created: {{ formatTimestamp(selectedSale.createdAtUtc) }}</small>
            </section>

            <section class="detail-panel">
              <p class="card-label">Settlement</p>
              <div class="cart-summary compact-summary">
                <div class="summary-row">
                  <span>Subtotal</span>
                  <strong>{{ formatCurrency(selectedSale.subtotalAmount) }}</strong>
                </div>
                <div class="summary-row">
                  <span>Discounts</span>
                  <strong>{{ formatCurrency(selectedSale.lineDiscountAmount + selectedSale.cartDiscountAmount) }}</strong>
                </div>
                <div class="summary-row">
                  <span>Tax</span>
                  <strong>{{ formatCurrency(selectedSale.taxAmount) }}</strong>
                </div>
                <div class="summary-row summary-row-total">
                  <span>Total</span>
                  <strong>{{ formatCurrency(selectedSale.totalAmount) }}</strong>
                </div>
                <div class="summary-row">
                  <span>Paid</span>
                  <strong>{{ formatCurrency(selectedSale.paidAmount) }}</strong>
                </div>
                <div class="summary-row">
                  <span>Change</span>
                  <strong>{{ formatCurrency(selectedSale.changeAmount) }}</strong>
                </div>
              </div>
            </section>
          </div>

          <div class="detail-grid">
            <section class="detail-panel">
              <p class="card-label">Receipt Notes</p>
              <p>{{ selectedSale.note || "No cashier note saved for this receipt." }}</p>
              <small>Receipt email: {{ selectedSale.receiptEmail || "Not requested" }}</small>
              <small>Updated: {{ formatTimestamp(selectedSale.updatedAtUtc) }}</small>
            </section>

            <section class="detail-panel">
              <p class="card-label">Refund</p>
              <template v-if="selectedSale.status === 'Refunded'">
                <strong>Refund completed</strong>
                <p>{{ selectedSale.refundReason || "Refund reason not stored." }}</p>
                <small>Refunded by {{ selectedSale.refundedByUserName || "Unknown user" }}</small>
                <small>{{ formatTimestamp(selectedSale.refundedAtUtc) }}</small>
                <small>{{ selectedSale.restockedOnRefund ? "Inventory was restocked." : "Inventory was not restocked." }}</small>
              </template>
              <template v-else-if="canManageSale(selectedSale)">
                <label class="field">
                  <span>Refund reason</span>
                  <textarea
                    v-model="refundReason"
                    :disabled="busyAction === 'refund'"
                    maxlength="500"
                    placeholder="State why this receipt is being reversed."
                    rows="3"
                  />
                </label>
                <label class="checkbox-field">
                  <input v-model="restockOnRefund" :disabled="busyAction === 'refund'" type="checkbox" />
                  <span>Return sold quantities to inventory</span>
                </label>
                <button class="primary-button" :disabled="!canRefundSelectedSale" type="button" @click="submitRefund">
                  {{ busyAction === "refund" ? "Processing refund..." : "Refund sale" }}
                </button>
              </template>
              <template v-else>
                <p>This session can review this receipt but cannot issue refunds for it.</p>
              </template>
            </section>
          </div>

          <div class="record-list">
            <article v-for="line in selectedSale.lineItems" :key="line.id" class="record-card">
              <div class="record-body">
                <strong>{{ line.productName }}</strong>
                <div class="chip-list">
                  <span class="stat-chip">{{ line.productCode }}</span>
                  <span class="stat-chip">{{ line.category }}</span>
                  <span class="stat-chip">{{ line.quantity }} qty</span>
                </div>
                <small>{{ formatCurrency(line.unitPrice) }} each</small>
                <small>Discount: {{ formatCurrency(line.discountAmount) }}</small>
                <small>Line total: {{ formatCurrency(line.lineTotalAmount) }}</small>
              </div>
            </article>
          </div>
        </div>
      </article>
    </div>
  </section>
</template>
