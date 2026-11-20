/* eslint-env node */
import { defineConfig } from "#q-app";
import path from "path";
import { fileURLToPath } from "url";
import { createRequire } from "module";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const require = createRequire(import.meta.url);

// determine the current environment
const envName = process.env.QENV || "dev";

// dynamically require the environment file
const envConfig = require(`./config/env.${envName}.cjs`);

export default defineConfig((ctx) => {
  return {
    eslint: {
      warnings: true,
      errors: true
    },

    boot: [
      "error",
      "axios",
      "interceptors",
      "permissions",
      "sanitize",
      "title",
      "components",
      "i18n",
      "apexcharts"
    ],

    css: [
      "typography.scss",
      "colors.scss",
      "app.scss",
      "auth.scss",
      "page.scss",
      "custom.scss",
      "website.scss",
      "ui-standards.scss"
    ],

    extras: [
      "mdi-v5",
      "fontawesome-v6",
      "roboto-font",
      "material-icons-outlined"
    ],

    build: {
      minify: true,
      sourcemap: false,
      target: {
        browser: ["es2019", "edge88", "firefox78", "chrome87", "safari13.1"],
        node: "node16"
      },

      vueRouterMode: "history",
      // The values of config/env.<QENV>.cjs, read in the app as import.meta.env.<NAME>.
      defineEnv: envConfig,
      // FullCalendar, ApexCharts and two of our own components are written with the Options API.
      vueOptionsAPI: true,
      publicPath: envConfig.BUILD_PUBLIC_PATH,
      ignorePublicFolder: envConfig.IGNORE_PUBLIC_FOLDER,
      distDir: "../publish/spa/" + envConfig.PUBLISH_FOLDER,
      // The publish folder sits beside WEB, not inside it; app-vite 3 refuses that unless told.
      allowOutsideProjectDistDir: true,

      alias: {
        // app-vite 3 only provides "@"; these are the folder aliases the code has always imported from.
        src: path.join(__dirname, "./src"),
        app: __dirname,
        components: path.join(__dirname, "./src/components"),
        layouts: path.join(__dirname, "./src/layouts"),
        pages: path.join(__dirname, "./src/pages"),
        assets: path.join(__dirname, "./src/assets"),
        boot: path.join(__dirname, "./src/boot"),
        stores: path.join(__dirname, "./src/stores"),
        shared: path.join(__dirname, "./src/shared"),
        services: path.join(__dirname, "./src/services"),
        validators: path.join(__dirname, "./src/validators"),
        modules: path.join(__dirname, "./src/modules"),
        dialogs: path.join(__dirname, "./src/dialogs"),
        composables: path.join(__dirname, "./src/composables")
      },

      vitePlugins: [
        ["@intlify/unplugin-vue-i18n/vite", {
          include: path.resolve(__dirname, "./src/i18n/**")
        }]
      ],
      extendViteConf(viteConf) {
        viteConf.build = {
          ...viteConf.build,
          chunkSizeWarningLimit: 750,
          rollupOptions: {
            output: {
              manualChunks(id) {
                if (!id.includes('node_modules')) return;

                if (id.includes('vue') || id.includes('vue-router') || id.includes('pinia')) {
                  return 'vendor-vue';
                }

                if (id.includes('quasar')) {
                  return 'vendor-quasar';
                }

                if (
                  id.includes('@fullcalendar/core') ||
                  id.includes('@fullcalendar/vue3') ||
                  id.includes('@fullcalendar/daygrid') ||
                  id.includes('@fullcalendar/timegrid')
                ) {
                  return 'vendor-fullcalendar';
                }

                if (
                  id.includes('d3') ||
                  id.includes('d3-org-chart') ||
                  id.includes('d3-flextree')
                ) {
                  return 'vendor-d3';
                }

                // fallback
                return 'vendor';
              }
            }
          }
        };
      },
    },
    devServer: {
      open: true
    },

    framework: {
      config: {},
      iconSet: "material-icons-outlined",
      plugins: [
        "LocalStorage",
        "Notify",
        "Loading",
        "Dialog",
        "Meta",
        "Cookies",
        "BottomSheet"
      ]
    },

    animations: [],

    ssr: {
      pwa: false,
      prodPort: 3000,
      middlewares: ["render"]
    },

    pwa: {
      workboxMode: "generateSW",
      injectPwaMetaTags: true,
      swFilename: "sw.js",
      manifestFilename: "manifest.json",
      useCredentialsForManifestTag: false
    },

    cordova: {},

    capacitor: {
      hideSplashscreen: true
    },

    electron: {
      inspectPort: 5858,
      bundler: "packager",
      packager: {},
      builder: {
        appId: "Meldep"
      }
    },

    bex: {
      contentScripts: ["my-content-script"]
    }
  };
});
