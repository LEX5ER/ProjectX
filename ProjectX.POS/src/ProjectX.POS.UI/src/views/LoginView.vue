<script setup lang="ts">
import { computed, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { auth } from "../auth/session";

const router = useRouter();
const route = useRoute();

const form = reactive({
  userNameOrEmail: "admin",
  password: "ChangeMe123!"
});

const errorMessage = ref("");

const redirectTarget = computed(() => {
  return typeof route.query.redirect === "string" ? route.query.redirect : "/";
});

async function submit(): Promise<void> {
  errorMessage.value = "";

  try {
    await auth.login(form.userNameOrEmail, form.password);
    await router.replace(redirectTarget.value);
  } catch (error) {
    errorMessage.value = error instanceof Error ? error.message : "Unable to sign in.";
  }
}
</script>

<template>
  <section class="auth-page auth-page-pos">
    <div class="login-showcase">
      <div class="login-showcase-grid">
        <div class="login-intro">
          <p class="eyebrow">ProjectX POS</p>
          <h1 class="login-headline">Open the retail catalog with IAM already handling identity.</h1>
          <p class="login-lead">
            POS delegates authentication to IAM, then shifts straight into pricing, stock posture, and product
            maintenance without adding a second sign-in flow.
          </p>
        </div>

        <div class="login-stat-grid">
          <article class="login-stat-card">
            <span class="login-stat-label">Retail View</span>
            <strong class="login-stat-value">POS</strong>
            <p class="login-stat-copy">Catalog, pricing, and inventory snapshots stay owned by the POS domain.</p>
          </article>

          <article class="login-stat-card">
            <span class="login-stat-label">Auth Source</span>
            <strong class="login-stat-value">IAM</strong>
            <p class="login-stat-copy">Login, refresh, logout, and current-user identity still resolve from IAM.</p>
          </article>

          <article class="login-stat-card">
            <span class="login-stat-label">Access Model</span>
            <strong class="login-stat-value">Scoped</strong>
            <p class="login-stat-copy">Catalog access follows the authenticated user and active IAM project context.</p>
          </article>
        </div>

        <div class="login-rail">
          <article class="login-note-card">
            <p class="login-note-label">What Happens Next</p>
            <ol class="login-checklist">
              <li>
                <div class="login-detail-stack">
                  <strong>Authenticate through IAM</strong>
                  <span>POS asks the shared identity service to issue and refresh the user session.</span>
                </div>
              </li>
              <li>
                <div class="login-detail-stack">
                  <strong>Restore the current project context</strong>
                  <span>The active IAM project determines which POS records this session can read or manage.</span>
                </div>
              </li>
              <li>
                <div class="login-detail-stack">
                  <strong>Work inside the POS catalog</strong>
                  <span>Once signed in, the UI is focused on retail records instead of identity administration.</span>
                </div>
              </li>
            </ol>
          </article>

          <article class="login-note-card login-note-card-highlight">
            <p class="login-note-label">Development IAM Account</p>
            <strong>admin</strong>
            <span>SuperAdmin</span>
            <code class="login-mono">ChangeMe123!</code>
          </article>
        </div>
      </div>
    </div>

    <form class="login-panel" @submit.prevent="submit">
      <div class="login-panel-header">
        <p class="eyebrow">Retail Sign In</p>
        <span class="login-status-pill">IAM Backed</span>
        <h2 class="panel-title">Sign in with your IAM identity</h2>
        <p class="login-panel-copy">
          Enter once, let IAM establish the session, and continue directly into catalog and stock workflows inside POS.
        </p>
      </div>

      <div class="login-field-stack">
        <label class="field">
          <span>Username or email</span>
          <input
            v-model="form.userNameOrEmail"
            autocomplete="username"
            name="userNameOrEmail"
            placeholder="admin"
            required
            type="text"
          />
        </label>

        <label class="field">
          <span>Password</span>
          <input
            v-model="form.password"
            autocomplete="current-password"
            name="password"
            placeholder="ChangeMe123!"
            required
            type="password"
          />
        </label>
      </div>

      <p v-if="errorMessage" class="form-error">{{ errorMessage }}</p>

      <button class="primary-button login-submit" :disabled="auth.state.authenticating" type="submit">
        {{ auth.state.authenticating ? "Signing in..." : "Enter POS" }}
      </button>

      <div class="login-panel-footer">
        <article class="login-inline-card">
          <span class="login-inline-label">Environment</span>
          <strong>Shared development credentials</strong>
          <p>Local POS login uses the same seeded IAM account that powers the platform-wide auth flow.</p>
        </article>

        <article class="login-inline-card">
          <span class="login-inline-label">Continuation</span>
          <strong>No second sign-in step</strong>
          <p>After IAM authenticates the user, POS continues with the current-user session already in place.</p>
        </article>
      </div>
    </form>
  </section>
</template>
