<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { auth } from "../auth/session";
import {
  createProduct,
  deleteProduct,
  listProductsPage,
  type ProductRecord,
  type ProductStatus,
  updateProduct
} from "../products/client";

const products = ref<ProductRecord[]>([]);
const currentPage = ref(1);
const pageSize = 10;
const totalCount = ref(0);
const totalPages = ref(0);
const loading = ref(false);
const busyAction = ref("");
const errorMessage = ref("");
const successMessage = ref("");

const productForm = reactive({
  id: "",
  code: "",
  name: "",
  description: "",
  category: "",
  unitPrice: "",
  stockQuantity: "",
  status: "Draft" as ProductStatus
});

const editingProduct = computed(() => Boolean(productForm.id));
const currentUser = computed(() => auth.state.user);
const activeProject = computed(() => auth.getActiveProject());
const activeProjectId = computed(() => activeProject.value?.id ?? "");
const activeProjectSummary = computed(() => activeProject.value?.name ?? "the active IAM project");
const globalPermissions = computed(() => new Set(currentUser.value?.globalPermissions ?? []));
const activeProjectPermissions = computed(() => new Set(currentUser.value?.activeProjectPermissions ?? []));
const hasGlobalWrite = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess || globalPermissions.value.has("projects.write")
));
const canReadProducts = computed(() => Boolean(
  currentUser.value?.hasGlobalFullAccess
  || globalPermissions.value.has("projects.read")
  || globalPermissions.value.has("projects.write")
  || currentUser.value?.hasAllPermissions
  || activeProjectPermissions.value.has("projects.read")
  || activeProjectPermissions.value.has("projects.write")
));
const canManageProducts = computed(() => Boolean(
  hasGlobalWrite.value
  || currentUser.value?.hasAllPermissions
  || activeProjectPermissions.value.has("projects.write")
));
const statusOptions: ProductStatus[] = ["Draft", "Active", "LowStock", "OutOfStock", "Archived"];
const selectedProduct = computed(() => products.value.find(product => product.id === productForm.id) ?? null);
const canSubmitProduct = computed(() => {
  if (!canManageProducts.value) {
    return false;
  }

  if (editingProduct.value) {
    return Boolean(selectedProduct.value && canEditProduct(selectedProduct.value));
  }

  return Boolean(activeProjectId.value);
});

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function clearForm(): void {
  productForm.id = "";
  productForm.code = "";
  productForm.name = "";
  productForm.description = "";
  productForm.category = "";
  productForm.unitPrice = "";
  productForm.stockQuantity = "";
  productForm.status = "Draft";
}

function canEditProduct(product: ProductRecord): boolean {
  return canManageProducts.value
    && (hasGlobalWrite.value || product.projectId === activeProjectId.value);
}

function editProduct(product: ProductRecord): void {
  clearMessages();

  if (!canEditProduct(product)) {
    errorMessage.value = `This session can only manage products linked to ${activeProjectSummary.value}.`;
    return;
  }

  productForm.id = product.id;
  productForm.code = product.code;
  productForm.name = product.name;
  productForm.description = product.description;
  productForm.category = product.category;
  productForm.unitPrice = product.unitPrice.toString();
  productForm.stockQuantity = product.stockQuantity.toString();
  productForm.status = product.status;
}

