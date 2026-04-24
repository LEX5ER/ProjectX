<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { auth } from "../auth/session";
import { canManageProjectData } from "../auth/access";
import { listCatalog, type ProductRecord } from "../products/client";
import { listCustomers, type CustomerRecord } from "../customers/client";
import { checkoutSale, type PaymentMethod, type SaleDetailRecord } from "../sales/client";

interface CartLine {
  productId: string;
  code: string;
  name: string;
  category: string;
  unitPrice: number;
  availableStock: number;
  quantity: number;
  discountAmount: string;
}

const catalog = ref<ProductRecord[]>([]);
const customers = ref<CustomerRecord[]>([]);
const cartLines = ref<CartLine[]>([]);
const catalogSearch = ref("");
const selectedCustomerId = ref("");
const note = ref("");
const receiptEmail = ref("");
const cartDiscount = ref("0");
const taxRate = ref("12");
const paymentMethod = ref<PaymentMethod>("Cash");
const amountReceived = ref("");
const loading = ref(false);
const busy = ref(false);
const errorMessage = ref("");
const successMessage = ref("");
const latestSale = ref<SaleDetailRecord | null>(null);

const currentUser = computed(() => auth.state.user);
const activeProjectId = computed(() => auth.state.user?.activeProjectId ?? "");
const activeProjectName = computed(() => auth.getActiveProject()?.name ?? "the active IAM project");
const canManage = computed(() => canManageProjectData(currentUser.value));
const selectedCustomer = computed(() => customers.value.find(customer => customer.id === selectedCustomerId.value) ?? null);
const filteredCatalog = computed(() => {
  const normalizedSearch = catalogSearch.value.trim().toLowerCase();

  if (!normalizedSearch) {
    return catalog.value;
  }

  return catalog.value.filter(product =>
    product.code.toLowerCase().includes(normalizedSearch)
    || product.name.toLowerCase().includes(normalizedSearch)
    || product.category.toLowerCase().includes(normalizedSearch)
    || product.description.toLowerCase().includes(normalizedSearch));
});
const subtotalAmount = computed(() => cartLines.value.reduce((total, line) => total + line.unitPrice * line.quantity, 0));
const lineDiscountAmount = computed(() => cartLines.value.reduce((total, line) => total + normalizeMoney(line.discountAmount), 0));
const cartDiscountAmount = computed(() => normalizeMoney(cartDiscount.value));
const taxableBaseAmount = computed(() => Math.max(0, subtotalAmount.value - lineDiscountAmount.value - cartDiscountAmount.value));
const requestedTaxRatePercentage = computed(() => normalizeMoney(taxRate.value));
const effectiveTaxRatePercentage = computed(() => selectedCustomer.value?.taxExempt ? 0 : requestedTaxRatePercentage.value);
const taxAmount = computed(() => roundCurrency(taxableBaseAmount.value * (effectiveTaxRatePercentage.value / 100)));
const totalAmount = computed(() => roundCurrency(taxableBaseAmount.value + taxAmount.value));
const cashReceivedAmount = computed(() => normalizeMoney(amountReceived.value));
const hasSufficientCash = computed(() => paymentMethod.value !== "Cash" || cashReceivedAmount.value >= totalAmount.value);
const canCheckout = computed(() =>
  canManage.value
  && Boolean(activeProjectId.value)
  && cartLines.value.length > 0
  && !busy.value
  && hasSufficientCash.value);

function clearMessages(): void {
  errorMessage.value = "";
  successMessage.value = "";
}

function normalizeMoney(value: string): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
}

