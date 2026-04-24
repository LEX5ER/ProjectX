import { defineConfig, loadEnv } from "vite";
import vue from "@vitejs/plugin-vue";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const posProxyTarget = env.VITE_PROXY_POS_TARGET || "https://localhost:7014";
  const iamProxyTarget = env.VITE_PROXY_IAM_TARGET || "https://localhost:7141";

  return {
    plugins: [vue()],
    server: {
      host: "localhost",
      port: 5175,
      strictPort: true,
      proxy: {
        "/api": {
          target: posProxyTarget,
          changeOrigin: true,
          secure: false
        },
        "/iam-api": {
          target: iamProxyTarget,
          changeOrigin: true,
          secure: false,
          rewrite: path => path.replace(/^\/iam-api/, "")
        }
      }
    }
  };
});
