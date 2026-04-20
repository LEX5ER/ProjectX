import { createRouter, createWebHistory } from "vue-router";
import { auth } from "../auth/session";

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/login",
      name: "login",
      component: () => import("../views/LoginView.vue"),
      meta: {
        guestOnly: true
      }
    },
    {
      path: "/",
      name: "home",
      component: () => import("../views/HomeView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/users",
      name: "users",
      component: () => import("../views/UsersView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/projects",
      name: "projects",
      component: () => import("../views/ProjectsView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/roles",
      name: "roles",
      component: () => import("../views/RolesView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/audits",
      name: "audits",
      component: () => import("../views/AuditView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/admin",
      redirect: {
        name: "users"
      },
      meta: {
        requiresAuth: true
      }
    }
  ]
});

router.beforeEach(async to => {
  await auth.initialize();

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return {
      name: "login",
      query: {
        redirect: to.fullPath
      }
    };
  }

  if (to.meta.guestOnly && auth.isAuthenticated) {
    return {
      name: "home"
    };
  }

  return true;
});

export default router;
