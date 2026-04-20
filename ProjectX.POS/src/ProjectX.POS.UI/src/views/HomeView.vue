<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { auth } from "../auth/session";
import { listProducts, type ProductRecord } from "../products/client";

const products = ref<ProductRecord[]>([]);
const loading = ref(false);
const errorMessage = ref("");

const currentUser = computed(() => auth.state.user);
const totalProducts = computed(() => products.value.length);
const inStockCount = computed(() => products.value.filter(product => product.stockQuantity > 0).length);
const lowStockCount = computed(() => products.value.filter(product => product.stockQuantity > 0 && product.stockQuantity <= 15).length);
const recentProducts = computed(() => products.value.slice(0, 4));

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-PH", {
    style: "currency",
    currency: "PHP"
  }).format(value);
}

async function loadData(): Promise<void> {
  loading.value = true;
  errorMessage.value = "";

  try {
    products.value = await listProducts();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load products.";
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
      <p class="eyebrow">ProjectX.POS</p>
      <h1 class="hero-title">Own the retail catalog here, not in IAM.</h1>
      <p class="hero-text">
        POS keeps product definitions, pricing, and stock posture inside the retail domain while IAM continues to own
        identity and project selection.
      </p>
      <p class="hero-text">
        Signed in as {{ currentUser?.userName }} through IAM.
      </p>
      <RouterLink class="primary-button hero-action-link" to="/products">
        Open product workspace
      </RouterLink>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>

    <div class="dashboard-grid">
      <article class="surface-card metric-card">
        <p class="card-label">Catalog</p>
        <strong>{{ loading ? "..." : totalProducts }}</strong>
        <span>Total products</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Available</p>
        <strong>{{ loading ? "..." : inStockCount }}</strong>
        <span>Products in stock</span>
      </article>

      <article class="surface-card metric-card">
        <p class="card-label">Attention</p>
        <strong>{{ loading ? "..." : lowStockCount }}</strong>
        <span>Low-stock items</span>
      </article>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Recent updates</p>
            <h2>Latest product records</h2>
          </div>
        </div>

        <div v-if="loading" class="empty-state">Loading product activity...</div>
        <div v-else-if="recentProducts.length === 0" class="empty-state">No products are visible yet.</div>
        <div v-else class="record-list">
          <article v-for="product in recentProducts" :key="product.id" class="record-card">
            <div class="record-body">
              <strong>{{ product.name }}</strong>
              <p>{{ product.description }}</p>
              <small>{{ product.code }} | {{ product.category }} | {{ product.status }}</small>
              <small>{{ product.stockQuantity }} units | {{ formatCurrency(product.unitPrice) }}</small>
              <code>{{ product.id }}</code>
            </div>
          </article>
        </div>
      </article>
    </div>
  </section>
</template>
