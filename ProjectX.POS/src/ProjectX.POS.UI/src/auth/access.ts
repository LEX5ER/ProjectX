import type { AuthUser } from "./session";

export function canReadProjectData(user: AuthUser | null): boolean {
  return Boolean(
    user?.hasGlobalFullAccess
    || user?.hasAllPermissions
    || user?.globalPermissions.includes("projects.read")
    || user?.globalPermissions.includes("projects.write")
    || user?.activeProjectPermissions.includes("projects.read")
    || user?.activeProjectPermissions.includes("projects.write")
  );
}

export function canManageProjectData(user: AuthUser | null): boolean {
  return Boolean(
    user?.hasGlobalFullAccess
    || user?.hasAllPermissions
    || user?.globalPermissions.includes("projects.write")
    || user?.activeProjectPermissions.includes("projects.write")
  );
}

export function canManageAcrossProjects(user: AuthUser | null): boolean {
  return Boolean(
    user?.hasGlobalFullAccess
    || user?.globalPermissions.includes("projects.write")
  );
}
