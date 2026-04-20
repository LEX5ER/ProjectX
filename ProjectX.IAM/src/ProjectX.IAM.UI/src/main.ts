import { createApp } from "vue";
import App from "./App.vue";
import { auth } from "./auth/session";
import router from "./router";
import "./styles/main.css";

async function bootstrap(): Promise<void> {
  await auth.initialize();
  createApp(App).use(router).mount("#app");
}

void bootstrap();