async function loadData(): Promise<void> {
  loading.value = true;
  clearMessages();

  try {
    if (!canReadProducts.value) {
      products.value = [];
      totalCount.value = 0;
      totalPages.value = 0;
      return;
    }

    const result = await listProductsPage(currentPage.value, pageSize);
    products.value = result.items;
    totalCount.value = result.totalCount;
    totalPages.value = result.totalPages;
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load products.";
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

async function submitProduct(): Promise<void> {
  clearMessages();

  if (!activeProjectId.value && !editingProduct.value) {
    errorMessage.value = "Select the POS IAM project before creating products.";
    return;
  }

  if (!canSubmitProduct.value) {
    errorMessage.value = editingProduct.value
      ? `This session can only manage products linked to ${activeProjectSummary.value}.`
      : `This session does not have permission to create products for ${activeProjectSummary.value}.`;
    return;
  }

  const unitPrice = Number(productForm.unitPrice);
  const stockQuantity = Number(productForm.stockQuantity);

  if (Number.isNaN(unitPrice) || Number.isNaN(stockQuantity)) {
    errorMessage.value = "Unit price and stock quantity must be valid numbers.";
    return;
  }

  busyAction.value = "product";

  try {
    const payload = {
      code: productForm.code,
      name: productForm.name,
      description: productForm.description,
      category: productForm.category,
      unitPrice,
      stockQuantity,
      status: productForm.status
    };

    if (editingProduct.value) {
      await updateProduct(productForm.id, payload);
      successMessage.value = "Product updated.";
    } else {
      await createProduct(payload);
      successMessage.value = "Product created.";
    }

    clearForm();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to save product.";
  } finally {
    busyAction.value = "";
  }
}

async function removeProduct(product: ProductRecord): Promise<void> {
  clearMessages();

  if (!canEditProduct(product)) {
    errorMessage.value = `This session can only manage products linked to ${activeProjectSummary.value}.`;
    return;
  }

  if (!window.confirm(`Delete product "${product.name}"?`)) {
    return;
  }

  busyAction.value = `delete-${product.id}`;

  try {
    await deleteProduct(product.id);
    successMessage.value = "Product deleted.";

    if (productForm.id === product.id) {
      clearForm();
    }

    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to delete product.";
  } finally {
    busyAction.value = "";
  }
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-PH", {
    style: "currency",
    currency: "PHP"
  }).format(value);
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
      <p class="eyebrow">Products</p>
      <h1 class="hero-title">Manage the POS product catalog</h1>
      <p class="hero-text">
        Products created here stay tied to the active IAM project context. That keeps catalog edits aligned with the
        same scope IAM already uses for the session.
      </p>
      <p class="hero-text">
        Global admins can manage across projects. Scoped users can manage only the products attached to the active IAM
        project.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div v-if="!canReadProducts" class="empty-state">
      Select the POS IAM project in IAM before managing product records.
    </div>

    <div v-else class="management-split">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Product Form</p>
            <h2>{{ editingProduct ? "Edit product" : "Create product" }}</h2>
          </div>
          <button class="ghost-button" type="button" @click="clearForm">Reset</button>
        </div>

        <form class="stack-form" @submit.prevent="submitProduct">
          <label class="field">
            <span>Code</span>
            <input
              v-model="productForm.code"
              :disabled="busyAction === 'product'"
              maxlength="50"
              placeholder="BEV-COLA-330"
              required
              type="text"
            />
          </label>

          <label class="field">
            <span>Name</span>
            <input
              v-model="productForm.name"
              :disabled="busyAction === 'product'"
              maxlength="150"
              placeholder="Cola 330ml"
              required
              type="text"
            />
          </label>

          <label class="field">
            <span>Description</span>
            <textarea
              v-model="productForm.description"
              :disabled="busyAction === 'product'"
              maxlength="500"
              placeholder="Fast-moving beverage item for checkout lanes."
              required
              rows="4"
            />
          </label>

          <label class="field">
            <span>Category</span>
            <input
              v-model="productForm.category"
              :disabled="busyAction === 'product'"
              maxlength="100"
              placeholder="Beverages"
              required
              type="text"
            />
          </label>

          <div class="field-grid">
            <label class="field">
              <span>Unit price</span>
              <input
                v-model="productForm.unitPrice"
                :disabled="busyAction === 'product'"
                inputmode="decimal"
                min="0"
                placeholder="35.00"
                required
                step="0.01"
                type="number"
              />
            </label>

            <label class="field">
              <span>Stock quantity</span>
              <input
                v-model="productForm.stockQuantity"
                :disabled="busyAction === 'product'"
                inputmode="numeric"
                min="0"
                placeholder="84"
                required
                step="1"
                type="number"
              />
            </label>
          </div>

          <label class="field">
            <span>Status</span>
            <select v-model="productForm.status" :disabled="busyAction === 'product'">
              <option v-for="status in statusOptions" :key="status" :value="status">
                {{ status }}
              </option>
            </select>
          </label>

          <button class="primary-button" :disabled="busyAction === 'product' || !canSubmitProduct" type="submit">
            {{ busyAction === "product" ? "Saving..." : editingProduct ? "Update product" : "Create product" }}
          </button>
        </form>

        <p v-if="!editingProduct && !activeProjectId" class="empty-state slim-empty-state">
          Select the POS IAM project before adding products so the record is scoped correctly.
        </p>
      </article>

      <article class="surface-card users-card">
        <div class="management-header">
          <div>
            <p class="card-label">Catalog</p>
            <h2>Accessible products</h2>
          </div>
          <span class="stat-chip">{{ totalCount }} total</span>
        </div>

        <div v-if="loading" class="empty-state">Loading products...</div>
        <div v-else-if="products.length === 0" class="empty-state">No products are visible for this session yet.</div>
        <div v-else class="record-list">
          <article v-for="product in products" :key="product.id" class="record-card">
            <div class="record-body">
              <strong>{{ product.name }}</strong>
              <p>{{ product.description }}</p>
              <div class="chip-list">
                <span class="stat-chip">{{ product.code }}</span>
                <span class="stat-chip">{{ product.category }}</span>
                <span class="stat-chip">{{ product.status }}</span>
              </div>
              <small>Price: {{ formatCurrency(product.unitPrice) }}</small>
              <small>Stock: {{ product.stockQuantity }} units</small>
              <small>Updated: {{ formatTimestamp(product.updatedAtUtc) }}</small>
              <code>{{ product.id }}</code>
            </div>
            <div class="record-actions">
              <button class="secondary-button" :disabled="!canEditProduct(product)" type="button" @click="editProduct(product)">
                {{ canEditProduct(product) ? "Edit" : "Read only" }}
              </button>
              <button
                class="ghost-button"
                :disabled="busyAction === `delete-${product.id}` || !canEditProduct(product)"
                type="button"
                @click="removeProduct(product)"
              >
                {{ busyAction === `delete-${product.id}` ? "Deleting..." : canEditProduct(product) ? "Delete" : "Locked" }}
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
