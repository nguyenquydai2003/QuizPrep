import http from './http'
import type { AuthOptions, LoginResponse, User } from './types'

export const authApi = {
  options: () => http.get<AuthOptions>('/auth/options').then((r) => r.data),
  login: (userName: string, password: string) =>
    http.post<LoginResponse>('/auth/login', { userName, password }).then((r) => r.data),
  register: (body: { userName: string; displayName: string; password: string }) =>
    http.post<LoginResponse>('/auth/register', body).then((r) => r.data),
  me: () => http.get<User>('/auth/me').then((r) => r.data),
  updateProfile: (displayName: string) => http.put<User>('/auth/me', { displayName }).then((r) => r.data),
  changePassword: (currentPassword: string, newPassword: string) =>
    http.post('/auth/change-password', { currentPassword, newPassword }).then(() => undefined),
}
