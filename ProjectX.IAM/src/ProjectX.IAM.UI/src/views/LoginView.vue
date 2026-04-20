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
  <section class="auth-page auth-page-iam">
    <div class="login-showcase">
      <div class="login-showcase-grid">
        <div class="login-intro">
          <p class="eyebrow">ProjectX IAM</p>
          <h1 class="login-headline">Identity control with project-aware access in one place.</h1>
          <p class="login-lead">
            Sign in to administer global roles, project memberships, and token-backed sessions from the console that
            anchors every ProjectX application.
          </p>
        </div>

        <div class="login-stat-grid">
          <article class="login-stat-card">
            <span class="login-stat-label">Authority</span>
            <strong class="login-stat-value">IAM</strong>
            <p class="login-stat-copy">Authentication, refresh, logout, and active identity all resolve here first.</p>
          </article>

          <article class="login-stat-card">
            <span class="login-stat-label">Role Resolution</span>
            <strong class="login-stat-value">Live</strong>
            <p class="login-stat-copy">Global and project-scoped access stay aligned with the current workspace.</p>
          </article>

          <article class="login-stat-card">
            <span class="login-stat-label">Session Reach</span>
            <strong class="login-stat-value">Shared</strong>
            <p class="login-stat-copy">Move into PM with the same authenticated identity instead of signing in twice.</p>
          </article>
        </div>

        <div class="login-rail">
          <article class="login-note-card">
            <p class="login-note-label">Security Flow</p>
            <ol class="login-checklist">
              <li>
                <div class="login-detail-stack">
                  <strong>Authenticate once against IAM</strong>
                  <span>Establish the user session, token pair, and current identity context.</span>
                </div>
              </li>
              <li>
                <div class="login-detail-stack">
                  <strong>Apply global and project access</strong>
                  <span>Permission boundaries stay centralized while still honoring the selected project.</span>
                </div>
              </li>
              <li>
                <div class="login-detail-stack">
                  <strong>Continue into downstream apps</strong>
                  <span>PM consumes the same identity so operations stay consistent across the platform.</span>
                </div>
              </li>
            </ol>
          </article>

          <article class="login-note-card login-note-card-highlight">
            <p class="login-note-label">Seeded Development Account</p>
            <strong>admin</strong>
            <span>SuperAdmin</span>
            <code class="login-mono">ChangeMe123!</code>
          </article>
        </div>
      </div>
    </div>

    <form class="login-panel" @submit.prevent="submit">
      <div class="login-panel-header">
        <p class="eyebrow">Secure Sign In</p>
        <span class="login-status-pill">Access Core</span>
        <h2 class="panel-title">Sign in to the identity workspace</h2>
        <p class="login-panel-copy">
          Use your IAM credentials to manage users, roles, permissions, and project boundaries from the source of
          truth.
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
        {{ auth.state.authenticating ? "Signing in..." : "Enter IAM" }}
      </button>

      <div class="login-panel-footer">
        <article class="login-inline-card">
          <span class="login-inline-label">Environment</span>
          <strong>Development seed enabled</strong>
          <p>Use <code>admin</code> with the seeded password for local setup and smoke testing.</p>
        </article>

        <article class="login-inline-card">
          <span class="login-inline-label">Session</span>
          <strong>Identity persists into PM</strong>
          <p>Authenticate here once, then carry the same current-user context into project workflows.</p>
        </article>
      </div>
    </form>
  </section>
</template>
