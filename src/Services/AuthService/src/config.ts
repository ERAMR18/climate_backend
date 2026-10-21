export function required(name: string): string {
  const value = process.env[name];
  if (!value?.trim()) throw new Error(`Missing configuration: ${name}`);
  return value;
}

export function positiveInteger(name: string, fallback: number): number {
  const value = Number(process.env[name] ?? fallback);
  if (!Number.isSafeInteger(value) || value < 1) throw new Error(`Invalid configuration: ${name}`);
  return value;
}

export const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
export const roles = ['Administrator', 'Operator', 'Viewer'] as const;
export type Role = typeof roles[number];
export function permissions(role: Role): string[] {
  return role === 'Administrator' ? ['reports:generate', 'reports:read', 'reports:download', 'users:manage']
    : role === 'Operator' ? ['reports:generate', 'reports:read'] : ['reports:read'];
}
