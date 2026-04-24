<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { auth } from "../auth/session";
import { canReadProjectData } from "../auth/access";
import { getDashboardSummary, type DashboardSummaryRecord } from "../sales/client";

const summary = ref<DashboardSummaryRecord | null>(null);
const loading = ref(false);
const errorMessage = ref("");
const currentUser = computed(() => auth.state.user);
const activeProjectId = computed(() => auth.state.user?.activeProjectId ?? "");
const activeProjectName = computed(() => auth.getActiveProject()?.name ?? "the active IAM project");
const canRead = computed(() => canReadProjectData(currentUser.value));

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-PH", {
    style: "currency",
    currency: "PHP"
  }).format(value);
}

function formatTimestamp(value: string): string {
  return new Date(value).toLocaleString();
}

function formatPaymentMethod(value: string): string {
  return value.replace(/([A-Z])/g, " $1").trim();
}

async function loadData(): Promise<void> {
  if (!canRead.value) {
    summary.value = null;
    return;
  }

  loading.value = true;
  errorMessage.value = "";

  try {
    summary.value = await getDashboardSummary();
  } catch (error) {
    summary.value = null;
    errorMessage.value = error instanceof Error ? error.message : "Unable to load dashboard data.";
  } finally {
    loading.value = false;
  }
}

watch(activeProjectId, async () => {
  errorMessage.value = "";
  await loadData();
}, { immediate: true });
</script>

<template>
  <section class="dashboard-page">
    <div class="hero-panel">
      <p class="eyebrow">ProjectX.POS</p>
      <h1 class="hero-title">Run the counter from one screen.</h1>
      <p class="hero-text">
        IAM still handles identity and project scope. POS now owns customers, live checkout, sales receipts, refunds,
        and catalog-backed inventory movement for {{ activeProjectName }}.
      </p>
      <p class="hero-text">
        Signed in as {{ currentUser?.userName }}.
      </p>
      <div class="hero-actions">
        <RouterLink class="primary-button hero-action-link" to="/checkout">
          Start checkout
        </RouterLink>
        <RouterLink class="secondary-button hero-action-link" to="/sales">
          Review sales
        </RouterLink>
        <RouterLink class="ghost-button hero-action-link" to="/customers">
          Open customers
        </RouterLink>
      </div>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>

    <div v-if="!canRead" class="empty-state">
      Select the POS IAM project in IAM before loading the operational dashboard.
    </div>

    <div v-else-if="summary" class="dashboard-grid">
      <article class="surface-card metric-card">
        <p class="card-label">Today</p>
        <strong>{{ loading ? "..." : formatCurrency(summary.todayNetSales) }}</strong>
        <span>Net sales after refunds</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Transactions</p>
        <strong>{{ loading ? "..." : summary.todaySalesCount }}</strong>
        <span>Sales created today</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Customers</p>
        <strong>{{ loading ? "..." : summary.totalCustomers }}</strong>
        <span>Profiles in scope</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Inventory</p>
        <strong>{{ loading ? "..." : formatCurrency(summary.inventoryValue) }}</strong>
        <span>Tracked inventory value</span>
      </article>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Low Stock</p>
            <h2>Items needing attention</h2>
          </div>
          <span class="stat-chip">{{ summary.lowStockProducts }} alerts</span>
        </div>

        <div v-if="loading" class="empty-state">Loading stock alerts...</div>
        <div v-else-if="summary.lowStockItems.length === 0" class="empty-state">No low-stock products in this scope.</div>
        <div v-else class="record-list">
          <article v-for="product in summary.lowStockItems" :key="product.id" class="record-card">
            <div class="record-body">
              <strong>{{ product.name }}</strong>
              <div class="chip-list">
                <span class="stat-chip">{{ product.code }}</span>
                <span class="stat-chip">{{ product.status }}</span>
              </div>
              <small>{{ product.stockQuantity }} units remaining</small>
            </div>
          </article>
        </div>
      </article>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Recent Sales</p>
            <h2>Latest receipts</h2>
          </div>
        </div>

        <div v-if="loading" class="empty-state">Loading sales activity...</div>
        <div v-else-if="summary.recentSales.length === 0" class="empty-state">No receipts have been issued yet.</div>
        <div v-else class="record-list">
          <article v-for="sale in summary.recentSales" :key="sale.id" class="record-card">
            <div class="record-body">
              <strong>{{ sale.receiptNumber }}</strong>
              <p>{{ sale.customerName || "Walk-in customer" }}</p>
              <div class="chip-list">
                <span class="stat-chip">{{ sale.status }}</span>
                <span class="stat-chip">{{ formatPaymentMethod(sale.paymentMethod) }}</span>
              </div>
              <small>{{ sale.totalItems }} items | {{ formatCurrency(sale.totalAmount) }}</small>
              <small>{{ formatTimestamp(sale.createdAtUtc) }}</small>
            </div>
          </article>
        </div>
      </article>
    </div>
  </section>
</template>
