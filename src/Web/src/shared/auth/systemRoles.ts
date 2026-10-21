/**
 * The system roles of IAM-02. The values are the role names the Gateway returns from /bff/user.
 * The server checks roles on every action; the web app uses them only to show or hide screens.
 */
export const SystemRole = {
  PlatformAdmin: 'PlatformAdmin',
  AppDesigner: 'AppDesigner',
  User: 'User',
} as const;

export type SystemRole = (typeof SystemRole)[keyof typeof SystemRole];