function roundCurrency(value: number): number {
  return Math.round(value * 100) / 100;
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

function formatPaymentMethod(value: PaymentMethod): string {
  return value.replace(/([A-Z])/g, " $1").trim();
}

async function loadData(): Promise<void> {
  if (!canManage.value || !activeProjectId.value) {
    catalog.value = [];
    customers.value = [];
    return;
  }

  loading.value = true;
  errorMessage.value = "";

  try {
    const [catalogItems, customerItems] = await Promise.all([
      listCatalog(),
      listCustomers()
    ]);

    catalog.value = catalogItems;
    customers.value = customerItems;
    syncCartWithCatalog();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to load checkout data.";
  } finally {
    loading.value = false;
  }
}

function syncCartWithCatalog(): void {
  cartLines.value = cartLines.value
    .map(line => {
      const product = catalog.value.find(entry => entry.id === line.productId);

      if (!product) {
        return null;
      }

      return {
        ...line,
        unitPrice: product.unitPrice,
        availableStock: product.stockQuantity,
        quantity: Math.min(line.quantity, product.stockQuantity)
      };
    })
    .filter((line): line is CartLine => Boolean(line && line.quantity > 0));
}

function resetCheckoutState(): void {
  cartLines.value = [];
  catalogSearch.value = "";
  selectedCustomerId.value = "";
  note.value = "";
  receiptEmail.value = "";
  cartDiscount.value = "0";
  taxRate.value = "12";
  paymentMethod.value = "Cash";
  amountReceived.value = "";
}

function addToCart(product: ProductRecord): void {
  clearMessages();

  if (!activeProjectId.value) {
    errorMessage.value = "Select the POS IAM project before starting checkout.";
    return;
  }

  const existingLine = cartLines.value.find(line => line.productId === product.id);

  if (existingLine) {
    if (existingLine.quantity >= existingLine.availableStock) {
      errorMessage.value = `Only ${existingLine.availableStock} unit(s) of ${product.name} are available.`;
      return;
    }

    existingLine.quantity += 1;
    return;
  }

  if (product.stockQuantity <= 0) {
    errorMessage.value = `${product.name} is currently out of stock.`;
    return;
  }

  cartLines.value.push({
    productId: product.id,
    code: product.code,
    name: product.name,
    category: product.category,
    unitPrice: product.unitPrice,
    availableStock: product.stockQuantity,
    quantity: 1,
    discountAmount: "0"
  });
}

function incrementLineQuantity(line: CartLine): void {
  clearMessages();

  if (line.quantity >= line.availableStock) {
    errorMessage.value = `Only ${line.availableStock} unit(s) of ${line.name} are available.`;
    return;
  }

  line.quantity += 1;
}

function decrementLineQuantity(line: CartLine): void {
  if (line.quantity <= 1) {
    removeLine(line.productId);
    return;
  }

  line.quantity -= 1;
}

function removeLine(productId: string): void {
  cartLines.value = cartLines.value.filter(line => line.productId !== productId);
}

function clearReceipt(): void {
  latestSale.value = null;
}

async function submitCheckout(): Promise<void> {
  clearMessages();

  if (!canCheckout.value) {
    errorMessage.value = `This session does not have permission to complete sales for ${activeProjectName.value}.`;
    return;
  }

  if (cartLines.value.length === 0) {
    errorMessage.value = "Add at least one product to the cart.";
    return;
  }

  busy.value = true;

  try {
    const sale = await checkoutSale({
      customerId: selectedCustomerId.value || null,
      cartDiscountAmount: cartDiscountAmount.value,
      taxRatePercentage: requestedTaxRatePercentage.value,
      paymentMethod: paymentMethod.value,
      amountReceived: paymentMethod.value === "Cash" ? cashReceivedAmount.value : totalAmount.value,
      note: note.value,
      receiptEmail: receiptEmail.value,
      lines: cartLines.value.map(line => ({
        productId: line.productId,
        quantity: line.quantity,
        discountAmount: normalizeMoney(line.discountAmount)
      }))
    });

    latestSale.value = sale;
    successMessage.value = `Receipt ${sale.receiptNumber} completed successfully.`;
    resetCheckoutState();
    await loadData();
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to complete checkout.";
  } finally {
    busy.value = false;
  }
}

watch(selectedCustomerId, () => {
  if (selectedCustomer.value?.email && !receiptEmail.value.trim()) {
    receiptEmail.value = selectedCustomer.value.email;
  }
});

watch(activeProjectId, async () => {
  clearMessages();
  resetCheckoutState();
  clearReceipt();
  await loadData();
}, { immediate: true });
</script>

<template>
  <section class="admin-shell">
    <div class="hero-panel">
      <p class="eyebrow">Checkout</p>
      <h1 class="hero-title">Sell directly from the active IAM project.</h1>
      <p class="hero-text">
        Search the live catalog, build the cart, attach a customer, apply retail discounts and tax, then issue a
        receipt that deducts stock in the POS domain for {{ activeProjectName }}.
      </p>
    </div>

    <p v-if="errorMessage" class="form-error management-banner">{{ errorMessage }}</p>
    <p v-else-if="successMessage" class="success-banner management-banner">{{ successMessage }}</p>

    <div v-if="!canManage || !activeProjectId" class="empty-state">
      Select the POS IAM project and use an IAM session with write access before completing sales.
    </div>

    <div v-else class="checkout-layout">
      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Catalog</p>
            <h2>Available products</h2>
          </div>
          <span class="stat-chip">{{ catalog.length }} items</span>
        </div>

        <label class="field">
          <span>Search catalog</span>
          <input v-model="catalogSearch" placeholder="Search code, product, or category" type="text" />
        </label>

        <div v-if="loading" class="empty-state">Loading catalog...</div>
        <div v-else-if="filteredCatalog.length === 0" class="empty-state">No saleable products match this search.</div>
        <div v-else class="catalog-grid">
          <article v-for="product in filteredCatalog" :key="product.id" class="catalog-card">
            <div class="record-body">
              <strong>{{ product.name }}</strong>
              <p>{{ product.description }}</p>
              <div class="chip-list">
                <span class="stat-chip">{{ product.code }}</span>
                <span class="stat-chip">{{ product.category }}</span>
                <span class="stat-chip">{{ product.status }}</span>
              </div>
              <small>{{ product.stockQuantity }} in stock</small>
              <small>{{ formatCurrency(product.unitPrice) }}</small>
            </div>
            <button class="secondary-button" type="button" @click="addToCart(product)">
              Add to cart
            </button>
          </article>
        </div>
      </article>

      <article class="surface-card">
        <div class="management-header">
          <div>
            <p class="card-label">Cart</p>
            <h2>Current sale</h2>
          </div>
          <button class="ghost-button" type="button" @click="resetCheckoutState">Clear cart</button>
        </div>

        <div v-if="cartLines.length === 0" class="empty-state">Add products from the catalog to begin checkout.</div>
        <div v-else class="cart-list">
          <article v-for="line in cartLines" :key="line.productId" class="cart-line">
            <div class="record-body">
              <strong>{{ line.name }}</strong>
              <small>{{ line.code }} | {{ line.category }}</small>
              <small>{{ formatCurrency(line.unitPrice) }} each | {{ line.availableStock }} available</small>
            </div>
            <div class="cart-line-controls">
              <div class="quantity-stepper">
                <button class="ghost-button" type="button" @click="decrementLineQuantity(line)">-</button>
                <span class="stat-chip">{{ line.quantity }}</span>
                <button class="ghost-button" type="button" @click="incrementLineQuantity(line)">+</button>
              </div>

              <label class="field inline-field">
                <span>Line discount</span>
                <input v-model="line.discountAmount" inputmode="decimal" min="0" step="0.01" type="number" />
              </label>

              <button class="ghost-button" type="button" @click="removeLine(line.productId)">Remove</button>
            </div>
          </article>
        </div>

        <div class="stack-form">
          <label class="field">
            <span>Customer</span>
            <select v-model="selectedCustomerId">
              <option value="">Walk-in customer</option>
              <option v-for="customer in customers" :key="customer.id" :value="customer.id">
                {{ customer.fullName }}{{ customer.email ? ` | ${customer.email}` : "" }}
              </option>
            </select>
          </label>

          <p v-if="selectedCustomer?.taxExempt" class="empty-state slim-empty-state">
            {{ selectedCustomer.fullName }} is marked tax-exempt. VAT is removed from this sale automatically.
          </p>

          <div class="field-grid">
            <label class="field">
              <span>Receipt email</span>
              <input v-model="receiptEmail" placeholder="customer@example.com" type="email" />
            </label>

            <label class="field">
              <span>Payment method</span>
              <select v-model="paymentMethod">
                <option value="Cash">Cash</option>
                <option value="Card">Card</option>
                <option value="EWallet">E-Wallet</option>
                <option value="BankTransfer">Bank transfer</option>
                <option value="Other">Other</option>
              </select>
            </label>
          </div>

          <div class="field-grid">
            <label class="field">
              <span>Cart discount</span>
              <input v-model="cartDiscount" inputmode="decimal" min="0" step="0.01" type="number" />
            </label>

            <label class="field">
              <span>Tax rate %</span>
              <input v-model="taxRate" inputmode="decimal" min="0" step="0.01" type="number" />
            </label>
          </div>

          <label v-if="paymentMethod === 'Cash'" class="field">
            <span>Cash received</span>
            <input v-model="amountReceived" inputmode="decimal" min="0" step="0.01" type="number" />
          </label>

          <p v-if="paymentMethod === 'Cash' && !hasSufficientCash && cartLines.length > 0" class="form-error">
            Cash received must be at least {{ formatCurrency(totalAmount) }} to complete this sale.
          </p>

          <label class="field">
            <span>Sale note</span>
            <textarea v-model="note" maxlength="500" rows="3" placeholder="Optional note for staff or receipt context." />
          </label>
        </div>

        <div class="cart-summary">
          <div class="summary-row">
            <span>Subtotal</span>
            <strong>{{ formatCurrency(subtotalAmount) }}</strong>
          </div>
          <div class="summary-row">
            <span>Line discounts</span>
            <strong>{{ formatCurrency(lineDiscountAmount) }}</strong>
          </div>
          <div class="summary-row">
            <span>Cart discount</span>
            <strong>{{ formatCurrency(cartDiscountAmount) }}</strong>
          </div>
          <div class="summary-row">
            <span>Tax</span>
            <strong>{{ formatCurrency(taxAmount) }}</strong>
          </div>
          <div v-if="selectedCustomer?.taxExempt" class="summary-row">
            <span>Applied tax rate</span>
            <strong>0%</strong>
          </div>
          <div class="summary-row summary-row-total">
            <span>Total</span>
            <strong>{{ formatCurrency(totalAmount) }}</strong>
          </div>
        </div>

        <button class="primary-button" :disabled="!canCheckout" type="button" @click="submitCheckout">
          {{ busy ? "Completing sale..." : "Complete sale" }}
        </button>
      </article>
    </div>

    <article v-if="latestSale" class="surface-card">
      <div class="management-header">
        <div>
          <p class="card-label">Latest Receipt</p>
          <h2>{{ latestSale.receiptNumber }}</h2>
        </div>
        <button class="ghost-button" type="button" @click="clearReceipt">Dismiss</button>
      </div>

      <div class="receipt-grid">
        <div class="record-body">
          <strong>{{ latestSale.customerName || "Walk-in customer" }}</strong>
          <small>{{ formatTimestamp(latestSale.createdAtUtc) }}</small>
          <small>{{ formatPaymentMethod(latestSale.paymentMethod) }}</small>
          <small v-if="latestSale.receiptEmail">{{ latestSale.receiptEmail }}</small>
        </div>

        <div class="cart-summary">
          <div class="summary-row">
            <span>Total</span>
            <strong>{{ formatCurrency(latestSale.totalAmount) }}</strong>
          </div>
          <div class="summary-row">
            <span>Paid</span>
            <strong>{{ formatCurrency(latestSale.paidAmount) }}</strong>
          </div>
          <div class="summary-row">
            <span>Change</span>
            <strong>{{ formatCurrency(latestSale.changeAmount) }}</strong>
          </div>
        </div>
      </div>

      <div class="record-list">
        <article v-for="line in latestSale.lineItems" :key="line.id" class="record-card">
          <div class="record-body">
            <strong>{{ line.productName }}</strong>
            <small>{{ line.productCode }} | {{ line.quantity }} x {{ formatCurrency(line.unitPrice) }}</small>
            <small>Discount {{ formatCurrency(line.discountAmount) }} | Line total {{ formatCurrency(line.lineTotalAmount) }}</small>
          </div>
        </article>
      </div>
    </article>
  </section>
</template>
