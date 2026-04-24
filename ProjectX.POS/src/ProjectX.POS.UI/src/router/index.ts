import { createRouter, createWebHistory } from "vue-router";
import { auth } from "../auth/session";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
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
      path: "/checkout",
      name: "checkout",
      component: () => import("../views/CheckoutView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/sales",
      name: "sales",
      component: () => import("../views/SalesView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/customers",
      name: "customers",
      component: () => import("../views/CustomersView.vue"),
      meta: {
        requiresAuth: true
      }
    },
    {
      path: "/products",
      name: "products",
      component: () => import("../views/ProductsView.vue"),
      meta: {
        requiresAuth: true
      }
    }
  ]
});

router.beforeEach(async to => {
  await auth.initialize();

  if (auth.isAuthenticated) {
    await auth.ensureProjectSelection();
  }

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
