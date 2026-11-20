export default [
  {
    path: "/requirement-v2",
    component: () => import("layouts/layout.vue"),
    children: [
      // The requirement pages (v2), side by side with the current ones under /requirement. Nothing in
      // the current screens links to the list: it is opened by its address, /requirement-v2. Records,
      // creation and editing are pages of their own (no side panels). They use the permission of the
      // Requirement menu, as the API does: View opens the list and a record, Manage opens create and edit.
      { path: "", name: "requirement-v2", component: () => import("modules/requirement-v2/pages/index.vue"), meta: { requiresAuth: true, permission: { menu: ["/requirement"], level: "view" }, title: "Requirements" } },
      { path: "new", name: "requirement-v2-create", component: () => import("modules/requirement-v2/pages/create.vue"), meta: { requiresAuth: true, permission: { menu: ["/requirement"], level: "manage" }, title: "New Requirement" } },
      { path: ":id", name: "requirement-v2-detail", component: () => import("modules/requirement-v2/pages/detail.vue"), meta: { requiresAuth: true, permission: { menu: ["/requirement"], level: "view" }, title: "Requirement" } },
      { path: ":id/edit", name: "requirement-v2-edit", component: () => import("modules/requirement-v2/pages/edit.vue"), meta: { requiresAuth: true, permission: { menu: ["/requirement"], level: "manage" }, title: "Edit Requirement" } }
    ]
  }
];
